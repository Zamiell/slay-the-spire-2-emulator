namespace Sts2Emulator.Core;

// Action encoding:
//   0..hand.Count-1  → play card at that hand index (targeting first enemy)
//   hand.Count       → end turn
//   hand.Count+1..   → use potion at slot (index - hand.Count - 1)

public static class CombatEngine
{
    public static StepResult Step(CombatState state, int action, Random rng)
    {
        int endTurnAction = state.Hand.Count;

        if (action == endTurnAction)
            return EndTurn(state, rng);

        if (action < endTurnAction)
            return PlayCard(state, action, rng);

        int potionSlot = action - endTurnAction - 1;
        return UsePotion(state, potionSlot);
    }

    private static StepResult PlayCard(CombatState state, int handIndex, Random rng)
    {
        var card = state.Hand[handIndex];
        var def = GeneratedData.Cards.Get(card.DefId);

        int effectiveCost = EffectiveCost(def, state);
        if (effectiveCost > state.Energy)
            return StepResult.Invalid;

        // Snapshot HP before effects.
        int playerHpBefore = state.PlayerHp;
        Span<int> enemyHpsBefore = stackalloc int[3];
        for (int i = 0; i < state.Enemies.Count; i++)
            enemyHpsBefore[i] = state.Enemies[i].Hp;

        state.Energy -= effectiveCost;
        state.Hand.RemoveAt(handIndex);

        Effects.CardEffects.Apply(def, card.Upgraded, state, rng);

        // Rage: gain block when playing an Attack.
        if (def.Type == CardType.Attack)
        {
            int rage = BuffSystem.Get(state.PlayerBuffs, BuffId.Rage);
            if (rage > 0) Effects.CardEffects.GainBlock(state, rage);
        }

        // Corruption: Skills exhaust instead of discard.
        bool corruptedSkill = def.Type == CardType.Skill
            && BuffSystem.Get(state.PlayerBuffs, BuffId.Corruption) > 0;
        if (def.Exhaust || corruptedSkill)
            Effects.CardEffects.ExhaustCard(state, card);
        else
            state.DiscardPile.Add(card);

        bool playerDead = state.PlayerHp <= 0;
        bool allDead = state.Enemies.All(e => e.Hp <= 0);

        return new StepResult(
            Terminal: playerDead || allDead,
            PlayerWon: allDead && !playerDead,
            Reward: ComputeReward(state, playerDead, allDead, playerHpBefore, enemyHpsBefore)
        );
    }

    private static StepResult EndTurn(CombatState state, Random rng)
    {
        // Snapshot HP before enemies act.
        int playerHpBefore = state.PlayerHp;
        Span<int> enemyHpsBefore = stackalloc int[3];
        for (int i = 0; i < state.Enemies.Count; i++)
            enemyHpsBefore[i] = state.Enemies[i].Hp;

        // ── End of player turn ────────────────────────────────────────────────
        // Metallicize: gain block at end of player turn.
        int metallicize = BuffSystem.Get(state.PlayerBuffs, BuffId.Metallicize);
        if (metallicize > 0) Effects.CardEffects.GainBlock(state, metallicize);

        // Rage expires at end of player turn.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.Rage);

        // Move hand to discard.
        state.DiscardPile.AddRange(state.Hand);
        state.Hand.Clear();

        // Tick enemy debuffs before enemies act (Vulnerable/Weak on enemies tick down).
        foreach (var enemy in state.Enemies)
            BuffSystem.TickEndOfTurn(enemy.Buffs);

        // ── Enemy turns ───────────────────────────────────────────────────────
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
            EnemyAI.ExecuteIntent(enemy, state, rng);

        // FlameBarrier expires after enemies have acted.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.FlameBarrier);

        // ── Start of next player turn ─────────────────────────────────────────
        state.Turn++;
        state.Energy = state.MaxEnergy;

        // Barricade: block does not reset.
        if (BuffSystem.Get(state.PlayerBuffs, BuffId.Barricade) == 0)
            state.PlayerBlock = 0;

        // DemonForm: gain Strength at start of player turn.
        int demonForm = BuffSystem.Get(state.PlayerBuffs, BuffId.DemonForm);
        if (demonForm > 0)
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, demonForm);

        // Tick player debuffs at start of player turn (Vulnerable etc. tick down).
        BuffSystem.TickEndOfTurn(state.PlayerBuffs);

        // Draw five cards.
        Effects.CardEffects.DrawCards(state, 5, rng);

        // Enemies choose their next intent.
        EnemyAI.ChooseIntents(state.Enemies, state.Turn, rng);

        bool playerDead = state.PlayerHp <= 0;
        bool allDead = state.Enemies.All(e => e.Hp <= 0);

        return new StepResult(
            Terminal: playerDead || allDead,
            PlayerWon: allDead && !playerDead,
            Reward: ComputeReward(state, playerDead, allDead, playerHpBefore, enemyHpsBefore)
        );
    }

    private static StepResult UsePotion(CombatState state, int slot)
    {
        if (slot < 0 || slot >= state.PotionSlots.Length || state.PotionSlots[slot] == 0)
            return StepResult.Invalid;

        Effects.PotionEffects.Apply(state.PotionSlots[slot], state);
        state.PotionSlots[slot] = 0;

        return new StepResult(Terminal: false, PlayerWon: false, Reward: 0f);
    }

    // Shaped reward: fraction of enemy HP dealt minus fraction of player HP lost,
    // plus ±1 terminal bonus for win/death.
    private static float ComputeReward(
        CombatState state, bool playerDead, bool allDead,
        int playerHpBefore, ReadOnlySpan<int> enemyHpsBefore)
    {
        float totalMaxHp = 0f;
        float dmgDealt = 0f;
        for (int i = 0; i < state.Enemies.Count; i++)
        {
            totalMaxHp += state.Enemies[i].MaxHp;
            if (i < enemyHpsBefore.Length)
                dmgDealt += Math.Max(0, enemyHpsBefore[i] - state.Enemies[i].Hp);
        }

        float dmgTaken = Math.Max(0, playerHpBefore - state.PlayerHp);

        float shaped = (totalMaxHp > 0f ? dmgDealt / totalMaxHp : 0f)
                     - dmgTaken / (float)state.PlayerMaxHp;

        float terminal = (allDead && !playerDead) ? 1f : (playerDead ? -1f : 0f);

        return shaped + terminal;
    }

    // Returns the energy cost of a card after applying active powers (e.g. Corruption).
    private static int EffectiveCost(CardDef def, CombatState state)
    {
        if (def.Type == CardType.Skill && BuffSystem.Get(state.PlayerBuffs, BuffId.Corruption) > 0)
            return 0;
        return def.Cost;
    }

    public static int[] ValidActions(CombatState state)
    {
        var actions = new List<int>();

        for (int i = 0; i < state.Hand.Count; i++)
        {
            var def = GeneratedData.Cards.Get(state.Hand[i].DefId);
            if (EffectiveCost(def, state) <= state.Energy)
                actions.Add(i);
        }

        actions.Add(state.Hand.Count); // end turn always valid

        for (int s = 0; s < state.PotionSlots.Length; s++)
            if (state.PotionSlots[s] != 0)
                actions.Add(state.Hand.Count + 1 + s);

        return [.. actions];
    }
}

public readonly record struct StepResult(bool Terminal, bool PlayerWon, float Reward)
{
    public static readonly StepResult Invalid = new(false, false, 0f);
}
