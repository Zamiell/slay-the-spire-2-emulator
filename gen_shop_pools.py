import re

with open('src/Sts2Emulator/Generated/Cards.g.cs', 'r') as f:
    content = f.read()

pattern = r'new CardDef\(Id: (\d+), Name: "([^"]+)"'
matches = re.findall(pattern, content)
card_map = {int(m[0]): m[1] for m in matches}

import numpy as np

# IRONCLAD_REWARD_POOL (already sorted)
IRONCLAD_REWARD_POOL = [
    9, 13, 18, 20, 29, 31, 46, 45, 47, 50,
    58, 60, 66, 69, 87, 95, 99, 113, 114, 119,
    141, 142, 147, 150, 155, 174, 175, 183, 185, 188,
    189, 195, 205, 238, 240, 246, 247, 254, 261, 262,
    263, 265, 268, 272, 273, 295, 313, 328, 332, 334,
    339, 349, 353, 358, 364, 374, 378, 381, 396, 404,
    414, 421, 433, 454, 455, 462, 464, 465, 466, 486,
    492, 493, 494, 505, 508, 516, 517, 519, 521, 525,
    526, 529, 533, 538,
]

# SHOP pools (filter from IRONCLAD_REWARD_POOL to preserve sort order)
# We need CARD_TYPE_BY_ID to filter
# I'll just use the old lists and sort them by name

old_shop_attack = [13, 20, 50, 60, 69, 87, 147, 189, 240, 247, 254, 268, 349, 358, 421, 454, 465, 486, 508, 519, 538]
old_shop_skill = [18, 31, 45, 46, 150, 155, 174, 175, 205, 238, 396, 414, 433, 455, 493, 516, 517, 521]
old_shop_power = [185, 265, 273, 462, 533]

def sort_by_name(ids):
    items = [(cid, card_map[cid]) for cid in ids if cid in card_map]
    items.sort(key=lambda x: x[1])
    return [cid for cid, name in items]

print("SHOP_ATTACK_CARDS = np.array(" + str(sort_by_name(old_shop_attack)) + ", dtype=np.int32)")
print("SHOP_SKILL_CARDS = np.array(" + str(sort_by_name(old_shop_skill)) + ", dtype=np.int32)")
print("SHOP_POWER_CARDS = np.array(" + str(sort_by_name(old_shop_power)) + ", dtype=np.int32)")
