using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;

namespace MegaCrit.Sts2.Core.Rewards;

public class RewardsSet
{
    public static Func<RewardsSet, Task>? testSelector;

    private bool _allowEmptyRewards;

    private bool _disallowSkipping;

    private bool _isGenerated;

    private readonly RewardsSetSynchronizer _synchronizer;

    public AbstractRoom? Room { get; private set; }

    public Player Player { get; }

    public List<Reward> Rewards { get; } = new List<Reward>();

    public bool ThrowInTestIfRewardsNotTaken { get; set; } = true;

    public bool DisallowSkipping => _disallowSkipping;

    public bool AllRewardsSuccessfullySelected => Rewards.All((Reward r) => r.SuccessfullySelected);

    public int Id { get; set; } = -1;

    public RewardsSet(Player player, RewardsSetSynchronizer? synchronizer = null)
    {
        Player = player;
        _synchronizer = synchronizer ?? RunManager.Instance.RewardsSetSynchronizer;
    }

    public RewardsSet EmptyForRoom(AbstractRoom room)
    {
        Room = room;
        _allowEmptyRewards = true;
        return this;
    }

    public RewardsSet WithRewardsFromRoom(AbstractRoom room)
    {
        Room = room;
        if (
            room.RoomType == RoomType.Boss
            && Player.RunState.CurrentActIndex >= Player.RunState.Acts.Count - 1
        )
        {
            return this;
        }
        if (!TryGenerateTutorialRewards(Player, room))
        {
            Rewards.AddRange(GenerateRewardsFor(Player, room));
        }
        if (
            Room is CombatRoom combatRoom
            && combatRoom.ExtraRewards.TryGetValue(Player, out List<Reward> value)
        )
        {
            Rewards.AddRange(value);
        }
        return this;
    }

    public RewardsSet WithCustomRewards(List<Reward> rewards)
    {
        Rewards.AddRange(rewards);
        return this;
    }

    public RewardsSet WithSkippingDisallowed()
    {
        _disallowSkipping = true;
        return this;
    }

    public async Task GenerateWithoutOffering()
    {
        if (_isGenerated)
        {
            return;
        }
        List<Reward> second = Rewards.ToList();
        foreach (Reward reward in Rewards)
        {
            reward.Populate();
        }
        IEnumerable<AbstractModel> modifiers = Hook.ModifyRewards(
            Player.RunState,
            Player,
            Rewards,
            Room
        );
        foreach (Reward item in Rewards.Except(second))
        {
            if (!item.IsPopulated)
            {
                item.Populate();
            }
        }
        await Hook.AfterModifyingRewards(Player.RunState, modifiers);
        Rewards.Sort((Reward x, Reward y) => x.RewardsSetIndex.CompareTo(y.RewardsSetIndex));
        _isGenerated = true;
    }

    public async Task Offer()
    {
        if (Player.Creature.IsDead)
        {
            return;
        }
        await GenerateWithoutOffering();
        bool flag = Room is CombatRoom;
        Task task = _synchronizer.BeginRewardsSet(this);
        if (Rewards.Count <= 0 && !flag && !_allowEmptyRewards)
        {
            return;
        }
        if (!Rewards.All((Reward r) => r.IsPopulated) && Rewards.Any((Reward r) => r.IsPopulated))
        {
            Log.Warn(
                "Some rewards are populated and others are not when calling RewardsCmd.Offer! This might lead to hooks getting called twice"
            );
        }
        if (LocalContext.IsMe(Player))
        {
            if (TestMode.IsOn)
            {
                if (testSelector != null)
                {
                    await testSelector(this);
                }
                else
                {
                    foreach (Reward reward in Rewards)
                    {
                        await _synchronizer.SelectLocalReward(reward);
                    }
                }
                if (!_synchronizer.IsRewardsSetCompleted(this) && ThrowInTestIfRewardsNotTaken)
                {
                    throw new InvalidOperationException(
                        "The RewardsSet is not complete after rewards were selected!"
                    );
                }
            }
            else
            {
                NRewardsScreen.ShowScreen(this, flag, Player.RunState);
            }
        }
        await task;
    }

