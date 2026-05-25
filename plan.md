# Slay the Spire 2 Emulator Completion Plan

## Goal

Make the emulator complete enough for repeatable reinforcement-learning training and parity-focused evaluation against Slay the Spire 2. Use decompiled game logic as the source of truth for mechanics, and use seeded STS2MCP traces to confirm integration, timing, and RNG ordering.

## Current emulator scope

The repository is split into three main implementation areas:

- `src\Sts2Emulator`: the NativeAOT C# combat engine loaded by Python through `ctypes`.
- `src\Sts2Emulator\Core`: combat state, turn flow, card effects, buffs, enemy AI, potions, relics, and rewards.
- `src\Sts2Emulator\Generated`: generated IDs and data tables for cards, enemies, potions, powers, and relics.
- `src\Sts2Emulator\Interop`: native exports consumed by the Python bindings.
- `src\sts2_gym`: Python bindings plus `Sts2CombatEnv` and the simplified full-run `Sts2RunEnv`.
- `src\Sts2Emulator.Tests` and `tests\python`: regression coverage for native combat and Python environment behavior.
- `scripts`: build, trace capture, real-game validation, data extraction, evaluation, and training helpers.

Implemented scope already includes starter-deck Ironclad combat, deterministic seeded resets, action masks, single-combat observations, first-combat Act 1 weak encounter sampling for Overgrowth and Underdocks, forced supported encounters for evaluation, a simplified full-run wrapper, card rewards, gold, shops, shop card removal, rest sites, modeled trace-observed events, relic rewards, potion slots, decompilation-derived potion reward odds and potion rarity generation, run deck tracking, upgraded-card encoding, stale native-DLL protection, and validation through `bash lint-and-test.sh`.

Native combat currently supports the starter deck plus a growing subset of trace-observed Ironclad cards, enemy powers, potion behavior, and initial relic combat effects. Recent parity work also covers decompilation-derived card reward rarity odds, rarity offset behavior, merchant sale RNG ordering, and rarity-based prices for supported cards.

## Priority gaps

1. **Trace-driven deterministic parity:** compare curated full-run traces against emulator runs at stable floor and combat boundaries, then convert each first mismatch into a focused regression test.
2. **Native card behavior coverage:** implement missing trace-observed cards before broadening to the full card pool. Prioritize cards that appear in curated traces and affect combat decisions, such as Inferno or Colossus from `traces\full-run\FULLRUN_TRACE_CARDS_1.json`.
3. **RNG subsystem fidelity:** replace approximate RNG ordering for potion drops, relic rewards, map generation, event routing, event outcomes, and full-pool generation with decompiled ordering.
4. **Run-level event and reward coverage:** expand events, rewards, shops, relic pools, potion pools, and map traversal beyond the currently modeled trace-observed subset.
5. **Native relic coverage:** implement more relic combat and run-level effects, especially relics that alter starting combat state, rewards, healing, card costs, or potion flow.
6. **Observation and interop completeness:** expose additional state only when needed for policy correctness or parity diagnostics, while preserving existing observation/action compatibility unless a versioned change is documented.
7. **Validation infrastructure:** keep real-game trace capture repeatable, retain only curated traces that document durable milestones or regressions, and ensure native rebuild checks prevent stale rollouts.

## Implementation milestones

### Milestone 1: establish deterministic trace comparison

- Added `scripts\replay_full_run_trace.py` to replay retained STS2MCP full-run traces against `Sts2RunEnv` and report the first floor/combat-boundary mismatch on configured summary fields.
- Added replay diagnostics that print any available boundary mismatches before stopping on an unsupported live trace action, and include the reference step, state type, and floor in unsupported-action errors. Current retained traces now identify reward/card-select divergence with combat-boundary context instead of stopping without the mismatch context.
- Recorded the current first deterministic mismatch with enough context to reproduce it in tests: retained reward/card-select traces diverge from `Sts2RunEnv` because live `rewards` and `card_select` screens are not directly modeled, while `FULLRUN_DRUM_1.json` also surfaces earlier floor/enemy/player combat-boundary differences.
- Added replay coalescing for live reward/event substeps that `Sts2RunEnv` already applies immediately, so traces can advance past redundant `claim_reward`, `proceed`, and delayed Neow proceed actions until a real emulator mismatch remains.
- Keep curated traces small and purposeful; do not retain large temporary captures unless they document a durable parity target.

