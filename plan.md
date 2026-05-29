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
- Fixed `_init_seed_array` int32 overflow: the second loop now uses `_int32(arr[i] - arr[1+n])` to match C# unchecked int32 arithmetic, eliminating `IndexError` crashes in `shuffle()` for INSTANT_5 and INSTANT_9 traces.
- Implemented decompilation-accurate encounter selection: act is chosen via a separate `DotNetRandom(_int32(run_seed))` matching `BeginRunLocally`'s local Rng, and UpFront RNG is advanced through 202 empirical pre-calls (SharedRelicPool + PlayerRelicPool shuffles + ancient distribution) then 31 (Overgrowth) or 12 (Underdocks) event shuffle calls before GrabBag grabs the 3 weak encounters. Pool ordering matches decompiled GrabBag order: Overgrowth=[FuzzyWurm, Nibbit, ShrinkerBeetle, Slimes], Underdocks=[CorpseSlugs, Seapunk, SludgeSpinner, Toadpoles].
- Fixed combat seed formula: `_combat_seed()` now returns `_int32(_uint32(run_seed + niche_hash))` matching `RunState.Rng.Niche` used by `SetUniqueMonsterHpValue`, verified correct HP for all 5 curated traces (FuzzyWurm=59, Seapunk=47, Nibbit=47, SludgeSpinner=41, Slimes multi-enemy still offset due to emulator-internal RNG mismatch).
- Moved encounter assignment from `_update_map_options` to `_assign_encounter_ids` called once after map generation: Monster nodes are grouped by floor row and all nodes at the same row share the pre-selected weak encounter, preventing per-option consumption of `_weak_encounters`.
- First mismatch for DRUM_1, INSTANT_5, RARITY_1, and CARDS_1 now advances past enemy HP at step 3 to combat flow or post-combat boundary differences; INSTANT_9 retains HP mismatch due to Slimes multi-enemy RNG ordering in the emulator.
- Implemented decompilation-accurate gold and potion reward RNG: `PlayerRngSet` class added to `game_rng.py` with a `rewards` GameRng seeded as `uint(run_seed + NetId)` matching `PlayerRngSet`; `_gold_reward_for_node()` now uses `player_rng.rewards`; `_after_combat_win()` advances rewards RNG in potion-first order (`_check_potion_roll` + `_gold_reward_for_node`); `_NEOW_REWARDS_RNG_ADVANCES` dict pre-advances rewards RNG for relic `AfterObtained()` calls before first combat. DRUM_1 second-combat gold now matches (reference=108 emulator=108).
- Fixed Underdocks weak encounter selection: `_UNDERDOCKS_EVENT_SHUFFLE_CALLS` recalibrated from 12 to 57 against all three Underdocks traces (DRUM_1, TRACE_CARDS_1, INSTANT_10), eliminating the DRUM_1 step-25 enemy-count mismatch.
- Fixed map generation RNG and Overgrowth event shuffle calibration: map generation now uses `GameRng(seed, 'act_1_map')` matching `StandardActMap`'s constructor-level RNG; added `next_gaussian_int` (Box-Muller) and `stable_shuffle` (sort + Fisher-Yates) to `GameRng`; `_OVERGROWTH_EVENT_SHUFFLE_CALLS` recalibrated from 31 to 60 against INSTANT_5 and RARITY_1. INSTANT_5 floor-3 node is now correctly Monster/SlimesWeak (3 enemies) instead of Event. Remaining INSTANT_5 HP differences are the known slimes RNG ordering issue shared with INSTANT_9.
- Fixed SlimesWeak type-selection RNG: `EncounterModel.GenerateMonstersWithSlots` seeds a per-encounter RNG as `uint((int)run_seed + total_floor + GetDeterministicHashCode(entry))` where entry = "SLIMES_WEAK"; `_encounter_rng_seed()` in `run_env.py` computes and passes this seed to `CombatFactory.Reset`; `CreateSlimeEncounter` now uses a separate `typeRng` for small/medium type selection when the seed is provided (natives API bumped to v4). Eliminates the SlimesWeak enemy HP mismatch in INSTANT_5 and INSTANT_9.
- Added native Dark Embrace support: the card now draws a card whenever any card is exhausted, including a deferred draw for Ethereal cards exhausted at end of turn; updated `ExhaustCard` and `EndTurn` to support this behavior and pass `rng` for drawing.
- Fixed player debuff timing: Vulnerable, Weak, and Frail now tick down at the end of the player turn rather than the start, ensuring enemy-applied debuffs correctly affect the player's next turn.
- Fixed Power card cycle: Powers now correctly exhaust after play by default, preventing them from being shuffled back and re-drawn.
- Added native Aggression support: the power now adds a random upgraded Ironclad card to hand at the start of each player turn.
- Added native Hellraiser support: the power now automatically plays any Strike card drawn during the player's turn.
- Fixed card reward RNG: `_generate_card_rewards` now uses `_player_rng.rewards` (DotNetRandom GameRng) instead of numpy for rarity rolls and card selection, matching `CardFactory.CreateForReward` which uses `player.PlayerRng.Rewards.NextItem(items)`. Also added the upgrade roll (`rng.NextFloat()` → `rng.next_double()`) per card as required by `RollForUpgrade` in `CardFactory.CreateForReward`, bringing total per-card Rewards RNG calls from 2 to 3. DRUM_1 first-combat card rewards now match the reference [FightMe, BodySlam, PommelStrike] exactly. Known gap: mid-combat discard reshuffles use a separate fixed shuffle RNG seed in the native code rather than continuing the Python `_run_rng_set.shuffle` GameRng, so subsequent-combat hand ordering diverges when floor-2 has a reshuffle (DRUM_1 floor-3 hand mismatch).
- Fixed RELIC_LOST_COFFER Neow card reward: `_step_neow` now generates 3 card choices via `_player_rng.rewards` (3 calls per card = 9 total matching RegularEncounter + upgrade roll) and transitions to PHASE_CARD_REWARD so the player can select one. Previously `_obtain_relic` directly added a numpy-random card. TRACE_CARDS_1 first-combat mismatch advances from step 22 (floor 2) to step 56+ (floor 5). Remaining gap: slimes encounter HP values at floor 3 differ by 1 per monster (encounter RNG off-by-one).
- Fixed mid-combat discard reshuffle shuffle RNG tracking (natives API bumped to v7→v8): added `CountingRandom` (System.Random subclass counting Next() calls) as `CombatState.ShuffleRng`; exposed `Sts2_GetShuffleRngCallCount` native export; after each combat win `_after_combat_win` advances `_run_rng_set.shuffle` by the extra mid-combat calls so the next combat's initial shuffle uses the correct position in the shared shuffle RNG stream. Also pass `shufflePreSkip` (= current Python shuffle call count before pre-shuffle) to native so mid-combat reshuffles also use the correct position. DRUM_1 floor-3 hand now matches exactly; DRUM_1 boundary mismatch advances from 28 to 7 (event behavior at floor 4).
- Fixed monster HP computation (native API v8): added separate `NicheHpRng` (CountingRandom) seeded from the combat seed (= `RunState.Rng.Niche` seed) and pre-advanced by `_niche_calls_consumed` (total HP calls from prior combats in the run), matching the reference game's continuous `RunState.Rng.Niche` stream. Implemented set-based unique-HP selection (`{minHp..maxHp} minus already-used values`, then `NextInt(0, remaining.Count)`) matching `Creature.SetUniqueMonsterHpValue`. TRACE_CARDS_1 toadpole HP now matches (24, 25); INSTANT_5 slimes HP now matches; both traces advance past the step-23 HP mismatch boundary.
- Normal encounter selection now uses GrabBag from `up_front` Rng (matching ActModel.GenerateRooms grabBag2). Five missing status card definitions (Infection 10008, Burn 10009, Disintegration 10010, Wound 10011, Wither 10012) added to Cards.g.cs to fix DrawCards crash when these appear in the discard pile.
- Investigated map post-processing (PruneDuplicateSegments, CenterGrid, SpreadAdjacentMapPoints, StraightenPaths) as root cause of RARITY_1 map layout mismatch. Pruning removes redundant path segments using mapRng (StableShuffle), which the emulator does not implement. Partial implementations were rejected due to test failures or no improvement. Confirmed from decompiled `StandardActMap.cs` that the constructor calls all four post-processing steps; implementing them requires accurate mapRng tracking which is a future gap.
- Investigated event pre-selection: decompiled `ActModel.GenerateRooms` shuffles `AllEvents.Concat(ModelDb.AllSharedEvents)` using `up_front` Rng. The full event list for an Underdocks run is 58 items (matching empirical 57 shuffle calls = N-1). For DRUM_1, the shuffled list's first position contains DoorsOfLightAndDark, but the exact initial event list order cannot be determined from the decompiled code alone (ModelDb.Acts = [Overgrowth, Hive, Glory, Underdocks] doesn't produce DoorsOfLightAndDark at index 0 in any tested ordering). Event selection remains broken (uses numpy from a small known-event pool).
- Added PHASE_TRANSFORM_SELECT (=9) for card-transform Neow relics (New Leaf): `_step_neow` now transitions to this phase, player selects which deck card to transform via `select_card index=N`, then `confirm_selection` applies the transform using the Niche RNG. Updated replay script to handle these actions. INSTANT_10 no longer crashes at step 2; remaining mismatch from multi-enemy targeting.
- Implemented DoorsOfLightAndDark (Light: StableShuffle upgradable cards with Niche RNG, upgrade first 2; result page before exit) and SunkenTreasury (FirstChest: gain ~60 gold; SecondChest: gain ~333 gold) events. Both added to event pool. Event selection still uses numpy so won't deterministically match reference game without event pre-selection fix.
- Current curated trace mismatch status: DRUM_1 step 49 (floor 4 event behavior - wrong event selected; emulator picks JUNGLE_MAZE_ADVENTURE instead of DOORS_OF_LIGHT_AND_DARK), TRACE_CARDS_1 step 46 (floor 5 encounter type - map layout mismatch), RARITY_1 step 27 (map layout wrong - needs map pruning), INSTANT_5 step 43 (multi-enemy targeting - architectural), INSTANT_9 step 23 (multi-enemy targeting - architectural). Remaining gaps: event pre-selection (unknown initial list order), map pruning (complex), elite/boss encounter selection (uses numpy).
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

