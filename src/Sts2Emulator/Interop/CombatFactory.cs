using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;

namespace Sts2Emulator.Interop;

public static class CombatFactory
{
    private const int StartingPlayerHp = 64;
    private const int StartingPlayerMaxHp = 80;
    private const int StartingEnergy = 3;

    // Ironclad starting deck card IDs (from Generated/Cards.g.cs).
    private const int StrikeId = IC.StrikeIronclad; // 472
    private const int DefendId = IC.DefendIronclad;  // 131
    private const int BashId = IC.Bash;              // 30
    private const int AscendersBaneId = IC.AscendersBane; // 10001

    private enum ActOneEncounter
    {
        Cultists,
        Chompers,
        Nibbit,
        Slimes,
        Exoskeletons,
        Inklets,
        TwoTailedRats,
        GremlinMerc,
        FuzzyWurmCrawler,
        CorpseSlugs,
        SludgeSpinner,
        ShrinkerBeetle,
        Seapunk,
        Toadpoles,
        Mawler,
        Nibbits,
        LargeSlimes,
        SlimeAndFlyconid,
        JaxfruitAndFlyconid,
        CubexConstruct,
        VineShambler,
        ShrinkerAndFuzzy,
        CultistAndSeapunk,
        FossilStalker,
        PunchConstruct,
        SewerClam,
        HauntedShip,
        SlitheringStrangler,
        RubyRaiders,
        Fogmog,
        LivingFog,
    }

    private static readonly ActOneEncounter[] OvergrowthWeakEncounters =
    [
        ActOneEncounter.Nibbit,
        ActOneEncounter.Slimes,
        ActOneEncounter.ShrinkerBeetle,
        ActOneEncounter.FuzzyWurmCrawler,
    ];

    private static readonly ActOneEncounter[] UnderdocksWeakEncounters =
    [
        ActOneEncounter.CorpseSlugs,
        ActOneEncounter.Seapunk,
        ActOneEncounter.SludgeSpinner,
        ActOneEncounter.Toadpoles,
    ];

    public static CombatState NewCombat(int seed)
    {
        return NewCombat(new Random(seed));
    }

    public static CombatState NewCombat(Random rng)
    {
        var state = new CombatState
        {
            PlayerHp    = StartingPlayerHp,
            PlayerMaxHp = StartingPlayerMaxHp,
            Energy      = StartingEnergy,
            MaxEnergy   = StartingEnergy,
        };
        Reset(state, rng);
        return state;
    }

    public static void Reset(CombatState state, int? seed = null)
    {
        Reset(state, seed.HasValue ? new Random(seed.Value) : new Random());
    }

    public static void Reset(CombatState state, Random rng)
    {
        Reset(state, rng, StarterDeck());
    }

    public static void Reset(CombatState state, Random rng, ReadOnlySpan<int> deckIds)
    {
        Reset(state, rng, deckIds, null);
    }

