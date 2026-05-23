# Slay the Spire 2 Emulator — Build Plan

## Goal

Build a high-performance emulator of STS2's game logic in C#, compiled to a native library via
.NET NativeAOT, and wrapped in a Python Gymnasium environment for reinforcement learning training.

---

## Architecture Overview

```txt
┌─────────────────────────────────────────┐
│           Python RL Training            │
│  (Stable Baselines3 / RLlib / custom)   │
└────────────────┬────────────────────────┘
                 │ numpy arrays
┌────────────────▼────────────────────────┐
│        sts2_gym (Python package)        │
│  Gymnasium env wrapper + ctypes layer   │
└────────────────┬────────────────────────┘
                 │ ctypes / cffi calls
┌────────────────▼────────────────────────┐
│        Sts2Emulator.dll (native)        │
│  C# game logic compiled via NativeAOT  │
└─────────────────────────────────────────┘
```

No process boundary, no sockets, no serialization overhead — Python calls the native library
directly in-process, the same way it would call a C library.

---

## Current Status

The repository currently has a working NativeAOT C# combat emulator, Python `ctypes` bindings,
`Sts2CombatEnv` for single-combat RL, and an experimental `Sts2RunEnv` wrapper for deterministic
card rewards and map encounter choices between combats. The modeled combat scope includes
highest-difficulty Ironclad starts, random Act 1 selection between Overgrowth and Underdocks,
act-specific first-three weak encounter pools, explicit unplayable/ethereal/exhaust card behavior,
status-card mechanics, enemy powers, deterministic seeded resets, reward shaping,
trace/evaluation scripts, and regression coverage in C# and Python. `scripts/evaluate.py` supports
forced encounters and per-encounter metrics; normal encounters like Chompers remain available for
forced evaluation but are no longer sampled as opening fights.

## Remaining Next Steps

1. Validate emulator-vs-real-game traces with STS2MCP for fixed seeds and fixed action sequences.
2. Fill out missing mechanics for currently simplified weak encounters, especially Shrinker
   Beetle's Shrink and Toadpole's Thorns.
3. Add rest-site, shop, relic, elite, boss, and richer map node layers for full-run training.
4. Run longer MaskablePPO training experiments and use forced/per-encounter `scripts/evaluate.py`
   metrics to guide combat tuning.

---

## Project Structure

```txt
slay-the-spire-2-emulator/
├── PLAN.md
├── src/
│   ├── Sts2Emulator/                  # C# .NET 8 project
│   │   ├── Sts2Emulator.csproj
│   │   ├── Core/
│   │   │   ├── GameState.cs           # Full run state (deck, HP, relics, map position)
│   │   │   ├── CombatState.cs         # In-combat state (hand, energy, enemies, buffs)
│   │   │   ├── CombatEngine.cs        # Turn flow, action resolution, win/loss detection
│   │   │   ├── Card.cs                # Card data + effect dispatch
│   │   │   ├── Enemy.cs               # Enemy data + intent AI
│   │   │   ├── Relic.cs               # Relic effect hooks
│   │   │   └── Effects/               # Individual card/relic/buff effect implementations
│   │   ├── Generated/                 # AUTO-GENERATED — do not edit by hand
│   │   │   ├── Cards.g.cs             # Card definitions emitted by extract_data.py
│   │   │   ├── Enemies.g.cs           # Enemy definitions emitted by extract_data.py
│   │   │   └── Relics.g.cs            # Relic definitions emitted by extract_data.py
│   │   └── Interop/
│   │       └── NativeExports.cs       # All [UnmanagedCallersOnly] exported functions
│   └── sts2_gym/                      # Python package
│       ├── __init__.py
│       ├── native.py                  # ctypes bindings to Sts2Emulator.dll
│       └── env.py                     # Gymnasium-compatible environment
├── scripts/
│   ├── decompile.sh                   # Decompile game DLLs (skips if DLL hash unchanged)
│   ├── extract_data.py                # Parse decompiled C# → Generated/*.g.cs source files
│   ├── diff_patch.py                  # Diff generated files between versions; print change report
│   ├── patch_update.sh                # Orchestrate full patch workflow (see Phase 1.5)
│   └── build.sh                       # Build + NativeAOT publish
├── decompiled/                        # ILSpy output — committed to git, diffed on each patch
│   └── .version                       # SHA-256 of Assembly-CSharp.dll at last decompile
└── tests/
    ├── Sts2Emulator.Tests/            # xUnit tests for game logic correctness
    └── python/                        # Integration tests against real game via STS2MCP
```

---

## Phase 1: Reverse Engineering the Game

