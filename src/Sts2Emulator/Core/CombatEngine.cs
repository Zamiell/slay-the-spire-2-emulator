namespace Sts2Emulator.Core;

// Action encoding:
//   0..hand.Count-1  → play card at that hand index (targeting first enemy)
//   hand.Count       → end turn
//   hand.Count+1..   → use potion at slot (index - hand.Count - 1)
//
// This will be expanded once card targeting and multi-enemy rooms are implemented.

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

        if (def.Cost > state.Energy)
            return StepResult.Invalid;

        state.Energy -= def.Cost;
        state.Hand.RemoveAt(handIndex);

        Effects.CardEffects.Apply(def, card.Upgraded, state, rng);

        (state.DiscardPile, state.ExhaustPile) = def.Exhaust
            ? (state.DiscardPile, [.. state.ExhaustPile, card])
            : ([.. state.DiscardPile, card], state.ExhaustPile);

        bool playerDead = state.PlayerHp <= 0;
        bool allDead = state.Enemies.All(e => e.Hp <= 0);

        return new StepResult(
            Terminal: playerDead || allDead,
            PlayerWon: allDead && !playerDead,
            Reward: ComputeReward(state, playerDead, allDead)
        );
    }

    private static StepResult EndTurn(CombatState state, Random rng)
    {
        // Tick player end-of-turn buffs
        BuffSystem.TickEndOfTurn(state.PlayerBuffs);

        // Move hand to discard
        state.DiscardPile.AddRange(state.Hand);
        state.Hand.Clear();

        // Enemy turns
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
            EnemyAI.ExecuteIntent(enemy, state, rng);

        // Start next player turn
        state.Turn++;
        state.Energy = state.MaxEnergy;
        state.PlayerBlock = 0;
        DrawCards(state, 5, rng);
        EnemyAI.ChooseIntents(state.Enemies, state.Turn, rng);

        bool playerDead = state.PlayerHp <= 0;
        bool allDead = state.Enemies.All(e => e.Hp <= 0);

        return new StepResult(
            Terminal: playerDead || allDead,
            PlayerWon: allDead && !playerDead,
            Reward: ComputeReward(state, playerDead, allDead)
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

    private static void DrawCards(CombatState state, int count, Random rng)
    {
        for (int i = 0; i < count; i++)
        {
            if (state.DrawPile.Count == 0)
            {
                state.DrawPile = state.DiscardPile.OrderBy(_ => rng.Next()).ToList();
                state.DiscardPile.Clear();
            }
            if (state.DrawPile.Count == 0) break;

            int idx = rng.Next(state.DrawPile.Count);
            state.Hand.Add(state.DrawPile[idx]);
            state.DrawPile.RemoveAt(idx);
        }
    }

    private static float ComputeReward(CombatState state, bool playerDead, bool allDead)
    {
        if (allDead && !playerDead) return 1.0f;
        if (playerDead) return -1.0f;
        return 0f;
    }

    public static int[] ValidActions(CombatState state)
    {
        var actions = new List<int>();

        for (int i = 0; i < state.Hand.Count; i++)
        {
            var def = GeneratedData.Cards.Get(state.Hand[i].DefId);
            if (def.Cost <= state.Energy)
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
