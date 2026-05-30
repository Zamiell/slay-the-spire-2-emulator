import re

with open(
    "decompiled/MegaCrit.Sts2.Core.Models.CardPools/IroncladCardPool.cs", "r"
) as f:
    content = f.read()

pattern = r"ModelDb\.Card<([^>]+)>\(\)"
matches = re.findall(pattern, content)

ordered_names = matches

with open("src/Sts2Emulator/Generated/Cards.g.cs", "r") as f:
    g_content = f.read()

pattern_g = r'new CardDef\(Id: (\d+), Name: "([^"]+)"'
matches_g = re.findall(pattern_g, g_content)
name_to_id = {m[1]: int(m[0]) for m in matches_g}

# Handle special names if any (e.g. StrikeIronclad vs Strike)
# In Cards.g.cs it is "StrikeIronclad"
# In IroncladCardPool.cs it is ModelDb.Card<StrikeIronclad>()

ordered_ids = []
for name in ordered_names:
    if name in name_to_id:
        ordered_ids.append(name_to_id[name])
    else:
        print(f"WARNING: Card {name} not found in Cards.g.cs")

print("IRONCLAD_REWARD_POOL = np.array(")
print("    [")
for i in range(0, len(ordered_ids), 10):
    print("        " + ", ".join(map(str, ordered_ids[i : i + 10])) + ",")
print("    ],")
print("    dtype=np.int32,")
print(")")
