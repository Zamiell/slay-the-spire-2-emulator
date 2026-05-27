namespace Sts2Emulator.Core;

public sealed class CombatState
{
    // Player
    public int PlayerHp;
    public int PlayerMaxHp;
    public int PlayerBlock;
    public int Energy;
    public int MaxEnergy;
    public int PlayerGold;

    // Cards
    public List<CardInstance> Hand = [];
    public List<CardInstance> DrawPile = [];
    public List<CardInstance> DiscardPile = [];
    public List<CardInstance> ExhaustPile = [];
    public List<CardInstance> ReturnToHandBeforeDraw = [];

    // Potions: slot index → potion def ID, 0 = empty
    public int[] PotionSlots = new int[3];

    // Relics
    public List<RelicInstance> Relics = [];

    // Buffs/debuffs on the player
    public List<BuffState> PlayerBuffs = [];

    // Enemies
    public List<EnemyState> Enemies = [];
    public int EncounterId;
    public bool IsEliteCombat;

    // Shuffle RNG (RunRngSet.shuffle subsystem) — used for mid-combat discard reshuffles.
    // Null falls back to the combat RNG (only valid when no pre-shuffle was done).
    public Random? ShuffleRng;

    // Turn tracking
    public int Turn;
    public bool PlayerTurn = true;
    public bool SkillPlayedWhileSmoggy;
    public int AttackCardsPlayedThisTurn;
    public int AttackOrSkillCardsPlayedThisTurn;
    public int PlayerHpLostThisTurn;
    public int CardsExhaustedThisTurn;
    public int UnblockedDamageHitCount; // times player took unblocked damage this combat (TearAsunder)
}
