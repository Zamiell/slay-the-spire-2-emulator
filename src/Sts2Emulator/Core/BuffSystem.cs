namespace Sts2Emulator.Core;

public static class BuffSystem
{
    public static void Apply(List<BuffState> buffs, BuffId id, int magnitude)
    {
        if (magnitude == 0) return;

        if (magnitude > 0 && IsDebuff(id))
        {
            int artifact = Get(buffs, BuffId.Artifact);
            if (artifact > 0)
            {
                int artifactIdx = buffs.FindIndex(b => b.Id == BuffId.Artifact);
                if (artifact == 1)
                    buffs.RemoveAt(artifactIdx);
                else
                    buffs[artifactIdx] = buffs[artifactIdx] with { Magnitude = artifact - 1 };
                return;
            }
        }

        int idx = buffs.FindIndex(b => b.Id == id);
        if (idx >= 0)
            buffs[idx] = buffs[idx] with { Magnitude = buffs[idx].Magnitude + magnitude };
        else
            buffs.Add(new BuffState(id, magnitude));
    }

    public static int Get(List<BuffState> buffs, BuffId id)
    {
        int idx = buffs.FindIndex(b => b.Id == id);
        return idx >= 0 ? buffs[idx].Magnitude : 0;
    }

    public static void Remove(List<BuffState> buffs, BuffId id)
        => buffs.RemoveAll(b => b.Id == id);

    // Called at end of turn for the owning side (tick debuffs down by 1).
    public static void TickEndOfTurn(List<BuffState> buffs)
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            var b = buffs[i];
            switch (b.Id)
            {
                case BuffId.Poison:
                case BuffId.Vulnerable:
                case BuffId.Weak:
                case BuffId.Frail:
                    buffs[i] = b with { Magnitude = b.Magnitude - 1 };
                    if (buffs[i].Magnitude <= 0) buffs.RemoveAt(i);
                    break;
            }
        }
    }

    public static int IncomingDamage(int baseDamage, List<BuffState> attackerBuffs, List<BuffState> defenderBuffs)
    {
        float dmg = baseDamage;
        dmg += Get(attackerBuffs, BuffId.Strength);
        if (Get(attackerBuffs, BuffId.Weak) > 0) dmg *= 0.75f;
        if (Get(defenderBuffs, BuffId.Vulnerable) > 0) dmg *= 1.5f;
        return Math.Max(0, (int)dmg);
    }

    public static int IncomingBlock(int baseBlock, List<BuffState> buffs)
    {
        float block = baseBlock;
        block += Get(buffs, BuffId.Dexterity);
        if (Get(buffs, BuffId.Frail) > 0) block *= 0.75f;
        return Math.Max(0, (int)block);
    }

    private static bool IsDebuff(BuffId id) =>
        id is BuffId.Vulnerable
            or BuffId.Weak
            or BuffId.Frail
            or BuffId.Poison
            or BuffId.Burn
            or BuffId.Shrink;
}
