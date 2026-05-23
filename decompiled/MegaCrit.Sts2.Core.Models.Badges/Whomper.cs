using MegaCrit.Sts2.Core.Saves;

namespace MegaCrit.Sts2.Core.Models.Badges;

public class Whomper : Badge
{
	private const float _damageRequirement = 100f;

	public override BadgeRarity Rarity => BadgeRarity.Bronze;

	public Whomper(SerializableRun run, bool won, ulong playerId)
		: base(run, won, playerId, "WHOMPER", requiresWin: false, multiplayerOnly: false)
	{
	}

	public override bool IsObtained()
	{
		return false;
	}
}
