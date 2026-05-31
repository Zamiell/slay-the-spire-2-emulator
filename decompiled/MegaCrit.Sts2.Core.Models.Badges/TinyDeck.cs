using MegaCrit.Sts2.Core.Saves;

namespace MegaCrit.Sts2.Core.Models.Badges;

public class TinyDeck : Badge
{
    public override BadgeRarity Rarity
    {
        get
        {
            int count = _localPlayer.Deck.Count;
            if (count <= 10)
            {
                if (count <= 5)
                {
                    return BadgeRarity.Gold;
                }
                return BadgeRarity.Silver;
            }
            if (count <= 20)
            {
                return BadgeRarity.Bronze;
            }
            return BadgeRarity.None;
        }
    }

    public TinyDeck(SerializableRun run, bool won, ulong playerId)
        : base(run, won, playerId, "TINY_DECK", requiresWin: true, multiplayerOnly: false) { }

    public override bool IsObtained()
    {
        return Rarity != BadgeRarity.None;
    }
}
