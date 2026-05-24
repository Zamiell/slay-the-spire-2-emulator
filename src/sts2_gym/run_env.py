"""Run-level Gymnasium wrapper for simplified full-run training."""

from __future__ import annotations

import ctypes

import gymnasium as gym
import numpy as np
from gymnasium import spaces

from . import native
from .env import ENCOUNTER_NAMES, MAX_ACTIONS, MAX_EPISODE_STEPS

REWARD_SKIP_ACTION = 3
REST_HEAL_ACTION = 0
REST_UPGRADE_ACTION = 1
SHOP_SKIP_ACTION = 4
MAP_CHOICES = 3
RUN_EXTRA_OBS = 22
RUN_OBS_SIZE = native.OBS_SIZE + RUN_EXTRA_OBS

PHASE_COMBAT = 0
PHASE_CARD_REWARD = 1
PHASE_MAP = 2
PHASE_REST = 3
PHASE_SHOP = 4
PHASE_RELIC_REWARD = 5
PHASE_COMPLETE = 6

NODE_NONE = 0
NODE_NORMAL = 1
NODE_ELITE = 2
NODE_REST = 3
NODE_SHOP = 4
NODE_RELIC = 5
NODE_BOSS = 6

ACT_OVERGROWTH = 1
ACT_UNDERDOCKS = 2

RELIC_BURNING_BLOOD = 1
RELIC_ANCHOR = 2
RELIC_VAJRA = 3
RELIC_ODDLY_SMOOTH_STONE = 4
RELIC_BAG_OF_PREPARATION = 5
RELIC_REWARD_POOL = np.array(
    [
        RELIC_ANCHOR,
        RELIC_VAJRA,
        RELIC_ODDLY_SMOOTH_STONE,
        RELIC_BAG_OF_PREPARATION,
    ],
    dtype=np.int32,
)

STARTER_DECK = [472] * 5 + [131] * 4 + [30, 10001]
IRONCLAD_REWARD_POOL = np.array([13, 18, 265, 358, 433, 508, 519], dtype=np.int32)
UPGRADABLE_STARTER_CARDS = {30, 131, 472}
OVERGROWTH_WEAK_ENCOUNTERS = np.array([2, 3, 11, 8], dtype=np.int32)
UNDERDOCKS_WEAK_ENCOUNTERS = np.array([9, 12, 10, 13], dtype=np.int32)
OVERGROWTH_NORMAL_ENCOUNTERS = np.array(
    [5, 14, 15, 16, 17, 18, 19, 20, 21, 27, 28, 29], dtype=np.int32
)
UNDERDOCKS_NORMAL_ENCOUNTERS = np.array(
    [9, 0, 7, 6, 22, 23, 24, 25, 26, 30], dtype=np.int32
)
ELITE_ENCOUNTERS = np.array(
    [62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72], dtype=np.int32
)
BOSS_ENCOUNTERS = np.array(
    [73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84], dtype=np.int32
)


