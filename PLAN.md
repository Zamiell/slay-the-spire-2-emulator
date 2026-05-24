# Slay the Spire 2 Emulator Plan

## Immediate next steps

1. Continue replacing simplified `Sts2RunEnv` behavior with decompilation-derived rules, preserving deterministic seeds and Gym-compatible observations/actions.
2. Expand native relic coverage and exact timing from decompiled mechanics beyond the currently modeled subset.
3. Capture more seeded full-run traces with varied map routes and stronger autopilot policies to validate decompilation-derived behavior end-to-end.
4. Use trace mismatches to find integration, RNG-order, timing, and state-transition bugs, then re-run combat and full-run validation after each behavior change.

## Completed decompilation-first systems

- Map generation/traversal now uses generated act-map nodes, edges, floor restrictions, and child choices.
- Rewards and shops now use decompilation-inspired combat gold ranges, merchant slot layout, prices, potion rewards, relic rewards, and card removal.
- Rest/event handling now includes rest/smith behavior plus modeled Unrest Site and Aroma of Chaos outcomes.
- Neow now offers three decompilation-derived relic options instead of the old gold/max-HP/skip choices, with solo-disallowed relics filtered out and modeled pickup effects for Golden Pearl, Neow's Torment, Neow's Bones, Nutritious Oyster, Small/Large Capsule, Pomander, Neow's Talisman, Cursed Pearl, Hefty Tablet, Leafy Poultice, Precarious Shears, Silken Tress, Silver Crucible, Arcane Scroll, Lead Paperweight, Lost Coffer, New Leaf, Phial Holster, Precise Scissors, Kaleidoscope, and Scroll Boxes.
- Run-level relic effects now include Amethyst Aubergine, Pantograph, Stone Humidifier, Fishing Rod, Winged Boots, Black Blood, and War Hammer on top of native combat relic effects.
- Act-specific elite and boss encounter pools now use the decompiled Overgrowth and Underdocks encounter sets.
- Gremlin Merc now steals gold after each attack, transfers stolen gold to the spawned Fat Gremlin heist, and returns it when that Fat Gremlin dies.
- The full-run STS2MCP trace collector can proceed past full potion rewards, confirm deck card-selection screens, clamp alternate route choices to available map options, and has captured multiple seeded runs through defeat for validation.

## Known fidelity gaps

- Exact map generation, reward odds, shop inventory/prices, rest options, event routing/outcomes, and potion/relic reward odds are not yet fully implemented from decompiled logic.
- Native relic coverage is incomplete beyond the currently modeled subset.
