# Slay the Spire 2 Emulator Plan

## Current focus

Use the decompiled game logic as the implementation source of truth, then validate integration and timing with seeded STS2MCP full-run traces.

## Current status

Major parity infrastructure is in place: run-env traversal/rewards/shops/rest/events, trace collection, native combat coverage for the current subset, stale native-DLL protection, and validation via `bash lint-and-test.sh`. Reward and shop card generation now use decompiled rarity odds, rarity offset behavior, merchant sale RNG ordering, and rarity-based prices for the supported card subset. Native combat also models trace-observed Vicious, Drum of Battle, and Fight Me behavior, and the run-env pools include the newly observed supported Ironclad cards from the latest STS2MCP full-run captures. Use the README and git history for durable completed-capability details.

## Remaining fidelity gaps

- Exact RNG ordering for map generation, event routing/outcomes, potion drops, relic drops, and full-pool card/relic generation is still approximate.
- Event coverage is still partial beyond the modeled initial and trace-observed event subset.
- Native card coverage is still partial outside the current starter/common/trace-observed subset.
- Native relic coverage is still partial outside the modeled run/combat relic subset.
- Shop inventory and price generation are decompilation-inspired and rarity-aware for supported cards, but not yet exact across colorless-only cards, all item categories, and relic/potion rarity pools.

## Next concrete work items

1. Compare `traces\full-run\FULLRUN_TRACE_CARDS_1.json` against emulator runs at each floor boundary and turn the first deterministic mismatch into a focused test.
2. Expand the next trace-observed missing card behavior from `FULLRUN_TRACE_CARDS_1` using decompiled behavior, prioritizing Inferno or Colossus because they are now visible in the latest capture and are not yet modeled natively.
3. Replace the next approximate RNG subsystem, prioritizing potion drops or relic rewards because card reward/shop rarity ordering is now covered for supported cards.
4. Capture another live trace after each parity batch; keep it only if it documents a durable milestone or regression case, and update this plan with durable progress plus the next actionable gap.
