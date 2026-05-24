import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "src"))

from sts2_gym import Sts2CombatEnv, Sts2RunEnv
from sts2_gym.run_env import (
    NODE_BOSS,
    NODE_ELITE,
    NODE_EVENT,
    NODE_NORMAL,
    NODE_RELIC,
    NODE_REST,
    NODE_SHOP,
    PHASE_CARD_REWARD,
    PHASE_COMBAT,
    PHASE_COMPLETE,
    PHASE_EVENT,
    PHASE_MAP,
    PHASE_NEOW,
    PHASE_RELIC_REWARD,
    PHASE_REST,
    PHASE_SHOP,
    NEOW_CURSE_RELICS,
    NEOW_POSITIVE_RELICS,
    RELIC_ANCHOR,
    RELIC_ARCANE_SCROLL,
    RELIC_BLACK_BLOOD,
    RELIC_BLOOD_VIAL,
    RELIC_CURSED_PEARL,
    RELIC_FISHING_ROD,
    RELIC_KALEIDOSCOPE,
    RELIC_LEAD_PAPERWEIGHT,
    RELIC_LEES_WAFFLE,
    RELIC_LARGE_CAPSULE,
    RELIC_LOST_COFFER,
    RELIC_MANGO,
    RELIC_MEAT_ON_THE_BONE,
    RELIC_MASSIVE_SCROLL,
    RELIC_NEOWS_TALISMAN,
    RELIC_NEW_LEAF,
    RELIC_NUTRITIOUS_OYSTER,
    RELIC_OLD_COIN,
    RELIC_PHIAL_HOLSTER,
    RELIC_PEAR,
    RELIC_PRECISE_SCISSORS,
    RELIC_ORICHALCUM,
    RELIC_SCROLL_BOXES,
    RELIC_SILVER_CRUCIBLE,
    RELIC_SILKEN_TRESS,
    RELIC_STRAWBERRY,
    RELIC_VENERABLE_TEA_SET,
    RELIC_WAR_HAMMER,
    RELIC_WINGED_BOOTS,
    EVENT_SIMPLE_REWARD,
    EVENT_BRAIN_LEECH,
    EVENT_JUNGLE_MAZE_ADVENTURE,
    EVENT_MORPHIC_GROVE,
    EVENT_THE_LEGENDS_WERE_TRUE,
    OVERGROWTH_BOSS_ENCOUNTERS,
    OVERGROWTH_ELITE_ENCOUNTERS,
    SPOILS_MAP_CARD,
    UNDERDOCKS_BOSS_ENCOUNTERS,
    UNDERDOCKS_ELITE_ENCOUNTERS,
)

HAND_ID_INDICES = range(8, 28, 2)
ENEMY_INTENT_INDICES = (47, 87, 127)
ASCENDERS_BANE_OBS_ID = 10001


def first_valid_action(env: Sts2CombatEnv) -> int:
    return int(np.flatnonzero(env.action_masks())[0])


