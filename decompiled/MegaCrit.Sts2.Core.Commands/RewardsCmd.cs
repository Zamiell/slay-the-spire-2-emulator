using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace MegaCrit.Sts2.Core.Commands;

public static class RewardsCmd
{
    public static async Task OfferForRoomEnd(Player player, AbstractRoom room)
    {
        RewardsSet rewardsSet;
        if (room is CombatRoom combatRoom)
        {
            EncounterModel encounter = combatRoom.Encounter;
            if (encounter != null && !encounter.ShouldGiveRewards)
            {
                rewardsSet = new RewardsSet(player).EmptyForRoom(room);
                goto IL_00b4;
            }
        }
        rewardsSet = new RewardsSet(player).WithRewardsFromRoom(room);
        goto IL_00b4;
        IL_00b4:
        await rewardsSet.Offer();
    }

    public static async Task OfferCustom(Player player, List<Reward> rewards)
    {
        await new RewardsSet(player).WithCustomRewards(rewards).Offer();
    }

    public static async Task<RewardsSet> GenerateForRoomEnd(Player player, AbstractRoom room)
    {
        RewardsSet set = new RewardsSet(player).WithRewardsFromRoom(room);
        await set.GenerateWithoutOffering();
        return set;
    }

    public static async Task<RewardsSet> GenerateCustom(Player player, List<Reward> rewards)
    {
        RewardsSet set = new RewardsSet(player).WithCustomRewards(rewards);
        await set.GenerateWithoutOffering();
        return set;
    }
}
