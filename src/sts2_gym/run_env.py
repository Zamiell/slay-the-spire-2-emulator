"""Run-level Gymnasium wrapper for simplified full-run training."""

from __future__ import annotations

import ctypes
from dataclasses import dataclass, field

import gymnasium as gym
import numpy as np
from gymnasium import spaces

from . import native
from .env import ENCOUNTER_NAMES, MAX_ACTIONS

REWARD_SKIP_ACTION = 3
REST_HEAL_ACTION = 0
REST_UPGRADE_ACTION = 1
SHOP_CARD_ACTIONS = range(0, 7)
SHOP_RELIC_ACTIONS = range(7, 10)
SHOP_POTION_ACTIONS = range(10, 13)
SHOP_REMOVE_ACTION = 13
SHOP_SKIP_ACTION = 14
EVENT_SKIP_ACTION = 3
NEOW_SKIP_ACTION = 3
MAP_CHOICES = 4
RUN_EXTRA_OBS = 35
RUN_OBS_SIZE = native.OBS_SIZE + RUN_EXTRA_OBS
RUN_MAX_EPISODE_STEPS = 1000

PHASE_COMBAT = 0
PHASE_CARD_REWARD = 1
PHASE_MAP = 2
PHASE_REST = 3
PHASE_SHOP = 4
PHASE_RELIC_REWARD = 5
PHASE_COMPLETE = 6
PHASE_EVENT = 7
PHASE_NEOW = 8

NODE_NONE = 0
NODE_NORMAL = 1
NODE_ELITE = 2
NODE_REST = 3
NODE_SHOP = 4
NODE_RELIC = 5
NODE_BOSS = 6
NODE_EVENT = 7

ACT_OVERGROWTH = 1
ACT_UNDERDOCKS = 2

