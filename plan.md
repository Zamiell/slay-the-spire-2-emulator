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

Implemented scope already includes starter-deck Ironclad combat, deterministic seeded resets, action masks, single-combat observations, first-combat Act 1 weak encounter sampling for Overgrowth and Underdocks, forced supported encounters for evaluation, a simplified full-run wrapper, card rewards, gold, shops, shop card removal, rest sites, modeled trace-observed events, relic rewards, potion slots, deterministic potion drops and purchases, run deck tracking, upgraded-card encoding, stale native-DLL protection, and validation through `bash lint-and-test.sh`.

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
- Record the first deterministic mismatch with enough context to reproduce it in a unit or integration test.
- Keep curated traces small and purposeful; do not retain large temporary captures unless they document a durable parity target.

### Milestone 2: close the next native card gap

- Select the highest-impact missing trace-observed card from the current curated traces.
- Implement the card in `src\Sts2Emulator\Core` using decompiled game logic for effect order, targeting, exhaust/ethereal behavior, and upgraded values.
- Add C# regression tests in `src\Sts2Emulator.Tests` and update Python expectations only if interop-visible behavior changes.
- Mark this milestone complete when `bash lint-and-test.sh` passes and the selected trace mismatch advances to the next unsupported behavior.

### Milestone 3: make potion and relic rewards decompilation-accurate

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
