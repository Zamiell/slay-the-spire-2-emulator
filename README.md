# Slay the Spire 2 Emulator

This repository contains a high-performance emulator for a subset of Slay the Spire 2 combat logic. The core simulator is written in C# and published as a NativeAOT shared library, then loaded from Python through `ctypes` and exposed as a Gymnasium environment for reinforcement learning experiments.

## What is included

- `src\Sts2Emulator`: C# combat simulator targeting .NET 9 NativeAOT.
- `src\Sts2Emulator\Core`: combat state, turn flow, card effects, buffs, enemy AI, potions, relic data types, and reward calculation.
- `src\Sts2Emulator\Generated`: generated card, enemy, potion, power, and relic definitions.
- `src\Sts2Emulator\Interop`: native exports used by Python.
- `src\sts2_gym`: Python `ctypes` bindings and a Gymnasium-compatible environment.
- `scripts`: build, data extraction, patch update, and MaskablePPO training scripts.
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

- 80 player HP and 3 energy.
- A starter deck of 5 Strikes and 4 Defends.
- A single Calcified Cultist encounter.
- A fixed-size integer observation vector.
- Maskable discrete actions for playable cards, end turn, and potions.
- Dense reward shaping based on enemy HP damage, player HP loss, and terminal win/loss bonus.
- A default 50-step Gymnasium truncation cap.

This is not yet a full game emulator. Run progression, map navigation, rewards, shops, rests, and broad encounter coverage are still future work.

## Requirements

- .NET 9 SDK
- Python 3.11+ recommended
- Native build tools required by .NET NativeAOT for the target platform
- Python packages from `requirements.txt`

Install Python dependencies:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
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

The Python bindings look for the native library in `out\Sts2Emulator.dll` on Windows. You can also set `STS2_LIB_PATH` to point at a directory containing the native library.

## Run tests and checks

Run the C# test suite:

```powershell
dotnet test "src\Sts2Emulator.Tests\Sts2Emulator.Tests.csproj"
```

Check the Gymnasium environment:

```powershell
.\.venv\Scripts\python.exe scripts\train.py --check
```

Run a short training job:

```powershell
.\.venv\Scripts\python.exe scripts\train.py --timesteps 5000 --n-envs 2
```

## Training

`scripts\train.py` trains `MaskablePPO` from `sb3-contrib` using action masks from the environment:

```powershell
.\.venv\Scripts\python.exe scripts\train.py --timesteps 1000000 --n-envs 4 --save-path checkpoints\maskable_ppo
```

The resulting model is saved as a Stable Baselines3 checkpoint.

## Updating generated game data

The repository includes scripts intended to keep generated data synchronized with Slay the Spire 2 patches:

- `scripts\decompile.sh`: decompile the game assembly when it changes.
- `scripts\extract_data.py`: regenerate C# data tables from decompiled sources.
- `scripts\diff_patch.py`: summarize generated data changes.
- `scripts\patch_update.sh`: run the full patch-update pipeline.

See `PLAN.md` for the longer design notes and planned expansion path.
