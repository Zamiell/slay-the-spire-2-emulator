import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path('src').absolute()))
from sts2_gym.run_env import IRONCLAD_REWARD_POOL
from sts2_gym.env import _NAME_TO_CARD_ID

trace = json.load(open("traces/full-run/FULLRUN_TRACE_CARDS_1.json"))
observed = {c["id"] for step in trace["trace"] for c in step.get("summary", {}).get("card_reward", {}).get("cards", [])}
pool = set(IRONCLAD_REWARD_POOL)
print("Missing:", [c for c in observed if _NAME_TO_CARD_ID.get(c) not in pool])
