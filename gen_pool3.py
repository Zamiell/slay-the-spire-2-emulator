import re

with open("src/Sts2Emulator/Generated/Cards.g.cs", "r") as f:
    content = f.read()

pattern = r'new CardDef\(Id: (\d+), Name: "([^"]+)"'
matches = re.findall(pattern, content)
name_to_id = {m[1]: int(m[0]) for m in matches}

ordered_names = [
    "Aggression",
    "Anger",
    "Armaments",
    "AshenStrike",
    "Barricade",
    "Bash",
    "BattleTrance",
    "BloodWall",
    "Bloodletting",
    "Bludgeon",
    "BodySlam",
    "Brand",
    "Break",
    "Breakthrough",
    "Bully",
    "BurningPact",
    "Cascade",
    "Cinder",
    "Colossus",
    "Conflagration",
    "Corruption",
    "CrimsonMantle",
    "Cruelty",
    "DarkEmbrace",
    "DefendIronclad",
    "DemonForm",
    "DemonicShield",
    "Dismantle",
    "Dominate",
    "DrumOfBattle",
    "EvilEye",
    "ExpectAFight",
    "Feed",
    "FeelNoPain",
    "FiendFire",
    "FightMe",
    "FlameBarrier",
    "ForgottenRitual",
    "Havoc",
    "Headbutt",
    "Hellraiser",
    "Hemokinesis",
    "HowlFromBeyond",
    "Impervious",
    "InfernalBlade",
    "Inferno",
    "Inflame",
    "IronWave",
    "Juggernaut",
    "Juggling",
    "Mangle",
    "MoltenFist",
    "NotYet",
    "Offering",
    "OneTwoPunch",
    "PactsEnd",
    "PerfectedStrike",
    "Pillage",
    "PommelStrike",
    "PrimalForce",
    "Pyre",
    "Rage",
    "Rampage",
    "Rupture",
    "SecondWind",
    "SetupStrike",
    "ShrugItOff",
    "Spite",
    "Stampede",
    "Stoke",
    "Stomp",
    "StoneArmor",
    "StrikeIronclad",
    "SwordBoomerang",
    "Tank",
    "Taunt",
    "TearAsunder",
    "Thrash",
    "Thunderclap",
    "Tremble",
    "TrueGrit",
    "TwinStrike",
    "Unmovable",
    "Unrelenting",
    "Uppercut",
    "Vicious",
    "Whirlwind",
]

ordered_ids = [name_to_id[name] for name in ordered_names if name in name_to_id]

print("IRONCLAD_REWARD_POOL = np.array(")
print("    [")
for i in range(0, len(ordered_ids), 10):
    print("        " + ", ".join(map(str, ordered_ids[i : i + 10])) + ",")
print("    ],")
print("    dtype=np.int32,")
print(")")
