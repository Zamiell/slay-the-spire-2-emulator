"""Emit a deterministic emulator trace for comparing against real-game traces."""

import argparse
import json
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from sts2_gym import Sts2CombatEnv


def valid_actions(env: Sts2CombatEnv) -> list[int]:
    return [int(i) for i in np.flatnonzero(env.action_masks())]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--actions", type=int, nargs="*", default=[])
    parser.add_argument("--max-steps", type=int, default=50)
    args = parser.parse_args()

    env = Sts2CombatEnv(seed=args.seed, max_episode_steps=args.max_steps)
    try:
        obs, info = env.reset()
        trace = [
            {
                "step": 0,
                "action": None,
                "reward": 0.0,
                "terminated": False,
                "truncated": False,
                "valid_actions": valid_actions(env),
                "observation": obs.tolist(),
                "info": info,
            }
        ]

        for step, action in enumerate(args.actions, start=1):
            obs, reward, terminated, truncated, info = env.step(action)
            trace.append(
                {
                    "step": step,
                    "action": action,
                    "reward": reward,
                    "terminated": terminated,
                    "truncated": truncated,
                    "valid_actions": valid_actions(env) if not (terminated or truncated) else [],
                    "observation": obs.tolist(),
                    "info": info,
                }
            )
            if terminated or truncated:
                break

        print(json.dumps({"seed": args.seed, "trace": trace}, indent=2))
    finally:
        env.close()


if __name__ == "__main__":
    main()
