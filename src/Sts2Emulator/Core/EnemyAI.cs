namespace Sts2Emulator.Core;

using Effects;

public static class EnemyAI
{
    public static void ChooseIntents(List<EnemyState> enemies, int turn, Random rng)
    {
        foreach (var enemy in enemies.Where(e => e.Hp > 0))
            enemy.CurrentIntent = SelectIntent(enemy, rng);
    }

    public static void ExecuteIntent(EnemyState enemy, CombatState state, Random rng)
    {
        bool wasBuffMove = enemy.CurrentIntent.Type == IntentType.Buff;

        enemy.Block = 0; // block clears at start of enemy turn

        switch (enemy.CurrentIntent.Type)
        {
            case IntentType.Attack:
            {
                int damage = BuffSystem.IncomingDamage(
                    enemy.CurrentIntent.Magnitude,
                    enemy.Buffs,
                    state.PlayerBuffs
                );
                int absorbed = Math.Min(state.PlayerBlock, damage);
                state.PlayerBlock -= absorbed;
                state.PlayerHp = Math.Max(0, state.PlayerHp - (damage - absorbed));

                // FlameBarrier: retaliate with flat unpowered damage.
                int fb = BuffSystem.Get(state.PlayerBuffs, BuffId.FlameBarrier);
                if (fb > 0)
                {
                    int fbAbs = Math.Min(enemy.Block, fb);
                    enemy.Block -= fbAbs;
                    enemy.Hp = Math.Max(0, enemy.Hp - (fb - fbAbs));
                }
                break;
            }

            case IntentType.Defend:
                enemy.Block += BuffSystem.IncomingBlock(
                    enemy.CurrentIntent.Magnitude, enemy.Buffs);
                break;

            case IntentType.Buff:
                ApplyBuffIntent(enemy, state);
                break;

            case IntentType.Debuff:
                ApplyDebuffIntent(enemy, state);
                break;
        }

        if (enemy.DefId == KE.Nibbit
            && enemy.CurrentIntent.Type == IntentType.Attack
            && enemy.MoveIndex % 3 == 1)
        {
            enemy.Block += BuffSystem.IncomingBlock(6, enemy.Buffs);
        }

        enemy.MoveIndex++;

        // Ritual: gain Strength at end of each enemy turn except the turn it was applied.
        if (!wasBuffMove)
        {
            int ritual = BuffSystem.Get(enemy.Buffs, BuffId.Ritual);
            if (ritual > 0)
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, ritual);
        }
    }

    // ── Per-enemy intent selection ─────────────────────────────────────────────

    private static Intent SelectIntent(EnemyState enemy, Random rng)
    {
        switch (enemy.DefId)
        {
            case KE.CalcifiedCultist:
                // Turn 0: Incantation (Buff). Turn 1+: Dark Strike (9 dmg, loops).
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(IntentType.Attack, 9);

            case KE.DampCultist:
                // Turn 0: Incantation (Buff). Turn 1+: Dark Strike (3 dmg, loops).
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(IntentType.Attack, 3);

            case KE.Chomper:
                // Alternates Clamp (9x2) and Screech (add Dazed).
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Attack, 18)
                    : new Intent(IntentType.Debuff, 3);

            case KE.Exoskeleton:
                return rng.Next(3) switch
                {
                    0 => new Intent(IntentType.Attack, 4),
                    1 => new Intent(IntentType.Attack, 9),
                    _ => new Intent(IntentType.Buff, 0),
                };

            case KE.FuzzyWurmCrawler:
                return (enemy.MoveIndex % 3) == 1
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(IntentType.Attack, 6);

            case KE.GremlinMerc:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 16),
                    1 => new Intent(IntentType.Debuff, 14),
                    _ => new Intent(IntentType.Buff, 9),
                };

            case KE.Inklet:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 4),
                    1 => new Intent(IntentType.Attack, 11),
                    _ => new Intent(IntentType.Attack, 9),
                };

            case KE.Seapunk:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 13),
                    1 => new Intent(IntentType.Attack, 8),
                    _ => new Intent(IntentType.Buff, 0),
                };

            case KE.ShrinkerBeetle:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Debuff, 1)
                    : (enemy.MoveIndex % 2 == 1
                        ? new Intent(IntentType.Attack, 8)
                        : new Intent(IntentType.Attack, 14));

            case KE.Nibbit:
                // Alone Nibbit: Butt, Slice+block, Hiss loop.
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 13),
                    1 => new Intent(IntentType.Attack, 7),
                    _ => new Intent(IntentType.Buff, 0),
                };

            case KE.LeafSlimeS:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Attack, 4)
                    : new Intent(IntentType.Debuff, 1);

            case KE.TwigSlimeS:
                return new Intent(IntentType.Attack, 5);

            case KE.LeafSlimeM:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Attack, 9)
                    : new Intent(IntentType.Debuff, 2);

            case KE.TwigSlimeM:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Attack, 12)
                    : new Intent(IntentType.Debuff, 1);

            case KE.TwoTailedRat:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 9),
                    1 => new Intent(IntentType.Attack, 7),
                    _ => new Intent(IntentType.Debuff, 1),
                };

            case KE.CorpseSlug:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 6),
                    1 => new Intent(IntentType.Attack, 9),
                    _ => new Intent(IntentType.Debuff, 2),
                };

            case KE.SludgeSpinner:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Debuff, 9),
                    1 => new Intent(IntentType.Attack, 12),
                    _ => new Intent(IntentType.Buff, 7),
                };

            case KE.Toadpole:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Buff, 0),
                    1 => new Intent(IntentType.Attack, 8),
                    _ => new Intent(IntentType.Attack, 12),
                };

            default:
                return GeneratedData.Enemies.ChooseIntent(enemy.DefId, enemy.MoveIndex, 0, rng);
        }
    }

    // ── Per-enemy buff actions ─────────────────────────────────────────────────

    private static void ApplyBuffIntent(EnemyState enemy, CombatState state)
    {
        switch (enemy.DefId)
        {
            case KE.CalcifiedCultist:
                // Incantation: apply 2 Ritual to self (gains +2 Strength each subsequent turn).
                BuffSystem.Apply(enemy.Buffs, BuffId.Ritual, 2);
                break;

            case KE.DampCultist:
                // Incantation: apply Ritual to self (deadly ascension value).
                BuffSystem.Apply(enemy.Buffs, BuffId.Ritual, 6);
                break;

            case KE.Nibbit:
                // Hiss: gain Strength.
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.Exoskeleton:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.FuzzyWurmCrawler:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 7);
                break;

            case KE.GremlinMerc:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.SludgeSpinner:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.Seapunk:
                enemy.Block += BuffSystem.IncomingBlock(8, enemy.Buffs);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.Toadpole:
                BuffSystem.Apply(enemy.Buffs, BuffId.Thorns, 2);
                break;
        }
    }

    private static void ApplyDebuffIntent(EnemyState enemy, CombatState state)
    {
        switch (enemy.DefId)
        {
            case KE.Chomper:
                AddStatus(state, ST.Dazed, 3);
                break;

            case KE.LeafSlimeS:
            case KE.TwigSlimeM:
                AddStatus(state, ST.Slimed, 1);
                break;

            case KE.LeafSlimeM:
                AddStatus(state, ST.Slimed, 2);
                break;

            case KE.TwoTailedRat:
            case KE.CorpseSlug:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, enemy.CurrentIntent.Magnitude);
                break;

            case KE.GremlinMerc:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 2);
                break;

            case KE.SludgeSpinner:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 1);
                break;

            case KE.ShrinkerBeetle:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Shrink, 1);
                break;
        }
    }

    private static void DealAttackDamage(EnemyState enemy, CombatState state, int baseDamage)
    {
        int damage = BuffSystem.IncomingDamage(baseDamage, enemy.Buffs, state.PlayerBuffs);
        int absorbed = Math.Min(state.PlayerBlock, damage);
        state.PlayerBlock -= absorbed;
        state.PlayerHp = Math.Max(0, state.PlayerHp - (damage - absorbed));
    }

    private static void AddStatus(CombatState state, int cardId, int count)
    {
        for (int i = 0; i < count; i++)
            state.DiscardPile.Add(new CardInstance(cardId, false));
    }
}

// Known enemy def IDs (from Generated/Enemies.g.cs).
internal static class KE
{
    public const int CalcifiedCultist = 14;
    public const int Chomper = 16;
    public const int CorpseSlug = 17;
    public const int DampCultist = 21;
    public const int Exoskeleton = 25;
    public const int FuzzyWurmCrawler = 34;
    public const int GremlinMerc = 37;
    public const int Inklet = 42;
    public const int LeafSlimeM = 47;
    public const int LeafSlimeS = 48;
    public const int Nibbit = 56;
    public const int Seapunk = 69;
    public const int ShrinkerBeetle = 71;
    public const int SludgeSpinner = 75;
    public const int Toadpole = 93;
    public const int TwigSlimeM = 99;
    public const int TwigSlimeS = 100;
    public const int TwoTailedRat = 101;
}
