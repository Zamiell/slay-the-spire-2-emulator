using MegaCrit.Sts2.Core.Saves;

namespace MegaCrit.Sts2.Core.Models.Badges;

public class Glutton : Badge
{
	public override BadgeRarity Rarity
	{
		get
		{
			int startingHp = SaveUtil.CharacterOrDeprecated(_localPlayer.CharacterId).StartingHp;
			int num = _localPlayer.MaxHp - startingHp;
			if (num >= 30)
			{
				if (num >= 50)
				{
					return BadgeRarity.Gold;
				}
				return BadgeRarity.Silver;
			}
			if (num >= 15)
			{
				return BadgeRarity.Bronze;
			}
			return BadgeRarity.None;
		}
	}

	public Glutton(SerializableRun run, bool won, ulong playerId)
		: base(run, won, playerId, "GLUTTON", requiresWin: true, multiplayerOnly: false)
	{
	}

	public override bool IsObtained()
	{
		return Rarity != BadgeRarity.None;
	}
}
