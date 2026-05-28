import re

# Parse Cards.g.cs
card_defs = {}
with open('src/Sts2Emulator/Generated/Cards.g.cs', 'r') as f:
    for line in f:
        m = re.search(r'Id: (\d+), Name: "([^"]+)", .*?Rarity: CardRarity\.(\w+)', line)
        if m:
            card_defs[m.group(2)] = (int(m.group(1)), m.group(3))

# Parse plan.md for all supported cards
supported_cards = []
with open('plan.md', 'r') as f:
    in_cards = False
    for line in f:
        if line.startswith('### Ironclad') or line.startswith('### Colorless'):
            in_cards = True
        elif line.startswith('### Status/Curse'):
            in_cards = False
        elif in_cards and line.startswith('- [x] '):
            m = re.search(r'\[x\] (\w+) \(ID: (\d+)\)', line)
            if m:
                supported_cards.append((m.group(1), int(m.group(2))))

print("IRONCLAD_REWARD_POOL = np.array([")
for name, cid in sorted(supported_cards, key=lambda x: x[1]):
    if name in card_defs:
        rarity = card_defs[name][1].upper()
        if rarity not in ['BASIC', 'CURSE', 'STATUS', 'SPECIAL']:
            print(f"        {cid},")
print("    ], dtype=np.int32)")

print("\nCARD_RARITY_BY_ID = {")
for name, cid in sorted(supported_cards, key=lambda x: x[1]):
    if name in card_defs:
        rarity = card_defs[name][1].upper()
        if rarity in ['COMMON', 'UNCOMMON', 'RARE']:
            print(f"    {cid}: CARD_RARITY_{rarity},")
print("}")
