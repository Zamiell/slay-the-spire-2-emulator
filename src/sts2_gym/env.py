"""Gymnasium environment wrapping the Sts2Emulator native library."""

import ctypes
import numpy as np
import gymnasium as gym
from gymnasium import spaces

from . import native

MAX_ACTIONS = 32  # hand(10) + end_turn(1) + potions(3) + buffer


class Sts2CombatEnv(gym.Env):
    metadata = {"render_modes": []}

    def __init__(self, seed: int = 0):
        super().__init__()
        self._seed = seed
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
        native.reset(self._handle, self._obs_buf)
        return self._obs(), self._info()

    def step(self, action: int):
        assert self._handle is not None, "Call reset() before step()"
        terminal = native.step(self._handle, action, self._obs_buf, self._rew_buf)
        reward = float(self._rew_buf[0])
        return self._obs(), reward, terminal, False, self._info()

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
        return {}
