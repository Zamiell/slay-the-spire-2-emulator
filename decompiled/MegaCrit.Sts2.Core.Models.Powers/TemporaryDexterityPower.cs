using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace MegaCrit.Sts2.Core.Models.Powers;

public abstract class TemporaryDexterityPower : PowerModel, ITemporaryPower
{
    private bool _shouldIgnoreNextInstance;

    public override PowerType Type
    {
        get
        {
            if (!IsPositive)
            {
                return PowerType.Debuff;
            }
            return PowerType.Buff;
        }
    }

    public override PowerStackType StackType => PowerStackType.Counter;

    public abstract AbstractModel OriginModel { get; }

    public PowerModel InternallyAppliedPower => ModelDb.Power<DexterityPower>();

    protected virtual bool IsPositive => true;

    private int Sign
    {
        get
        {
            if (!IsPositive)
            {
                return -1;
            }
            return 1;
        }
    }

    public override LocString Title
    {
        get
        {
            AbstractModel originModel = OriginModel;
            if (!(originModel is CardModel cardModel))
            {
                if (!(originModel is PotionModel potionModel))
                {
                    if (originModel is RelicModel relicModel)
                    {
                        return relicModel.Title;
                    }
                    throw new InvalidOperationException();
                }
                return potionModel.Title;
            }
            return cardModel.TitleLocString;
        }
    }

    public override LocString Description =>
        new LocString(
            "powers",
            IsPositive
                ? "TEMPORARY_DEXTERITY_POWER.description"
                : "TEMPORARY_DEXTERITY_DOWN.description"
        );

    protected override string SmartDescriptionLocKey
    {
        get
        {
            if (!IsPositive)
            {
                return "TEMPORARY_DEXTERITY_DOWN.smartDescription";
            }
            return "TEMPORARY_DEXTERITY_POWER.smartDescription";
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            List<IHoverTip> list = new List<IHoverTip>();
            List<IHoverTip> list2 = list;
            AbstractModel originModel = OriginModel;
            IEnumerable<IHoverTip> collection;
            if (!(originModel is CardModel card))
            {
                if (!(originModel is PotionModel model))
                {
                    if (!(originModel is RelicModel relic))
                    {
                        throw new InvalidOperationException();
                    }
                    collection = HoverTipFactory.FromRelic(relic);
                }
                else
                {
                    collection = new global::_003C_003Ez__ReadOnlySingleElementList<IHoverTip>(
                        HoverTipFactory.FromPotion(model)
                    );
                }
            }
            else
            {
                collection = new global::_003C_003Ez__ReadOnlySingleElementList<IHoverTip>(
                    HoverTipFactory.FromCard(card)
                );
            }
            list2.AddRange(collection);
            list.Add(HoverTipFactory.FromPower<DexterityPower>());
            return new _003C_003Ez__ReadOnlyList<IHoverTip>(list);
        }
    }

    public void IgnoreNextInstance()
    {
        _shouldIgnoreNextInstance = true;
    }

    public override async Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource
    )
    {
        if (_shouldIgnoreNextInstance)
        {
            _shouldIgnoreNextInstance = false;
        }
        else
        {
            await PowerCmd.Apply<DexterityPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                (decimal)Sign * amount,
                applier,
                cardSource,
                silent: true
            );
        }
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource
    )
    {
        if (!(amount == (decimal)base.Amount) && power == this)
        {
            if (_shouldIgnoreNextInstance)
            {
                _shouldIgnoreNextInstance = false;
            }
            else
            {
                await PowerCmd.Apply<DexterityPower>(
                    choiceContext,
                    base.Owner,
                    (decimal)Sign * amount,
                    applier,
                    cardSource,
                    silent: true
                );
            }
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants
    )
    {
        if (participants.Contains(base.Owner))
        {
            Flash();
            await PowerCmd.Remove(this);
            await PowerCmd.Apply<DexterityPower>(
                choiceContext,
                base.Owner,
                -Sign * base.Amount,
                base.Owner,
                null
            );
        }
    }
}
