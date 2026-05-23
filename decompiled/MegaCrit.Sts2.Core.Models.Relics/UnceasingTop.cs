using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MegaCrit.Sts2.Core.Models.Relics;

public sealed class UnceasingTop : RelicModel
{
	public override string FlashSfx => "event:/sfx/ui/relic_activate_draw";

	public override RelicRarity Rarity => RelicRarity.Rare;

	public override async Task AfterHandEmptied(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner && IsValidPhase(player.PlayerCombatState.Phase))
		{
			Flash();
			await CardPileCmd.Draw(choiceContext, player);
		}
	}

	private static bool IsValidPhase(PlayerTurnPhase phase)
	{
		if ((uint)(phase - 2) <= 2u)
		{
			return true;
		}
		return false;
	}
}
