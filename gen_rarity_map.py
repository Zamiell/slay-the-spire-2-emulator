import re

with open('src/Sts2Emulator/Generated/Cards.g.cs', 'r') as f:
    content = f.read()

# Extract Id and Rarity
pattern = r'new CardDef\(Id: (\d+), .*? Rarity: CardRarity\.(\w+)'
matches = re.findall(pattern, content)

rarity_map = {'Common': 'CARD_RARITY_COMMON', 'Uncommon': 'CARD_RARITY_UNCOMMON', 'Rare': 'CARD_RARITY_RARE', 'Basic': 'CARD_RARITY_BASIC', 'Ancient': 'CARD_RARITY_ANCIENT', 'Event': 'CARD_RARITY_EVENT', 'Status': 'CARD_RARITY_STATUS', 'Token': 'CARD_RARITY_TOKEN', 'Curse': 'CARD_RARITY_CURSE'}

print("CARD_RARITY_BY_ID = {")
for cid, rarity in matches:
    print(f"    {cid}: {rarity_map.get(rarity, 'CARD_RARITY_COMMON')},")
print("}")
