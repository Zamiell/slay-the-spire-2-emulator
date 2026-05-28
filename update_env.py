import re

with open("new_pool.txt", "r") as f:
    new_pool_content = f.read()

with open("src/sts2_gym/run_env.py", "r") as f:
    env_content = f.read()

# Replace IRONCLAD_REWARD_POOL
env_content = re.sub(
    r"IRONCLAD_REWARD_POOL = np\.array\([\s\S]*?dtype=np\.int32,\n\)",
    new_pool_content.split("CARD_RARITY_BY_ID")[0].strip(),
    env_content,
)

# Replace CARD_RARITY_BY_ID
env_content = re.sub(
    r"CARD_RARITY_BY_ID = \{[\s\S]*?\}",
    "CARD_RARITY_BY_ID = " + new_pool_content.split("CARD_RARITY_BY_ID = ")[1].strip(),
    env_content,
)

with open("src/sts2_gym/run_env.py", "w") as f:
    f.write(env_content)
