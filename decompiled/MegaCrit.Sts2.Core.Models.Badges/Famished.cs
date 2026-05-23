using MegaCrit.Sts2.Core.Saves;

namespace MegaCrit.Sts2.Core.Models.Badges;

public class Famished : Badge
{
	public override BadgeRarity Rarity => BadgeRarity.Bronze;

	public Famished(SerializableRun run, bool won, ulong playerId)
		: base(run, won, playerId, "FAMISHED", requiresWin: true, multiplayerOnly: false)
	{
	}

	public override bool IsObtained()
	{
		int num = (SaveUtil.CharacterOrDeprecated(_localPlayer.CharacterId).StartingHp + 1) / 2;
		return _localPlayer.MaxHp < num;
	}
}
