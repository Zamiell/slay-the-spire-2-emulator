import re

with open('src/Sts2Emulator/Generated/Relics.g.cs', 'r') as f:
    content = f.read()

# Extract Id and Name
# new RelicDef(Id: 1, Name: "Anchor", ...
pattern = r'new RelicDef\(Id: (\d+), Name: "([^"]+)"'
matches = re.findall(pattern, content)

relic_map = {int(m[0]): m[1] for m in matches}

# Current RELIC_REWARD_POOL IDs from run_env.py
RELIC_REWARD_POOL = [
    3, 4, 9, 19, 23, 41, 110, 114, 128, 135, 144, 149, 279, 282, 170, 169, 172, 10, 186, 190, 215, 250, 252, 286
]

pool_with_names = [(rid, relic_map[rid]) for rid in RELIC_REWARD_POOL if rid in relic_map]

# Sort alphabetically by name (Ordinal)
pool_with_names.sort(key=lambda x: x[1])

sorted_ids = [rid for rid, name in pool_with_names]

print("RELIC_REWARD_POOL = np.array(")
print("    [")
for i in range(0, len(sorted_ids), 10):
    print("        " + ", ".join(map(str, sorted_ids[i:i+10])) + ",")
print("    ],")
print("    dtype=np.int32,")
print(")")
