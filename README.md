# Slay the Spire 2 Emulator

This repository contains a high-performance emulator for a subset of Slay the Spire 2 combat logic. The core simulator is written in C# and published as a NativeAOT shared library, then loaded from Python through `ctypes` and exposed as a Gymnasium environment for reinforcement learning experiments.

## What is included

- `src\Sts2Emulator`: C# combat simulator targeting .NET 9 NativeAOT.
- `src\Sts2Emulator\Core`: combat state, turn flow, card effects, buffs, enemy AI, potions, an initial set of relic effects, and reward calculation.
- `src\Sts2Emulator\Generated`: generated card, enemy, potion, power, and relic definitions.
- `src\Sts2Emulator\Interop`: native exports used by Python.
- `src\sts2_gym`: Python `ctypes` bindings, the single-combat Gymnasium environment, and an experimental run wrapper.
- `scripts`: build, data extraction, patch update, trace validation, full-run trace capture, and MaskablePPO training scripts.
- `src\Sts2Emulator.Tests`: xUnit tests for combat behavior.

## Architecture

```text
Python RL training
    |
    | Gymnasium + NumPy observations
    v
src\sts2_gym
    |
    | ctypes
    v
out\Sts2Emulator.dll
    |
    | NativeAOT C# combat engine
    v
src\Sts2Emulator
```

The Python environment calls the native library in-process, avoiding sockets or serialization overhead.

## Current emulator scope

The current combat factory starts an Ironclad-style combat with:

- 64/80 player HP and 3 energy, matching the highest difficulty starting HP.
- A starter deck of 5 Strikes, 4 Defends, 1 Bash, and 1 unplayable, ethereal Ascender's Bane.
- Seeded Act 1 selection between Overgrowth and Underdocks.
- Enemy HP ranges are generated from max-ascension `ToughEnemies` values.
- First-combat sampling from the act-specific weak encounter pools: Overgrowth uses Nibbit, Slimes, Shrinker Beetle, or Fuzzy Wurm Crawler; Underdocks uses Corpse Slugs, Seapunk, Sludge Spinner, or Toadpoles.
- Forced evaluation support for modeled normal encounters, including Chompers plus the Act 1 Overgrowth and Underdocks normal pools.
- A fixed-size integer observation vector.
- Maskable discrete actions for playable cards, end turn, and potions.
- Seeded per-instance RNG for deterministic resets and rollouts.
- Dense reward shaping based on enemy HP damage, player HP loss, and terminal win/loss bonus.
- A default 50-step Gymnasium truncation cap.
- Encounter identity in Python `info`, allowing evaluation by encounter type.
- An experimental `Sts2RunEnv` wrapper for simplified full-run training: Neow rewards, act-specific first-three weak combats, deterministic map choices, normal/elite/boss combat nodes, card rewards, gold, shops, shop card removal, rest sites, modeled trace-observed events including The Legends Were True, relic rewards, potion slots, deterministic potion drops/purchases, run deck tracking, upgraded-card encoding, and decompilation-derived run-level relic pickup/heal effects.
- `Sts2RunEnv` uses a run-scale default truncation cap of 1000 steps; single-combat `Sts2CombatEnv` keeps its 50-step cap.
- Modeled enemy powers for supported fights include Artifact, Hard to Kill, Shrink, Thorns, Ravenous, Slippery, Surprise, Two-Tailed Rat backup calls, Plating, Tangled, Constrict, Smoggy, Illusion, and Gas Bomb minions.
- Trace-observed Ironclad card effects now include Burning Pact, Expect a Fight, Havoc, Perfected Strike, Restlessness, Setup Strike, Splash, Stampede tracking, Sword Boomerang, True Grit, and Juggling in addition to the starter/common pool used by run rewards.
- Initial native relic combat effects for Anchor, Bag of Marbles, Bag of Preparation, Blood Vial, Bronze Scales, Captain's Wheel, Happy Flower, Horn Cleat, Lantern, Oddly Smooth Stone, Orichalcum, Red Skull, Venerable Tea Set, and Vajra, with run-level HP, potions, and relics passed into native combat.
- Secondary intent metadata for known mixed attack+buff/debuff enemy moves is exposed in the reserved observation area.

This is not yet a full game emulator. Full live map generation, exact Neow/shop/reward/event odds and broad native relic coverage are still future work.

## Requirements

