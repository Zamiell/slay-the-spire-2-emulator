using MegaCrit.Sts2.Core.Saves;

namespace MegaCrit.Sts2.Core.Models.Badges;

public class TabletBadge : Badge
{
    public override BadgeRarity Rarity => BadgeRarity.Gold;

    public TabletBadge(SerializableRun run, bool won, ulong playerId)
        : base(run, won, playerId, "TABLET", requiresWin: true, multiplayerOnly: false) { }

    public override bool IsObtained()
    {
        return _localPlayer.MaxHp == 1;
    }
}
