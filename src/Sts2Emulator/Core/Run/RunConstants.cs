using Sts2Emulator.Core.Effects;

namespace Sts2Emulator.Core.Run;

public static class RunConstants
{
    public const int CombatObsSize = 164;
    public const int RunExtraObsSize = 35;
    public const int RunObsSize = CombatObsSize + RunExtraObsSize;
    public const int RunInfoSize = 11;
    public const int MaxActions = 32;
    public const int MapChoices = 4;
    public const int MapWidth = 7;
    public const int MapBossRow = 16;
    public const int MapPathIterations = 7;
    public const int MapTreasureRow = MapBossRow - 7;
    public const int MapFinalRestRow = MapBossRow - 1;
    public const int MapStartCol = MapWidth / 2;
    public const int RewardSkipAction = 3;
    public const int RestHealAction = 0;
    public const int RestUpgradeAction = 1;
    public const int ShopRemoveAction = 13;
    public const int ShopSkipAction = 14;
    public const int EventSkipAction = 3;

    public const int NodeNone = 0;
    public const int NodeNormal = 1;
    public const int NodeElite = 2;
    public const int NodeRest = 3;
    public const int NodeShop = 4;
    public const int NodeRelic = 5;
    public const int NodeBoss = 6;
    public const int NodeEvent = 7;

    public const int ActOvergrowth = 1;
    public const int ActUnderdocks = 2;

    public const int EventResultPending = -1;
    public const int EventUnrestSite = 1;
    public const int EventAromaOfChaos = 2;
    public const int EventSimpleReward = 3;
    public const int EventJungleMazeAdventure = 4;
    public const int EventMorphicGrove = 5;
    public const int EventBrainLeech = 6;
    public const int EventTheLegendsWereTrue = 7;
    public const int EventDoorsOfLightAndDark = 8;
    public const int EventSunkenTreasury = 9;

    public const int SlimesWeakEncounterId = 3;
    public const int GremlinMercEncounterId = 7;

    public static ReadOnlySpan<int> OvergrowthWeakEncounters => [8, 2, 11, 3];
    public static ReadOnlySpan<int> UnderdocksWeakEncounters => [9, 12, 10, 13];
    public static ReadOnlySpan<int> OvergrowthNormalEncounters =>
        [19, 17, 29, 5, 14, 15, 21, 28, 16, 27, 18, 20];
    public static ReadOnlySpan<int> UnderdocksNormalEncounters =>
        [9, 0, 23, 7, 26, 30, 24, 12, 25, 6];
    public static ReadOnlySpan<int> OvergrowthEliteEncounters => [68, 65];
    public static ReadOnlySpan<int> UnderdocksEliteEncounters => [72, 67];
    public static ReadOnlySpan<int> OvergrowthBossEncounters => [83, 74, 82];
    public static ReadOnlySpan<int> UnderdocksBossEncounters => [84, 79, 77];

    public const int RelicBurningBlood = 36;
    public const int RelicBlackBlood = 19;
    public const int RelicMeatOnTheBone = 149;
    public const int RelicArcaneScroll = 5;
    public const int RelicCursedPearl = 54;
    public const int RelicFishingRod = 89;
    public const int RelicGoldenPearl = 105;
    public const int RelicHeftyTablet = 111;
    public const int RelicKaleidoscope = 124;
    public const int RelicLeadPaperweight = 133;
    public const int RelicLeafyPoultice = 134;
    public const int RelicLeesWaffle = 135;
    public const int RelicLostCoffer = 140;
    public const int RelicMango = 144;
    public const int RelicLargeCapsule = 129;
    public const int RelicLavaRock = 132;
    public const int RelicNeowsBones = 161;
    public const int RelicNeowsTalisman = 162;
    public const int RelicNeowsTorment = 163;
    public const int RelicNewLeaf = 164;
    public const int RelicNutritiousOyster = 167;
    public const int RelicOldCoin = 170;
    public const int RelicPear = 190;
    public const int RelicPhialHolster = 195;
    public const int RelicPomander = 201;
    public const int RelicPrecariousShears = 205;
    public const int RelicPreciseScissors = 206;
    public const int RelicScrollBoxes = 231;
    public const int RelicSilkenTress = 239;
    public const int RelicSilverCrucible = 240;
    public const int RelicSmallCapsule = 242;
    public const int RelicStrawberry = 252;
    public const int RelicStoneHumidifier = 250;
    public const int RelicWingedBoots = 293;
    public const int RelicPrismaticGem = 1533;

    public const int RelicAstrolabe = 1332;
    public const int RelicCallingBell = 1363;
    public const int RelicDustyTome = 1394;
    public const int RelicEmptyCage = 1399;
    public const int RelicPandorasBox = 1510;

    public const int CursePlaceholderCard = 10001;
    public const int SpoilsMapCard = 10002;
    public const int NeowsFuryCard = 321;

    public static ReadOnlySpan<int> StarterDeckIds =>
        [
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.StrikeIronclad,
            IC.DefendIronclad,
            IC.DefendIronclad,
            IC.DefendIronclad,
            IC.DefendIronclad,
            IC.Bash,
            IC.AscendersBane,
        ];

    public static ReadOnlySpan<int> NeowCurseOptions => [54, 111, 129, 134, 161, 205, 239, 240];

    public static ReadOnlySpan<int> NeowPositiveOptions =>
        [5, 29, 89, 105, 124, 133, 140, 163, 164, 195, 206, 231, 293];

    public static bool IsRunCardUpgradable(CardInstance card)
    {
        return !card.Upgraded && !IsNonUpgradableCard(card.DefId);
    }

    private static bool IsNonUpgradableCard(int cardId)
    {
        return cardId
            is 36
                or 128
                or 166
                or 206
                or 440
                or 457
                or 512
                or 10001
                or 10002
                or 10008
                or 10009
                or 10010
                or 10011
                or 10012;
    }
}
