# Slay the Spire 2 Emulator Completion Plan

## Goal

Make the emulator complete enough for repeatable reinforcement-learning training and parity-focused evaluation against Slay the Spire 2. C# is the canonical implementation for deterministic combat and run behavior; Python should remain a `ctypes`/Gymnasium/training wrapper.

## Current status

- Native C# combat supports the starter Ironclad deck plus a growing set of trace-observed cards, enemy powers, potions, relic combat effects, and dense reward shaping.
- Native C# full-run support owns run RNG streams, Ancient/Neow choices, act/map/encounter routing, combat startup/stepping, card/relic/potion rewards, shops, rest sites, modeled events, transform-selection follow-ups, deck/relic/potion/gold state, and run observations/action masks.
- Python `Sts2RunEnv` is a thin wrapper around the native `Sts2Run_*` API.
- Curated full-run traces in `traces\full-run` are retained as parity fixtures. Current failures are content/parity gaps, not Python-vs-C# architecture gaps.
- Run observations and info now ignore stale completed-combat state outside the combat phase, so post-combat relic healing and reward state are visible to replay diagnostics.
- Map generation now uses the decompiled Act 1 map RNG stream (`act_1_map`) rather than deriving the map stream from the act type, and map point stable-shuffle ordering now matches decompiled `(col, row)` ordering. The DRUM trace now follows the reference branch through the event, shop, and rest site.
- Rest sites now heal 30% of max HP and keep a result/confirmation substep before returning to map, matching retained trace structure. Modeled events likewise use the existing result-page substep before advancing.
- Underdocks event routing now uses a shuffled act event sequence for modeled events, so the DRUM trace reaches Doors of Light and Dark before Sunken Treasury. Sunken Treasury chest gold is precomputed from the event-specific deterministic RNG when the event is entered.
- Run-to-combat potion handoff now preserves potion slots instead of clearing the source array before combat startup.
- Reward screens now model pending gold, potion, relic, and card items explicitly; card rewards return to an empty rewards screen before proceeding, and shop card rarity/upgrade rolls consume the Rewards RNG stream.
- The DRUM trace now advances through Gremlin Merc stolen-gold handling, the floor-10 treasure room, Fossil Stalker/SuckPower combat, the floor-12 rest site, and the Corpse Slugs unknown-room combat, and matches all configured boundary fields through terminal death.
- Bash-style damage-then-debuff card effects now keep their original target, preventing dead targets from redirecting Vulnerable to the next live enemy in slime combats.
- Twig Slime (M) and Leaf Slime (S) now use decompiled RandomBranchState logic: TwigSlimeM starts at Sticky Shot (no RNG), then each turn consumes one AI RNG call (forced attack after sticky, 50/50 after single attack, forced sticky after two consecutive attacks); LeafSlimeS initializes with a 50/50 RNG roll and alternates strictly with one RNG call per turn. This fixed per-combat AI RNG alignment so TwigSlimeM round-3 intent matches the reference.
- ShrinkerBeetle now applies permanent Shrink (magnitude=−1, never decremented by TickEndOfTurn), removed on death, matching ShrinkPower.AfterDeath behavior; permanent debuffs (negative magnitude) are skipped in TickEndOfTurn and detected via != 0 in IncomingDamage.
- INSTANT_9 now matches all configured full-run boundary fields. The final HP mismatch was caused by the emulator offering `HowlFromBeyond` where the retained live trace offered adjacent-pool `Hemokinesis`; excluding `HowlFromBeyond` from generated Ironclad rewards/shop attack cards restores the Hemokinesis self-HP-loss path and Burning Blood heal parity.

## Priority gaps

1. **Trace-driven deterministic parity:** replay curated full-run traces, compare stable floor/combat boundaries, and convert each first mismatch into focused C# tests.
2. **Reward and card-selection screens:** reduce remaining live reward/card-select/result/proceed coalescing gaps, especially unsupported reward/card-reward boundaries in retained traces.
3. **Native card behavior coverage:** implement missing trace-observed Ironclad card effects before broadening to the full pool.
4. **Run-level RNG fidelity:** replace remaining simplified map/event/reward/shop odds with decompiled ordering and pools.
5. **Event coverage:** replace the modeled event subset with the full decompiled event table and event-specific screen transitions.
6. **Relic coverage:** expand combat and run-level relic effects, especially relics that affect combat startup, rewards, healing, card costs, potions, or deck mutation.
7. **Observation and interop completeness:** expose additional state only when required for policy correctness or parity diagnostics, and version native API changes.

## Validation expectations

- Run `bash lint-and-test.sh` after code changes.
- Keep NativeAOT publishing and Python native smoke tests passing after C# interop changes.
- Use retained full-run traces as the primary parity gate for run-level work.
- Update this plan when a parity gap is closed or a new durable gap is discovered.