### Milestone 2: close the next native card gap

- Added native Colossus support: the card now gains block, applies a one-enemy-side counter power, and halves powered attack damage from Vulnerable enemies while active.
- Added native Inferno support: the card now applies its counter power, tracks stacking start-of-turn self-damage, and burns all enemies after unblocked player self-damage during the player turn.
- Added native Breakthrough support: the card now loses 1 player HP before dealing upgraded-aware attack damage to all enemies.
- Added native Cinder support: the card now deals upgraded-aware attack damage, exhausts one random remaining hand card after damage, and exhausts itself through the normal card exhaust path.
- Added native Armaments support: the card now gains block, upgrades the first remaining upgradable hand card for base Armaments, and upgrades all remaining upgradable hand cards when upgraded.
- Added native Pillage support: the card now deals upgraded-aware attack damage, then draws one card at a time until it draws a non-Attack card or reaches the 10-card hand cap.
- Added native One-Two Punch support: the card now applies a counter power that duplicates the next one or two Attack card effects, decrements after each affected Attack, and expires at end of player turn.
- Added native Stomp support: the card now deals upgraded-aware attack damage to all enemies and costs 1 less this turn for each Attack played.
- Added native Molten Fist support: the card now deals upgraded-aware attack damage, then reapplies the surviving target's current Vulnerable stacks.
- Added native Spite support: the card now tracks player HP lost during the current player turn and hits one, two, or three times based on that condition and upgrade state.
- Added native Iron Wave support: the card now gains upgraded-aware block before dealing upgraded-aware attack damage, preserving block-trigger timing.
- Added native Infernal Blade support: the card now generates a random Ironclad Attack into hand, marks it free for the current turn, and upgraded Infernal Blade costs 0.
- Added native Stampede support: the power now auto-plays random playable Attack cards from hand after the next player-turn draw, repeats by stack count, bypasses energy spending, and preserves normal Attack play hooks.
- Added native Dramatic Entrance support from the retained card trace: the colorless card now exhausts and deals upgraded-aware damage to all enemies instead of falling back to first-enemy damage.
- Added native Bolas support from the retained card trace: the colorless card now deals upgraded-aware damage, queues itself after play, and returns to hand before the next player-turn draw.
- Added native Volley support from the retained run traces: the colorless X-cost card now spends all current energy and deals upgraded-aware random-enemy attack hits once per energy.
- Added native Salvo support from the retained run traces: the colorless card now deals upgraded-aware attack damage and retains the remaining hand through the next end-turn discard.
- Added native Neow's Fury support from the retained run traces: the ancient card now deals upgraded-aware attack damage, moves up to two or three cards from discard to hand while respecting the hand cap, and exhausts through the generated card definition.
- Added native Omnislice support from the retained rarity trace: the colorless card now deals upgraded-aware attack damage, then splashes the effective first-hit damage as unpowered damage to the target's other living enemies.
- Added native Forgotten Ritual support from the retained run traces: the card now grants upgraded-aware energy only after a card has already exhausted during the current player turn, then exhausts through the generated card definition.
- Added native Evil Eye support: the card now gains upgraded-aware block once normally, gains it twice if a card already exhausted during the current player turn, and exhausts through the generated card definition.
- Added native Prolong support from the retained run traces: the colorless card now stores the player's current block, grants that amount as unpowered block after the next player-turn block clear, and no longer exhausts when upgraded.
- Refined native Drum of Battle support from the retained run traces: the card now draws on play and grants upgraded-aware energy from its self-exhaust hook, including when another card exhausts it from hand.
- Added native Dark Shackles support from the retained card trace: the colorless card now applies upgraded-aware temporary Strength loss to the target, consumes Artifact as a debuff, exhausts, and restores the enemy's Strength after that enemy turn.
- Added native Nostalgia support from the retained run traces: the power now stacks a counter, makes the first Attack or Skill card played each turn go on top of the draw pile instead of discard, and costs 0 when upgraded.
- Fixed floor tracking: `_floor` now increments when entering a map node via `_step_map` rather than when leaving it via `_advance_after_node`, matching the reference game where the floor counter reflects the node being entered. This eliminates the `run.floor` boundary mismatch across all curated traces and shifts the first visible mismatch to encounter/enemy HP divergence rooted in RNG ordering.
- Added native Stone Armor support: the power now applies upgraded-aware Plating stacks (4 base / 6 upgraded), grants that block at end of each player turn, and decrements Plating by 1 at the start of each player turn.
- Implemented `game_rng.py`: `DotNetRandom` (System.Random legacy Knuth subtractive algorithm), `GameRng` (per-subsystem RNG matching `Rng(uint seed, string name)`), `RunRngSet` (12 named subsystem RNGs from a string seed via deterministic hash), and `neow_rng()` (per-event RNG seeded as `seed + NetId + hash("NEOW")` with `NetId=1` for singleplayer).
- Made `Sts2RunEnv` accept string seeds: `seed: int | str` now constructs a `RunRngSet` from the string, derives the numpy RNG from `run_rng_set.seed` (the uint hash), and derives combat seeds as `run_rng_set.seed + floor - 1`.
- Fixed Neow option generation to match decompiled `Neow.cs` order: curse picked first via `next_int`, positive list filtered by curse pairing, two variable options appended via `next_bool`, then Fisher-Yates shuffle and take-2. MassiveScroll excluded from positive pool as it fails `IsAllowedAtNeow` for Ironclad.
- Updated `replay_full_run_trace.py` to accept `--emulator-seed` as a string and auto-detect it from the trace's top-level `seed` field when not specified. Confirmed FULLRUN_DRUM_1 Neow boundary matches; first remaining mismatch is now enemy HP at step 3 due to encounter selection RNG ordering.
- **Blocker (encounter selection):** fixing encounter parity requires knowing the exact count of UpFront RNG pre-calls before `act.GenerateRooms()`, which depends on relic pool rarity distribution by unlock state and event list counts. Without decompiled unlock-state data, encounter parity cannot be made deterministic.
- Select the highest-impact missing trace-observed card from the current curated traces.
- Implement the card in `src\Sts2Emulator\Core` using decompiled game logic for effect order, targeting, exhaust/ethereal behavior, and upgraded values.
- Add C# regression tests in `src\Sts2Emulator.Tests` and update Python expectations only if interop-visible behavior changes.
- Mark this milestone complete when `bash lint-and-test.sh` passes and the selected trace mismatch advances to the next unsupported behavior.