MAP_WIDTH = 7
MAP_PATH_ITERATIONS = 7
MAP_BOSS_ROW = 16
MAP_TREASURE_ROW = MAP_BOSS_ROW - 7
MAP_FINAL_REST_ROW = MAP_BOSS_ROW - 1
MAP_START_COORD = (MAP_WIDTH // 2, 0)
MAP_BOSS_COORD = (MAP_WIDTH // 2, MAP_BOSS_ROW)
MAP_NODE_TO_OBS = {
    "Monster": NODE_NORMAL,
    "Elite": NODE_ELITE,
    "RestSite": NODE_REST,
    "Shop": NODE_SHOP,
    "Treasure": NODE_RELIC,
    "Unknown": NODE_EVENT,
    "Boss": NODE_BOSS,
}
MAP_LOWER_RESTRICTED = {"RestSite", "Elite"}
MAP_UPPER_RESTRICTED = {"RestSite"}
MAP_ADJACENCY_RESTRICTED = {"Elite", "RestSite", "Treasure", "Shop"}
MAP_SIBLING_RESTRICTED = {"RestSite", "Monster", "Unknown", "Elite", "Shop"}

RELIC_BURNING_BLOOD = 36
RELIC_ANCHOR = 4
RELIC_AMETHYST_AUBERGINE = 3
RELIC_ARCANE_SCROLL = 5
RELIC_BLOOD_VIAL = 23
RELIC_BOOMING_CONCH = 29
RELIC_CURSED_PEARL = 54
RELIC_FISHING_ROD = 89
RELIC_GOLDEN_PEARL = 105
RELIC_HEFTY_TABLET = 111
RELIC_KALEIDOSCOPE = 124
RELIC_LARGE_CAPSULE = 129
RELIC_LAVA_ROCK = 132
RELIC_LEAD_PAPERWEIGHT = 133
RELIC_LEAFY_POULTICE = 134
RELIC_LOST_COFFER = 140
RELIC_MASSIVE_SCROLL = 145
RELIC_NEOWS_BONES = 161
RELIC_NEOWS_TALISMAN = 162
RELIC_NEOWS_TORMENT = 163
RELIC_NEW_LEAF = 164
RELIC_NUTRITIOUS_OYSTER = 167
RELIC_PANTOGRAPH = 186
RELIC_PHIAL_HOLSTER = 195
RELIC_POMANDER = 201
RELIC_PRECARIOUS_SHEARS = 205
RELIC_PRECISE_SCISSORS = 206
RELIC_SCROLL_BOXES = 231
RELIC_SILKEN_TRESS = 239
RELIC_SILVER_CRUCIBLE = 240
RELIC_SMALL_CAPSULE = 242
RELIC_VAJRA = 279
RELIC_ODDLY_SMOOTH_STONE = 169
RELIC_ORICHALCUM = 172
RELIC_BAG_OF_PREPARATION = 10
RELIC_BLACK_BLOOD = 19
RELIC_STONE_HUMIDIFIER = 250
RELIC_WAR_HAMMER = 286
RELIC_WINGED_BOOTS = 293
NEOWS_FURY_CARD = 321
CURSE_PLACEHOLDER_CARD = 10001
RELIC_REWARD_POOL = np.array(
    [
        RELIC_AMETHYST_AUBERGINE,
        RELIC_ANCHOR,
        RELIC_BLACK_BLOOD,
        RELIC_BLOOD_VIAL,
        RELIC_VAJRA,
        RELIC_ODDLY_SMOOTH_STONE,
        RELIC_ORICHALCUM,
        RELIC_BAG_OF_PREPARATION,
        RELIC_PANTOGRAPH,
        RELIC_STONE_HUMIDIFIER,
        RELIC_WAR_HAMMER,
    ],
    dtype=np.int32,
)
NEOW_POSITIVE_RELICS = np.array(
    [
        RELIC_ARCANE_SCROLL,
        RELIC_BOOMING_CONCH,
        RELIC_FISHING_ROD,
        RELIC_GOLDEN_PEARL,
        RELIC_KALEIDOSCOPE,
        RELIC_LEAD_PAPERWEIGHT,
        RELIC_LOST_COFFER,
        RELIC_NEOWS_TORMENT,
        RELIC_NEW_LEAF,
        RELIC_PHIAL_HOLSTER,
        RELIC_PRECISE_SCISSORS,
        RELIC_SCROLL_BOXES,
        RELIC_WINGED_BOOTS,
    ],
    dtype=np.int32,
)
NEOW_CURSE_RELICS = np.array(
    [
        RELIC_CURSED_PEARL,
        RELIC_HEFTY_TABLET,
        RELIC_LARGE_CAPSULE,
        RELIC_LEAFY_POULTICE,
        RELIC_NEOWS_BONES,
        RELIC_PRECARIOUS_SHEARS,
        RELIC_SILKEN_TRESS,
        RELIC_SILVER_CRUCIBLE,
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
OVERGROWTH_ELITE_ENCOUNTERS = np.array([88, 85], dtype=np.int32)
UNDERDOCKS_ELITE_ENCOUNTERS = np.array([92, 87], dtype=np.int32)
OVERGROWTH_BOSS_ENCOUNTERS = np.array([103, 94, 102], dtype=np.int32)
UNDERDOCKS_BOSS_ENCOUNTERS = np.array([104, 99, 97], dtype=np.int32)
GREMLIN_MERC_ENCOUNTER = 7
EVENT_UNREST_SITE = 1
EVENT_AROMA_OF_CHAOS = 2
EVENT_SIMPLE_REWARD = 3
POOR_SLEEP_CARD = 10001


@dataclass
class RunMapNode:
    col: int
    row: int
    node_type: str = "Unassigned"
    can_be_modified: bool = True
    children: set[tuple[int, int]] = field(default_factory=set)
    parents: set[tuple[int, int]] = field(default_factory=set)


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
        max_episode_steps: int = RUN_MAX_EPISODE_STEPS,
        max_floors: int = 16,
    ):
        super().__init__()
        self._seed = seed
        self._rng = np.random.default_rng(seed)
        self._max_episode_steps = max_episode_steps
        self._max_floors = max_floors
        self._elapsed_steps = 0
        self._floor = 1
        self._phase = PHASE_NEOW
        self._deck = list(STARTER_DECK)
        self._gold = 99
        self._player_hp = 64
        self._player_max_hp = 80
        self._potions = [0, 0, 0]
        self._relics = [RELIC_BURNING_BLOOD]
        self._current_node_type = NODE_NORMAL
        self._pending_relic_reward = False
        self._reward_cards = np.zeros(3, dtype=np.int32)
        self._reward_upgraded = np.zeros(3, dtype=bool)
        self._shop_cards = np.zeros(7, dtype=np.int32)
        self._shop_relics = np.zeros(3, dtype=np.int32)
        self._shop_potions = np.zeros(3, dtype=np.int32)
        self._shop_costs = np.zeros(14, dtype=np.int32)
        self._map_node_types = np.zeros(MAP_CHOICES, dtype=np.int32)
        self._map_choices = np.zeros(MAP_CHOICES, dtype=np.int32)
        self._relic_reward = 0
        self._neow_options = np.zeros(3, dtype=np.int32)
        self._silver_crucible_card_rewards_seen = 0
        self._silver_crucible_treasure_seen = 0
        self._fishing_rod_combats_seen = 0
        self._winged_boots_times_used = 0
        self._event_id = 0
        self._act = "overgrowth"
        self._weak_encounters = np.zeros(3, dtype=np.int32)
        self._map_nodes: dict[tuple[int, int], RunMapNode] = {}
        self._current_map_coord = MAP_START_COORD
        self._map_option_coords: list[tuple[int, int] | None] = [None] * MAP_CHOICES
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
        self._phase = PHASE_NEOW
        self._deck = list(STARTER_DECK)
        self._gold = 99
        self._player_hp = 64
        self._player_max_hp = 80
        self._potions = [0, 0, 0]
        self._relics = [RELIC_BURNING_BLOOD]
        self._current_node_type = NODE_NORMAL
        self._pending_relic_reward = False
        self._reward_cards[:] = 0
        self._reward_upgraded[:] = False
        self._shop_cards[:] = 0
        self._shop_relics[:] = 0
        self._shop_potions[:] = 0
        self._shop_costs[:] = 0
        self._map_node_types[:] = 0
        self._map_choices[:] = 0
        self._relic_reward = 0
        self._neow_options[:] = 0
        self._silver_crucible_card_rewards_seen = 0
        self._silver_crucible_treasure_seen = 0
        self._fishing_rod_combats_seen = 0
        self._winged_boots_times_used = 0
        self._event_id = 0
        self._map_nodes = {}
        self._current_map_coord = MAP_START_COORD
        self._map_option_coords = [None] * MAP_CHOICES
        if self._handle is not None:
            native.destroy(self._handle)
            self._handle = None
        for i in range(native.OBS_SIZE):
            self._combat_obs_buf[i] = 0
        self._select_act_and_weak_encounters()
        self._generate_act_map()
        self._generate_neow_options()
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
        if self._phase == PHASE_EVENT:
            return self._step_event(action)
        if self._phase == PHASE_NEOW:
            return self._step_neow(action)
        if self._phase == PHASE_COMPLETE:
            return self._obs(), 0.0, True, False, self._info()

        assert self._handle is not None, "Call reset() before step()"
        terminal = native.step(
            self._handle, action, self._combat_obs_buf, self._rew_buf
        )
        reward = float(self._rew_buf[0])
        self._sync_run_state_from_combat_obs()
        truncated = not terminal and self._elapsed_steps >= self._max_episode_steps

        if terminal and native.player_won(self._handle):
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
                mask[i] = card_id != 0 and self._gold >= int(self._shop_costs[i])
            for action in SHOP_RELIC_ACTIONS:
                index = action - SHOP_RELIC_ACTIONS.start
                mask[action] = self._shop_relics[index] != 0 and self._gold >= int(
                    self._shop_costs[action]
                )
            for action in SHOP_POTION_ACTIONS:
                index = action - SHOP_POTION_ACTIONS.start
                mask[action] = (
                    self._shop_potions[index] != 0
                    and self._gold >= int(self._shop_costs[action])
                    and any(potion == 0 for potion in self._potions)
                )
            mask[SHOP_REMOVE_ACTION] = self._gold >= 75 and len(self._deck) > 1
            mask[SHOP_SKIP_ACTION] = True
            return mask

        if self._phase == PHASE_RELIC_REWARD:
            mask[0] = self._relic_reward != 0
            return mask

        if self._phase == PHASE_EVENT:
            mask[EVENT_SKIP_ACTION] = True
            if self._event_id == EVENT_UNREST_SITE:
                mask[0] = self._player_hp < self._player_max_hp
                mask[1] = self._player_max_hp > 8
            elif self._event_id == EVENT_AROMA_OF_CHAOS:
                mask[0] = len(self._deck) > 0
                mask[1] = any(self._is_upgradable(card) for card in self._deck)
            else:
                mask[: EVENT_SKIP_ACTION + 1] = True
            return mask

        if self._phase == PHASE_NEOW:
            mask[: len(self._neow_options)] = self._neow_options != 0
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

    def _invalid_action(self):
        return self._obs(), -1.0, False, False, self._info()

    def _step_reward(self, action: int):
        if 0 <= action < len(self._reward_cards):
            card_id = int(self._reward_cards[action])
            self._deck.append(-card_id if self._reward_upgraded[action] else card_id)
        elif action != REWARD_SKIP_ACTION:
            return self._invalid_action()

        self._reward_cards[:] = 0
        self._reward_upgraded[:] = False
        if self._pending_relic_reward:
            self._pending_relic_reward = False
            self._enter_relic_reward_phase()
            return self._obs(), 0.0, False, False, self._info()

        return self._advance_after_node()

    def _step_map(self, action: int):
        if not 0 <= action < MAP_CHOICES or self._map_node_types[action] == NODE_NONE:
            return self._invalid_action()

        self._current_node_type = int(self._map_node_types[action])
        encounter_id = int(self._map_choices[action])
        option_coord = self._map_option_coords[action]
        previous_coord = self._current_map_coord
        if option_coord is not None:
            self._current_map_coord = option_coord
            previous = self._map_nodes[previous_coord]
            if option_coord not in (previous.children or set()):
                self._winged_boots_times_used += 1
        self._map_node_types[:] = 0
        self._map_choices[:] = 0
        self._map_option_coords = [None] * MAP_CHOICES

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
            if (
                RELIC_SILVER_CRUCIBLE in self._relics
                and self._silver_crucible_treasure_seen == 0
            ):
                self._silver_crucible_treasure_seen += 1
                return self._advance_after_node()
            self._enter_relic_reward_phase()
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_EVENT:
            self._enter_event_phase()
            return self._obs(), 0.0, False, False, self._info()

        raise ValueError(f"Unsupported map node type: {self._current_node_type}")

    def _step_rest(self, action: int):
        if action == REST_HEAL_ACTION:
            heal = max(1, int(self._player_max_hp * 0.3))
            self._player_hp = min(self._player_max_hp, self._player_hp + heal)
            if RELIC_STONE_HUMIDIFIER in self._relics:
                self._player_max_hp += 5
        elif action == REST_UPGRADE_ACTION:
            upgraded = self._upgrade_first_card()
            if not upgraded:
                return self._invalid_action()
        else:
            return self._invalid_action()
        return self._advance_after_node()

    def _step_shop(self, action: int):
        if action in SHOP_CARD_ACTIONS:
            card_id = int(self._shop_cards[action])
            cost = int(self._shop_costs[action])
            if card_id == 0 or self._gold < cost:
                return self._invalid_action()
            self._gold -= cost
            self._deck.append(card_id)
            self._shop_cards[action] = 0
        elif action in SHOP_RELIC_ACTIONS:
            index = action - SHOP_RELIC_ACTIONS.start
            relic_id = int(self._shop_relics[index])
            cost = int(self._shop_costs[action])
            if relic_id == 0 or self._gold < cost:
                return self._invalid_action()
            self._gold -= cost
            self._relics.append(relic_id)
            self._shop_relics[index] = 0
        elif action in SHOP_POTION_ACTIONS:
            index = action - SHOP_POTION_ACTIONS.start
            potion_id = int(self._shop_potions[index])
            cost = int(self._shop_costs[action])
            if potion_id == 0 or self._gold < cost or not self._add_potion(potion_id):
                return self._invalid_action()
            self._gold -= cost
            self._shop_potions[index] = 0
        elif action == SHOP_REMOVE_ACTION:
            if self._gold < 75 or len(self._deck) <= 1:
                return self._invalid_action()
            self._gold -= self._shop_removal_cost()
            self._remove_lowest_priority_card()
        elif action != SHOP_SKIP_ACTION:
            return self._invalid_action()

        return self._advance_after_node()

    def _step_relic_reward(self, action: int):
        if action != 0 or self._relic_reward == 0:
            return self._invalid_action()
        self._obtain_relic(int(self._relic_reward))
        self._relic_reward = 0
        if self._current_node_type == NODE_BOSS:
            self._phase = PHASE_COMPLETE
            return self._obs(), 1.0, True, False, self._info()
        return self._advance_after_node()

    def _step_event(self, action: int):
        if self._event_id == EVENT_UNREST_SITE:
            if action == 0:
                self._player_hp = self._player_max_hp
                self._deck.append(POOR_SLEEP_CARD)
            elif action == 1:
                self._player_max_hp = max(1, self._player_max_hp - 8)
                self._player_hp = min(self._player_hp, self._player_max_hp)
                self._relics.append(self._next_relic())
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_AROMA_OF_CHAOS:
            if action == 0:
                self._transform_first_card()
            elif action == 1:
                upgraded = self._upgrade_first_card()
                if not upgraded:
                    return self._invalid_action()
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif action == 0:
            self._gold += 50
            self._add_potion(1)
        elif action == 1:
            if self._player_hp >= self._player_max_hp:
                return self._invalid_action()
            self._player_hp = min(self._player_max_hp, self._player_hp + 15)
        elif action == 2:
            self._deck.append(int(self._rng.choice(IRONCLAD_REWARD_POOL)))
        elif action != EVENT_SKIP_ACTION:
            return self._invalid_action()

        self._event_id = 0
        return self._advance_after_node()

    def _step_neow(self, action: int):
        if not 0 <= action < len(self._neow_options):
            return self._invalid_action()
        relic_id = int(self._neow_options[action])
        if relic_id == 0:
            return self._invalid_action()

        self._obtain_relic(relic_id)
        self._phase = PHASE_COMBAT
        self._enter_map_phase()
        return self._obs(), 0.0, False, False, self._info()

    def _obtain_relic(self, relic_id: int) -> None:
        self._relics.append(relic_id)
        if relic_id == RELIC_GOLDEN_PEARL:
            self._gold += 150
        elif relic_id == RELIC_NEOWS_TORMENT:
            self._deck.append(NEOWS_FURY_CARD)
        elif relic_id == RELIC_NEOWS_BONES:
            for _ in range(2):
                bonus = int(self._rng.choice(NEOW_POSITIVE_RELICS))
                if bonus not in self._relics:
                    self._relics.append(bonus)
            self._deck.append(CURSE_PLACEHOLDER_CARD)
        elif relic_id == RELIC_NUTRITIOUS_OYSTER:
            self._player_max_hp += 11
            self._player_hp = min(self._player_max_hp, self._player_hp + 11)
        elif relic_id == RELIC_SMALL_CAPSULE:
            self._obtain_relic(self._next_relic())
        elif relic_id == RELIC_LARGE_CAPSULE:
            for _ in range(2):
                self._obtain_relic(self._next_relic())
            self._deck.extend([472, 131])
        elif relic_id == RELIC_POMANDER:
            self._upgrade_first_card()
        elif relic_id == RELIC_NEOWS_TALISMAN:
            self._upgrade_last_card_matching(472)
            self._upgrade_last_card_matching(131)
        elif relic_id == RELIC_CURSED_PEARL:
            self._deck.append(CURSE_PLACEHOLDER_CARD)
            self._gold += 333
        elif relic_id == RELIC_HEFTY_TABLET:
            self._deck.append(int(self._rng.choice(IRONCLAD_REWARD_POOL)))
            self._deck.append(CURSE_PLACEHOLDER_CARD)
        elif relic_id == RELIC_KALEIDOSCOPE:
            self._deck.extend(
                int(card_id)
                for card_id in self._rng.choice(
                    IRONCLAD_REWARD_POOL, size=2, replace=False
                )
            )
        elif relic_id == RELIC_ARCANE_SCROLL:
            self._deck.append(int(self._rng.choice(IRONCLAD_REWARD_POOL)))
        elif relic_id == RELIC_LEAD_PAPERWEIGHT:
            self._deck.append(int(self._rng.choice(IRONCLAD_REWARD_POOL)))
        elif relic_id == RELIC_LOST_COFFER:
            self._deck.append(int(self._rng.choice(IRONCLAD_REWARD_POOL)))
            self._add_potion(self._next_potion())
        elif relic_id == RELIC_NEW_LEAF:
            self._transform_first_card()
        elif relic_id == RELIC_PHIAL_HOLSTER:
            self._add_potion(self._next_potion())
            self._add_potion(self._next_potion())
        elif relic_id == RELIC_PRECISE_SCISSORS:
            self._remove_lowest_priority_card()
        elif relic_id == RELIC_SCROLL_BOXES:
            self._deck.extend(
                int(card_id)
                for card_id in self._rng.choice(
                    IRONCLAD_REWARD_POOL, size=3, replace=False
                )
            )
        elif relic_id == RELIC_LEAFY_POULTICE:
            self._player_max_hp = max(1, self._player_max_hp - 12)
            self._player_hp = min(self._player_hp, self._player_max_hp)
            self._transform_first_card_matching(472)
            self._transform_first_card_matching(131)
        elif relic_id == RELIC_PRECARIOUS_SHEARS:
            self._remove_lowest_priority_card()
            self._remove_lowest_priority_card()
            self._player_hp = max(0, self._player_hp - 16)
        elif relic_id == RELIC_SILKEN_TRESS:
            self._gold = 0

    def _after_combat_win(self) -> None:
        self._gold += self._gold_reward_for_node()
        if RELIC_AMETHYST_AUBERGINE in self._relics and self._current_node_type in (
            NODE_NORMAL,
            NODE_ELITE,
        ):
            self._gold += 15
        if self._floor % 3 == 0:
            self._add_potion(self._next_potion())
        if RELIC_BURNING_BLOOD in self._relics:
            self._player_hp = min(self._player_max_hp, self._player_hp + 6)
        if RELIC_BLACK_BLOOD in self._relics:
            self._player_hp = min(self._player_max_hp, self._player_hp + 12)
        if RELIC_FISHING_ROD in self._relics and self._current_node_type == NODE_NORMAL:
            self._fishing_rod_combats_seen += 1
            if self._fishing_rod_combats_seen % 3 == 0:
                self._upgrade_random_card()
        if RELIC_WAR_HAMMER in self._relics and self._current_node_type == NODE_ELITE:
            for _ in range(4):
                if not self._upgrade_random_card():
                    break
        self._pending_relic_reward = self._current_node_type in (NODE_ELITE, NODE_BOSS)
        self._enter_reward_phase()

    def _advance_after_node(self):
        if self._floor >= self._max_floors:
            self._phase = PHASE_COMPLETE
            return self._obs(), 0.0, True, False, self._info()

        self._floor += 1
        self._enter_map_phase()
        return self._obs(), 0.0, False, False, self._info()

    def _enter_reward_phase(self):
        self._phase = PHASE_CARD_REWARD
        self._reward_cards[:] = self._rng.choice(
            IRONCLAD_REWARD_POOL, size=3, replace=False
        )
        self._reward_upgraded[:] = False
        if (
            RELIC_SILVER_CRUCIBLE in self._relics
            and self._silver_crucible_card_rewards_seen < 3
        ):
            self._reward_upgraded[:] = True
            self._silver_crucible_card_rewards_seen += 1

    def _enter_map_phase(self):
        self._phase = PHASE_MAP
        self._map_node_types[:] = 0
        self._map_choices[:] = 0
        self._map_option_coords = [None] * MAP_CHOICES
        current = self._map_nodes[self._current_map_coord]
        children = sorted(
            current.children or set(), key=lambda coord: (coord[1], coord[0])
        )
        options = children
        if RELIC_WINGED_BOOTS in self._relics and self._winged_boots_times_used < 3:
            next_row = current.row + 1
            winged_coords = sorted(
                [
                    coord
                    for coord, node in self._map_nodes.items()
                    if node.row == next_row and coord not in options
                ],
                key=lambda coord: (coord[1], coord[0]),
            )
            options = [*options, *winged_coords]
        for i, coord in enumerate(options[:MAP_CHOICES]):
            node = self._map_nodes[coord]
            node_type = MAP_NODE_TO_OBS[node.node_type]
            self._map_node_types[i] = node_type
            self._map_choices[i] = self._encounter_for_node(node_type)
            self._map_option_coords[i] = coord

    def _enter_shop_phase(self):
        self._phase = PHASE_SHOP
        self._shop_cards[:] = self._rng.choice(
            IRONCLAD_REWARD_POOL, size=len(self._shop_cards), replace=False
        )
        self._shop_relics[:] = [self._next_relic() for _ in range(3)]
        self._shop_potions[:] = [self._next_potion() for _ in range(3)]
        self._shop_costs[:] = 0
        sale_index = int(self._rng.integers(0, 5))
        for action, card_id in enumerate(self._shop_cards):
            cost = self._shop_card_cost(int(card_id), colorless=action >= 5)
            if action == sale_index:
                cost //= 2
            self._shop_costs[action] = cost
        for action in SHOP_RELIC_ACTIONS:
            self._shop_costs[action] = self._shop_relic_cost()
        for action in SHOP_POTION_ACTIONS:
            self._shop_costs[action] = self._shop_potion_cost(action)
        self._shop_costs[SHOP_REMOVE_ACTION] = self._shop_removal_cost()

    def _enter_relic_reward_phase(self):
        self._phase = PHASE_RELIC_REWARD
        self._relic_reward = self._next_relic()

    def _enter_event_phase(self):
        self._phase = PHASE_EVENT
        if self._player_hp <= int(self._player_max_hp * 0.7):
            event_pool = [EVENT_UNREST_SITE, EVENT_AROMA_OF_CHAOS, EVENT_SIMPLE_REWARD]
        else:
            event_pool = [EVENT_AROMA_OF_CHAOS, EVENT_SIMPLE_REWARD]
        self._event_id = int(self._rng.choice(event_pool))

    def _select_act_and_weak_encounters(self):
        if self._rng.integers(2) == 0:
            self._act = "overgrowth"
            pool = OVERGROWTH_WEAK_ENCOUNTERS
        else:
            self._act = "underdocks"
            pool = UNDERDOCKS_WEAK_ENCOUNTERS
        self._weak_encounters[:] = self._rng.choice(pool, size=3, replace=False)

    def _generate_act_map(self) -> None:
        self._map_nodes = {}
        self._current_map_coord = MAP_START_COORD
        self._map_option_coords = [None] * MAP_CHOICES
        self._get_or_create_map_node(*MAP_START_COORD).node_type = "Ancient"
        self._get_or_create_map_node(*MAP_BOSS_COORD).node_type = "Boss"

        start_points: set[tuple[int, int]] = set()
        for path_index in range(MAP_PATH_ITERATIONS):
            start = self._get_or_create_map_node(
                int(self._rng.integers(0, MAP_WIDTH)), 1
            )
            if path_index == 1:
                while (start.col, start.row) in start_points:
                    start = self._get_or_create_map_node(
                        int(self._rng.integers(0, MAP_WIDTH)), 1
                    )
            start_points.add((start.col, start.row))
            self._generate_map_path(start)

        for coord in start_points:
            self._add_map_edge(MAP_START_COORD, coord)
        for coord, node in list(self._map_nodes.items()):
            if node.row == MAP_BOSS_ROW - 1:
                self._add_map_edge(coord, MAP_BOSS_COORD)

        self._assign_map_point_types()

    def _get_or_create_map_node(self, col: int, row: int) -> RunMapNode:
        coord = (col, row)
        node = self._map_nodes.get(coord)
        if node is None:
            node = RunMapNode(col, row)
            self._map_nodes[coord] = node
        return node

    def _add_map_edge(
        self, parent_coord: tuple[int, int], child_coord: tuple[int, int]
    ):
        parent = self._get_or_create_map_node(*parent_coord)
        child = self._get_or_create_map_node(*child_coord)
        parent.children.add(child_coord)
        child.parents.add(parent_coord)

    def _generate_map_path(self, start: RunMapNode) -> None:
        current = start
        while current.row < MAP_BOSS_ROW - 1:
            child_coord = self._generate_next_map_coord(current)
            self._add_map_edge((current.col, current.row), child_coord)
            current = self._map_nodes[child_coord]

    def _generate_next_map_coord(self, current: RunMapNode) -> tuple[int, int]:
        deltas = np.array([-1, 0, 1], dtype=np.int32)
        self._rng.shuffle(deltas)
        for delta in deltas:
            target_col = max(0, min(MAP_WIDTH - 1, current.col + int(delta)))
            if not self._has_invalid_crossover(current, target_col):
                return (target_col, current.row + 1)
        raise RuntimeError(
            f"Cannot find next map node from {(current.col, current.row)}"
        )

    def _has_invalid_crossover(self, current: RunMapNode, target_col: int) -> bool:
        delta = target_col - current.col
        if delta == 0:
            return False
        sibling = self._map_nodes.get((target_col, current.row))
        if sibling is None:
            return False
        for child_col, _ in sibling.children or set():
            if child_col - sibling.col == -delta:
                return True
        return False

    def _assign_map_point_types(self) -> None:
        for node in self._map_nodes.values():
            if node.row == MAP_FINAL_REST_ROW:
                node.node_type = "RestSite"
                node.can_be_modified = False
            elif node.row == MAP_TREASURE_ROW:
                node.node_type = "Treasure"
                node.can_be_modified = False
            elif node.row == 1:
                node.node_type = "Monster"
                node.can_be_modified = False

        rest_count = int(np.clip(np.rint(self._rng.normal(7, 1)), 6, 7))
        unknown_count = int(np.clip(np.rint(self._rng.normal(12, 1)), 10, 14))
        type_queue = (
            ["RestSite"] * rest_count
            + ["Shop"] * 3
            + ["Elite"] * 8
            + ["Unknown"] * unknown_count
        )
        candidates = [
            node
            for node in self._map_nodes.values()
            if node.node_type == "Unassigned" and node.row not in (0, MAP_BOSS_ROW)
        ]
        for _ in range(3):
            if not type_queue:
                break
            self._rng.shuffle(candidates)
            for node in candidates:
                if not type_queue or node.node_type != "Unassigned":
                    continue
                node.node_type = self._next_valid_map_point_type(type_queue, node)

        for node in self._map_nodes.values():
            if node.node_type == "Unassigned":
                node.node_type = "Monster"

        self._map_nodes[MAP_START_COORD].node_type = "Ancient"
        self._map_nodes[MAP_BOSS_COORD].node_type = "Boss"

    def _next_valid_map_point_type(
        self, type_queue: list[str], node: RunMapNode
    ) -> str:
        for _ in range(len(type_queue)):
            node_type = type_queue.pop(0)
            if self._is_valid_map_point_type(node_type, node):
                return node_type
            type_queue.append(node_type)
        return "Unassigned"

    def _is_valid_map_point_type(self, node_type: str, node: RunMapNode) -> bool:
        if node.row < 6 and node_type in MAP_LOWER_RESTRICTED:
            return False
        if node.row >= MAP_BOSS_ROW - 2 and node_type in MAP_UPPER_RESTRICTED:
            return False
        if node_type in MAP_ADJACENCY_RESTRICTED:
            adjacent = (node.parents or set()) | (node.children or set())
            if any(self._map_nodes[coord].node_type == node_type for coord in adjacent):
                return False
        if node_type in MAP_SIBLING_RESTRICTED:
            siblings = set()
            for parent_coord in node.parents or set():
                siblings.update(self._map_nodes[parent_coord].children or set())
            siblings.discard((node.col, node.row))
            if any(self._map_nodes[coord].node_type == node_type for coord in siblings):
                return False
        return True

    def _normal_encounter_pool(self) -> np.ndarray:
        return (
            OVERGROWTH_NORMAL_ENCOUNTERS
            if self._act == "overgrowth"
            else UNDERDOCKS_NORMAL_ENCOUNTERS
        )

    def _elite_encounter_pool(self) -> np.ndarray:
        return (
            OVERGROWTH_ELITE_ENCOUNTERS
            if self._act == "overgrowth"
            else UNDERDOCKS_ELITE_ENCOUNTERS
        )

    def _boss_encounter_pool(self) -> np.ndarray:
        return (
            OVERGROWTH_BOSS_ENCOUNTERS
            if self._act == "overgrowth"
            else UNDERDOCKS_BOSS_ENCOUNTERS
        )

    def _encounter_for_node(self, node_type: int) -> int:
        if node_type == NODE_NORMAL:
            return int(self._rng.choice(self._normal_encounter_pool()))
        if node_type == NODE_ELITE:
            return int(self._rng.choice(self._elite_encounter_pool()))
        if node_type == NODE_BOSS:
            return int(self._rng.choice(self._boss_encounter_pool()))
        return 0

    def _reset_combat(self, seed: int, encounter_id: int | None = None):
        if self._handle is not None:
            native.destroy(self._handle)
        self._handle = native.create(seed)
        if encounter_id is not None and self._current_node_type == NODE_BOSS:
            if RELIC_PANTOGRAPH in self._relics:
                self._player_hp = min(self._player_max_hp, self._player_hp + 25)
        if encounter_id is None:
            native.reset_with_deck(self._handle, self._deck, self._combat_obs_buf)
        else:
            native.reset_run_combat(
                self._handle,
                self._deck,
                encounter_id,
                self._relics,
                self._player_hp,
                self._player_max_hp,
                self._potions,
                self._gold,
                self._combat_obs_buf,
            )

    def _sync_run_state_from_combat_obs(self) -> None:
        self._player_hp = max(0, int(self._combat_obs_buf[0]))
        self._player_max_hp = max(1, int(self._combat_obs_buf[1]))
        self._gold = max(0, int(self._combat_obs_buf[156]))
        self._potions = [int(self._combat_obs_buf[28 + i * 2]) for i in range(3)]

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
                int(self._map_node_types[3]),
                int(self._map_choices[0]),
                int(self._map_choices[1]),
                int(self._map_choices[2]),
                int(self._map_choices[3]),
                int(self._shop_cards[0]),
                int(self._shop_cards[1]),
                int(self._shop_cards[2]),
                int(self._relic_reward),
                int(self._event_id),
                int(self._potions[0]),
                int(self._potions[1]),
                int(self._potions[2]),
                int(self._shop_relics[0]),
                int(self._shop_relics[1]),
                int(self._shop_relics[2]),
                int(self._shop_potions[0]),
                int(self._shop_potions[1]),
                int(self._shop_potions[2]),
                int(self._shop_costs[SHOP_REMOVE_ACTION]),
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
            "potions": tuple(self._potions),
            "relics": tuple(self._relics),
            "current_node_type": self._current_node_type,
            "card_rewards": tuple(int(card_id) for card_id in self._reward_cards),
            "card_reward_upgraded": tuple(
                bool(value) for value in self._reward_upgraded
            ),
            "shop_cards": tuple(int(card_id) for card_id in self._shop_cards),
            "shop_relics": tuple(int(relic_id) for relic_id in self._shop_relics),
            "shop_potions": tuple(int(potion_id) for potion_id in self._shop_potions),
            "shop_costs": tuple(int(cost) for cost in self._shop_costs),
            "relic_reward": int(self._relic_reward),
            "neow_options": tuple(int(relic_id) for relic_id in self._neow_options),
            "silver_crucible_card_rewards_seen": self._silver_crucible_card_rewards_seen,
            "silver_crucible_treasure_seen": self._silver_crucible_treasure_seen,
            "fishing_rod_combats_seen": self._fishing_rod_combats_seen,
            "winged_boots_times_used": self._winged_boots_times_used,
            "event_id": int(self._event_id),
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
            return int(self._rng.integers(26, 34))
        if self._current_node_type == NODE_BOSS:
            return 75
        return int(self._rng.integers(7, 16))

    def _shop_card_cost(self, card_id: int, *, colorless: bool = False) -> int:
        base_cost = 50 + (card_id % 3) * 25
        if colorless:
            base_cost = int(round(base_cost * 1.15))
        return int(round(base_cost * self._rng.uniform(0.95, 1.05)))

    def _shop_relic_cost(self) -> int:
        return int(round(200 * self._rng.uniform(0.85, 1.15)))

    def _shop_potion_cost(self, action: int) -> int:
        rarity_base = (50, 75, 100)[action % 3]
        return int(round(rarity_base * self._rng.uniform(0.95, 1.05)))

    def _shop_removal_cost(self) -> int:
        return 100

    def _add_potion(self, potion_id: int) -> bool:
        for index, current in enumerate(self._potions):
            if current == 0:
                self._potions[index] = potion_id
                return True
        return False

    def _next_potion(self) -> int:
        return 1 + ((self._seed + self._floor + sum(self._potions)) % 5)

    def _remove_lowest_priority_card(self) -> None:
        for card_id in (10001, 472, 131, 30):
            for index, encoded_card in enumerate(self._deck):
                if abs(encoded_card) == card_id:
                    del self._deck[index]
                    return
        self._deck.pop()

    def _next_relic(self) -> int:
        available = [
            int(relic) for relic in RELIC_REWARD_POOL if int(relic) not in self._relics
        ]
        if not available:
            return int(self._rng.choice(RELIC_REWARD_POOL))
        return int(self._rng.choice(available))

    def _generate_neow_options(self) -> None:
        positive = [int(relic) for relic in self._rng.permutation(NEOW_POSITIVE_RELICS)]
        positive.extend(
            [
                RELIC_LAVA_ROCK if self._rng.integers(0, 2) else RELIC_SMALL_CAPSULE,
                (
                    RELIC_NUTRITIOUS_OYSTER
                    if self._rng.integers(0, 2)
                    else RELIC_STONE_HUMIDIFIER
                ),
                RELIC_NEOWS_TALISMAN if self._rng.integers(0, 2) else RELIC_POMANDER,
            ]
        )
        cursed = int(self._rng.choice(NEOW_CURSE_RELICS))
        if cursed == RELIC_CURSED_PEARL:
            positive = [relic for relic in positive if relic != RELIC_GOLDEN_PEARL]
        elif cursed == RELIC_HEFTY_TABLET:
            positive = [relic for relic in positive if relic != RELIC_ARCANE_SCROLL]
        elif cursed == RELIC_LEAFY_POULTICE:
            positive = [relic for relic in positive if relic != RELIC_NEW_LEAF]
        elif cursed == RELIC_PRECARIOUS_SHEARS:
            positive = [relic for relic in positive if relic != RELIC_PRECISE_SCISSORS]
        self._neow_options[:] = [positive[0], positive[1], cursed]

    def _is_upgradable(self, encoded_card: int) -> bool:
        return encoded_card > 0 and encoded_card in UPGRADABLE_STARTER_CARDS

    def _upgrade_first_card(self) -> bool:
        for index, encoded_card in enumerate(self._deck):
            if self._is_upgradable(encoded_card):
                self._deck[index] = -encoded_card
                return True
        return False

    def _upgrade_last_card_matching(self, card_id: int) -> bool:
        for index in range(len(self._deck) - 1, -1, -1):
            if self._deck[index] == card_id and self._is_upgradable(self._deck[index]):
                self._deck[index] = -card_id
                return True
        return False

    def _upgrade_random_card(self) -> bool:
        indexes = [
            index
            for index, encoded_card in enumerate(self._deck)
            if self._is_upgradable(encoded_card)
        ]
        if not indexes:
            return False
        index = int(self._rng.choice(indexes))
        self._deck[index] = -self._deck[index]
        return True

    def _transform_first_card_matching(self, card_id: int) -> bool:
        for index, encoded_card in enumerate(self._deck):
            if abs(encoded_card) == card_id:
                self._deck[index] = int(self._rng.choice(IRONCLAD_REWARD_POOL))
                return True
        return False

    def _transform_first_card(self) -> None:
        if not self._deck:
            raise ValueError("No card available to transform.")
        self._deck[0] = int(self._rng.choice(IRONCLAD_REWARD_POOL))
