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
        if (BuffSystem.Get(enemy.Buffs, BuffId.Stunned) > 0)
        {
            BuffSystem.Apply(enemy.Buffs, BuffId.Stunned, -1);
            enemy.MoveIndex++;
            return;
        }

        switch (enemy.CurrentIntent.Type)
        {
            case IntentType.Attack:
            {
                if (enemy.DefId == KE.Toadpole && enemy.MoveIndex % 3 == 1)
                {
                    BuffSystem.Apply(enemy.Buffs, BuffId.Thorns, -2);
                    for (int i = 0; i < 3; i++)
                        DealAttackDamage(enemy, state, 4);
                    break;
                }

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
                if (enemy.DefId == KE.GasBomb)
                    enemy.Hp = 0;
                break;
            }

            case IntentType.Defend:
                enemy.Block += BuffSystem.IncomingBlock(
                    enemy.CurrentIntent.Magnitude, enemy.Buffs);
                break;

            case IntentType.Buff:
                ApplyBuffIntent(enemy, state, rng);
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

        if (enemy.DefId == KE.TwoTailedRat && enemy.CurrentIntent.Type != IntentType.Buff)
            TickRatSummonCooldown(enemy);

        int plating = BuffSystem.Get(enemy.Buffs, BuffId.Plating);
        if (plating > 0)
        {
            enemy.Block += BuffSystem.IncomingBlock(plating, enemy.Buffs);
            BuffSystem.Apply(enemy.Buffs, BuffId.Plating, -1);
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

            case KE.Mawler:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 10),
                    1 => new Intent(IntentType.Debuff, 3),
                    _ => new Intent(IntentType.Attack, 16),
                };

            case KE.GremlinMerc:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 16),
                    1 => new Intent(IntentType.Debuff, 14),
                    _ => new Intent(IntentType.Buff, 9),
                };

            case KE.SneakyGremlin:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Unknown, 0)
                    : new Intent(IntentType.Attack, 10);

            case KE.FatGremlin:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Unknown, 0)
                    : new Intent(IntentType.Buff, 0);

            case KE.Inklet:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 4),
                    1 => new Intent(IntentType.Attack, 11),
                    _ => new Intent(IntentType.Attack, 9),
                };

            case KE.Flyconid:
                return rng.Next(6) switch
                {
                    0 or 1 or 2 => new Intent(IntentType.Debuff, 2),
                    3 or 4 => new Intent(IntentType.Debuff, 9),
                    _ => new Intent(IntentType.Attack, 12),
                };

            case KE.SnappingJaxfruit:
                return new Intent(IntentType.Buff, 4);

            case KE.CubexConstruct:
                return (enemy.MoveIndex % 4) switch
                {
                    0 => new Intent(IntentType.Buff, 0),
                    1 => new Intent(IntentType.Buff, 8),
                    2 => new Intent(IntentType.Buff, 8),
                    _ => new Intent(IntentType.Attack, 12),
                };

            case KE.VineShambler:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Attack, 14),
                    1 => new Intent(IntentType.Debuff, 9),
                    _ => new Intent(IntentType.Attack, 18),
                };

            case KE.SlitheringStrangler:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Debuff, 3),
                    1 => new Intent(IntentType.Attack, 8),
                    _ => new Intent(IntentType.Attack, 13),
                };

            case KE.HauntedShip:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Debuff, 5),
                    1 => new Intent(IntentType.Attack, 14),
                    _ => new Intent(IntentType.Attack, 15),
                };

            case KE.LivingFog:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Debuff, 9),
                    1 => new Intent(IntentType.Buff, 6),
                    _ => new Intent(IntentType.Attack, 9),
                };

            case KE.Fogmog:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Buff, 0),
                    1 => new Intent(IntentType.Buff, 9),
                    _ => new Intent(IntentType.Attack, 16),
                };

            case KE.EyeWithTeeth:
                return new Intent(IntentType.Debuff, 3);

            case KE.GasBomb:
                return new Intent(IntentType.Attack, 9);

            case KE.AxeRubyRaider:
                return (enemy.MoveIndex % 3) == 2
                    ? new Intent(IntentType.Attack, 13)
                    : new Intent(IntentType.Attack, 6);

            case KE.AssassinRubyRaider:
                return new Intent(IntentType.Attack, 11);

            case KE.BruteRubyRaider:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Attack, 8)
                    : new Intent(IntentType.Buff, 0);

            case KE.CrossbowRubyRaider:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Defend, 3)
                    : new Intent(IntentType.Attack, 16);

            case KE.TrackerRubyRaider:
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Debuff, 2)
                    : new Intent(IntentType.Attack, 9);

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
                if (CanRatSummon(enemy, rng))
                    return new Intent(IntentType.Buff, 0);

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
                    1 => new Intent(IntentType.Attack, 12),
                    _ => new Intent(IntentType.Attack, 8),
                };

            case KE.FossilStalker:
                return rng.Next(3) switch
                {
                    0 => new Intent(IntentType.Debuff, 11),
                    1 => new Intent(IntentType.Attack, 14),
                    _ => new Intent(IntentType.Attack, 8),
                };

            case KE.PunchConstruct:
                return (enemy.MoveIndex % 3) switch
                {
                    0 => new Intent(IntentType.Defend, 10),
                    1 => new Intent(IntentType.Debuff, 12),
                    _ => new Intent(IntentType.Attack, 16),
                };

            case KE.SewerClam:
                return enemy.MoveIndex % 2 == 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(IntentType.Attack, 11);

            default:
                return GeneratedData.Enemies.ChooseIntent(enemy.DefId, enemy.MoveIndex, 0, rng);
        }
    }

    // ── Per-enemy buff actions ─────────────────────────────────────────────────

    private static void ApplyBuffIntent(EnemyState enemy, CombatState state, Random rng)
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

            case KE.SnappingJaxfruit:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.CubexConstruct:
                if (enemy.CurrentIntent.Magnitude > 0)
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 2);
                break;

            case KE.SewerClam:
                enemy.Block += BuffSystem.IncomingBlock(BuffSystem.Get(enemy.Buffs, BuffId.Plating), enemy.Buffs);
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 4);
                break;

            case KE.Fogmog:
                if (enemy.CurrentIntent.Magnitude > 0)
                {
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 1);
                }
                else if (!state.Enemies.Any(e => e.Hp > 0 && e.DefId == KE.EyeWithTeeth))
                {
                    var eye = CreateEnemy(KE.EyeWithTeeth, rng, new Intent(IntentType.Debuff, 3), stunned: true);
                    BuffSystem.Apply(eye.Buffs, BuffId.Illusion, 1);
                    state.Enemies.Add(eye);
                }
                break;

            case KE.LivingFog:
                if (state.Enemies.Count(e => e.Hp > 0 && e.DefId == KE.GasBomb) < 3)
                {
                    var bomb = CreateEnemy(KE.GasBomb, rng, new Intent(IntentType.Attack, 9), stunned: true);
                    BuffSystem.Apply(bomb.Buffs, BuffId.Minion, 1);
                    state.Enemies.Add(bomb);
                }
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                break;

            case KE.BruteRubyRaider:
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, 3);
                break;

            case KE.Toadpole:
                BuffSystem.Apply(enemy.Buffs, BuffId.Thorns, 2);
                break;

            case KE.FatGremlin:
                enemy.Hp = 0;
                break;

            case KE.TwoTailedRat:
                SummonRatBackup(enemy, state, rng);
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

            case KE.Mawler:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 3);
                break;

            case KE.Flyconid:
                if (enemy.CurrentIntent.Magnitude > 2)
                {
                    DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                }
                else
                {
                    BuffSystem.Apply(state.PlayerBuffs, BuffId.Vulnerable, 2);
                }
                break;

            case KE.LivingFog:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Smoggy, 1);
                break;

            case KE.EyeWithTeeth:
                AddStatus(state, ST.Dazed, 3);
                break;

            case KE.TrackerRubyRaider:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 2);
                break;

            case KE.VineShambler:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Tangled, 1);
                break;

            case KE.FossilStalker:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                break;

            case KE.PunchConstruct:
                DealAttackDamage(enemy, state, enemy.CurrentIntent.Magnitude);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Frail, 1);
                break;

            case KE.SlitheringStrangler:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Constrict, 3);
                break;

            case KE.HauntedShip:
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Weak, 3);
                AddStatus(state, ST.Dazed, 5);
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

    private static bool CanRatSummon(EnemyState enemy, Random rng)
    {
        return BuffSystem.Get(enemy.Buffs, BuffId.SummonCooldown) <= 0
            && BuffSystem.Get(enemy.Buffs, BuffId.BackupCount) < 3
            && rng.NextDouble() < 0.75;
    }

    private static void TickRatSummonCooldown(EnemyState enemy)
    {
        if (enemy.DefId != KE.TwoTailedRat)
            return;

        int cooldown = BuffSystem.Get(enemy.Buffs, BuffId.SummonCooldown);
        if (cooldown > 0)
            BuffSystem.Apply(enemy.Buffs, BuffId.SummonCooldown, -1);
    }

    private static void SummonRatBackup(EnemyState enemy, CombatState state, Random rng)
    {
        if (state.Enemies.Count(e => e.Hp > 0 && e.DefId == KE.TwoTailedRat) >= 6)
            return;

        state.Enemies.Add(CreateEnemy(KE.TwoTailedRat, rng, new Intent(IntentType.Unknown, 0), stunned: true));

        int nextBackupCount = state.Enemies
            .Where(e => e.DefId == KE.TwoTailedRat)
            .Select(e => BuffSystem.Get(e.Buffs, BuffId.BackupCount))
            .DefaultIfEmpty(0)
            .Max() + 1;
        foreach (var rat in state.Enemies.Where(e => e.DefId == KE.TwoTailedRat))
        {
            int current = BuffSystem.Get(rat.Buffs, BuffId.BackupCount);
            BuffSystem.Apply(rat.Buffs, BuffId.BackupCount, nextBackupCount - current);
        }
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
        if (defId == KE.TwoTailedRat)
            BuffSystem.Apply(enemy.Buffs, BuffId.SummonCooldown, 2);
        return enemy;
    }
}

