namespace MegaCrit.Sts2.Core.Models;

public abstract class BadgeModel : AbstractModel
{
    public override bool ShouldReceiveCombatHooks => true;
}