## Card Emulation Progress

Current status of cards from the supported character and generic pools (Ironclad, Colorless, Status, Curse).

**Total Progress (Supported Pools): 89/157 (56.7%)**

### Ironclad (77/87)
- [x] Aggression (ID: 9)
- [x] Anger (ID: 13)
- [x] Armaments (ID: 18)
- [x] AscendersBane (ID: 10001)
- [x] AshenStrike (ID: 20)
- [x] Barricade (ID: 29)
- [x] Bash (ID: 30)
- [x] BattleTrance (ID: 31)
- [x] BloodWall (ID: 46)
- [x] Bloodletting (ID: 45)
- [x] Bludgeon (ID: 47)
- [x] BodySlam (ID: 50)
- [x] Brand (ID: 58)
- [x] Break (ID: 59)
- [x] Breakthrough (ID: 60)
- [x] Bully (ID: 66)
- [x] BurningPact (ID: 69)
- [x] Cinder (ID: 87)
- [x] Colossus (ID: 95)
- [x] Conflagration (ID: 99)
- [x] Corruption (ID: 107)
- [x] CrimsonMantle (ID: 113)
- [x] Cruelty (ID: 114)
- [x] DarkEmbrace (ID: 119)
- [x] DefendIronclad (ID: 131)
- [x] DemonForm (ID: 141)
- [x] DemonicShield (ID: 142)
- [x] Dismantle (ID: 147)
- [x] Dominate (ID: 150)
- [x] DrumOfBattle (ID: 155)
- [x] EvilEye (ID: 174)
- [x] ExpectAFight (ID: 175)
- [x] Feed (ID: 183)
- [x] FeelNoPain (ID: 185)
- [x] FiendFire (ID: 188)
- [x] FightMe (ID: 189)
- [x] FlameBarrier (ID: 195)
- [x] ForgottenRitual (ID: 205)
- [x] Havoc (ID: 238)
- [x] Headbutt (ID: 240)
- [x] Hellraiser (ID: 246)
- [x] Hemokinesis (ID: 247)
- [x] HowlFromBeyond (ID: 254)
- [x] Impervious (ID: 261)
- [x] InfernalBlade (ID: 262)
- [x] Inferno (ID: 263)
- [x] Inflame (ID: 265)
- [x] IronWave (ID: 268)
- [x] Juggernaut (ID: 272)
- [x] Juggling (ID: 273)
- [x] Mangle (ID: 295)
- [x] MoltenFist (ID: 313)
- [x] NotYet (ID: 328)
- [x] Offering (ID: 332)
- [x] OneTwoPunch (ID: 334)
- [x] PactsEnd (ID: 339)
- [x] PerfectedStrike (ID: 349)
- [x] Pillage (ID: 353)
- [x] PommelStrike (ID: 358)
- [x] PrimalForce (ID: 364)
- [x] Pyre (ID: 374)
- [x] Rage (ID: 378)
- [x] Rampage (ID: 381)
- [x] Rupture (ID: 404)
- [x] SecondWind (ID: 414)
- [x] SetupStrike (ID: 421)
- [x] ShrugItOff (ID: 433)
- [x] Spite (ID: 454)
- [x] Stampede (ID: 462)
- [x] Stoke (ID: 464)
- [x] Stomp (ID: 465)
- [x] StoneArmor (ID: 466)
- [x] StrikeIronclad (ID: 472)
- [x] SwordBoomerang (ID: 486)
- [x] Tank (ID: 492)
- [x] Taunt (ID: 493)
- [x] TearAsunder (ID: 494)
- [x] Thrash (ID: 505)
- [x] Thunderclap (ID: 508)
- [x] Tremble (ID: 516)
- [x] TrueGrit (ID: 517)
- [x] TwinStrike (ID: 519)
- [x] Unmovable (ID: 525)
- [x] Unrelenting (ID: 526)
- [x] Uppercut (ID: 529)
- [x] Vicious (ID: 533)
- [x] Whirlwind (ID: 538)

