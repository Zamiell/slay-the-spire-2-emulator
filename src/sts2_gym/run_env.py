"""Run-level Gymnasium wrapper with a card-reward phase between combats."""

from __future__ import annotations

import ctypes

import gymnasium as gym
import numpy as np
from gymnasium import spaces

from . import native
from .env import ENCOUNTER_NAMES, MAX_ACTIONS, MAX_EPISODE_STEPS

REWARD_SKIP_ACTION = 3
MAP_CHOICES = 3
RUN_EXTRA_OBS = 9
RUN_OBS_SIZE = native.OBS_SIZE + RUN_EXTRA_OBS

PHASE_COMBAT = 0
PHASE_CARD_REWARD = 1
PHASE_MAP = 2

STARTER_DECK = [466] * 5 + [137] * 4 + [30, 10001]
IRONCLAD_REWARD_POOL = np.array([13, 18, 265, 358, 433, 508, 519], dtype=np.int32)
OVERGROWTH_WEAK_ENCOUNTERS = np.array([2, 3, 11, 8], dtype=np.int32)
UNDERDOCKS_WEAK_ENCOUNTERS = np.array([9, 12, 10, 13], dtype=np.int32)
OVERGROWTH_NORMAL_ENCOUNTERS = np.array([5, 2, 3, 8], dtype=np.int32)
UNDERDOCKS_NORMAL_ENCOUNTERS = np.array([9, 0, 7, 6], dtype=np.int32)


