using MegaCrit.Sts2.Core.Nodes.Animation;

namespace MegaCrit.Sts2.Core.Models.Monsters;

public sealed class DecimillipedeSegmentBack : DecimillipedeSegment
{
    public override void SegmentAttack()
    {
        base.Creature.GetCreatureNode()
            ?.GetSpecialNode<NDecimillipedeSegmentDriver>("%Visuals/SegmentDriver")
            ?.AttackShake();
    }
}