// Known enemy def IDs (from Generated/Enemies.g.cs).
internal static class KE
{
    public const int CalcifiedCultist = 14;
    public const int Chomper = 16;
    public const int CorpseSlug = 17;
    public const int AxeRubyRaider = 5;
    public const int AssassinRubyRaider = 3;
    public const int BruteRubyRaider = 10;
    public const int CrossbowRubyRaider = 18;
    public const int DampCultist = 21;
    public const int EyeWithTeeth = 26;
    public const int Exoskeleton = 25;
    public const int Flyconid = 30;
    public const int Fogmog = 31;
    public const int FossilStalker = 32;
    public const int GasBomb = 35;
    public const int FuzzyWurmCrawler = 34;
    public const int FatGremlin = 28;
    public const int GremlinMerc = 37;
    public const int HauntedShip = 39;
    public const int Inklet = 42;
    public const int LivingFog = 49;
    public const int LeafSlimeM = 47;
    public const int LeafSlimeS = 48;
    public const int Mawler = 53;
    public const int Nibbit = 56;
    public const int PunchConstruct = 65;
    public const int Seapunk = 69;
    public const int SewerClam = 70;
    public const int ShrinkerBeetle = 71;
    public const int SneakyGremlin = 78;
    public const int SnappingJaxfruit = 77;
    public const int SlitheringStrangler = 74;
    public const int SludgeSpinner = 75;
    public const int Toadpole = 93;
    public const int TrackerRubyRaider = 96;
    public const int TwigSlimeM = 99;
    public const int TwigSlimeS = 100;
    public const int TwoTailedRat = 101;
    public const int CubexConstruct = 20;
    public const int VineShambler = 103;
}
