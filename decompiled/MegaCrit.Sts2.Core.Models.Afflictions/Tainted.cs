using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MegaCrit.Sts2.Core.Models.Afflictions;

public sealed class Tainted : AfflictionModel
{
    public override bool IsStackable => true;

    public override bool HasExtraCardText => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromPowerWithPowerHoverTips<TaintedPower>(base.Amount);

    public override bool CanAfflictCardType(CardType cardType)
    {
        return cardType == CardType.Skill;
    }
}
