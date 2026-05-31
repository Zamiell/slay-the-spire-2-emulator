using Sts2Emulator.Core.Rng;

namespace Sts2Emulator.Core.Run;

public enum RunFollowUp
{
    None,
    CardReward,
    TransformSelect,
}

public static class RunNonCombatEffects
{
    private static readonly int[] RareIroncladSingleplayerPool =
    [
        9,
        29,
        58,
        546,
        99,
        113,
        114,
        119,
        141,
        183,
        188,
        246,
        261,
        272,
        295,
        328,
        332,
        334,
        339,
        364,
        374,
        464,
        494,
        505,
        525,
    ];

    public static RunFollowUp ApplyRelicPickup(RunState state, int relicId)
    {
        if (state.Relics.All(relic => relic.DefId != relicId))
            state.Relics.Add(new RelicInstance(relicId));

        switch (relicId)
        {
            case RunConstants.RelicGoldenPearl:
                state.Gold += 150;
                break;
            case RunConstants.RelicNeowsTorment:
                state.Deck.Add(new CardInstance(RunConstants.NeowsFuryCard, Upgraded: false));
                break;
            case RunConstants.RelicNeowsBones:
                for (int i = 0; i < 2; i++)
                    ApplyRelicPickup(
                        state,
                        state.Rng.UpFront.NextItem(RunConstants.NeowPositiveOptions.ToArray())
                    );
                state.Deck.Add(
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                break;
            case RunConstants.RelicNutritiousOyster:
                GainMaxHp(state, 11);
                break;
            case RunConstants.RelicStrawberry:
                GainMaxHp(state, 7);
                break;
            case RunConstants.RelicPear:
                GainMaxHp(state, 10);
                break;
            case RunConstants.RelicMango:
                GainMaxHp(state, 14);
                break;
            case RunConstants.RelicLeesWaffle:
                GainMaxHp(state, 7);
                state.PlayerHp = state.PlayerMaxHp;
                break;
            case RunConstants.RelicOldCoin:
                state.Gold += 300;
                break;
            case RunConstants.RelicSmallCapsule:
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                break;
            case RunConstants.RelicLargeCapsule:
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                state.Deck.Add(new CardInstance(472, Upgraded: false));
                state.Deck.Add(new CardInstance(131, Upgraded: false));
                break;
            case RunConstants.RelicPomander:
                UpgradeFirstCard(state);
                break;
            case RunConstants.RelicNeowsTalisman:
                UpgradeLastCardMatching(state, 472);
                UpgradeLastCardMatching(state, 131);
                break;
            case RunConstants.RelicCursedPearl:
                state.Deck.Add(
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                state.Gold += 333;
                break;
            case RunConstants.RelicHeftyTablet:
                AddRandomRewardCard(state, state.Rng.UpFront);
                state.Deck.Add(
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                break;
            case RunConstants.RelicKaleidoscope:
                AddRandomRewardCard(state, state.Rng.UpFront);
                AddRandomRewardCard(state, state.Rng.UpFront);
                break;
            case RunConstants.RelicArcaneScroll:
                state.Deck.Add(
                    new CardInstance(
                        state.PlayerRng.Rewards.NextItem(RareIroncladSingleplayerPool),
                        Upgraded: false
                    )
                );
                break;
            case RunConstants.RelicLeadPaperweight:
                AddRandomRewardCard(state, state.Rng.UpFront);
                break;
            case RunConstants.RelicPhialHolster:
                RunRewardGenerator.AddPotion(
                    state,
                    RunRewardGenerator.NextPotion(state, state.PlayerRng.Rewards)
                );
                RunRewardGenerator.AddPotion(
                    state,
                    RunRewardGenerator.NextPotion(state, state.PlayerRng.Rewards)
                );
                break;
            case RunConstants.RelicPreciseScissors:
                RemoveLowestPriorityCard(state);
                break;
            case RunConstants.RelicScrollBoxes:
                AddRandomRewardCard(state, state.Rng.UpFront);
                AddRandomRewardCard(state, state.Rng.UpFront);
                AddRandomRewardCard(state, state.Rng.UpFront);
                break;
            case RunConstants.RelicLeafyPoultice:
                state.PlayerMaxHp = Math.Max(1, state.PlayerMaxHp - 12);
                state.PlayerHp = Math.Min(state.PlayerHp, state.PlayerMaxHp);
                TransformFirstCardMatching(state, 472);
                TransformFirstCardMatching(state, 131);
                break;
            case RunConstants.RelicPrecariousShears:
                RemoveLowestPriorityCard(state);
                RemoveLowestPriorityCard(state);
                state.PlayerHp = Math.Max(0, state.PlayerHp - 16);
                break;
            case RunConstants.RelicSilkenTress:
                state.Gold = 0;
                break;
            case RunConstants.RelicPandorasBox:
                TransformAllMatching(state, 472);
                TransformAllMatching(state, 131);
                break;
            case RunConstants.RelicCallingBell:
                state.Deck.Add(
                    new CardInstance(RunConstants.CursePlaceholderCard, Upgraded: false)
                );
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                ApplyRelicPickup(state, RunRewardGenerator.NextRelic(state));
                break;
            case RunConstants.RelicDustyTome:
                state.Deck.Add(
                    new CardInstance(RandomRewardCard(state.Rng.UpFront), Upgraded: true)
                );
                break;
            case RunConstants.RelicPrismaticGem:
                AddRandomRewardCard(state, state.Rng.UpFront);
                break;
            case RunConstants.RelicNewLeaf:
                state.TransformSelectedDeckIndex = null;
                return RunFollowUp.TransformSelect;
            case RunConstants.RelicAstrolabe:
                state.TransformSelectedDeckIndex = -3;
                return RunFollowUp.TransformSelect;
            case RunConstants.RelicEmptyCage:
                state.TransformSelectedDeckIndex = -2;
                return RunFollowUp.TransformSelect;
        }

        return RunFollowUp.None;
    }

    public static void EnterEvent(RunState state)
    {
        state.EventValue0 = null;
        state.EventValue1 = null;
        List<int> eventPool = [];
        while (state.EventSequenceIndex < state.EventSequence.Length)
        {
            int eventId = state.EventSequence[state.EventSequenceIndex++];
            if (IsEventAllowed(state, eventId))
            {
                state.EventId = eventId;
                CalculateEventVars(state);
                state.Phase = RunPhase.Event;
                return;
            }
        }

        eventPool.Add(RunConstants.EventJungleMazeAdventure);
        eventPool.Add(RunConstants.EventBrainLeech);
        eventPool.Add(RunConstants.EventDoorsOfLightAndDark);
        eventPool.Add(RunConstants.EventSunkenTreasury);
        if (
            state.PlayerHp >= 10
            && state.Deck.Any(card => card.DefId != RunConstants.SpoilsMapCard)
        )
            eventPool.Add(RunConstants.EventTheLegendsWereTrue);
        if (state.Gold >= 100 && state.Deck.Count >= 2)
            eventPool.Add(RunConstants.EventMorphicGrove);
        if (state.PlayerHp <= (int)(state.PlayerMaxHp * 0.7))
        {
            eventPool.Add(RunConstants.EventUnrestSite);
            eventPool.Add(RunConstants.EventAromaOfChaos);
            eventPool.Add(RunConstants.EventSimpleReward);
        }
        else
        {
            eventPool.Add(RunConstants.EventAromaOfChaos);
            eventPool.Add(RunConstants.EventSimpleReward);
        }

        state.EventId = state.Rng.UpFront.NextItem(eventPool);
        CalculateEventVars(state);
        state.Phase = RunPhase.Event;
    }

    private static bool IsEventAllowed(RunState state, int eventId)
    {
        return eventId switch
        {
            RunConstants.EventMorphicGrove => state.Gold >= 100 && state.Deck.Count >= 2,
            RunConstants.EventTheLegendsWereTrue => state.PlayerHp >= 10
                && state.Deck.Any(card => card.DefId != RunConstants.SpoilsMapCard),
            RunConstants.EventUnrestSite
            or RunConstants.EventAromaOfChaos
            or RunConstants.EventSimpleReward => true,
            _ => true,
        };
    }

    public static int SunkenTreasurySmallChestGold(RunState state)
    {
        EnsureSunkenTreasuryVars(state);
        return state.EventValue0!.Value;
    }

    public static int SunkenTreasuryLargeChestGold(RunState state)
    {
        EnsureSunkenTreasuryVars(state);
        return state.EventValue1!.Value;
    }

    private static void EnsureSunkenTreasuryVars(RunState state)
    {
        if (state.EventValue0 is null || state.EventValue1 is null)
            CalculateSunkenTreasuryVars(state);
    }

    private static void CalculateEventVars(RunState state)
    {
        if (state.EventId == RunConstants.EventSunkenTreasury)
            CalculateSunkenTreasuryVars(state);
    }

    private static void CalculateSunkenTreasuryVars(RunState state)
    {
        GameRng rng = EventRng(state, "SUNKEN_TREASURY");
        state.EventValue0 = 60 + rng.NextInt(16) - 8;
        state.EventValue1 = 333 + rng.NextInt(61) - 30;
    }

    private static GameRng EventRng(RunState state, string eventEntry)
    {
        uint eventSeed = unchecked(
            state.Rng.Seed + 1u + (uint)DeterministicHash.GetDeterministicHashCode(eventEntry)
        );
        return new GameRng(eventSeed);
    }

    public static void GainMaxHp(RunState state, int amount)
    {
        state.PlayerMaxHp += amount;
        state.PlayerHp = Math.Min(state.PlayerMaxHp, state.PlayerHp + amount);
    }

    public static bool UpgradeFirstCard(RunState state)
    {
        for (int i = 0; i < state.Deck.Count; i++)
        {
            if (!RunConstants.IsRunCardUpgradable(state.Deck[i]))
                continue;
            state.Deck[i] = state.Deck[i] with { Upgraded = true };
            return true;
        }
        return false;
    }

    public static void UpgradeTwoRandomCardsWithNiche(RunState state)
    {
        var indexes = state
            .Deck.Select((card, index) => (card, index))
            .Where(item => RunConstants.IsRunCardUpgradable(item.card))
            .Select(item => item.index)
            .OrderBy(index => Math.Abs(state.Deck[index].DefId))
            .ToList();
        state.Rng.Niche.Shuffle(indexes);
        foreach (int index in indexes.Take(2))
            state.Deck[index] = state.Deck[index] with { Upgraded = true };
    }

    public static void RemoveLowestPriorityCard(RunState state)
    {
        if (state.Deck.Count == 0)
            return;

        foreach (int cardId in new[] { RunConstants.CursePlaceholderCard, 472, 131, 30 })
        {
            int index = state.Deck.FindIndex(card => Math.Abs(card.DefId) == cardId);
            if (index >= 0)
            {
                state.Deck.RemoveAt(index);
                return;
            }
        }
        state.Deck.RemoveAt(state.Deck.Count - 1);
    }

    public static void TransformCardAt(RunState state, int deckIndex, GameRng rng)
    {
        if ((uint)deckIndex >= (uint)state.Deck.Count)
            return;

        int originalId = Math.Abs(state.Deck[deckIndex].DefId);
        var pool = RunRewardGenerator
            .IroncladRewardPool.ToArray()
            .Where(cardId => cardId != originalId)
            .ToArray();
        if (pool.Length == 0)
            return;
        state.Deck[deckIndex] = new CardInstance(rng.NextItem(pool), Upgraded: false);
    }

    public static void TransformFirstCard(RunState state) =>
        TransformCardAt(state, 0, state.PlayerRng.Transformations);

    public static void TransformFirstCardMatching(RunState state, int cardId)
    {
        int index = state.Deck.FindIndex(card => Math.Abs(card.DefId) == cardId);
        if (index >= 0)
            TransformCardAt(state, index, state.PlayerRng.Transformations);
    }

    private static void TransformAllMatching(RunState state, int cardId)
    {
        for (int i = 0; i < state.Deck.Count; i++)
            if (Math.Abs(state.Deck[i].DefId) == cardId)
                TransformCardAt(state, i, state.PlayerRng.Transformations);
    }

    private static void UpgradeLastCardMatching(RunState state, int cardId)
    {
        for (int i = state.Deck.Count - 1; i >= 0; i--)
        {
            if (state.Deck[i].DefId != cardId || !RunConstants.IsRunCardUpgradable(state.Deck[i]))
                continue;
            state.Deck[i] = state.Deck[i] with { Upgraded = true };
            return;
        }
    }

    private static int RandomRewardCard(GameRng rng) =>
        rng.NextItem(RunRewardGenerator.IroncladRewardPool.ToArray());

    private static void AddRandomRewardCard(RunState state, GameRng rng)
    {
        state.Deck.Add(new CardInstance(RandomRewardCard(rng), Upgraded: false));
    }
}