class Sts2GymTests(unittest.TestCase):
    def test_reset_is_deterministic_for_same_seed(self):
        first = Sts2CombatEnv(seed=123)
        second = Sts2CombatEnv(seed=123)
        try:
            first_obs, _ = first.reset()
            second_obs, _ = second.reset()

            self.assertTrue(np.array_equal(first_obs, second_obs))
        finally:
            first.close()
            second.close()

    def test_action_mask_excludes_ascenders_bane(self):
        env = Sts2CombatEnv(seed=0)
        try:
            for seed in range(128):
                obs, _ = env.reset(seed=seed)
                hand_ids = [int(obs[i]) for i in HAND_ID_INDICES]
                if ASCENDERS_BANE_OBS_ID not in hand_ids:
                    continue

                bane_index = hand_ids.index(ASCENDERS_BANE_OBS_ID)
                mask = env.action_masks()

                self.assertFalse(mask[bane_index])
                self.assertTrue(
                    mask[len([card_id for card_id in hand_ids if card_id != 0])]
                )
                return

            self.fail("No tested seed put Ascender's Bane in the opening hand.")
        finally:
            env.close()

    def test_episode_truncates_at_step_cap(self):
        env = Sts2CombatEnv(seed=0, max_episode_steps=1)
        try:
            env.reset()
            _, _, terminated, truncated, _ = env.step(first_valid_action(env))

            self.assertFalse(terminated)
            self.assertTrue(truncated)
        finally:
            env.close()

    def test_info_reports_encounter_identity(self):
        seen = set()
        env = Sts2CombatEnv(seed=0)
        try:
            for seed in range(64):
                _, info = env.reset(seed=seed)
                self.assertIsInstance(info["encounter_id"], int)
                self.assertIsInstance(info["encounter"], str)
                self.assertNotEqual(info["encounter"], "none")
                seen.add(info["encounter"])

            self.assertGreaterEqual(len(seen), 8)
        finally:
            env.close()

    def test_reset_can_force_encounter(self):
        env = Sts2CombatEnv(seed=0, encounter="chompers")
        try:
            _, info = env.reset()
            self.assertEqual(info["encounter"], "chompers")

            _, info = env.reset(options={"encounter": "cultists"})
            self.assertEqual(info["encounter"], "cultists")
        finally:
            env.close()

    def test_enemy_status_move_adds_cards_to_discard(self):
        env = Sts2CombatEnv(seed=0)
        try:
            for seed in range(128):
                obs, info = env.reset(seed=seed)
                if info["encounter"] not in {"chompers", "slimes"}:
                    continue
                if not any(int(obs[i]) == 3 for i in ENEMY_INTENT_INDICES):
                    continue

                end_turn = len(
                    [int(obs[i]) for i in HAND_ID_INDICES if int(obs[i]) != 0]
                )
                obs, _, _, _, _ = env.step(end_turn)

                self.assertGreaterEqual(int(obs[6]), 6)
                return

            self.fail("No tested seed produced an opening enemy status move.")
        finally:
            env.close()

    def test_run_env_reward_pick_tracks_deck_and_advances_floor(self):
        env = Sts2RunEnv(seed=0, max_floors=2)
        try:
            _, info = env.reset()
            self.assertEqual(info["phase"], PHASE_NEOW)
            self.assertEqual(info["deck_size"], 11)

            _, _, terminated, truncated, info = env.step(0)
            self.assertFalse(terminated)
            self.assertFalse(truncated)
            self.assertEqual(info["phase"], PHASE_MAP)

            _, _, terminated, truncated, info = env.step(0)
            self.assertFalse(terminated)
            self.assertFalse(truncated)
            self.assertEqual(info["phase"], PHASE_COMBAT)

            deck_size_before_reward = len(env._deck)
            env._enter_reward_phase()
            mask = env.action_masks()
            self.assertTrue(
                np.array_equal(mask[:4], np.array([True, True, True, True]))
            )
            self.assertFalse(mask[4:].any())

            reward_card = env._reward_cards[0]
            obs, _, terminated, truncated, info = env.step(0)

            self.assertFalse(terminated)
            self.assertFalse(truncated)
            self.assertEqual(info["phase"], PHASE_MAP)
            self.assertEqual(info["floor"], 2)
            self.assertEqual(info["deck_size"], deck_size_before_reward + 1)
            self.assertIn(int(reward_card), [abs(card) for card in env._deck])
            self.assertTrue(info["map_choices"])
        finally:
            env.close()

    def test_run_env_map_can_route_to_utility_nodes(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._phase = PHASE_MAP
            env._map_node_types[:] = [NODE_REST, 0, 0, 0]
            env._map_choices[:] = [0, 0, 0, 0]

            self.assertEqual(env._phase, PHASE_MAP)

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_REST)
        finally:
            env.close()

    def test_run_env_rest_site_heals_or_upgrades(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._phase = PHASE_REST
            env._player_hp = 10

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertGreater(info["player_hp"], 10)

            env._phase = PHASE_REST
            positive_upgrades_before = sum(1 for card in env._deck if card > 0)
            env.step(1)

            self.assertEqual(sum(1 for card in env._deck if card < 0), 1)
            self.assertEqual(
                sum(1 for card in env._deck if card > 0),
                positive_upgrades_before - 1,
            )
        finally:
            env.close()

    def test_run_env_shop_buys_card_or_relic(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._gold = 200
            env._enter_shop_phase()
            card = int(env._shop_cards[0])

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertIn(card, env._deck)
            self.assertLess(info["gold"], 200)

            env._gold = 200
            env._enter_shop_phase()
            env._gold = int(env._shop_costs[7])
            relic_count = len(env._relics)
            env.step(7)

            self.assertEqual(len(env._relics), relic_count + 1)
        finally:
            env.close()

    def test_run_env_shop_buys_potion(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._gold = 200
            env._potions = [0, 0, 0]
            env._enter_shop_phase()

            _, _, terminated, _, info = env.step(10)

            self.assertFalse(terminated)
            self.assertLess(info["gold"], 200)
            self.assertGreater(info["potions"][0], 0)
        finally:
            env.close()

    def test_run_env_shop_uses_decompiled_slot_layout_and_prices(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._enter_shop_phase()

            self.assertIn(
                int(env._shop_cards[0]),
                {13, 50, 60, 87, 147, 247, 268, 358, 454, 486, 508, 519, 538},
            )
            self.assertIn(
                int(env._shop_cards[1]),
                {13, 50, 60, 87, 147, 247, 268, 358, 454, 486, 508, 519, 538},
            )
            self.assertIn(
                int(env._shop_cards[2]),
                {18, 31, 45, 46, 150, 174, 175, 238, 396, 414, 433, 455, 521},
            )
            self.assertIn(
                int(env._shop_cards[3]),
                {18, 31, 45, 46, 150, 174, 175, 238, 396, 414, 433, 455, 521},
            )
            self.assertIn(int(env._shop_cards[4]), {265, 273, 462})

            for action, card_id in enumerate(env._shop_cards):
                base = (
                    75
                    if int(card_id)
                    in {
                        31,
                        147,
                        150,
                        174,
                        175,
                        247,
                        265,
                        273,
                        396,
                        414,
                        454,
                        455,
                        462,
                        521,
                        538,
                    }
                    else 50
                )
                if action >= 5:
                    base = int(base * 1.15 + 0.5)
                cost = int(env._shop_costs[action])
                if action < 5 and cost < base * 0.75:
                    self.assertEqual(cost, int((base * 1.05 + 0.5) // 2))
                else:
                    self.assertIn(
                        cost, range(int(base * 0.95 + 0.5), int(base * 1.05 + 0.5) + 1)
                    )
        finally:
            env.close()

    def test_run_env_shop_removes_card(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._gold = 200
            env._enter_shop_phase()
            deck_size = len(env._deck)

            _, _, terminated, _, info = env.step(13)

            self.assertFalse(terminated)
            self.assertEqual(info["deck_size"], deck_size - 1)
            self.assertLess(info["gold"], 200)
            self.assertNotIn(10001, env._deck)
        finally:
            env.close()

    def test_run_env_shop_removal_cost_increases(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._gold = 300
            env._enter_shop_phase()

            _, _, _, _, info = env.step(13)

            self.assertEqual(info["shop_removals_used"], 1)
            self.assertEqual(env._shop_removal_cost(), 150)
        finally:
            env.close()

    def test_run_env_relic_reward_and_boss_completion(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._current_node_type = NODE_ELITE
            env._enter_relic_reward_phase()
            reward = env._relic_reward

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertIn(reward, info["relics"])

            env._current_node_type = NODE_BOSS
            env._enter_relic_reward_phase()
            _, _, terminated, _, info = env.step(0)

            self.assertTrue(terminated)
            self.assertEqual(info["phase"], PHASE_COMPLETE)
        finally:
            env.close()

    def test_run_env_relic_node_enters_relic_reward_phase(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._phase = PHASE_MAP
            env._map_node_types[:] = [NODE_RELIC, 0, 0, 0]
            env._map_choices[:] = [0, 0, 0, 0]

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_RELIC_REWARD)
        finally:
            env.close()

    def test_run_env_event_node_applies_event_rewards(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._phase = PHASE_MAP
            env._map_node_types[:] = [NODE_EVENT, 0, 0, 0]
            env._map_choices[:] = [0, 0, 0, 0]

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_EVENT)
            self.assertGreater(info["event_id"], 0)
            env._event_id = EVENT_SIMPLE_REWARD

            gold_before = info["gold"]
            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertGreater(info["gold"], gold_before)
            self.assertEqual(info["event_id"], 0)
        finally:
            env.close()

    def test_run_env_trace_observed_events_apply_decompiled_outcomes(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._phase = PHASE_EVENT
            env._event_id = EVENT_JUNGLE_MAZE_ADVENTURE
            env._player_hp = 64
            env._gold = 99

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["player_hp"], 46)
            self.assertGreaterEqual(info["gold"], 234)
            self.assertLessEqual(info["gold"], 264)

            env._phase = PHASE_EVENT
            env._event_id = EVENT_JUNGLE_MAZE_ADVENTURE
            env._gold = 99
            _, _, _, _, info = env.step(1)
            self.assertGreaterEqual(info["gold"], 134)
            self.assertLessEqual(info["gold"], 164)

            env._phase = PHASE_EVENT
            env._event_id = EVENT_MORPHIC_GROVE
            env._gold = 263
            env._player_hp = 20
            env._player_max_hp = 80
            _, _, _, _, info = env.step(1)
            self.assertEqual(info["player_hp"], 25)
            self.assertEqual(info["player_max_hp"], 85)

            env._phase = PHASE_EVENT
            env._event_id = EVENT_MORPHIC_GROVE
            env._gold = 263
            deck_before = tuple(env._deck)
            _, _, _, _, info = env.step(0)
            self.assertEqual(info["gold"], 0)
            self.assertNotEqual(tuple(env._deck[:2]), deck_before[:2])

            env._phase = PHASE_EVENT
            env._event_id = EVENT_BRAIN_LEECH
            env._player_hp = 20
            deck_size = len(env._deck)
            _, _, _, _, info = env.step(0)
            self.assertEqual(info["deck_size"], deck_size + 1)

            env._phase = PHASE_EVENT
            env._event_id = EVENT_BRAIN_LEECH
            env._player_hp = 20
            _, _, terminated, _, info = env.step(1)
            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_CARD_REWARD)
            self.assertEqual(info["player_hp"], 15)
        finally:
            env.close()

    def test_run_env_neow_relic_option_starts_map(self):
        env = Sts2RunEnv(seed=0)
        try:
            _, info = env.reset()
            self.assertEqual(info["phase"], PHASE_NEOW)

            neow_options = info["neow_options"]
            self.assertEqual(len(neow_options), 3)
            self.assertIn(neow_options[0], NEOW_POSITIVE_RELICS)
            self.assertIn(neow_options[1], NEOW_POSITIVE_RELICS)
            self.assertIn(neow_options[2], NEOW_CURSE_RELICS)
            self.assertNotIn(RELIC_MASSIVE_SCROLL, NEOW_POSITIVE_RELICS)
            obs, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_MAP)
            self.assertEqual(info["floor"], 1)
            self.assertIn(neow_options[0], info["relics"])
            self.assertTrue(info["map_choices"])
            self.assertEqual(sum(1 for i in HAND_ID_INDICES if int(obs[i]) != 0), 0)
        finally:
            env.close()

    def test_run_env_neow_pickup_relic_effects(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._player_hp = 64
            env._player_max_hp = 80
            env._gold = 99

            env._obtain_relic(RELIC_NUTRITIOUS_OYSTER)
            self.assertEqual(env._player_max_hp, 91)
            self.assertEqual(env._player_hp, 75)

            deck_size = len(env._deck)
            env._obtain_relic(RELIC_CURSED_PEARL)
            self.assertEqual(env._gold, 432)
            self.assertEqual(len(env._deck), deck_size + 1)

            env._obtain_relic(RELIC_SILKEN_TRESS)
            self.assertEqual(env._gold, 0)
        finally:
            env.close()

    def test_run_env_fruit_and_gold_pickup_relic_effects(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._player_hp = 20
            env._player_max_hp = 80

            env._obtain_relic(RELIC_STRAWBERRY)
            self.assertEqual((env._player_hp, env._player_max_hp), (27, 87))

            env._obtain_relic(RELIC_PEAR)
            self.assertEqual((env._player_hp, env._player_max_hp), (37, 97))

            env._obtain_relic(RELIC_MANGO)
            self.assertEqual((env._player_hp, env._player_max_hp), (51, 111))

            env._obtain_relic(RELIC_LEES_WAFFLE)
            self.assertEqual((env._player_hp, env._player_max_hp), (118, 118))

            gold_before = env._gold
            env._obtain_relic(RELIC_OLD_COIN)
            self.assertEqual(env._gold, gold_before + 300)
        finally:
            env.close()

    def test_run_env_neow_talisman_upgrades_last_basic_cards(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()

            env._obtain_relic(RELIC_NEOWS_TALISMAN)

            self.assertEqual(env._deck.count(-472), 1)
            self.assertEqual(env._deck.count(-131), 1)
        finally:
            env.close()

    def test_run_env_large_capsule_adds_relics_and_basic_cards(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            relic_count = len(env._relics)
            deck_size = len(env._deck)

            env._obtain_relic(RELIC_LARGE_CAPSULE)

            self.assertGreaterEqual(len(env._relics), relic_count + 3)
            self.assertEqual(len(env._deck), deck_size + 2)
            self.assertEqual(env._deck[-2:], [472, 131])
        finally:
            env.close()

    def test_run_env_more_neow_pickup_relic_effects(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            deck_size = len(env._deck)

            env._obtain_relic(RELIC_ARCANE_SCROLL)
            env._obtain_relic(RELIC_LEAD_PAPERWEIGHT)
            self.assertEqual(len(env._deck), deck_size + 2)

            env._obtain_relic(RELIC_PRECISE_SCISSORS)
            self.assertEqual(len(env._deck), deck_size + 1)

            first_card = env._deck[0]
            env._obtain_relic(RELIC_NEW_LEAF)
            self.assertNotEqual(env._deck[0], first_card)

            env._potions = [0, 0, 0]
            env._obtain_relic(RELIC_PHIAL_HOLSTER)
            self.assertEqual(sum(1 for potion in env._potions if potion != 0), 2)

            deck_size = len(env._deck)
            env._potions = [0, 0, 0]
            env._obtain_relic(RELIC_LOST_COFFER)
            self.assertEqual(len(env._deck), deck_size + 1)
            self.assertTrue(any(potion != 0 for potion in env._potions))

            deck_size = len(env._deck)
            env._obtain_relic(RELIC_KALEIDOSCOPE)
            self.assertEqual(len(env._deck), deck_size + 2)

            deck_size = len(env._deck)
            env._obtain_relic(RELIC_SCROLL_BOXES)
            self.assertEqual(len(env._deck), deck_size + 3)
        finally:
            env.close()

    def test_run_env_winged_boots_allows_three_free_travel_nodes(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._relics.append(RELIC_WINGED_BOOTS)
            env._phase = PHASE_MAP
            start = env._map_nodes[env._current_map_coord]
            start.children = {(1, 1)}
            child = env._get_or_create_map_node(1, 1)
            child.node_type = "Monster"
            winged_only = env._get_or_create_map_node(2, 1)
            winged_only.node_type = "Monster"

            env._enter_map_phase()

            self.assertIn((2, 1), env._map_option_coords)
            winged_index = env._map_option_coords.index((2, 1))
            _, _, terminated, _, info = env.step(winged_index)

            self.assertFalse(terminated)
            self.assertEqual(info["winged_boots_times_used"], 1)
        finally:
            env.close()

    def test_run_env_silver_crucible_upgrades_first_three_card_rewards(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._relics.append(RELIC_SILVER_CRUCIBLE)

            for seen in range(1, 4):
                env._enter_reward_phase()
                self.assertEqual(env._silver_crucible_card_rewards_seen, seen)
                self.assertTrue(env._reward_upgraded.all())
                card_id = int(env._reward_cards[0])
                env.step(0)
                self.assertIn(-card_id, env._deck)

            env._enter_reward_phase()
            self.assertFalse(env._reward_upgraded.any())
        finally:
            env.close()

    def test_run_env_silver_crucible_first_treasure_is_empty(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._relics.append(RELIC_SILVER_CRUCIBLE)
            env._phase = PHASE_MAP
            env._map_node_types[:] = [NODE_RELIC, 0, 0, 0]
            env._map_choices[:] = [0, 0, 0, 0]
            relic_count = len(env._relics)

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_MAP)
            self.assertEqual(info["silver_crucible_treasure_seen"], 1)
            self.assertEqual(len(env._relics), relic_count)
        finally:
            env.close()

    def test_run_env_act_specific_elite_and_boss_encounter_pools(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._act = "overgrowth"
            self.assertIn(
                env._encounter_for_node(NODE_ELITE), OVERGROWTH_ELITE_ENCOUNTERS
            )
            self.assertIn(
                env._encounter_for_node(NODE_BOSS), OVERGROWTH_BOSS_ENCOUNTERS
            )

            env._act = "underdocks"
            self.assertIn(
                env._encounter_for_node(NODE_ELITE), UNDERDOCKS_ELITE_ENCOUNTERS
            )
            self.assertIn(
                env._encounter_for_node(NODE_BOSS), UNDERDOCKS_BOSS_ENCOUNTERS
            )
        finally:
            env.close()

    def test_run_env_fishing_rod_upgrades_every_third_normal_combat(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._relics.append(RELIC_FISHING_ROD)
            env._current_node_type = NODE_NORMAL

            for expected_seen in range(1, 3):
                env._after_combat_win()
                self.assertEqual(env._fishing_rod_combats_seen, expected_seen)
                self.assertFalse(any(card < 0 for card in env._deck))

            env._after_combat_win()

            self.assertEqual(env._fishing_rod_combats_seen, 3)
            self.assertTrue(any(card < 0 for card in env._deck))
        finally:
            env.close()

    def test_run_env_after_combat_relics_heal_and_upgrade(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._relics.append(RELIC_BLACK_BLOOD)
            env._relics.append(RELIC_WAR_HAMMER)
            env._player_hp = 40
            env._player_max_hp = 80
            env._current_node_type = NODE_ELITE

            env._after_combat_win()

            self.assertEqual(env._player_hp, 58)
            self.assertEqual(sum(1 for card in env._deck if card < 0), 4)
        finally:
            env.close()

    def test_run_env_meat_on_the_bone_heals_after_low_hp_combat(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._relics = [RELIC_MEAT_ON_THE_BONE]
            env._player_hp = 30
            env._player_max_hp = 80
            env._current_node_type = NODE_NORMAL

            env._after_combat_win()

            self.assertEqual(env._player_hp, 42)
        finally:
            env.close()

    def test_run_env_venerable_tea_set_activates_after_rest(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._relics.append(RELIC_VENERABLE_TEA_SET)
            env._phase = PHASE_REST
            env._player_hp = 20

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertTrue(info["venerable_tea_set_active"])

            env._reset_combat(seed=0, encounter_id=1)
            obs = env._obs()

            self.assertEqual(int(obs[3]), 5)
            self.assertFalse(env._info()["venerable_tea_set_active"])
        finally:
            env.close()

    def test_run_env_normal_gold_uses_max_ascension_range(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._step_neow(0)
            env._reset_combat(seed=0, encounter_id=7)

            self.assertIn(env._gold_reward_for_node(), range(7, 16))
        finally:
            env.close()

    def test_run_env_gremlin_merc_steals_gold_during_combat(self):
        env = Sts2RunEnv(seed=0)
        try:
            obs, _ = env.reset()
            env._gold = 99
            env._phase = PHASE_COMBAT
            env._reset_combat(seed=0, encounter_id=7)
            obs = env._obs()
            end_turn = sum(1 for i in HAND_ID_INDICES if int(obs[i]) != 0)

            _, _, _, _, info = env.step(end_turn)

            self.assertEqual(info["gold"], 79)
        finally:
            env.close()

    def test_run_env_passes_relics_to_native_combat(self):
        env = Sts2RunEnv(seed=0)
        try:
            obs, _ = env.reset()
            self.assertEqual(int(obs[2]), 0)

            env._relics.append(RELIC_ANCHOR)
            env._reset_combat(seed=0, encounter_id=1)
            obs = env._obs()

            self.assertEqual(int(obs[2]), 10)
        finally:
            env.close()

    def test_run_env_passes_current_hp_to_native_combat(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._player_hp = 37
            env._reset_combat(seed=0, encounter_id=1)
            obs = env._obs()

            self.assertEqual(int(obs[0]), 37)
            self.assertEqual(int(obs[1]), 80)
        finally:
            env.close()

    def test_run_env_passes_potions_to_native_combat(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._step_neow(0)
            env._potions = [1, 0, 2]
            env._reset_combat(seed=0, encounter_id=1)
            obs = env._obs()

            self.assertEqual([int(obs[28]), int(obs[30]), int(obs[32])], [1, 0, 2])
            self.assertEqual(env._info()["potions"], (1, 0, 2))
        finally:
            env.close()

    def test_run_env_periodically_awards_combat_potions(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._floor = 3
            env._potions = [0, 0, 0]
            env._after_combat_win()

            self.assertGreater(env._potions[0], 0)
        finally:
            env.close()

    def test_run_env_legends_were_true_nab_map_adds_quest_card(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._phase = PHASE_EVENT
            env._event_id = EVENT_THE_LEGENDS_WERE_TRUE
            deck_size = len(env._deck)

            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_MAP)
            self.assertEqual(info["deck_size"], deck_size + 1)
            self.assertIn(SPOILS_MAP_CARD, env._deck)

            env._phase = PHASE_COMBAT
            env._reset_combat(seed=0, encounter_id=1)
            self.assertNotIn(SPOILS_MAP_CARD, env._combat_deck())
        finally:
            env.close()

    def test_run_env_legends_were_true_slow_exit_loses_hp_for_potion(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._phase = PHASE_EVENT
            env._event_id = EVENT_THE_LEGENDS_WERE_TRUE
            env._player_hp = 20
            env._potions = [0, 0, 0]

            _, _, terminated, _, info = env.step(1)

            self.assertFalse(terminated)
            self.assertEqual(info["player_hp"], 12)
            self.assertTrue(any(potion != 0 for potion in info["potions"]))
        finally:
            env.close()


if __name__ == "__main__":
    unittest.main()
