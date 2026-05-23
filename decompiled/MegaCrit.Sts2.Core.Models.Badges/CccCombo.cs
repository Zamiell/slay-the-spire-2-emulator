using MegaCrit.Sts2.Core.Saves;

namespace MegaCrit.Sts2.Core.Models.Badges;

public class CccCombo : Badge
{
	public override BadgeRarity Rarity => BadgeRarity.Bronze;

	public CccCombo(SerializableRun run, bool won, ulong playerId)
		: base(run, won, playerId, "CCCCOMBO", requiresWin: false, multiplayerOnly: false)
	{
	}

	public override bool IsObtained()
	{
		return _localPlayer.ExtraFields.CccomboBadgeUnlocked;
	}
}
