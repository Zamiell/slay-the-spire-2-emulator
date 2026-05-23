namespace Sts2Emulator.Core;

public sealed class CombatState
{
    // Player
    public int PlayerHp;
    public int PlayerMaxHp;
    public int PlayerBlock;
    public int Energy;
    public int MaxEnergy;

    // Cards
    public List<CardInstance> Hand = [];
    public List<CardInstance> DrawPile = [];
    public List<CardInstance> DiscardPile = [];
    public List<CardInstance> ExhaustPile = [];

    // Potions: slot index → potion def ID, 0 = empty
    public int[] PotionSlots = new int[3];

    // Buffs/debuffs on the player
    public List<BuffState> PlayerBuffs = [];

    // Enemies
    public List<EnemyState> Enemies = [];

    // Turn tracking
    public int Turn;
    public bool PlayerTurn = true;
}