STS2 is a Unity game written in C#. The compiled IL is stored in the game's managed DLLs and is
fully decompilable. The goal of this phase is not just a one-time extraction — every step is
scriptable so that re-running after a game patch requires a single command.

### 1.1 Locate the Game DLLs

STS2 is a **Godot + C#** game (not Unity). The main game logic lives in a standard .NET IL
assembly that ILSpy can decompile directly:

```txt
<STS2 install dir>/
    data_sts2_windows_x86_64/sts2.dll   ← main game logic (Godot C# assembly)
    data_sts2_windows_x86_64/           ← .NET runtime + GodotSharp + vendor DLLs
    mods/                               ← loaded mod DLLs (0Harmony / MonoMod based)
```

On Windows the default Steam path is:
`C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\`

`decompile.sh` auto-detects the install path by scanning Steam's `libraryfolders.vdf` across all
Steam libraries, falling back to the default path, with an optional positional argument override:

```bash
#!/usr/bin/env bash
# scripts/decompile.sh
set -euo pipefail

GAME_DIR="${1:-}"

if [ -z "$GAME_DIR" ]; then
    STEAM_ROOT="/c/Program Files (x86)/Steam"
    VDF="$STEAM_ROOT/steamapps/libraryfolders.vdf"
    # Extract library paths from vdf, convert backslashes, find the STS2 install
    GAME_DIR=$(grep -oP '(?<="path"\s{1,10}")[^"]+' "$VDF" 2>/dev/null \
        | sed 's|\\\\|/|g' \
        | while read -r lib; do
            candidate="$lib/steamapps/common/Slay the Spire 2"
            [ -d "$candidate" ] && echo "$candidate" && break
          done || true)
    GAME_DIR="${GAME_DIR:-$STEAM_ROOT/steamapps/common/Slay the Spire 2}"
fi

DLL="$GAME_DIR/data_sts2_windows_x86_64/sts2.dll"
HASH=$(sha256sum "$DLL" | awk '{print $1}')
STORED=$(cat decompiled/.version 2>/dev/null || echo "")

if [ "$HASH" = "$STORED" ]; then
    echo "sts2.dll unchanged ($HASH) — skipping decompile."
    exit 0
fi