class Sts2RunEnv(gym.Env):
    """Multi-combat wrapper that exposes deterministic card picks after wins.

    Combat is still resolved by the native single-combat emulator. Picked cards
    are tracked in the run deck so agents can learn reward choices before the
    native full-run/deck plumbing is complete.
    """

    metadata = {"render_modes": []}

    def __init__(
        self,
        seed: int = 0,
        max_episode_steps: int = MAX_EPISODE_STEPS,
        max_floors: int = 3,
    ):
        super().__init__()
        self._seed = seed
        self._rng = np.random.default_rng(seed)
        self._max_episode_steps = max_episode_steps
        self._max_floors = max_floors
        self._elapsed_steps = 0
        self._floor = 1
        self._phase = PHASE_COMBAT
        self._deck = list(STARTER_DECK)
        self._reward_cards = np.zeros(3, dtype=np.int32)
        self._map_choices = np.zeros(MAP_CHOICES, dtype=np.int32)
        self._act = "overgrowth"
        self._weak_encounters = np.zeros(3, dtype=np.int32)
        self._handle: int | None = None
        self._combat_obs_buf = (ctypes.c_int * native.OBS_SIZE)()
        self._rew_buf = (ctypes.c_float * 1)()

        self.observation_space = spaces.Box(
            low=0,
            high=2**15,
            shape=(RUN_OBS_SIZE,),
            dtype=np.int32,
        )
        self.action_space = spaces.Discrete(MAX_ACTIONS)

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        actual_seed = seed if seed is not None else self._seed
        self._rng = np.random.default_rng(actual_seed)
        self._elapsed_steps = 0
        self._floor = 1
        self._phase = PHASE_COMBAT
        self._deck = list(STARTER_DECK)
        self._reward_cards[:] = 0
        self._map_choices[:] = 0
        self._select_act_and_weak_encounters()
        self._reset_combat(actual_seed, int(self._weak_encounters[0]))
        return self._obs(), self._info()

    def step(self, action: int):
        assert self._handle is not None, "Call reset() before step()"
        self._elapsed_steps += 1

        if self._phase == PHASE_CARD_REWARD:
            return self._step_reward(action)

        if self._phase == PHASE_MAP:
            return self._step_map(action)

        terminal = native.step(self._handle, action, self._combat_obs_buf, self._rew_buf)
        reward = float(self._rew_buf[0])
        truncated = not terminal and self._elapsed_steps >= self._max_episode_steps

        if terminal and native.player_won(self._handle):
            self._enter_reward_phase()
            return self._obs(), reward, False, truncated, self._info()

        return self._obs(), reward, terminal, truncated, self._info()

    def action_masks(self) -> np.ndarray:
        if self._phase == PHASE_CARD_REWARD:
            mask = np.zeros(MAX_ACTIONS, dtype=bool)
            mask[: REWARD_SKIP_ACTION + 1] = True
            return mask

        if self._phase == PHASE_MAP:
            mask = np.zeros(MAX_ACTIONS, dtype=bool)
            mask[:MAP_CHOICES] = True
            return mask

        mask_buf = native.valid_actions(self._handle, MAX_ACTIONS)
        return np.array(mask_buf, dtype=bool)

    def close(self):
        if self._handle is not None:
            native.destroy(self._handle)
            self._handle = None

    def _step_reward(self, action: int):
        if 0 <= action < len(self._reward_cards):
            self._deck.append(int(self._reward_cards[action]))
        elif action != REWARD_SKIP_ACTION:
            raise ValueError(f"Invalid card reward action: {action}")

        self._reward_cards[:] = 0
        if self._floor >= self._max_floors:
            return self._obs(), 0.0, True, False, self._info()

        self._floor += 1
        if self._floor <= 3:
            self._phase = PHASE_COMBAT
            self._reset_combat(self._seed + self._floor - 1, int(self._weak_encounters[self._floor - 1]))
            return self._obs(), 0.0, False, False, self._info()

        self._phase = PHASE_MAP
        self._map_choices[:] = self._rng.choice(self._normal_encounter_pool(), size=MAP_CHOICES, replace=False)
        return self._obs(), 0.0, False, False, self._info()

    def _step_map(self, action: int):
        if not 0 <= action < MAP_CHOICES:
            raise ValueError(f"Invalid map action: {action}")

        encounter_id = int(self._map_choices[action])
        self._map_choices[:] = 0
        self._phase = PHASE_COMBAT
        self._reset_combat(self._seed + self._floor - 1, encounter_id)
        return self._obs(), 0.0, False, False, self._info()

    def _enter_reward_phase(self):
        self._phase = PHASE_CARD_REWARD
        self._reward_cards[:] = self._rng.choice(IRONCLAD_REWARD_POOL, size=3, replace=False)

    def _select_act_and_weak_encounters(self):
        if self._rng.integers(2) == 0:
            self._act = "overgrowth"
            pool = OVERGROWTH_WEAK_ENCOUNTERS
        else:
            self._act = "underdocks"
            pool = UNDERDOCKS_WEAK_ENCOUNTERS
        self._weak_encounters[:] = self._rng.choice(pool, size=3, replace=False)

    def _normal_encounter_pool(self) -> np.ndarray:
        return OVERGROWTH_NORMAL_ENCOUNTERS if self._act == "overgrowth" else UNDERDOCKS_NORMAL_ENCOUNTERS

    def _reset_combat(self, seed: int, encounter_id: int | None = None):
        if self._handle is not None:
            native.destroy(self._handle)
        self._handle = native.create(seed)
        if encounter_id is None:
            native.reset_with_deck(self._handle, self._deck, self._combat_obs_buf)
        else:
            native.reset_with_deck_and_encounter(
                self._handle, self._deck, encounter_id, self._combat_obs_buf
            )

    def _obs(self) -> np.ndarray:
        obs = np.zeros(RUN_OBS_SIZE, dtype=np.int32)
        obs[: native.OBS_SIZE] = np.frombuffer(self._combat_obs_buf, dtype=np.int32)
        obs[native.OBS_SIZE :] = np.array(
            [
                self._phase,
                self._floor,
                len(self._deck),
                int(self._reward_cards[0]),
                int(self._reward_cards[1]),
                int(self._reward_cards[2]),
                int(self._map_choices[0]),
                int(self._map_choices[1]),
                int(self._map_choices[2]),
            ],
            dtype=np.int32,
        )
        return obs

    def _info(self) -> dict:
        return {
            "phase": self._phase,
            "floor": self._floor,
            "act": self._act,
            "deck_size": len(self._deck),
            "card_rewards": tuple(int(card_id) for card_id in self._reward_cards),
            "map_choices": tuple(
                ENCOUNTER_NAMES.get(int(encounter_id), f"unknown-{encounter_id}")
                for encounter_id in self._map_choices
            )
            if self._phase == PHASE_MAP
            else (),
            "player_won": native.player_won(self._handle) if self._handle is not None else False,
            "encounter_id": native.encounter_id(self._handle) if self._handle is not None else -1,
            "encounter": ENCOUNTER_NAMES.get(native.encounter_id(self._handle), "none")
            if self._handle is not None
            else "none",
        }
