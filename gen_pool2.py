import re

with open("src/Sts2Emulator/Generated/Cards.g.cs", "r") as f:
    content = f.read()

# Extract Id and Name
# new CardDef(Id: 1, Name: "Abrasive", ...
pattern = r'new CardDef\(Id: (\d+), Name: "([^"]+)"'
matches = re.findall(pattern, content)

card_map = {int(m[0]): m[1] for m in matches}

# Current IRONCLAD_REWARD_POOL IDs from run_env.py
current_pool = [
    9,
    13,
    18,
    20,
    29,
    31,
    45,
    46,
    47,
    50,
    58,
    60,
    66,
    69,
    87,
    95,
    99,
    113,
    114,
    119,
    141,
    142,
    147,
    150,
    155,
    174,
    175,
    183,
    185,
    188,
    189,
    195,
    205,
    238,
    240,
    246,
    247,
    254,
    261,
    262,
    263,
    265,
    268,
    272,
    273,
    295,
    313,
    328,
    332,
    334,
    339,
    349,
    353,
    358,
    364,
    374,
    378,
    381,
    396,
    404,
    414,
    421,
    433,
    454,
    455,
    462,
    464,
    465,
    466,
    486,
    492,
    493,
    494,
    505,
    508,
    516,
    517,
    519,
    521,
    525,
    526,
    529,
    533,
    538,
]

# Map to names and sort by name
pool_with_names = [(cid, card_map[cid]) for cid in current_pool if cid in card_map]
for cid, name in pool_with_names:
    if "Blood" in name:
        print(f"DEBUG: {cid} -> {name}")

# Sort alphabetically by name
pool_with_names.sort(key=lambda x: x[1])

sorted_ids = [cid for cid, name in pool_with_names]

print("IRONCLAD_REWARD_POOL = np.array(")
print("    [")
for i in range(0, len(sorted_ids), 10):
    print("        " + ", ".join(map(str, sorted_ids[i : i + 10])) + ",")
print("    ],")
print("    dtype=np.int32,")
print(")")