### Milestone 3: make potion and relic rewards decompilation-accurate

- Added run-level potion reward odds matching decompiled `PotionRewardOdds`: 40% base odds, +/-10% updates, and the elite bonus threshold, plus rarity-based potion generation and shop potion prices for the supported potion pool.
- Replace approximate potion drop and relic reward ordering with decompiled RNG calls.
- Verify deterministic results against a seeded STS2MCP trace.
- Add tests for drop/no-drop boundaries, rarity selection, and run-state updates.

### Milestone 4: expand run-level systems

- Extend event coverage from trace-observed events to the next highest-frequency Act 1 events.
- Improve shop generation for all supported item categories, including relic and potion rarity pools.
- Add map-generation parity once enough run-level RNG subsystems are exact to make map comparisons meaningful.

### Milestone 5: broaden combat and relic coverage

- Continue implementing card, enemy, power, potion, and relic mechanics in trace-priority order.
- Add observation or interop fields only for mechanics that require policy input or validation visibility.
- Keep each mechanic isolated behind focused tests so future parity changes can be reviewed independently.

## Working rules

- Prefer decompiled Slay the Spire 2 logic over inferred behavior.
- For each parity change, add the smallest regression test that proves the modeled timing, values, and state transitions.
- Rebuild native code after C# changes before running Python checks.
- Run `bash lint-and-test.sh` before committing implementation changes.
- Update this plan whenever a milestone is completed or a new durable parity gap is discovered.
