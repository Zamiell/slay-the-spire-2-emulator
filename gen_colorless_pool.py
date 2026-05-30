import re

with open("src/Sts2Emulator/Generated/Cards.g.cs", "r") as f:
    content = f.read()

# Extract Id, Name and Rarity
# new CardDef(Id: 1, Name: "Abrasive", ..., Rarity: CardRarity.Uncommon),
pattern = r'new CardDef\(Id: (\d+), Name: "([^"]+)", .*? Rarity: CardRarity\.(Uncommon|Rare|Common|Basic|Ancient|Event)'
matches = re.findall(pattern, content)

# Colorless cards are usually IDs that are NOT in Ironclad pool and NOT basic?
# No, let's use the list from ColorlessCardPool.cs

colorless_names = [
    "Alchemize",
    "Anointed",
    "Automation",
    "BeaconOfHope",
    "BeatDown",
    "BelieveInYou",
    "Bolas",
    "Calamity",
    "Catastrophe",
    "Coordinate",
    "DarkShackles",
    "Discovery",
    "DramaticEntrance",
    "Entropy",
    "Equilibrium",
    "EternalArmor",
    "Fasten",
    "Finesse",
    "Fisticuffs",
    "FlashOfSteel",
    "GangUp",
    "GoldAxe",
    "HandOfGreed",
    "HiddenGem",
    "HuddleUp",
    "Impatience",
    "Intercept",
    "JackOfAllTrades",
    "Jackpot",
    "Knockdown",
    "Lift",
    "MasterOfStrategy",
    "Mayhem",
    "Mimic",
    "MindBlast",
    "Nostalgia",
    "Omnislice",
    "Panache",
    "PanicButton",
    "PrepTime",
    "Production",
    "Prolong",
    "Prowess",
    "Purity",
    "Rally",
    "Rend",
    "Restlessness",
    "RollingBoulder",
    "Salvo",
    "Scrawl",
    "SecretTechnique",
    "SecretWeapon",
    "SeekerStrike",
    "Shockwave",
    "Splash",
    "Stratagem",
    "TagTeam",
    "TheBomb",
    "TheGambit",
    "ThinkingAhead",
    "ThrummingHatchet",
    "UltimateDefend",
    "UltimateStrike",
    "Volley",
]

name_to_id = {m[1]: int(m[0]) for m in matches}

colorless_ids = [name_to_id[name] for name in colorless_names if name in name_to_id]

# Sort alphabetically by name (Ordinal)
colorless_ids_sorted = sorted(
    colorless_ids, key=lambda cid: matches[[int(m[0]) for m in matches].index(cid)][1]
)
# Wait, colorless_names is already sorted alphabetically!

print("COLORLESS_REWARD_POOL = np.array(")
print("    [")
for i in range(0, len(colorless_ids), 10):
    print("        " + ", ".join(map(str, colorless_ids[i : i + 10])) + ",")
print("    ],")
print("    dtype=np.int32,")
print(")")
