namespace Sts2Emulator.Core.Effects;

public static class RelicEffects
{
    public const int Anchor = 4;
    public const int BagOfPreparation = 10;
    public const int BloodVial = 23;
    public const int BoomingConch = 29;
    public const int BronzeScales = 35;
    public const int OddlySmoothStone = 169;
    public const int Orichalcum = 172;
    public const int Vajra = 279;

    public static void ApplyCombatStart(CombatState state, Random rng)
    {
        if (HasRelic(state, BloodVial))
            state.PlayerHp = Math.Min(state.PlayerHp + 2, state.PlayerMaxHp);

        if (HasRelic(state, Anchor))
            CardEffects.GainBlock(state, 10);

        if (HasRelic(state, Vajra))
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, 1);

        if (HasRelic(state, OddlySmoothStone))
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Dexterity, 1);

        if (HasRelic(state, BronzeScales))
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, 3);

        if (HasRelic(state, BagOfPreparation))
            CardEffects.DrawCards(state, 2, rng);

        if (HasRelic(state, BoomingConch) && state.IsEliteCombat)
        {
            CardEffects.DrawCards(state, 2, rng);
            state.Energy += 1;
        }
    }

    public static void ApplyEndOfPlayerTurn(CombatState state)
    {
        if (HasRelic(state, Orichalcum) && state.PlayerBlock == 0)
            CardEffects.GainBlock(state, 6);
    }

    private static bool HasRelic(CombatState state, int relicId) =>
        state.Relics.Any(relic => relic.DefId == relicId);
}
