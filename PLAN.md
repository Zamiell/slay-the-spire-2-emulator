# Slay the Spire 2 Emulator Plan

## Goal

Build a high-performance STS2 combat emulator for reinforcement learning: C# game logic compiled with .NET NativeAOT, loaded from Python through `ctypes`, and exposed as Gymnasium environments.

## Current status

- NativeAOT C# combat engine, Python bindings, `Sts2CombatEnv`, and experimental `Sts2RunEnv` are working.
- The emulator targets **max ascension** as the baseline:
  - Ironclad starts at 64/80 HP.
  - Enemy HP ranges are generated from max-ascension `ToughEnemies` values.
  - Deadly enemy move values are modeled for supported enemies.
- Act 1 coverage includes Overgrowth and Underdocks weak/normal encounter pools, many enemy powers, status-card mechanics, and deterministic forced encounter evaluation.
- STS2MCP live validation is working locally with seeded standard-run starts through `scripts\start_real_game_run.py`.
- Recent live traces validated/fixed Toadpoles, Shrinker Beetle, Corpse Slugs, Seapunk, and Fuzzy Wurm Crawler on normalized combat fields.

## Build and test

```powershell
dotnet publish "src\Sts2Emulator\Sts2Emulator.csproj" -c Release -r win-x64 --self-contained -o "out"
dotnet test "src\Sts2Emulator.Tests\Sts2Emulator.Tests.csproj"
.\.venv\Scripts\python.exe scripts\train.py --check
.\.venv\Scripts\python.exe scripts\evaluate.py --episodes 100 --policy starter-aggressive
```

Regenerate data from decompiled sources after a patch:

```powershell
.\.venv\Scripts\python.exe scripts\extract_data.py
```

## Trace validation workflow

1. Start STS2 through Steam, not by launching the executable directly.
2. Ensure the patched STS2MCP mod is installed and reachable at `http://localhost:15526`.
3. Start a seeded real-game run and enter first combat:

```powershell
.\.venv\Scripts\python.exe scripts\start_real_game_run.py VALIDATION_SEED --character IRONCLAD --abandon-existing --enter-first-combat
```

4. Capture the live trace:

```powershell
.\.venv\Scripts\python.exe scripts\trace_real_game.py --actions 0 1 2 > real.json
```

5. Capture the emulator trace with a matching forced encounter and action sequence:

```powershell
.\.venv\Scripts\python.exe scripts\trace.py --seed 0 --encounter toadpoles --actions 0 1 2 > emulator.json
```

6. Compare normalized fields:

```powershell
.\.venv\Scripts\python.exe scripts\compare_traces.py emulator.json real.json
```

## Immediate next steps

1. Continue seeded live trace validation across the remaining Act 1 weak encounters, then normal encounters.
2. Fix divergences found by live traces, prioritizing enemy HP/intents, damage, block, status stacks, and pile counts.
3. Improve card draw-order parity so hand comparisons can be enabled in trace comparison.
4. Expand validation scripts into repeatable sweeps over known seeds and encounters.
5. Continue adding map, reward, rest-site, shop, relic, elite, and boss behavior for full-run training.

## Known fidelity gaps

- Card draw order is not yet fully aligned with the real game RNG.
- Some enemy details remain simplified: Gremlin Merc theft/heist rewards, exact Two-Tailed Rat summon constraints, Slithering Strangler's small-slime variant, and mixed attack/buff intent fidelity.
- Neow relics and rewards are only handled enough for validation setup, not fully emulated.
- Full-run systems are incomplete: shops, rests, relic interactions, elites, bosses, events, and richer map routing.
