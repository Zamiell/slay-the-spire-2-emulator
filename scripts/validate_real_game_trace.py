"""Capture and compare a live STS2MCP combat trace against the emulator."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from sts2_gym import Sts2CombatEnv

import compare_traces
import start_real_game_run
import trace as emulator_trace
import trace_real_game

CARD_IDS = {
    "ASCENDERS_BANE": 10001,
    "BASH": 30,
    "DEFEND_IRONCLAD": 131,
    "STRIKE_IRONCLAD": 472,
}

ENCOUNTER_BY_ENEMY = {
    ("Corpse Slug", "Corpse Slug"): "corpse-slugs",
    ("Fuzzy Wurm Crawler",): "fuzzy-wurm-crawler",
    ("Nibbit",): "nibbit",
    ("Seapunk",): "seapunk",
    ("Shrinker Beetle",): "shrinker-beetle",
    ("Sludge Spinner",): "sludge-spinner",
    ("Toadpole", "Toadpole"): "toadpoles",
}


def enemy_names(summary: dict[str, Any]) -> tuple[str, ...]:
    return tuple(enemy.get("name") or "" for enemy in summary.get("enemies") or [])


def detect_encounter(summary: dict[str, Any]) -> str:
    names = enemy_names(summary)
    if names in ENCOUNTER_BY_ENEMY:
        return ENCOUNTER_BY_ENEMY[names]
    if names and all("Slime" in name for name in names):
        return "slimes"
    raise ValueError(f"Could not map live enemies to an encounter: {names!r}")


def live_hand_ids(summary: dict[str, Any]) -> list[int]:
    hand = []
    for card in (summary.get("player") or {}).get("hand") or []:
        card_id = CARD_IDS.get(str(card.get("id") or ""))
        if card_id is None:
            raise ValueError(
                f"Live hand contains unsupported card {card.get('id')!r}; "
                "choose a Neow option that preserves the starter deck."
            )
        hand.append(card_id)
    return hand


def validate_starter_player(summary: dict[str, Any]) -> None:
    player = summary.get("player") or {}
    hp = player.get("hp")
    max_hp = player.get("max_hp")
    if (hp, max_hp) != (64, 80):
        raise ValueError(
            f"Live run has {hp}/{max_hp} HP, expected max-ascension starter "
            "64/80. Choose a Neow option that does not change HP."
        )
    piles = (
        player.get("draw_pile_count"),
        player.get("discard_pile_count"),
        player.get("exhaust_pile_count"),
    )
    if piles != (6, 0, 0):
        raise ValueError(
            f"Live run has pile counts draw/discard/exhaust={piles}, expected "
            "starter deck counts (6, 0, 0). Choose a Neow option that does not "
            "add, remove, transform, or create cards."
        )


def emulator_initial_summary(seed: int, encounter: str) -> dict[str, Any]:
    env = Sts2CombatEnv(seed=seed, encounter=encounter)
    try:
        obs, _ = env.reset()
        return emulator_trace.summarize_observation(obs)
    finally:
        env.close()


def initial_matches(
    live_summary: dict[str, Any],
    emulator_summary: dict[str, Any],
    match_hand: bool,
    ignore_hand_order: bool,
) -> bool:
    if match_hand:
        live_hand = live_hand_ids(live_summary)
        emulator_hand = [
            int(card["id"])
            for card in (emulator_summary.get("player") or {}).get("hand") or []
        ]
        if ignore_hand_order:
            live_hand = sorted(live_hand)
            emulator_hand = sorted(emulator_hand)
        if live_hand != emulator_hand:
            return False

    live_enemies = live_summary.get("enemies") or []
    emulator_enemies = emulator_summary.get("enemies") or []
    if len(live_enemies) != len(emulator_enemies):
        return False

    for live_enemy, emulator_enemy in zip(live_enemies, emulator_enemies):
        if live_enemy.get("hp") != emulator_enemy.get("hp"):
            return False
        if live_enemy.get("max_hp") != emulator_enemy.get("max_hp"):
            return False
        live_intent = live_enemy_intent(live_enemy)
        if live_intent is not None:
            emulator_intent = (
                emulator_enemy.get("intent_type"),
                emulator_enemy.get("intent_magnitude"),
            )
            if live_intent[0] != emulator_intent[0]:
                return False
            if live_intent[1] is not None and live_intent[1] != emulator_intent[1]:
                return False

    return True


def live_enemy_intent(enemy: dict[str, Any]) -> tuple[int, int | None] | None:
    intents = enemy.get("intents") or []
    if not intents:
        return None
    if (
        enemy.get("name") == "Sludge Spinner"
        and any(intent.get("type") == "Attack" for intent in intents)
        and any(intent.get("type") == "Debuff" for intent in intents)
    ):
        attack = next(intent for intent in intents if intent.get("type") == "Attack")
        return 3, live_intent_magnitude(attack)

    intent = intents[0]
    intent_type = intent.get("type")
    intent_by_type = {
        "Attack": 0,
        "Block": 1,
        "Defend": 1,
        "Buff": 2,
        "Debuff": 3,
        "StatusCard": 3,
    }
    if intent_type not in intent_by_type:
        return None
    return intent_by_type[intent_type], live_intent_magnitude(intent)


def live_intent_magnitude(intent: dict[str, Any]) -> int | None:
    raw_label = intent.get("label")
    if raw_label in (None, ""):
        return None
    label = str(raw_label)
    if "x" in label:
        damage, repeats = label.split("x", 1)
        return int(damage) * int(repeats)
    return int(label)


def format_opening_summary(summary: dict[str, Any]) -> str:
    player = summary.get("player") or {}
    enemies = summary.get("enemies") or []
    hand = [
        str(card.get("id") or card.get("name") or "?")
        for card in player.get("hand") or []
    ]
    enemy_parts = [
        f"{enemy.get('name')} {enemy.get('hp')}/{enemy.get('max_hp')} "
        f"{enemy.get('intents') or ''}"
        for enemy in enemies
    ]
    return (
        f"hand={hand}; enemies={enemy_parts}; "
        f"player={player.get('hp')}/{player.get('max_hp')}"
    )


def find_matching_seed(
    live_summary: dict[str, Any],
    encounter: str,
    limit: int,
    match_hand: bool,
    ignore_hand_order: bool,
) -> int:
    for seed in range(limit):
        if initial_matches(
            live_summary,
            emulator_initial_summary(seed, encounter),
            match_hand,
            ignore_hand_order,
        ):
            return seed
    raise ValueError(
        f"No emulator seed below {limit} matched live {encounter} opening state: "
        f"{format_opening_summary(live_summary)}"
    )


def capture_emulator_trace(
    seed: int, encounter: str, actions: list[int]
) -> dict[str, Any]:
    env = Sts2CombatEnv(seed=seed, encounter=encounter)
    try:
        obs, info = env.reset()
        trace = [
            {
                "step": 0,
                "action": None,
                "reward": 0.0,
                "terminated": False,
                "truncated": False,
                "valid_actions": emulator_trace.valid_actions(env),
                "observation": obs.tolist(),
                "summary": emulator_trace.summarize_observation(obs),
                "info": info,
            }
        ]
        for step, action in enumerate(actions, start=1):
            obs, reward, terminated, truncated, info = env.step(action)
            trace.append(
                {
                    "step": step,
                    "action": action,
                    "reward": reward,
                    "terminated": terminated,
                    "truncated": truncated,
                    "valid_actions": (
                        emulator_trace.valid_actions(env)
                        if not (terminated or truncated)
                        else []
                    ),
                    "observation": obs.tolist(),
                    "summary": emulator_trace.summarize_observation(obs),
                    "info": info,
                }
            )
            if terminated or truncated:
                break
        return {"seed": seed, "encounter": encounter, "trace": trace}
    finally:
        env.close()


def capture_live_trace(
    base_url: str, actions: list[int], delay: float
) -> dict[str, Any]:
    state = trace_real_game.get_state(base_url)
    trace = [
        {
            "step": 0,
            "action": None,
            "post": None,
            "summary": trace_real_game.summarize_state(state),
            "raw_state": state,
        }
    ]
    for step, action in enumerate(actions, start=1):
        payload = trace_real_game.action_payload_from_index(state, action)
        post_result = trace_real_game.post_action(base_url, payload)
        state = trace_real_game.wait_for_state(
            base_url,
            delay,
            wait_for_play_phase=payload.get("action") == "end_turn",
        )
        trace.append(
            {
                "step": step,
                "action": action,
                "post": payload,
                "post_result": post_result,
                "summary": trace_real_game.summarize_state(state),
                "raw_state": state,
            }
        )
    return {"source": "sts2mcp", "base_url": base_url, "trace": trace}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default=trace_real_game.DEFAULT_BASE_URL)
    parser.add_argument("--actions", type=int, nargs="*", default=[])
    parser.add_argument("--delay", type=float, default=0.25)
    parser.add_argument("--seed-search-limit", type=int, default=500000)
    parser.add_argument(
        "--ignore-hand",
        action="store_true",
        help="Match only encounter/enemy HP; useful for end-turn-only enemy behavior checks",
    )
    parser.add_argument(
        "--ignore-hand-order",
        action="store_true",
        help="Match opening hand as an unordered multiset",
    )
    parser.add_argument("--output-dir", type=Path, default=None)
    parser.add_argument("--start-seed", default=None)
    parser.add_argument("--character", default="IRONCLAD")
    parser.add_argument("--map-index", type=int, default=0)
    parser.add_argument("--neow-option", type=int, default=-1)
    parser.add_argument("--max-diffs", type=int, default=40)
    args = parser.parse_args()

    if args.start_seed is not None:
        start_real_game_run.start_seeded_run(
            args.base_url,
            args.start_seed,
            args.character,
            abandon_existing=True,
        )
        start_real_game_run.enter_first_combat(
            args.base_url,
            args.neow_option,
            args.map_index,
        )

    live = capture_live_trace(args.base_url, args.actions, args.delay)
    live_summary = compare_traces.summary(live["trace"][0])
    validate_starter_player(live_summary)
    encounter = detect_encounter(live_summary)
    if args.output_dir is not None:
        args.output_dir.mkdir(parents=True, exist_ok=True)
        prefix = f"{args.start_seed or 'current'}-{encounter}"
        (args.output_dir / f"{prefix}-real.json").write_text(
            json.dumps(live, indent=2),
            encoding="utf-8",
        )
    emulator_seed = find_matching_seed(
        live_summary,
        encounter,
        args.seed_search_limit,
        match_hand=not args.ignore_hand,
        ignore_hand_order=args.ignore_hand_order,
    )
    emulator = capture_emulator_trace(emulator_seed, encounter, args.actions)
    diffs = compare_traces.compare(
        compare_traces.load_trace_from_payload(emulator),
        compare_traces.load_trace_from_payload(live),
        compare_traces.DEFAULT_FIELDS,
    )

    if args.output_dir is not None:
        (args.output_dir / f"{prefix}-emulator.json").write_text(
            json.dumps(emulator, indent=2),
            encoding="utf-8",
        )

    print(
        json.dumps(
            {
                "encounter": encounter,
                "emulator_seed": emulator_seed,
                "actions": args.actions,
                "diffs": diffs[: args.max_diffs],
                "diff_count": len(diffs),
            },
            indent=2,
        )
    )
    if diffs:
        raise SystemExit(1)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"validate_real_game_trace.py: {exc}", file=sys.stderr)
        raise SystemExit(1) from exc
