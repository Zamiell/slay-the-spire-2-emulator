using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace MegaCrit.Sts2.Core.Models.Badges;

public class Highlander : Badge
{
    public override BadgeRarity Rarity => BadgeRarity.Bronze;

    public Highlander(SerializableRun run, bool won, ulong playerId)
        : base(run, won, playerId, "HIGHLANDER", requiresWin: true, multiplayerOnly: false) { }

    public override bool IsObtained()
    {
        List<SerializableCard> list = _localPlayer
            .Deck.Where(
                (SerializableCard c) => SaveUtil.CardOrDeprecated(c.Id).Rarity != CardRarity.Basic
            )
            .ToList();
        return list.Select((SerializableCard card) => card.Id).Distinct().Count() == list.Count;
    }
}
