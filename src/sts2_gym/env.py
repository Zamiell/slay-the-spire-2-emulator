"""Gymnasium environment wrapping the Sts2Emulator native library."""

import ctypes
import numpy as np
import gymnasium as gym
from gymnasium import spaces

from . import native

MAX_ACTIONS = 32  # hand(10) + end_turn(1) + potions(3) + buffer
MAX_EPISODE_STEPS = 50
ENCOUNTER_NAMES = {
    0: "cultists",
    1: "chompers",
    2: "nibbit",
    3: "slimes",
    4: "exoskeletons",
    5: "inklets",
    6: "two-tailed-rats",
    7: "gremlin-merc",
    8: "fuzzy-wurm-crawler",
    9: "corpse-slugs",
    10: "sludge-spinner",
    11: "shrinker-beetle",
    12: "seapunk",
    13: "toadpoles",
}
ENCOUNTER_IDS = {name: encounter_id for encounter_id, name in ENCOUNTER_NAMES.items()}


class Sts2CombatEnv(gym.Env):
    metadata = {"render_modes": []}

    def __init__(
        self,
        seed: int = 0,
        max_episode_steps: int = MAX_EPISODE_STEPS,
        encounter: int | str | None = None,
    ):
        super().__init__()
        self._seed = seed
        self._max_episode_steps = max_episode_steps
        self._forced_encounter = self._normalize_encounter(encounter)
        self._elapsed_steps = 0
        self._handle: int | None = None
        self._obs_buf = (ctypes.c_int * native.OBS_SIZE)()
        self._rew_buf = (ctypes.c_float * 1)()

        self.observation_space = spaces.Box(
            low=0,
            high=2**15,
            shape=(native.OBS_SIZE,),
            dtype=np.int32,
        )
        self.action_space = spaces.Discrete(MAX_ACTIONS)

    # ── gymnasium API ─────────────────────────────────────────────────────────

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        if self._handle is not None:
            native.destroy(self._handle)
        self._handle = native.create(seed if seed is not None else self._seed)
        self._elapsed_steps = 0
        encounter = options.get("encounter") if options is not None else None
        encounter_id = self._normalize_encounter(encounter)
        if encounter_id is None:
            encounter_id = self._forced_encounter
        if encounter_id is None:
            native.reset(self._handle, self._obs_buf)
        else:
            native.reset_encounter(self._handle, encounter_id, self._obs_buf)
        return self._obs(), self._info()

    def step(self, action: int):
        assert self._handle is not None, "Call reset() before step()"
        terminal = native.step(self._handle, action, self._obs_buf, self._rew_buf)
        self._elapsed_steps += 1
        truncated = not terminal and self._elapsed_steps >= self._max_episode_steps
        reward = float(self._rew_buf[0])
        return self._obs(), reward, terminal, truncated, self._info()

    def action_masks(self) -> np.ndarray:
        """Return a boolean mask of valid actions (for MaskablePPO)."""
        mask_buf = native.valid_actions(self._handle, MAX_ACTIONS)
        return np.array(mask_buf, dtype=bool)

    def close(self):
        if self._handle is not None:
            native.destroy(self._handle)
            self._handle = None

    # ── internals ─────────────────────────────────────────────────────────────

    def _obs(self) -> np.ndarray:
        return np.frombuffer(self._obs_buf, dtype=np.int32).copy()

    def _info(self) -> dict:
        if self._handle is None:
            return {"player_won": False, "encounter_id": -1, "encounter": "none"}

        encounter_id = native.encounter_id(self._handle)
        return {
            "player_won": native.player_won(self._handle),
            "encounter_id": encounter_id,
            "encounter": ENCOUNTER_NAMES.get(encounter_id, f"unknown-{encounter_id}"),
        }

    @staticmethod
    def _normalize_encounter(encounter: int | str | None) -> int | None:
        if encounter is None:
            return None
        if isinstance(encounter, int):
            if encounter not in ENCOUNTER_NAMES:
                raise ValueError(f"Unknown encounter id: {encounter}")
            return encounter
        try:
            return ENCOUNTER_IDS[encounter]
        except KeyError as exc:
            valid = ", ".join(sorted(ENCOUNTER_IDS))
            raise ValueError(f"Unknown encounter '{encounter}'. Valid encounters: {valid}") from exc
