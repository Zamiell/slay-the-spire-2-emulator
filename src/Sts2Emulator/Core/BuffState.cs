namespace Sts2Emulator.Core;

public enum BuffId
{
    Strength,
    Dexterity,
    Vulnerable,
    Weak,
    Frail,
    Poison,
    Burn,
    Ritual,       // enemy: gain N Strength at end of each turn (skips turn applied)
    DemonForm,    // player: gain N Strength at start of each player turn
    Rage,         // player: gain N block when playing an Attack; removed at end of player turn
    FeelNoPain,   // player: gain N block when any card is exhausted
    Barricade,    // player: block does not clear at start of turn
    Corruption,   // player: Skills cost 0 and exhaust
    Metallicize,  // player: gain N block at end of player turn
    FlameBarrier, // player: deal N damage to melee attackers; removed at end of enemy turn
    Juggernaut,   // player: deal N unpowered damage to random enemy when gaining block
    RupturePower, // player: gain 1 Strength when losing HP from card effects
}

public record struct BuffState(BuffId Id, int Magnitude);
