# Slay the Spire 2 Emulator Plan

## Immediate next steps

1. Capture more seeded full-run traces with varied map routes and stronger autopilot policies so elite, boss, shop purchases, card removal, and Gremlin Merc behavior are observed.
2. Extract live map, reward, shop, rest, event, relic, potion, and Gremlin Merc behavior from those traces.
3. Continue replacing simplified `Sts2RunEnv` full-run systems with live-derived behavior where trace data is authoritative.
4. Expand native relic coverage and timing from captured/decompiled evidence.
5. Re-run combat and full-run validation after each behavior change.

## Known fidelity gaps

- Exact live map generation, reward odds, shop inventory/prices, rest options, event routing/outcomes, and potion/relic reward odds are not yet fully live-derived.
- Native relic coverage is incomplete beyond the currently modeled subset.
- Exact Gremlin Merc theft timing/recovery remains simplified.
