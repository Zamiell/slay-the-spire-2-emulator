# Slay the Spire 2 Emulator Plan

## Current focus

Use the decompiled game logic as the implementation source of truth, then validate integration and timing with seeded STS2MCP full-run traces.

## Completed parity work

- **Full-run trace collector**: Handles potion rewards, rest sites, bundle-selection overlays, card-selection confirmation, unready shops, map-option clamping, transient shop/rest timing races, and survival-aware combat/route heuristics. Curated live captures are kept only when they document durable parity milestones; the latest retained trace is `traces/full-run/FULLRUN_INSTANT_10.json`, which reached floor 7 and ended in a normal game-over state.
- **Run environment systems**: `Sts2RunEnv` now models act-map nodes/edges/restrictions, combat/map traversal, combat gold, card rewards, relic rewards, potion drops, shops, sale slots, card removal scaling, rest/smith actions, and modeled Unrest Site, Aroma of Chaos, Jungle Maze Adventure, Morphic Grove, Brain Leech, and The Legends Were True outcomes.
- **Encounter pools**: Act-specific Overgrowth and Underdocks weak/normal/elite/boss encounter pools are wired into run generation.
- **Neow**: Offers three decompilation-derived relic options with solo-disallowed filtering and modeled pickup effects for the currently supported Neow relic subset.
- **Trace-observed cards**: Run rewards/shops include live-observed cards including Anger, Armaments, Battle Trance, Bloodletting, Blood Wall, Body Slam, Breakthrough, Cinder, Dismantle, Dominate, Evil Eye, Havoc, Hemokinesis, Iron Wave, Pommel Strike, Restlessness, Second Wind, Shrug It Off, Spite, Splash, Stampede, Sword Boomerang, Ultimate Defend, Whirlwind, Expect a Fight, and Juggling. Native combat effects cover the currently modeled subset, with default damage/block behavior for simpler cards.
- **Run-level relic effects**: Modeled Amethyst Aubergine, Pantograph, Stone Humidifier, Fishing Rod, Winged Boots, Black Blood, War Hammer, Meat on the Bone, Old Coin, Strawberry, Pear, Mango, Lee's Waffle, and Venerable Tea Set activation.
- **Native combat relic effects**: Modeled Anchor, Bag of Marbles, Bag of Preparation, Blood Vial, Booming Conch, Bronze Scales, Captain's Wheel, Happy Flower, Horn Cleat, Lantern, Oddly Smooth Stone, Orichalcum, Red Skull, Vajra, and Venerable Tea Set next-combat energy.
- **Gremlin Merc**: Steals gold after attacks, transfers stolen gold to the spawned Fat Gremlin heist, and returns it when that Fat Gremlin dies.
- **Native build freshness**: Python bindings now prefer `STS2_LIB_PATH` when set and reject stale native libraries that are older than the C# source tree or missing the required native API version export, preventing run-env validation from silently using old `out` DLLs.
- **Bounded evaluation**: `scripts\evaluate.py` accepts `--max-episode-steps`, so run-env smoke checks can be made explicitly short instead of appearing to hang behind the default 1000-step cap.
- **Native encounter IDs**: Run-env elite/boss pools now use native `ActOneEncounter` IDs instead of generated enemy/model IDs, preventing run resets from passing out-of-range encounter values to the NativeAOT DLL.

## Remaining fidelity gaps

- Exact RNG ordering for map generation, rewards, shops, event routing/outcomes, potion drops, relic drops, and card reward generation is still approximate.
- Event coverage is still partial beyond the modeled initial and trace-observed event subset.
- Native card coverage is still partial outside the current starter/common/trace-observed subset.
- Native relic coverage is still partial outside the modeled run/combat relic subset.
- Shop inventory and price generation are decompilation-inspired but not yet exact across all item categories and relic/card rarities.

## Next concrete work items

1. Compare latest live traces against emulator runs at each floor boundary and turn the first deterministic mismatch into a focused test.
2. Expand the next trace-observed missing card or relic using decompiled behavior, then add native/run-env regression tests.
3. Replace one approximate RNG subsystem at a time, starting with reward generation or shop inventory because those are directly visible in full-run traces.
4. Capture another live trace after each parity batch; keep it only if it documents a durable milestone or regression case, and update this plan with durable progress plus the next actionable gap.