### Colorless (11/64)
- [x] Alchemize (ID: 10)
- [x] Anointed (ID: 14)
- [ ] Automation (ID: 23)
- [ ] BeaconOfHope (ID: 32)
- [ ] BeatDown (ID: 34)
- [ ] BelieveInYou (ID: 38)
- [x] Bolas (ID: 51)
- [ ] Calamity (ID: 73)
- [ ] Catastrophe (ID: 80)
- [ ] Coordinate (ID: 105)
- [x] DarkShackles (ID: 121)
- [x] Discovery (ID: 146)
- [x] DramaticEntrance (ID: 153)
- [x] Entropy (ID: 168)
- [ ] Equilibrium (ID: 170)
- [ ] EternalArmor (ID: 173)
- [x] Fasten (ID: 181)
- [x] Finesse (ID: 191)
- [ ] Fisticuffs (ID: 193)
- [x] FlashOfSteel (ID: 197)
- [ ] GangUp (ID: 213)
- [ ] GoldAxe (ID: 225)
- [x] HandOfGreed (ID: 234)
- [ ] HiddenGem (ID: 250)
- [ ] HuddleUp (ID: 255)
- [ ] Impatience (ID: 260)
- [ ] Intercept (ID: 266)
- [ ] JackOfAllTrades (ID: 270)
- [ ] Jackpot (ID: 271)
- [ ] Knockdown (ID: 277)
- [ ] Lift (ID: 286)
- [ ] MasterOfStrategy (ID: 297)
- [ ] Mayhem (ID: 300)
- [ ] Mimic (ID: 306)
- [ ] MindBlast (ID: 307)
- [x] Nostalgia (ID: 327)
- [x] Omnislice (ID: 333)
- [ ] Panache (ID: 342)
- [ ] PanicButton (ID: 343)
- [ ] PrepTime (ID: 363)
- [ ] Production (ID: 365)
- [x] Prolong (ID: 366)
- [ ] Prowess (ID: 369)
- [ ] Purity (ID: 372)
- [ ] Rally (ID: 380)
- [ ] Rend (ID: 394)
- [x] Restlessness (ID: 396)
- [ ] RollingBoulder (ID: 401)
- [x] Salvo (ID: 406)
- [ ] Scrawl (ID: 411)
- [ ] SecretTechnique (ID: 415)
- [ ] SecretWeapon (ID: 416)
- [ ] SeekerStrike (ID: 417)
- [ ] Shockwave (ID: 431)
- [x] Splash (ID: 455)
- [ ] Stratagem (ID: 470)
- [ ] TagTeam (ID: 491)
- [ ] TheBomb (ID: 498)
- [ ] TheGambit (ID: 499)
- [ ] ThinkingAhead (ID: 504)
- [ ] ThrummingHatchet (ID: 506)
- [x] UltimateDefend (ID: 521)
- [ ] UltimateStrike (ID: 522)
- [x] Volley (ID: 535)

### Status/Curse (1/6)
- [x] Beckon (ID: 36)
- [x] Dazed (ID: 10002)
- [x] Debris (ID: 128)
- [x] FranticEscape (ID: 206)
- [x] Slimed (ID: 440)
- [x] Toxic (ID: 512)
