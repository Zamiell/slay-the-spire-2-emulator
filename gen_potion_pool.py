import re

with open("src/Sts2Emulator/Generated/Potions.g.cs", "r") as f:
    content = f.read()

# Extract Id and Name
# new PotionDef(Id: 1, Name: "AttackPotion", ...
pattern = r'new PotionDef\(Id: (\d+), Name: "([^"]+)"'
matches = re.findall(pattern, content)

potion_map = {int(m[0]): m[1] for m in matches}

# Current POTION_REWARD_POOL IDs from run_env.py
# (Actually all available potions from POTION_RARITY_BY_ID)
import numpy as np

POTION_RARITY_BY_ID = {
    2: 1,
    3: 3,
    4: 2,
    5: 1,
    6: 1,
    8: 3,
    9: 2,
    10: 1,
    13: 2,
    14: 1,
    15: 3,
    16: 3,
    17: 2,
    18: 1,
    19: 3,
    21: 1,
    22: 3,
    23: 1,
    24: 1,
    26: 2,
    28: 3,
    29: 2,
    30: 2,
    32: 3,
    34: 2,
    36: 2,
    37: 3,
    38: 3,
    39: 3,
    40: 3,
    42: 2,
    47: 2,
    48: 1,
    49: 2,
    50: 2,
    51: 3,
    52: 3,
    53: 1,
    54: 3,
    56: 1,
    57: 2,
    59: 1,
    60: 1,
    61: 2,
    62: 1,
    63: 1,
    1: 2,
    58: 3,
}

pool = list(POTION_RARITY_BY_ID.keys())
pool_with_names = [(pid, potion_map[pid]) for pid in pool if pid in potion_map]

# Sort alphabetically by name (Ordinal)
pool_with_names.sort(key=lambda x: x[1])

sorted_ids = [pid for pid, name in pool_with_names]

print("POTION_REWARD_POOL = np.array(")
print("    [")
for i in range(0, len(sorted_ids), 10):
    print("        " + ", ".join(map(str, sorted_ids[i : i + 10])) + ",")
print("    ],")
print("    dtype=np.int32,")
print(")")
