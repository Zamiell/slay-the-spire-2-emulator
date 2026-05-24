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
    NODE_RELIC,
    NODE_REST,
    NODE_SHOP,
    PHASE_COMBAT,
    PHASE_COMPLETE,
    PHASE_EVENT,
    PHASE_MAP,
    PHASE_NEOW,
    PHASE_RELIC_REWARD,
    PHASE_REST,
    PHASE_SHOP,
    RELIC_ANCHOR,
    RELIC_BLOOD_VIAL,
    RELIC_ORICHALCUM,
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

            _, _, terminated, truncated, info = env.step(3)
            self.assertFalse(terminated)
            self.assertFalse(truncated)
            self.assertEqual(info["phase"], PHASE_COMBAT)

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
            self.assertEqual(info["phase"], PHASE_COMBAT)
            self.assertEqual(info["floor"], 2)
            self.assertEqual(info["deck_size"], 12)
            self.assertIn(int(reward_card), env._deck)
            hand_count = sum(1 for i in HAND_ID_INDICES if int(obs[i]) != 0)
            self.assertEqual(int(obs[5]) + hand_count, 12)
        finally:
            env.close()

    def test_run_env_map_can_route_to_utility_nodes(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._floor = 6
            env._enter_map_phase()

            self.assertEqual(env._phase, PHASE_MAP)
            self.assertEqual(
                [int(node_type) for node_type in env._map_node_types],
                [NODE_REST, NODE_SHOP, 1, 1],
            )

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
            relic_count = len(env._relics)
            env.step(3)

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

            _, _, terminated, _, info = env.step(5)

            self.assertFalse(terminated)
            self.assertLess(info["gold"], 200)
            self.assertGreater(info["potions"][0], 0)
        finally:
            env.close()

    def test_run_env_shop_removes_card(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._gold = 200
            env._enter_shop_phase()
            deck_size = len(env._deck)

            _, _, terminated, _, info = env.step(6)

            self.assertFalse(terminated)
            self.assertEqual(info["deck_size"], deck_size - 1)
            self.assertLess(info["gold"], 200)
            self.assertNotIn(10001, env._deck)
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

            gold_before = info["gold"]
            _, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertGreater(info["gold"], gold_before)
            self.assertEqual(info["event_id"], 0)
        finally:
            env.close()

    def test_run_env_neow_reward_starts_first_combat(self):
        env = Sts2RunEnv(seed=0)
        try:
            _, info = env.reset()
            self.assertEqual(info["phase"], PHASE_NEOW)

            gold_before = info["gold"]
            obs, _, terminated, _, info = env.step(0)

            self.assertFalse(terminated)
            self.assertEqual(info["phase"], PHASE_COMBAT)
            self.assertEqual(info["floor"], 1)
            self.assertEqual(info["gold"], gold_before + 150)
            self.assertGreater(sum(1 for i in HAND_ID_INDICES if int(obs[i]) != 0), 0)
        finally:
            env.close()

    def test_run_env_gremlin_merc_has_special_gold_reward(self):
        env = Sts2RunEnv(seed=0)
        try:
            env.reset()
            env._step_neow(3)
            env._reset_combat(seed=0, encounter_id=7)

            self.assertEqual(env._gold_reward_for_node(), 40)
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
            env._step_neow(3)
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


if __name__ == "__main__":
    unittest.main()
