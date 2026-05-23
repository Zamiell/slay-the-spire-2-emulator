using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace MegaCrit.Sts2.Core.Models.Powers;

public sealed class EbbPower : PowerModel
{
	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new global::_003C_003Ez__ReadOnlyArray<IHoverTip>(new IHoverTip[2]
	{
		HoverTipFactory.FromPower<StrengthPower>(),
		HoverTipFactory.FromPower<DexterityPower>()
	});

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Owner, -base.Amount, applier, null);
		await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), base.Owner, -base.Amount, applier, null);
	}

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (participants.Contains(base.Owner))
		{
			await PowerCmd.Apply<StrengthPower>(choiceContext, base.Owner, base.Amount, base.Applier, null);
			await PowerCmd.Apply<DexterityPower>(choiceContext, base.Owner, base.Amount, base.Applier, null);
			await PowerCmd.Remove(this);
		}
	}
}
