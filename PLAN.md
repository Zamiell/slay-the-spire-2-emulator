# Slay the Spire 2 Emulator Plan

## Immediate next steps

1. Use the decompiled game logic as the implementation source of truth for full-run systems: map generation, rewards, shops, rest sites, events, relics, potions, and Gremlin Merc behavior.
2. Replace simplified `Sts2RunEnv` behavior with decompilation-derived rules, preserving deterministic seeds and Gym-compatible observations/actions.
3. Expand native relic coverage and timing from decompiled mechanics.
4. Capture seeded full-run traces with varied map routes and stronger autopilot policies to validate the decompilation-derived emulator end-to-end.
5. Use trace mismatches to find integration, RNG-order, timing, and state-transition bugs, then re-run combat and full-run validation after each behavior change.

## Known fidelity gaps

- Exact map generation, reward odds, shop inventory/prices, rest options, event routing/outcomes, and potion/relic reward odds are not yet fully implemented from decompiled logic.
- Native relic coverage is incomplete beyond the currently modeled subset.
- Exact Gremlin Merc theft timing/recovery remains simplified.
