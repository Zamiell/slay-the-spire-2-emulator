# Slay the Spire 2 Emulator Plan

## Current focus

Use the decompiled game logic as the implementation source of truth, then validate integration and timing with seeded STS2MCP full-run traces.

## Current status

Major parity infrastructure is in place: run-env traversal/rewards/shops/rest/events, trace collection, native combat coverage for the current subset, stale native-DLL protection, and validation via `bash lint-and-test.sh`. Use the README and git history for durable completed-capability details.

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