echo "New version detected. Decompiling..."
ilspycmd "$DLL" --outputdir decompiled/ --project
echo "$HASH" > decompiled/.version
echo "Done. Commit decompiled/ to record this version."
```

The `decompiled/` directory is committed to git. Each patch produces a clean diff showing exactly
which types and methods changed.

### 1.2 What to Extract

Priority order:

| Component              | What to look for                                                                 |
| ---------------------- | -------------------------------------------------------------------------------- |
| **Card definitions**   | Card ID, cost, type (attack/skill/power), base damage/block values, effect class |
| **Card effects**       | Effect implementations (damage, block, draw, buff application)                   |
| **Enemy definitions**  | HP ranges, move sets, intent AI (pattern-based or conditional)                   |
| **Buff/debuff system** | Buff IDs, stack behaviour (additive/override), per-turn tick logic               |
| **Damage formula**     | How strength, vulnerability, weak, and other modifiers combine                   |
| **Combat flow**        | Turn start/end hooks, draw phase, end-of-turn discard, death checks              |
| **Relic hooks**        | Which events each relic listens to                                               |
| **Map / meta-game**    | Room types, reward generation, shop pricing, rest site options                   |

The STS2MCP mod source is a useful navigation aid — it already hooks into the game's internal
types and reveals which classes own which data.

### 1.3 Data Extraction Script

`scripts/extract_data.py` parses the decompiled C# source using regex and Roslyn-style pattern
matching to pull numeric constants and class structures, then **emits C# source files** directly
into `src/Sts2Emulator/Generated/`. Generated files are compiled into the emulator — no runtime
JSON parsing, no reflection, fully NativeAOT-compatible.

```txt
decompiled/          ──extract_data.py──►  src/Sts2Emulator/Generated/
  Cards/*.cs                                  Cards.g.cs
  Enemies/*.cs                                Enemies.g.cs
  Relics/*.cs                                 Relics.g.cs
  Buffs/*.cs                                  Buffs.g.cs
```

Each generated file contains a static readonly array or switch table. Example output:

```csharp
// Generated/Cards.g.cs — DO NOT EDIT. Re-run scripts/extract_data.py to update.
internal static class GeneratedCards {
    public static readonly CardDef[] All = {
        new CardDef(Id: 1, Name: "Strike",    Cost: 1, BaseDamage: 6, BaseBlock: 0, Type: CardType.Attack),
        new CardDef(Id: 2, Name: "Defend",    Cost: 1, BaseDamage: 0, BaseBlock: 5, Type: CardType.Skill),
        // ...
    };
}
```

Effect logic (what a card _does_) is still hand-written in `Core/Effects/` — only the numeric
parameters come from generated code. This means a patch that changes "Strike deals 6 → 7 damage"
is handled automatically by re-running the script, while a patch that adds a brand-new mechanic
still requires a human to implement the effect.

### 1.4 Patch Diff Script

`scripts/diff_patch.py` compares the freshly generated `*.g.cs` files against the previous git
commit and prints a human-readable change report:

```txt
$ python scripts/diff_patch.py

[CHANGED] Strike: BaseDamage 6 → 7
[CHANGED] Cultist: HP range 48–56 → 50–60
[NEW]     Card "Shiv+": Cost 0, BaseDamage 4, Type Attack
[REMOVED] Relic "Boot" (id 42)

Effects requiring manual review: 1 new card(s), 1 removed relic(s).
```

This makes it immediately clear what a patch changed and whether any manual effect implementation
work is needed.

### 1.5 Orchestration Script

`scripts/patch_update.sh` is the single entry point after a game update. Run it once; it chains
all steps:

```bash
#!/usr/bin/env bash
# scripts/patch_update.sh
set -euo pipefail

bash scripts/decompile.sh "$@"         # 1. Decompile (skips if unchanged)
python scripts/extract_data.py         # 2. Regenerate *.g.cs files
python scripts/diff_patch.py           # 3. Print change report
bash scripts/build.sh                  # 4. Rebuild native DLL
dotnet test src/Sts2Emulator.Tests/    # 5. Run correctness tests
```

After running, the developer reviews the diff report, implements any new effects, and re-trains if
the changes are significant.

### 1.6 Validation Strategy

The `decompiled/` directory is committed to git alongside `src/Sts2Emulator/Generated/`. Running
`git diff` after a patch immediately shows both the raw IL changes and the downstream effect on
generated data in one diff.

---

## Phase 2: C# Emulator

### 2.1 Project Setup

Target .NET 8+ with NativeAOT enabled from the start — this constrains what C# features can be
used (no runtime reflection, no `dynamic`, limited LINQ) and is easier to enforce early than to
retrofit later.

```xml
<!-- Sts2Emulator.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

### 2.2 State Representation

Game state must be serialisable to flat arrays of blittable types (int/float) for zero-copy
transfer to Python. Define a fixed-size observation vector:

- Player HP, block, energy, max energy, relics
- Hand (up to N card slots, each encoded as card ID + upgrades)
- Draw pile size, discard pile size, exhaust pile size
- Active buffs/debuffs on the player (fixed-size slot array: buff ID + magnitude)
- Potions (fixed-size slot array: potion ID, or 0 for empty slot)
- Each enemy: HP, block, intent ID, intent magnitude, buffs/debuffs

The exact vector layout will be documented in `Interop/NativeExports.cs` and mirrored in
`sts2_gym/native.py`.

### 2.3 Combat Engine

Implement combat as a deterministic step function:

```txt
(state, action) → (next_state, reward, is_terminal)
```

Randomness (card draw order, enemy RNG) is seeded and reproducible. The engine does not render
anything — it is pure logic.

### 2.4 NativeAOT Constraints

- No `Type.GetType()`, `Activator.CreateInstance()`, or attribute-driven reflection.
- Card and relic effects are dispatched via a static switch/dictionary of delegates, not
  reflection.
- No `System.Text.Json` with source-generation disabled — use source-generated serializers or
  avoid JSON entirely in hot paths.
- All exported functions are tagged `[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]`
  and take only blittable parameters (int*, float*, bool).

---

## Phase 3: NativeAOT Compilation

### 3.1 Build Script

```bash
#!/usr/bin/env bash
# scripts/build.sh
set -euo pipefail

dotnet publish src/Sts2Emulator/Sts2Emulator.csproj \
    -c Release \
    -r win-x64 \          # change to linux-x64 or osx-arm64 as needed
    --self-contained \
    -o out/
```

Output: `out/Sts2Emulator.dll` (Windows) or `out/Sts2Emulator.so` (Linux).

The library has no .NET runtime dependency — it is a plain native shared library.

### 3.2 Exported API Surface

All exports live in `Interop/NativeExports.cs`. The minimal API needed for RL:

```csharp
// Allocate a new environment instance; returns an opaque handle (int index into a pool).
[UnmanagedCallersOnly] public static int Sts2_Create(int seed);

// Reset to a new episode. Writes the initial observation into obs_buf (caller-allocated).
[UnmanagedCallersOnly] public static void Sts2_Reset(int handle, int* obs_buf);

// Advance one step. Returns 1 if the episode is terminal, 0 otherwise.
// Writes next observation into obs_buf and reward into reward_out.
[UnmanagedCallersOnly] public static int Sts2_Step(int handle, int action, int* obs_buf, float* reward_out);

// Query the number of valid actions in the current state.
[UnmanagedCallersOnly] public static int Sts2_ActionCount(int handle);

// Free the environment instance.
[UnmanagedCallersOnly] public static void Sts2_Destroy(int handle);

// Returns the fixed size of the observation vector (number of ints).
[UnmanagedCallersOnly] public static int Sts2_ObsSize();
```

---

## Phase 4: Python Gymnasium Wrapper

### 4.1 ctypes Binding (`native.py`)

Load the compiled library and declare function signatures matching the exports above. This file is
the only place in Python that knows about the native ABI.

### 4.2 Gymnasium Environment (`env.py`)

Wrap `native.py` in a `gymnasium.Env` subclass:

- `observation_space`: `Box(low=0, high=MAX_INT, shape=(OBS_SIZE,), dtype=np.int32)`
- `action_space`: `Discrete(MAX_ACTIONS)` with invalid actions masked
- `reset()` → calls `Sts2_Reset`, returns numpy array (zero-copy view into a ctypes buffer)
- `step(action)` → calls `Sts2_Step`, returns `(obs, reward, terminated, truncated, info)`

Action masking (returning a boolean array of valid actions) is important for card games — many
actions are illegal on any given turn.

### 4.3 Vectorised Environments

Because each environment instance is an independent native handle, standard vectorisation
wrappers (`gymnasium.vector.AsyncVectorEnv` or SB3's `SubprocVecEnv`) work without modification.
Start with 16–64 parallel environments on a single machine.

---

## Phase 5: RL Training

### 5.1 Scope — Combat First

Train exclusively on single combats before tackling full runs. A combat episode is short (~20–50
steps), has a clear terminal signal (win/lose), and isolates the core decision-making problem.

### 5.2 Reward Shaping

Raw win/loss is too sparse for early training. Suggested shaping:

- `+1.0` for winning the combat
- `-1.0` for dying
- `+(hp_after - hp_before) * 0.01` per step to encourage HP preservation
- `-0.001` per step as a small time penalty to discourage stalling

### 5.3 Algorithm

Start with **PPO** (Proximal Policy Optimisation) via Stable Baselines3. It handles large
discrete action spaces well and is robust to reward shaping. Enable invalid action masking
(`sb3-contrib MaskablePPO`).

### 5.4 Expanding to Full Runs

Once combat win rate is stable, extend the emulator and environment to cover:

1. Map navigation (path choice)
2. Card rewards after combat
3. Rest sites (heal vs. upgrade)
4. Shop (buy/remove cards, buy relics)
5. Boss encounters

Each layer adds meta-game decisions on top of combat; the combat policy can be frozen or
fine-tuned as the outer loop is added.

---

## Validation Against the Real Game

At each phase, validate the emulator against the actual game via STS2MCP:

1. Set a fixed seed in both the emulator and the real game.
2. Play the same sequence of actions in both.
3. Assert that HP, hand state, enemy HP, and buff stacks match after every step.

This catches divergences early and provides a regression suite when the game patches.

---

## Updating After Game Patches

When STS2 releases a patch, run one command:

```bash
bash scripts/patch_update.sh
```

This decompiles (skipping if the DLL hash is unchanged), regenerates all `*.g.cs` data files,
prints a diff report of what changed, rebuilds the native DLL, and runs the test suite.

The only manual steps are:

- Implementing C# effect logic for any **new** mechanics flagged in the diff report.
- Deciding whether the changes are large enough to warrant retraining from scratch vs. fine-tuning
  from an existing checkpoint.

---

## Dependencies

| Component              | Dependency                                                     |
| ---------------------- | -------------------------------------------------------------- |
| Decompilation          | [ILSpy CLI (`ilspycmd`)](https://github.com/icsharpcode/ILSpy) |
| Data extraction / diff | Python 3.11+, no third-party deps (stdlib only)                |
| C# emulator            | .NET 8 SDK                                                     |
| NativeAOT (Windows)    | Visual Studio C++ build tools (MSVC linker)                    |
| NativeAOT (Linux)      | `clang` + `zlib`                                               |
| Python env             | `gymnasium`, `numpy`, `stable-baselines3`, `sb3-contrib`       |
| Validation             | STS2 + [STS2MCP mod](https://github.com/Gennadiyev/STS2MCP)    |