    private List<Reward> GenerateRewardsFor(Player player, AbstractRoom room)
    {
        if (RunManager.Instance == null)
        {
            throw new InvalidOperationException("Only valid during a run.");
        }
        List<Reward> list = new List<Reward>();
        if (!(room is CombatRoom combatRoom))
        {
            if (!(room is TreasureRoom))
            {
                throw new InvalidOperationException(
                    "Tried to generate a reward for invalid room type: " + room.GetType().Name
                );
            }
        }
        else
        {
            switch (room.RoomType)
            {
                case RoomType.Monster:
                    if (combatRoom.GoldProportion > 0f)
                    {
                        list.Add(
                            new GoldReward(
                                (int)
                                    Math.Round(
                                        (float)combatRoom.Encounter.MinGoldReward
                                            * combatRoom.GoldProportion
                                    ),
                                (int)
                                    Math.Round(
                                        (float)combatRoom.Encounter.MaxGoldReward
                                            * combatRoom.GoldProportion
                                    ),
                                player
                            )
                        );
                    }
                    RollForPotionAndAddTo(list, player, room.RoomType);
                    list.Add(
                        new CardReward(
                            CardCreationOptions.ForRoom(player, room.RoomType),
                            3,
                            player
                        )
                    );
                    break;
                case RoomType.Elite:
                    list.Add(
                        new GoldReward(
                            combatRoom.Encounter.MinGoldReward,
                            combatRoom.Encounter.MaxGoldReward,
                            player
                        )
                    );
                    RollForPotionAndAddTo(list, player, room.RoomType);
                    list.Add(
                        new CardReward(
                            CardCreationOptions.ForRoom(player, room.RoomType),
                            3,
                            player
                        )
                    );
                    list.Add(new RelicReward(player));
                    break;
                case RoomType.Boss:
                    list.Add(
                        new GoldReward(
                            combatRoom.Encounter.MinGoldReward,
                            combatRoom.Encounter.MaxGoldReward,
                            player
                        )
                    );
                    RollForPotionAndAddTo(list, player, room.RoomType);
                    list.Add(
                        new CardReward(
                            CardCreationOptions.ForRoom(player, room.RoomType),
                            3,
                            player
                        )
                    );
                    break;
            }
        }
        return list;
    }

    private void RollForPotionAndAddTo(
        ICollection<Reward> rewards,
        Player player,
        RoomType roomType
    )
    {
        PotionRewardOdds potionReward = player.PlayerOdds.PotionReward;
        AscensionManager ascensionManager = RunManager.Instance.AscensionManager;
        if (potionReward.Roll(player, ascensionManager, roomType))
        {
            rewards.Add(new PotionReward(player));
        }
    }

    private bool TryGenerateTutorialRewards(Player player, AbstractRoom room)
    {
        CardCreationOptions rerollOptions = CardCreationOptions.ForRoom(player, room.RoomType);
        if (
            player.UnlockState.NumberOfRuns == 0
            && player.UnlockState.EpochUnlockCount() == 0
            && player.Character is Ironclad
            && room is CombatRoom combatRoom
        )
        {
            int num = player
                .RunState.MapPointHistory.SelectMany((IReadOnlyList<MapPointHistoryEntry> p) => p)
                .Count(
                    (MapPointHistoryEntry e) =>
                        e.Rooms.FindIndex(
                            (MapPointRoomHistoryEntry r) => r.RoomType == RoomType.Monster
                        ) >= 0
                );
            if (room.RoomType == RoomType.Monster && num <= 7)
            {
                (CardModel[], PotionModel)? tutorialMonsterRewards = GetTutorialMonsterRewards(
                    player,
                    num - 1
                );
                if (tutorialMonsterRewards.HasValue)
                {
                    (CardModel[], PotionModel) valueOrDefault =
                        tutorialMonsterRewards.GetValueOrDefault();
                    Rewards.Add(new GoldReward(10, 20, player));
                    if (valueOrDefault.Item2 != null)
                    {
                        Rewards.Add(new PotionReward(valueOrDefault.Item2, player));
                    }
                    Rewards.Add(
                        new CardReward(
                            valueOrDefault.Item1,
                            CardCreationSource.Encounter,
                            player,
                            rerollOptions
                        )
                    );
                    return true;
                }
                return false;
            }
            if (room.RoomType == RoomType.Elite)
            {
                switch (
                    player
                        .RunState.MapPointHistory.SelectMany(
                            (IReadOnlyList<MapPointHistoryEntry> l) => l
                        )
                        .Count((MapPointHistoryEntry e) => e.MapPointType == MapPointType.Elite)
                )
                {
                    case 1:
                    {
                        CardModel[] cardsToOffer2 = new CardModel[3]
                        {
                            player.RunState.CreateCard<Bludgeon>(player),
                            player.RunState.CreateCard<Pyre>(player),
                            player.RunState.CreateCard<EvilEye>(player),
                        };
                        Rewards.Add(
                            new GoldReward(
                                combatRoom.Encounter.MinGoldReward,
                                combatRoom.Encounter.MaxGoldReward,
                                player
                            )
                        );
                        Rewards.Add(
                            new PotionReward(ModelDb.Potion<BlockPotion>().ToMutable(), player)
                        );
                        Rewards.Add(new RelicReward(ModelDb.Relic<Vajra>().ToMutable(), player));
                        Rewards.Add(
                            new CardReward(
                                cardsToOffer2,
                                CardCreationSource.Encounter,
                                player,
                                rerollOptions
                            )
                        );
                        return true;
                    }
                    case 2:
                    {
                        CardModel[] cardsToOffer = new CardModel[3]
                        {
                            player.RunState.CreateCard<Pillage>(player),
                            player.RunState.CreateCard<Rampage>(player),
                            player.RunState.CreateCard<FlameBarrier>(player),
                        };
                        Rewards.Add(
                            new GoldReward(
                                combatRoom.Encounter.MinGoldReward,
                                combatRoom.Encounter.MaxGoldReward,
                                player
                            )
                        );
                        Rewards.Add(
                            new RelicReward(ModelDb.Relic<OrnamentalFan>().ToMutable(), player)
                        );
                        Rewards.Add(
                            new CardReward(
                                cardsToOffer,
                                CardCreationSource.Encounter,
                                player,
                                rerollOptions
                            )
                        );
                        return true;
                    }
                }
            }
            else if (
                room.RoomType == RoomType.Boss
                && player
                    .RunState.MapPointHistory.SelectMany(
                        (IReadOnlyList<MapPointHistoryEntry> l) => l
                    )
                    .Count((MapPointHistoryEntry e) => e.MapPointType == MapPointType.Boss) == 1
            )
            {
                CardModel[] cardsToOffer3 = new CardModel[3]
                {
                    player.RunState.CreateCard<PrimalForce>(player),
                    player.RunState.CreateCard<DemonForm>(player),
                    player.RunState.CreateCard<Thrash>(player),
                };
                Rewards.Add(
                    new GoldReward(
                        combatRoom.Encounter.MinGoldReward,
                        combatRoom.Encounter.MaxGoldReward,
                        player
                    )
                );
                Rewards.Add(
                    new CardReward(
                        cardsToOffer3,
                        CardCreationSource.Encounter,
                        player,
                        rerollOptions
                    )
                );
                return true;
            }
        }
        return false;
    }

