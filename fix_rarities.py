import os
import re

card_dir = 'decompiled/MegaCrit.Sts2.Core.Models.Cards'
rarity_map = {}

for filename in os.listdir(card_dir):
    if filename.endswith('.cs'):
        path = os.path.join(card_dir, filename)
        with open(path, 'r') as f:
            content = f.read()
            # Extract CardRarity from base constructor call
            # : base(..., CardRarity.Rare, ...)
            match = re.search(r'base\(.*?, CardRarity\.(\w+)', content)
            if match:
                rarity = match.group(1)
                name = filename[:-3]
                rarity_map[name] = rarity

# Now update Cards.g.cs with these rarities
with open('src/Sts2Emulator/Generated/Cards.g.cs', 'r') as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    # new CardDef(Id: 150, Name: "DemonForm", ..., Rarity: CardRarity.Uncommon),
    match = re.search(r'Name: "([^"]+)",', line)
    if match:
        name = match.group(1)
        if name in rarity_map:
            new_rarity = rarity_map[name]
            line = re.sub(r'Rarity: CardRarity\.\w+', f'Rarity: CardRarity.{new_rarity}', line)
    new_lines.append(line)

with open('src/Sts2Emulator/Generated/Cards.g.cs', 'w') as f:
    f.writelines(new_lines)

print(f"Updated rarities for {len(rarity_map)} cards.")
