import re

with open(
    "decompiled/MegaCrit.Sts2.Core.Models.CardPools/ColorlessCardPool.cs", "r"
) as f:
    content = f.read()

ordered_names = re.findall(r"ModelDb\.Card<([^>]+)>\(\)", content)

with open("src/Sts2Emulator/Generated/Cards.g.cs", "r") as f:
    g_content = f.read()

pattern_g = r'new CardDef\(Id: (\d+), Name: "([^"]+)"'
matches_g = re.findall(pattern_g, g_content)
name_to_id = {m[1]: int(m[0]) for m in matches_g}

missing = []
for name in ordered_names:
    if name not in name_to_id:
        missing.append(name)

print(f"Missing colorless cards: {missing}")

if not missing:
    ordered_ids = [name_to_id[name] for name in ordered_names]
    print("COLORLESS_REWARD_POOL = np.array(")
    print("    [")
    for i in range(0, len(ordered_ids), 10):
        print("        " + ", ".join(map(str, ordered_ids[i : i + 10])) + ",")
    print("    ],")
    print("    dtype=np.int32,")
    print(")")