    public static (CardModel[] Cards, PotionModel? Potion)? GetTutorialMonsterRewards(
        Player player,
        int index
    )
    {
        return index switch
        {
            0 => (
                new CardModel[3]
                {
                    player.RunState.CreateCard<SetupStrike>(player),
                    player.RunState.CreateCard<Tremble>(player),
                    player.RunState.CreateCard<BloodWall>(player),
                },
                null
            ),
            1 => (
                new CardModel[3]
                {
                    player.RunState.CreateCard<Breakthrough>(player),
                    player.RunState.CreateCard<Inflame>(player),
                    player.RunState.CreateCard<Anger>(player),
                },
                null
            ),
            2 => (
                new CardModel[3]
                {
                    player.RunState.CreateCard<IronWave>(player),
                    player.RunState.CreateCard<Dismantle>(player),
                    player.RunState.CreateCard<Cinder>(player),
                },
                ModelDb.Potion<FirePotion>().ToMutable()
            ),
            3 => (
                new CardModel[3]
                {
                    player.RunState.CreateCard<Stomp>(player),
                    player.RunState.CreateCard<ShrugItOff>(player),
                    player.RunState.CreateCard<Armaments>(player),
                },
                null
            ),
            4 => (
                new CardModel[3]
                {
                    player.RunState.CreateCard<Thunderclap>(player),
                    player.RunState.CreateCard<SetupStrike>(player),
                    player.RunState.CreateCard<Rage>(player),
                },
                ModelDb.Potion<StrengthPotion>().ToMutable()
            ),
            5 => (
                new CardModel[3]
                {
                    player.RunState.CreateCard<BattleTrance>(player),
                    player.RunState.CreateCard<TrueGrit>(player),
                    player.RunState.CreateCard<Uppercut>(player),
                },
                null
            ),
            6 => (
                new CardModel[3]
                {
                    player.RunState.CreateCard<Bloodletting>(player),
                    player.RunState.CreateCard<Whirlwind>(player),
                    player.RunState.CreateCard<Tremble>(player),
                },
                ModelDb.Potion<EnergyPotion>().ToMutable()
            ),
            _ => null,
        };
    }

    public override string ToString()
    {
        return $"Id: {Id} Owner: {Player.NetId} Rewards: {string.Join(",", Rewards)}";
    }
}