- .NET 9 SDK
- Python 3.11+ recommended
- uv
- Native build tools required by .NET NativeAOT for the target platform
- Python packages managed by uv from `pyproject.toml`

Install Python dependencies:

```powershell
uv sync
```

## Build the native library

On Windows:

```powershell
dotnet publish "src\Sts2Emulator\Sts2Emulator.csproj" -c Release -r win-x64 --self-contained -o "out"
```

Or with the helper script from a shell that can run Bash:

```bash
bash scripts/build.sh win-x64
```

The Python bindings look for the native library in `out\Sts2Emulator.dll` on Windows. You can also set `STS2_LIB_PATH` to point at a directory containing the native library; when set, that path takes precedence over `out`. To prevent stale native-code rollouts, Python fails fast if the loaded library is older than the C# source files or does not export the required native API version. Rebuild with the publish command above after C# changes.

## Run tests and checks

Run the C# test suite:

```powershell
dotnet test "src\Sts2Emulator.Tests\Sts2Emulator.Tests.csproj"
```

Check the Gymnasium environment:

```powershell
uv run python scripts\train.py --check
```

Run a short training job:

```powershell
uv run python scripts\train.py --timesteps 5000 --n-envs 2
```

Train against the simplified full-run wrapper:

```powershell
uv run python scripts\train.py --run-env --timesteps 5000 --n-envs 2
```

Evaluate a simple baseline policy over fixed seeds, including per-encounter win rates:

```powershell
uv run python scripts\evaluate.py --episodes 100 --policy first-valid
```

Evaluate simplified full-run episodes:

```powershell
uv run python scripts\evaluate.py --run-env --episodes 10 --policy first-valid --max-episode-steps 200
```

Force a specific encounter and use the starter-deck-aware baseline:

```powershell
uv run python scripts\evaluate.py --episodes 100 --policy starter-aggressive --encounter chompers
```

Emit a deterministic emulator trace for comparison against real-game traces:

```powershell
uv run python scripts\trace.py --seed 0 --encounter toadpoles --actions 0 1 2
```

Emit a trace from a running Slay the Spire 2 instance with the STS2MCP mod enabled:

```powershell
uv run python scripts\trace_real_game.py --actions 0 1 2
```

Capture a full-run trace from a running Slay the Spire 2 instance with STS2MCP enabled:

```powershell
uv run python scripts\trace_real_game_run.py FULLRUN_SEED --abandon-existing --output traces\full-run\FULLRUN_SEED.json
```

Generated full-run captures are ignored by default because they are large and usually superseded quickly. Keep only curated trace fixtures in `traces\full-run` when a trace documents a durable parity milestone or regression case.

Start a new real-game standard run with a specific seed through STS2MCP before tracing:

```powershell
uv run python scripts\start_real_game_run.py VALIDATION1 --character IRONCLAD --abandon-existing
```

Compare two trace JSON files on their normalized player/enemy fields:

```powershell
uv run python scripts\compare_traces.py emulator-trace.json real-game-trace.json
```

Run the repeatable STS2MCP validation sweep against a running game:

```powershell
uv run python scripts\validate_real_game_sweep.py --suite all --continue-on-failure
```

Use `--suite direct` for one-passive-turn direct encounter checks, `--suite passive-boss` for the current three-turn boss checks, or `--encounter aeonglass` to narrow the run.

When using STS2MCP, launch Slay the Spire 2 through Steam rather than starting the executable directly:

```powershell
Start-Process "steam://rungameid/2868840"
```

Then verify STS2MCP is reachable:

```powershell
Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:15526/"
```

## Training

`scripts\train.py` trains `MaskablePPO` from `sb3-contrib` using action masks from the environment:

```powershell
uv run python scripts\train.py --timesteps 1000000 --n-envs 4 --save-path checkpoints\maskable_ppo
```

The resulting model is saved as a Stable Baselines3 checkpoint.

## Updating generated game data

The repository includes scripts intended to keep generated data synchronized with Slay the Spire 2 patches:

- `scripts\decompile.sh`: decompile the game assembly when it changes.
- `scripts\extract_data.py`: regenerate C# data tables from decompiled sources.
- `scripts\diff_patch.py`: summarize generated data changes.
- `scripts\patch_update.sh`: run the full patch-update pipeline.

See `PLAN.md` for the active parity gaps and next implementation steps.
