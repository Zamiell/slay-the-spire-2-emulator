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
    Juggling,     // player: copy the third Attack played each turn into hand
    Stampede,     // player: auto-play a random Attack when play phase starts (tracked only)
    Vicious,      // player: draw N cards when applying Vulnerable
    TemporaryStrength, // player/enemy: remove this much Strength at side turn end
    Artifact,     // prevent the next N debuffs
    HardToKill,   // damage taken per hit is capped at N
    Thorns,       // enemy: retaliatory damage, currently observed but not triggered
    Shrink,       // player debuff from Shrinker Beetle, currently observed but not otherwise modeled
    Ravenous,     // enemy: gain Strength and skip next move when an ally dies
    Stunned,      // enemy: skip the next intent
    Slippery,     // enemy: each unblocked hit loses at most 1 HP, then decrements
    Surprise,     // Gremlin Merc: spawn reinforcements on death
    SummonCooldown, // Two-Tailed Rat: turns until Call for Backup is available
    BackupCount,    // Two-Tailed Rat: number of successful backup calls
    Plating,      // Sewer Clam: recurring block that decays each turn
    Tangled,      // Vine Shambler card debuff, currently tracked as a player debuff
    Constrict,    // Slithering Strangler pressure debuff, currently tracked
    Smoggy,       // Living Fog card affliction debuff, currently tracked
    Illusion,     // Fogmog summon marker
    Minion,       // Living Fog Gas Bomb marker
    PaperCuts,    // Scroll of Biting max-HP chip at end of enemy action
    Hex,
    Dampen,
    Adaptable,
    Asleep,
    ChainsOfBinding,
    Ebb,
    Enrage,
    Intangible,
    Plow,
    Sandpit,
    SteamEruption,
    Disintegration,
}

public record struct BuffState(BuffId Id, int Magnitude);
