import re

with open('src/Sts2Emulator/Generated/Cards.g.cs', 'r') as f:
    content = f.read()

pattern = r'new CardDef\(Id: (\d+), Name: "([^"]+)", .*? Rarity: CardRarity\.(Uncommon|Rare|Common|Basic|Ancient|Event)'
matches = re.findall(pattern, content)

import src.sts2_gym.run_env as env
ironclad_pool = set(env.IRONCLAD_REWARD_POOL)

uncommon_ironclad = []
for cid_str, name, rarity in matches:
    cid = int(cid_str)
    if cid in ironclad_pool and rarity == 'Uncommon':
        uncommon_ironclad.append((cid, name))

# Sort alphabetically
uncommon_ironclad.sort(key=lambda x: x[1])

for i, (cid, name) in enumerate(uncommon_ironclad):
    print(f"{i}: {name} ({cid})")