class Sts2RunEnv(gym.Env):
    """Deterministic simplified full-run environment.

    Native C# combat remains the source of truth for combat. Map, rewards,
    shops, rest sites, relic rewards, and run-level state are modeled in Python
    for fast training experiments.
    """

    metadata = {"render_modes": []}

    def __init__(
        self,
        seed: int = 0,
        max_episode_steps: int = MAX_EPISODE_STEPS,
        max_floors: int = 16,
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
        self._gold = 99
        self._player_hp = 64
        self._player_max_hp = 80
        self._relics = [RELIC_BURNING_BLOOD]
        self._current_node_type = NODE_NORMAL
        self._pending_relic_reward = False
        self._reward_cards = np.zeros(3, dtype=np.int32)
        self._shop_cards = np.zeros(3, dtype=np.int32)
        self._map_node_types = np.zeros(MAP_CHOICES, dtype=np.int32)
        self._map_choices = np.zeros(MAP_CHOICES, dtype=np.int32)
        self._relic_reward = 0
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
        self._seed = actual_seed
        self._rng = np.random.default_rng(actual_seed)
        self._elapsed_steps = 0
        self._floor = 1
        self._phase = PHASE_COMBAT
        self._deck = list(STARTER_DECK)
        self._gold = 99
        self._player_hp = 64
        self._player_max_hp = 80
        self._relics = [RELIC_BURNING_BLOOD]
        self._current_node_type = NODE_NORMAL
        self._pending_relic_reward = False
        self._reward_cards[:] = 0
        self._shop_cards[:] = 0
        self._map_node_types[:] = 0
        self._map_choices[:] = 0
        self._relic_reward = 0
        self._select_act_and_weak_encounters()
        self._reset_combat(actual_seed, int(self._weak_encounters[0]))
        return self._obs(), self._info()

    def step(self, action: int):
        self._elapsed_steps += 1

        if self._phase == PHASE_CARD_REWARD:
            return self._step_reward(action)
        if self._phase == PHASE_MAP:
            return self._step_map(action)
        if self._phase == PHASE_REST:
            return self._step_rest(action)
        if self._phase == PHASE_SHOP:
            return self._step_shop(action)
        if self._phase == PHASE_RELIC_REWARD:
            return self._step_relic_reward(action)
        if self._phase == PHASE_COMPLETE:
            return self._obs(), 0.0, True, False, self._info()

        assert self._handle is not None, "Call reset() before step()"
        terminal = native.step(
            self._handle, action, self._combat_obs_buf, self._rew_buf
        )
        reward = float(self._rew_buf[0])
        truncated = not terminal and self._elapsed_steps >= self._max_episode_steps

        if terminal and native.player_won(self._handle):
            self._player_hp = max(0, int(self._combat_obs_buf[0]))
            self._after_combat_win()
            return self._obs(), reward, False, truncated, self._info()

        if terminal:
            self._player_hp = 0
        return self._obs(), reward, terminal, truncated, self._info()

    def action_masks(self) -> np.ndarray:
        mask = np.zeros(MAX_ACTIONS, dtype=bool)
        if self._phase == PHASE_CARD_REWARD:
            mask[: REWARD_SKIP_ACTION + 1] = True
            return mask

        if self._phase == PHASE_MAP:
            mask[:MAP_CHOICES] = self._map_node_types != NODE_NONE
            return mask

        if self._phase == PHASE_REST:
            mask[REST_HEAL_ACTION] = True
            mask[REST_UPGRADE_ACTION] = any(
                self._is_upgradable(card) for card in self._deck
            )
            return mask

        if self._phase == PHASE_SHOP:
            for i, card_id in enumerate(self._shop_cards):
                mask[i] = card_id != 0 and self._gold >= self._shop_card_cost(
                    int(card_id)
                )
            mask[3] = self._gold >= 120
            mask[SHOP_SKIP_ACTION] = True
            return mask

        if self._phase == PHASE_RELIC_REWARD:
            mask[0] = self._relic_reward != 0
            return mask

        if self._phase == PHASE_COMPLETE:
            return mask

        assert self._handle is not None, "Call reset() before action_masks()"
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
        if self._pending_relic_reward:
            self._pending_relic_reward = False
            self._enter_relic_reward_phase()
            return self._obs(), 0.0, False, False, self._info()

        return self._advance_after_node()

    def _step_map(self, action: int):
        if not 0 <= action < MAP_CHOICES or self._map_node_types[action] == NODE_NONE:
            raise ValueError(f"Invalid map action: {action}")

        self._current_node_type = int(self._map_node_types[action])
        encounter_id = int(self._map_choices[action])
        self._map_node_types[:] = 0
        self._map_choices[:] = 0

        if self._current_node_type in (NODE_NORMAL, NODE_ELITE, NODE_BOSS):
            self._phase = PHASE_COMBAT
            self._reset_combat(self._combat_seed(), encounter_id)
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_REST:
            self._phase = PHASE_REST
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_SHOP:
            self._enter_shop_phase()
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_RELIC:
            self._enter_relic_reward_phase()
            return self._obs(), 0.0, False, False, self._info()

        raise ValueError(f"Unsupported map node type: {self._current_node_type}")

    def _step_rest(self, action: int):
        if action == REST_HEAL_ACTION:
            heal = max(1, int(self._player_max_hp * 0.3))
            self._player_hp = min(self._player_max_hp, self._player_hp + heal)
        elif action == REST_UPGRADE_ACTION:
            upgraded = self._upgrade_first_card()
            if not upgraded:
                raise ValueError("No upgradable card available.")
        else:
            raise ValueError(f"Invalid rest action: {action}")
        return self._advance_after_node()

    def _step_shop(self, action: int):
        if 0 <= action < len(self._shop_cards):
            card_id = int(self._shop_cards[action])
            cost = self._shop_card_cost(card_id)
            if card_id == 0 or self._gold < cost:
                raise ValueError(f"Cannot buy shop card action: {action}")
            self._gold -= cost
            self._deck.append(card_id)
        elif action == 3:
            if self._gold < 120:
                raise ValueError("Cannot afford shop relic.")
            self._gold -= 120
            self._relics.append(self._next_relic())
        elif action != SHOP_SKIP_ACTION:
            raise ValueError(f"Invalid shop action: {action}")

        self._shop_cards[:] = 0
        return self._advance_after_node()

    def _step_relic_reward(self, action: int):
        if action != 0 or self._relic_reward == 0:
            raise ValueError(f"Invalid relic reward action: {action}")
        self._relics.append(int(self._relic_reward))
        self._relic_reward = 0
        if self._current_node_type == NODE_BOSS:
            self._phase = PHASE_COMPLETE
            return self._obs(), 1.0, True, False, self._info()
        return self._advance_after_node()

    def _after_combat_win(self) -> None:
        self._gold += self._gold_reward_for_node()
        if RELIC_BURNING_BLOOD in self._relics:
            self._player_hp = min(self._player_max_hp, self._player_hp + 6)
        self._pending_relic_reward = self._current_node_type in (NODE_ELITE, NODE_BOSS)
        self._enter_reward_phase()

    def _advance_after_node(self):
        if self._floor >= self._max_floors:
            self._phase = PHASE_COMPLETE
            return self._obs(), 0.0, True, False, self._info()

        self._floor += 1
        if self._floor <= 3:
            self._current_node_type = NODE_NORMAL
            self._phase = PHASE_COMBAT
            self._reset_combat(
                self._combat_seed(),
                int(self._weak_encounters[self._floor - 1]),
            )
            return self._obs(), 0.0, False, False, self._info()

        self._enter_map_phase()
        return self._obs(), 0.0, False, False, self._info()

    def _enter_reward_phase(self):
        self._phase = PHASE_CARD_REWARD
        self._reward_cards[:] = self._rng.choice(
            IRONCLAD_REWARD_POOL, size=3, replace=False
        )

    def _enter_map_phase(self):
        self._phase = PHASE_MAP
        if self._floor >= self._max_floors:
            boss = int(self._rng.choice(BOSS_ENCOUNTERS))
            self._map_node_types[:] = [NODE_BOSS, NODE_BOSS, NODE_BOSS]
            self._map_choices[:] = [boss, boss, boss]
            return

        self._map_node_types[:] = self._node_types_for_floor()
        for i, node_type in enumerate(self._map_node_types):
            self._map_choices[i] = self._encounter_for_node(int(node_type))

    def _enter_shop_phase(self):
        self._phase = PHASE_SHOP
        self._shop_cards[:] = self._rng.choice(
            IRONCLAD_REWARD_POOL, size=3, replace=False
        )

    def _enter_relic_reward_phase(self):
        self._phase = PHASE_RELIC_REWARD
        self._relic_reward = self._next_relic()

    def _select_act_and_weak_encounters(self):
        if self._rng.integers(2) == 0:
            self._act = "overgrowth"
            pool = OVERGROWTH_WEAK_ENCOUNTERS
        else:
            self._act = "underdocks"
            pool = UNDERDOCKS_WEAK_ENCOUNTERS
        self._weak_encounters[:] = self._rng.choice(pool, size=3, replace=False)

    def _normal_encounter_pool(self) -> np.ndarray:
        return (
            OVERGROWTH_NORMAL_ENCOUNTERS
            if self._act == "overgrowth"
            else UNDERDOCKS_NORMAL_ENCOUNTERS
        )

    def _node_types_for_floor(self) -> np.ndarray:
        if self._floor in (6, 11):
            return np.array([NODE_REST, NODE_SHOP, NODE_NORMAL], dtype=np.int32)
        if self._floor in (8, 13):
            return np.array([NODE_ELITE, NODE_NORMAL, NODE_RELIC], dtype=np.int32)
        return np.array([NODE_NORMAL, NODE_NORMAL, NODE_SHOP], dtype=np.int32)

    def _encounter_for_node(self, node_type: int) -> int:
        if node_type == NODE_NORMAL:
            return int(self._rng.choice(self._normal_encounter_pool()))
        if node_type == NODE_ELITE:
            return int(self._rng.choice(ELITE_ENCOUNTERS))
        if node_type == NODE_BOSS:
            return int(self._rng.choice(BOSS_ENCOUNTERS))
        return 0

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
        obs[: native.OBS_SIZE] = np.ctypeslib.as_array(self._combat_obs_buf)
        obs[native.OBS_SIZE :] = np.array(
            [
                self._phase,
                self._floor,
                ACT_OVERGROWTH if self._act == "overgrowth" else ACT_UNDERDOCKS,
                len(self._deck),
                self._gold,
                self._player_hp,
                self._player_max_hp,
                len(self._relics),
                self._current_node_type,
                int(self._reward_cards[0]),
                int(self._reward_cards[1]),
                int(self._reward_cards[2]),
                int(self._map_node_types[0]),
                int(self._map_node_types[1]),
                int(self._map_node_types[2]),
                int(self._map_choices[0]),
                int(self._map_choices[1]),
                int(self._map_choices[2]),
                int(self._shop_cards[0]),
                int(self._shop_cards[1]),
                int(self._shop_cards[2]),
                int(self._relic_reward),
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
            "gold": self._gold,
            "player_hp": self._player_hp,
            "player_max_hp": self._player_max_hp,
            "relics": tuple(self._relics),
            "current_node_type": self._current_node_type,
            "card_rewards": tuple(int(card_id) for card_id in self._reward_cards),
            "shop_cards": tuple(int(card_id) for card_id in self._shop_cards),
            "relic_reward": int(self._relic_reward),
            "map_choices": (
                tuple(
                    {
                        "node_type": int(node_type),
                        "encounter": ENCOUNTER_NAMES.get(
                            int(encounter_id), f"unknown-{encounter_id}"
                        ),
                    }
                    for node_type, encounter_id in zip(
                        self._map_node_types, self._map_choices
                    )
                    if int(node_type) != NODE_NONE
                )
                if self._phase == PHASE_MAP
                else ()
            ),
            "player_won": (
                native.player_won(self._handle) if self._handle is not None else False
            ),
            "encounter_id": (
                native.encounter_id(self._handle) if self._handle is not None else -1
            ),
            "encounter": (
                ENCOUNTER_NAMES.get(native.encounter_id(self._handle), "none")
                if self._handle is not None
                else "none"
            ),
        }

    def _combat_seed(self) -> int:
        return self._seed + self._floor - 1

    def _gold_reward_for_node(self) -> int:
        if self._current_node_type == NODE_ELITE:
            return 35
        if self._current_node_type == NODE_BOSS:
            return 100
        return 15

    def _shop_card_cost(self, card_id: int) -> int:
        return 50 + (card_id % 3) * 25

    def _next_relic(self) -> int:
        available = [
            int(relic) for relic in RELIC_REWARD_POOL if int(relic) not in self._relics
        ]
        if not available:
            return int(self._rng.choice(RELIC_REWARD_POOL))
        return int(self._rng.choice(available))

    def _is_upgradable(self, encoded_card: int) -> bool:
        return encoded_card > 0 and encoded_card in UPGRADABLE_STARTER_CARDS

    def _upgrade_first_card(self) -> bool:
        for index, encoded_card in enumerate(self._deck):
            if self._is_upgradable(encoded_card):
                self._deck[index] = -encoded_card
                return True
        return False
