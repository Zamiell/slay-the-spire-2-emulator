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
        int energyToSpend = Math.Max(0, effectiveCost);
        if (def.Unplayable || energyToSpend > state.Energy || IsBlockedBySmoggy(def, state))
            return StepResult.Invalid;

        // Snapshot HP before effects.
        int playerHpBefore = state.PlayerHp;
        Span<int> enemyHpsBefore = stackalloc int[state.Enemies.Count];
        for (int i = 0; i < state.Enemies.Count; i++)
            enemyHpsBefore[i] = state.Enemies[i].Hp;

        state.Energy -= energyToSpend;
        state.Hand.RemoveAt(handIndex);
        if (def.Type == CardType.Skill && BuffSystem.Get(state.PlayerBuffs, BuffId.Smoggy) > 0)
            state.SkillPlayedWhileSmoggy = true;

        Effects.CardEffects.Apply(def, card.Upgraded, state, rng);
        HandleEnemyDeaths(state, enemyHpsBefore, rng);

        // Rage: gain block when playing an Attack.
        if (def.Type == CardType.Attack)
        {
            state.AttackCardsPlayedThisTurn++;
            if (state.AttackCardsPlayedThisTurn == 3)
            {
                int juggling = BuffSystem.Get(state.PlayerBuffs, BuffId.Juggling);
                for (int i = 0; i < juggling; i++)
                    state.Hand.Add(new CardInstance(card.DefId, card.Upgraded));
            }

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

        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

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
        Span<int> enemyHpsBefore = stackalloc int[state.Enemies.Count];
        for (int i = 0; i < state.Enemies.Count; i++)
            enemyHpsBefore[i] = state.Enemies[i].Hp;

        // ── End of player turn ────────────────────────────────────────────────
        // Metallicize: gain block at end of player turn.
        int metallicize = BuffSystem.Get(state.PlayerBuffs, BuffId.Metallicize);
        if (metallicize > 0) Effects.CardEffects.GainBlock(state, metallicize);
        Effects.RelicEffects.ApplyEndOfPlayerTurn(state);

        int temporaryStrength = BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength);
        if (temporaryStrength != 0)
        {
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, -temporaryStrength);
            BuffSystem.Remove(state.PlayerBuffs, BuffId.TemporaryStrength);
        }

        // Rage expires at end of player turn.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.Rage);

        int constrict = BuffSystem.Get(state.PlayerBuffs, BuffId.Constrict);
        if (constrict > 0)
            state.PlayerHp = Math.Max(0, state.PlayerHp - constrict);

        int disintegration = BuffSystem.Get(state.PlayerBuffs, BuffId.Disintegration);
        if (disintegration > 0)
            state.PlayerHp = Math.Max(0, state.PlayerHp - disintegration);

        int beckons = state.Hand.Count(card => card.DefId == Effects.ST.Beckon);
        if (beckons > 0)
            state.PlayerHp = Math.Max(0, state.PlayerHp - beckons * 6);

        // Move hand to discard, exhausting ethereal cards.
        foreach (var card in state.Hand)
        {
            if (GeneratedData.Cards.Get(card.DefId).Ethereal)
                Effects.CardEffects.ExhaustCard(state, card);
            else
                state.DiscardPile.Add(card);
        }
        state.Hand.Clear();

        // Tick enemy debuffs before enemies act (Vulnerable/Weak on enemies tick down).
        foreach (var enemy in state.Enemies.ToArray())
            BuffSystem.TickEndOfTurn(enemy.Buffs);

        // ── Enemy turns ───────────────────────────────────────────────────────
        state.PlayerTurn = false;
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToArray())
            EnemyAI.ExecuteIntent(enemy, state, rng);
        HandleEnemyDeaths(state, enemyHpsBefore, rng);

        int colossus = BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus);
        if (colossus > 0)
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Colossus, -1);

        // FlameBarrier expires after enemies have acted.
        BuffSystem.Remove(state.PlayerBuffs, BuffId.FlameBarrier);

        // ── Start of next player turn ─────────────────────────────────────────
        state.Turn++;
        state.PlayerTurn = true;
        state.Energy = state.MaxEnergy;

        // Barricade: block does not reset.
        if (BuffSystem.Get(state.PlayerBuffs, BuffId.Barricade) == 0)
            state.PlayerBlock = 0;

        Effects.RelicEffects.ApplyStartOfPlayerTurn(state);

        // DemonForm: gain Strength at start of player turn.
        int demonForm = BuffSystem.Get(state.PlayerBuffs, BuffId.DemonForm);
        if (demonForm > 0)
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, demonForm);

        int infernoSelfDamage = BuffSystem.Get(state.PlayerBuffs, BuffId.InfernoSelfDamage);
        if (infernoSelfDamage > 0)
        {
            Span<int> enemyHpsBeforeInferno = stackalloc int[state.Enemies.Count];
            for (int i = 0; i < state.Enemies.Count; i++)
                enemyHpsBeforeInferno[i] = state.Enemies[i].Hp;
            Effects.CardEffects.LoseHp(state, infernoSelfDamage);
            HandleEnemyDeaths(state, enemyHpsBeforeInferno, rng);
        }

        // Tick player debuffs at start of player turn (Vulnerable etc. tick down).
        BuffSystem.TickEndOfTurn(state.PlayerBuffs);
        BuffSystem.Remove(state.PlayerBuffs, BuffId.Tangled);
        BuffSystem.Remove(state.PlayerBuffs, BuffId.Smoggy);
        state.SkillPlayedWhileSmoggy = false;
        state.AttackCardsPlayedThisTurn = 0;

        // Draw five cards.
        Effects.CardEffects.DrawCards(state, 5, rng);

        // Enemies choose their next intent.
        EnemyAI.ChooseIntents(state.Enemies, state.Turn, rng);
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

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
        Effects.RelicEffects.ApplyAfterPlayerHpChanged(state);

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
        int cost = def.Cost;
        if (def.Type == CardType.Attack)
            cost += BuffSystem.Get(state.PlayerBuffs, BuffId.Tangled);
        return cost;
    }

    private static bool IsBlockedBySmoggy(CardDef def, CombatState state)
    {
        return def.Type == CardType.Skill
            && state.SkillPlayedWhileSmoggy
            && BuffSystem.Get(state.PlayerBuffs, BuffId.Smoggy) > 0;
    }

    public static int[] ValidActions(CombatState state)
    {
        var actions = new List<int>();

        for (int i = 0; i < state.Hand.Count; i++)
        {
            var def = GeneratedData.Cards.Get(state.Hand[i].DefId);
            int effectiveCost = EffectiveCost(def, state);
            int energyToSpend = Math.Max(0, effectiveCost);
            if (!def.Unplayable && energyToSpend <= state.Energy && !IsBlockedBySmoggy(def, state))
                actions.Add(i);
        }

        actions.Add(state.Hand.Count); // end turn always valid

        for (int s = 0; s < state.PotionSlots.Length; s++)
            if (state.PotionSlots[s] != 0)
                actions.Add(state.Hand.Count + 1 + s);

        return [.. actions];
    }

    private static void HandleEnemyDeaths(CombatState state, ReadOnlySpan<int> enemyHpsBefore, Random rng)
    {
        for (int i = 0; i < state.Enemies.Count && i < enemyHpsBefore.Length; i++)
        {
            if (enemyHpsBefore[i] <= 0 || state.Enemies[i].Hp > 0)
                continue;

            if (BuffSystem.Get(state.Enemies[i].Buffs, BuffId.Surprise) > 0)
                SpawnGremlinMercReinforcements(state, rng, state.Enemies[i].StolenGold);

            if (state.Enemies[i].HeistGold > 0)
            {
                state.PlayerGold += state.Enemies[i].HeistGold;
                state.Enemies[i].HeistGold = 0;
            }

            if (state.Enemies[i].DefId == KE.SlitheringStrangler)
                BuffSystem.Remove(state.PlayerBuffs, BuffId.Constrict);

            foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
            {
                int ravenous = BuffSystem.Get(enemy.Buffs, BuffId.Ravenous);
                if (ravenous <= 0)
                    continue;

                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, ravenous);
                BuffSystem.Apply(enemy.Buffs, BuffId.Stunned, 1);
            }
        }
    }

    private static void SpawnGremlinMercReinforcements(CombatState state, Random rng, int stolenGold)
    {
        state.Enemies.Add(CreateEnemy(78, rng, new Intent(IntentType.Unknown, 0), stunned: true));
        var fatGremlin = CreateEnemy(28, rng, new Intent(IntentType.Unknown, 0), stunned: true);
        fatGremlin.HeistGold = stolenGold;
        state.Enemies.Add(fatGremlin);
    }

    private static EnemyState CreateEnemy(int defId, Random rng, Intent intent, bool stunned = false)
    {
        var def = GeneratedData.Enemies.Get(defId);
        int hp = rng.Next(def.MinHp, def.MaxHp + 1);
        var enemy = new EnemyState
        {
            DefId = defId,
            Hp = hp,
            MaxHp = hp,
            CurrentIntent = intent,
            Buffs = [],
        };
        if (stunned)
            BuffSystem.Apply(enemy.Buffs, BuffId.Stunned, 1);
        return enemy;
    }
}

public readonly record struct StepResult(bool Terminal, bool PlayerWon, float Reward)
{
    public static readonly StepResult Invalid = new(false, false, 0f);
}
