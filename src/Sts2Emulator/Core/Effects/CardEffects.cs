namespace Sts2Emulator.Core.Effects;

public static class CardEffects
{
    public static void Apply(CardDef def, bool upgraded, CombatState state, Random rng)
    {
        // Dispatch to per-card effect by ID.
        // Populated incrementally as cards are reverse-engineered from sts2.dll.
        switch (def.Id)
        {
            case KnownCards.Strike:
                DealDamage(state, def.BaseDamage + (upgraded ? 3 : 0));
                break;

            case KnownCards.Defend:
                GainBlock(state, def.BaseBlock + (upgraded ? 3 : 0));
                break;

            default:
                // Fallback: apply raw base values until the card is implemented
                if (def.BaseDamage > 0) DealDamage(state, def.BaseDamage);
                if (def.BaseBlock > 0) GainBlock(state, def.BaseBlock);
                break;
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    public static void DealDamage(CombatState state, int amount)
    {
        var target = state.Enemies.FirstOrDefault(e => e.Hp > 0);
        if (target is null) return;

        int damage = BuffSystem.IncomingDamage(amount, state.PlayerBuffs, target.Buffs.ToList());
        int absorbed = Math.Min(target.Block, damage);
        target.Block -= absorbed;
        target.Hp -= damage - absorbed;
    }

    public static void GainBlock(CombatState state, int amount)
    {
        state.PlayerBlock += BuffSystem.IncomingBlock(amount, state.PlayerBuffs);
    }
}

// Populated from Generated/Cards.g.cs once extraction is complete.
internal static class KnownCards
{
    public const int Strike = 1;
    public const int Defend = 2;
}
