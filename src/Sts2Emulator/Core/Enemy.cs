namespace Sts2Emulator.Core;

public enum IntentType { Attack, Defend, Buff, Debuff, Unknown }

public readonly record struct EnemyDef(
    int Id,
    string Name,
    int MinHp,
    int MaxHp
);

public readonly record struct Intent(IntentType Type, int Magnitude);

public sealed class EnemyState
{
    public int DefId;
    public int Hp;
    public int MaxHp;
    public int Block;
    public Intent CurrentIntent;
    public BuffState[] Buffs = [];
    public int MoveIndex;
}