    public static void Reset(CombatState state, Random rng, ReadOnlySpan<int> deckIds, int? encounterId)
    {
        state.PlayerHp     = StartingPlayerHp;
        state.PlayerMaxHp  = StartingPlayerMaxHp;
        state.Energy       = StartingEnergy;
        state.MaxEnergy    = StartingEnergy;
        state.PlayerBlock  = 0;
        state.PlayerBuffs  = [];
        state.Hand         = [];
        state.DiscardPile  = [];
        state.ExhaustPile  = [];
        state.PotionSlots  = new int[3];
        state.Turn         = 0;
        state.PlayerTurn   = true;
        state.SkillPlayedWhileSmoggy = false;

        state.DrawPile = deckIds.ToArray().Select(id => new CardInstance(id, false)).ToList();

        var encounter = encounterId.HasValue
            ? (ActOneEncounter)encounterId.Value
            : SelectFirstCombatEncounter(rng);
        state.EncounterId = (int)encounter;
        state.Enemies = CreateEncounter(encounter, rng);

        // Shuffle draw pile and deal opening hand of 5.
        state.DrawPile = state.DrawPile.OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < 5 && state.DrawPile.Count > 0; i++)
        {
            state.Hand.Add(state.DrawPile[0]);
            state.DrawPile.RemoveAt(0);
        }
    }

    private static int[] StarterDeck() =>
    [
        .. Enumerable.Repeat(StrikeId, 5),
        .. Enumerable.Repeat(DefendId, 4),
        BashId,
        AscendersBaneId,
    ];

    private static List<EnemyState> CreateEncounter(ActOneEncounter encounter, Random rng) =>
        encounter switch
        {
            ActOneEncounter.Cultists =>
            [
                CreateEnemy(KE.CalcifiedCultist, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(KE.DampCultist, rng, new Intent(IntentType.Buff, 0)),
            ],

            ActOneEncounter.Chompers =>
            [
                CreateChomper(rng, new Intent(IntentType.Attack, 18)),
                CreateChomper(rng, new Intent(IntentType.Debuff, 3), moveIndex: 1),
            ],

            ActOneEncounter.Nibbit =>
            [
                CreateEnemy(KE.Nibbit, rng, new Intent(IntentType.Attack, 13)),
            ],

            ActOneEncounter.Slimes => CreateSlimeEncounter(rng),

            ActOneEncounter.Exoskeletons =>
            [
                CreateExoskeleton(rng, new Intent(IntentType.Attack, 4)),
                CreateExoskeleton(rng, new Intent(IntentType.Attack, 9)),
                CreateExoskeleton(rng, new Intent(IntentType.Buff, 0)),
            ],

            ActOneEncounter.Inklets =>
            [
                CreateInklet(rng, new Intent(IntentType.Attack, 4)),
                CreateInklet(rng, new Intent(IntentType.Attack, 9), moveIndex: 1),
                CreateInklet(rng, new Intent(IntentType.Attack, 4)),
            ],

            ActOneEncounter.TwoTailedRats => CreateTwoTailedRatsEncounter(rng),

            ActOneEncounter.GremlinMerc =>
            [
                CreateGremlinMerc(rng),
            ],

            ActOneEncounter.FuzzyWurmCrawler =>
            [
                CreateEnemy(KE.FuzzyWurmCrawler, rng, new Intent(IntentType.Attack, 6)),
            ],

            ActOneEncounter.CorpseSlugs => CreateCorpseSlugsEncounter(rng),

            ActOneEncounter.SludgeSpinner =>
            [
                CreateEnemy(KE.SludgeSpinner, rng, new Intent(IntentType.Debuff, 9)),
            ],

            ActOneEncounter.ShrinkerBeetle =>
            [
                CreateEnemy(KE.ShrinkerBeetle, rng, new Intent(IntentType.Debuff, 1)),
            ],

            ActOneEncounter.Seapunk =>
            [
                CreateEnemy(KE.Seapunk, rng, new Intent(IntentType.Attack, 13)),
            ],

            ActOneEncounter.Toadpoles =>
            [
                CreateEnemy(KE.Toadpole, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(KE.Toadpole, rng, new Intent(IntentType.Attack, 8), moveIndex: 1),
            ],

            ActOneEncounter.Mawler =>
            [
                CreateEnemy(KE.Mawler, rng, new Intent(IntentType.Attack, 10)),
            ],

            ActOneEncounter.Nibbits =>
            [
                CreateEnemy(KE.Nibbit, rng, new Intent(IntentType.Attack, 13)),
                CreateEnemy(KE.Nibbit, rng, new Intent(IntentType.Attack, 7), moveIndex: 1),
            ],

            ActOneEncounter.LargeSlimes =>
            [
                CreateSlime(KE.LeafSlimeM, rng),
                CreateSlime(KE.TwigSlimeM, rng),
                CreateSlime(KE.LeafSlimeS, rng),
                CreateSlime(KE.TwigSlimeS, rng),
            ],

            ActOneEncounter.SlimeAndFlyconid =>
            [
                CreateSlime(rng.Next(2) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM, rng),
                CreateEnemy(KE.Flyconid, rng, FlyconidInitialIntent(rng), moveIndex: rng.Next(2)),
            ],

            ActOneEncounter.JaxfruitAndFlyconid =>
            [
                CreateEnemy(KE.SnappingJaxfruit, rng, new Intent(IntentType.Buff, 4)),
                CreateEnemy(KE.Flyconid, rng, FlyconidInitialIntent(rng), moveIndex: rng.Next(2)),
            ],

            ActOneEncounter.CubexConstruct =>
            [
                CreateCubexConstruct(rng),
            ],

            ActOneEncounter.VineShambler =>
            [
                CreateEnemy(KE.VineShambler, rng, new Intent(IntentType.Attack, 14)),
            ],

            ActOneEncounter.ShrinkerAndFuzzy =>
            [
                CreateEnemy(KE.ShrinkerBeetle, rng, new Intent(IntentType.Debuff, 1)),
                CreateEnemy(KE.FuzzyWurmCrawler, rng, new Intent(IntentType.Attack, 6)),
            ],

            ActOneEncounter.CultistAndSeapunk =>
            [
                CreateEnemy(KE.CalcifiedCultist, rng, new Intent(IntentType.Buff, 0)),
                CreateEnemy(KE.Seapunk, rng, new Intent(IntentType.Attack, 13)),
            ],

            ActOneEncounter.FossilStalker =>
            [
                CreateEnemy(KE.FossilStalker, rng, new Intent(IntentType.Attack, 14), moveIndex: 1),
            ],

            ActOneEncounter.PunchConstruct =>
            [
                CreatePunchConstruct(rng, startsWithFastPunch: rng.Next(2) == 0),
            ],

            ActOneEncounter.SewerClam =>
            [
                CreateSewerClam(rng),
            ],

            ActOneEncounter.HauntedShip =>
            [
                CreateEnemy(KE.HauntedShip, rng, new Intent(IntentType.Debuff, 5)),
            ],

            ActOneEncounter.SlitheringStrangler =>
            [
                CreateEnemy(KE.SlitheringStrangler, rng, new Intent(IntentType.Debuff, 3)),
                rng.Next(3) switch
                {
                    0 => CreateEnemy(KE.SnappingJaxfruit, rng, new Intent(IntentType.Buff, 4)),
                    1 => CreateSlime(rng.Next(2) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM, rng),
                    _ => CreateSlime(rng.Next(2) == 0 ? KE.LeafSlimeS : KE.TwigSlimeS, rng),
                },
            ],

            ActOneEncounter.RubyRaiders => CreateRubyRaiders(rng),

            ActOneEncounter.Fogmog =>
            [
                CreateEnemy(KE.Fogmog, rng, new Intent(IntentType.Buff, 0)),
            ],

            ActOneEncounter.LivingFog =>
            [
                CreateEnemy(KE.LivingFog, rng, new Intent(IntentType.Debuff, 9)),
            ],

            _ => throw new ArgumentOutOfRangeException(nameof(encounter), encounter, null),
        };

    private static ActOneEncounter SelectFirstCombatEncounter(Random rng)
    {
        var pool = rng.Next(2) == 0 ? OvergrowthWeakEncounters : UnderdocksWeakEncounters;
        return pool[rng.Next(pool.Length)];
    }

    private static List<EnemyState> CreateSlimeEncounter(Random rng)
    {
        int firstSmall = rng.Next(2) == 0 ? KE.LeafSlimeS : KE.TwigSlimeS;
        int middle = rng.Next(2) == 0 ? KE.LeafSlimeM : KE.TwigSlimeM;
        int secondSmall = firstSmall == KE.LeafSlimeS ? KE.TwigSlimeS : KE.LeafSlimeS;

        return
        [
            CreateSlime(firstSmall, rng),
            CreateSlime(middle, rng),
            CreateSlime(secondSmall, rng),
        ];
    }

    private static EnemyState CreateSlime(int defId, Random rng)
    {
        return defId switch
        {
            KE.LeafSlimeS => rng.Next(2) == 0
                ? CreateEnemy(defId, rng, new Intent(IntentType.Attack, 4))
                : CreateEnemy(defId, rng, new Intent(IntentType.Debuff, 1), moveIndex: 1),

            KE.TwigSlimeS => CreateEnemy(defId, rng, new Intent(IntentType.Attack, 5)),

            KE.LeafSlimeM => CreateEnemy(defId, rng, new Intent(IntentType.Debuff, 2), moveIndex: 1),

            KE.TwigSlimeM => CreateEnemy(defId, rng, new Intent(IntentType.Debuff, 1), moveIndex: 1),

            _ => throw new ArgumentOutOfRangeException(nameof(defId), defId, null),
        };
    }

    private static List<EnemyState> CreateTwoTailedRatsEncounter(Random rng)
    {
        int firstMove = rng.Next(3);
        return
        [
            CreateTwoTailedRat(rng, firstMove),
            CreateTwoTailedRat(rng, (firstMove + 1) % 3),
            CreateTwoTailedRat(rng, (firstMove + 2) % 3),
        ];
    }

    private static List<EnemyState> CreateRubyRaiders(Random rng)
    {
        int[] raiders =
        [
            KE.AxeRubyRaider,
            KE.AssassinRubyRaider,
            KE.BruteRubyRaider,
            KE.CrossbowRubyRaider,
            KE.TrackerRubyRaider,
        ];

        return raiders
            .OrderBy(_ => rng.Next())
            .Take(3)
            .Select(id => id switch
            {
                KE.AxeRubyRaider => CreateEnemy(id, rng, new Intent(IntentType.Attack, 6)),
                KE.AssassinRubyRaider => CreateEnemy(id, rng, new Intent(IntentType.Attack, 11)),
                KE.BruteRubyRaider => CreateEnemy(id, rng, new Intent(IntentType.Attack, 8)),
                KE.CrossbowRubyRaider => CreateEnemy(id, rng, new Intent(IntentType.Defend, 3)),
                KE.TrackerRubyRaider => CreateEnemy(id, rng, new Intent(IntentType.Debuff, 2)),
                _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
            })
            .ToList();
    }

    private static EnemyState CreateTwoTailedRat(Random rng, int moveIndex)
    {
        var enemy = CreateEnemy(KE.TwoTailedRat, rng, RatIntent(moveIndex), moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.SummonCooldown, 2);
        return enemy;
    }

    private static Intent RatIntent(int moveIndex) =>
        (moveIndex % 3) switch
        {
            0 => new Intent(IntentType.Attack, 9),
            1 => new Intent(IntentType.Attack, 7),
            _ => new Intent(IntentType.Debuff, 1),
        };

    private static Intent FlyconidInitialIntent(Random rng) =>
        rng.Next(3) switch
        {
            0 or 1 => new Intent(IntentType.Debuff, 9),
            _ => new Intent(IntentType.Attack, 12),
        };

    private static List<EnemyState> CreateCorpseSlugsEncounter(Random rng)
    {
        int firstMove = rng.Next(3);
        return
        [
            CreateCorpseSlug(rng, firstMove),
            CreateCorpseSlug(rng, (firstMove + 1) % 3),
        ];
    }

    private static EnemyState CreateCorpseSlug(Random rng, int moveIndex) =>
        CreateCorpseSlugEnemy(rng, moveIndex);

    private static EnemyState CreateCorpseSlugEnemy(Random rng, int moveIndex)
    {
        var enemy = CreateEnemy(KE.CorpseSlug, rng, CorpseSlugIntent(moveIndex), moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.Ravenous, 5);
        return enemy;
    }

    private static Intent CorpseSlugIntent(int moveIndex) =>
        (moveIndex % 3) switch
        {
            0 => new Intent(IntentType.Attack, 6),
            1 => new Intent(IntentType.Attack, 9),
            _ => new Intent(IntentType.Debuff, 2),
        };

    private static EnemyState CreateEnemy(
        int defId, Random rng, Intent startingIntent, int moveIndex = 0)
    {
        var def = GeneratedData.Enemies.Get(defId);
        int hp = rng.Next(def.MinHp, def.MaxHp + 1);
        return new EnemyState
        {
            DefId         = defId,
            Hp            = hp,
            MaxHp         = hp,
            Block         = 0,
            CurrentIntent = startingIntent,
            Buffs         = [],
            MoveIndex     = moveIndex,
        };
    }

    private static EnemyState CreateChomper(
        Random rng, Intent startingIntent, int moveIndex = 0)
    {
        var enemy = CreateEnemy(KE.Chomper, rng, startingIntent, moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 2);
        return enemy;
    }

    private static EnemyState CreateInklet(
        Random rng, Intent startingIntent, int moveIndex = 0)
    {
        var enemy = CreateEnemy(KE.Inklet, rng, startingIntent, moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.Slippery, 1);
        return enemy;
    }

    private static EnemyState CreateGremlinMerc(Random rng)
    {
        var enemy = CreateEnemy(KE.GremlinMerc, rng, new Intent(IntentType.Attack, 16));
        BuffSystem.Apply(enemy.Buffs, BuffId.Surprise, 1);
        return enemy;
    }

    private static EnemyState CreateCubexConstruct(Random rng)
    {
        var enemy = CreateEnemy(KE.CubexConstruct, rng, new Intent(IntentType.Buff, 0));
        enemy.Block = 13;
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 1);
        return enemy;
    }

    private static EnemyState CreatePunchConstruct(Random rng, bool startsWithFastPunch)
    {
        var enemy = CreateEnemy(
            KE.PunchConstruct,
            rng,
            startsWithFastPunch ? new Intent(IntentType.Debuff, 12) : new Intent(IntentType.Defend, 10),
            startsWithFastPunch ? 1 : 0);
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 1);
        return enemy;
    }

    private static EnemyState CreateSewerClam(Random rng)
    {
        var enemy = CreateEnemy(KE.SewerClam, rng, new Intent(IntentType.Attack, 11), moveIndex: 1);
        BuffSystem.Apply(enemy.Buffs, BuffId.Plating, 9);
        return enemy;
    }

    private static EnemyState CreateExoskeleton(
        Random rng, Intent startingIntent, int moveIndex = 0)
    {
        var enemy = CreateEnemy(KE.Exoskeleton, rng, startingIntent, moveIndex);
        BuffSystem.Apply(enemy.Buffs, BuffId.HardToKill, 9);
        return enemy;
    }
}
