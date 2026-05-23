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
- STS2MCP live validation is working locally with seeded standard-run starts through `scripts\start_real_game_run.py`; seeded runs can now be replaced without restarting STS2.
- Recent live traces validated the full first-combat weak pool on normalized end-turn fields: Nibbit, Slimes, Toadpoles, Sludge Spinner, Corpse Slugs, Fuzzy Wurm Crawler, Shrinker Beetle, and Seapunk.

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

For faster encounter validation, use STS2MCP `debug_start_encounter` through:

```powershell
.\.venv\Scripts\python.exe scripts\validate_real_game_trace.py --start-seed FORCE_1 --encounter chompers --actions 5 --ignore-hand-order --output-dir "$env:TEMP\sts2-validation"
```

## Fight validation checklist

Status legend: `done` = live trace matched normalized emulator fields; `pending` = modeled in emulator but not yet validated with direct live encounter start; `not modeled` = live encounter exists but emulator does not implement this fight yet.

### Modeled emulator fights

| Status | Emulator encounter      | Live encounter model             | Notes                                   |
| ------ | ----------------------- | -------------------------------- | --------------------------------------- |
| done   | `nibbit`                | `NibbitsWeak`                    | First-combat seeded validation matched. |
| done   | `slimes`                | `SlimesWeak`                     | First-combat seeded validation matched. |
| done   | `shrinker-beetle`       | `ShrinkerBeetleWeak`             | First-combat seeded validation matched. |
| done   | `fuzzy-wurm-crawler`    | `FuzzyWurmCrawlerWeak`           | First-combat seeded validation matched. |
| done   | `corpse-slugs`          | `CorpseSlugsWeak`                | First-combat seeded validation matched. |
| done   | `seapunk`               | `SeapunkWeak`                    | First-combat seeded validation matched. |
| done   | `sludge-spinner`        | `SludgeSpinnerWeak`              | First-combat seeded validation matched. |
| done   | `toadpoles`             | `ToadpolesWeak`                  | First-combat seeded validation matched. |
| done   | `chompers`              | `ChompersNormal`                 | Direct debug encounter matched.         |
| done   | `cultists`              | `CultistsNormal`                 | Direct debug encounter matched.         |
| done   | `exoskeletons`          | `ExoskeletonsNormal`             | Direct debug encounter matched.         |
| done   | `inklets`               | `InkletsNormal`                  | Direct debug encounter matched.         |
| done   | `two-tailed-rats`       | `TwoTailedRatsNormal`            | Direct debug encounter matched.         |
| done   | `gremlin-merc`          | `GremlinMercNormal`              | Direct debug encounter matched.         |
| done   | `mawler`                | `MawlerNormal`                   | Direct debug encounter matched.         |
| done   | `nibbits`               | `NibbitsNormal`                  | Direct debug encounter matched.         |
| done   | `large-slimes`          | `SlimesNormal`                   | Direct debug encounter matched.         |
| done   | `slime-and-flyconid`    | `FlyconidNormal`                 | Direct debug encounter matched.         |
| done   | `jaxfruit-and-flyconid` | `SnappingJaxfruitNormal`         | Direct debug encounter matched.         |
| done   | `cubex-construct`       | `CubexConstructNormal`           | Direct debug encounter matched.         |
| done   | `vine-shambler`         | `VineShamblerNormal`             | Direct debug encounter matched.         |
| done   | `shrinker-and-fuzzy`    | `OvergrowthCrawlers`             | Direct debug encounter matched.         |
| done   | `cultist-and-seapunk`   | `SeapunkNormal`                  | Direct debug encounter matched.         |
| done   | `fossil-stalker`        | `FossilStalkerNormal`            | Direct debug encounter matched.         |
| done   | `punch-construct`       | `PunchConstructNormal`           | Direct debug encounter matched.         |
| done   | `sewer-clam`            | `SewerClamNormal`                | Direct debug encounter matched.         |
| done   | `haunted-ship`          | `HauntedShipNormal`              | Direct debug encounter matched.         |
| done   | `slithering-strangler`  | `SlitheringStranglerNormal`      | Direct debug encounter matched.         |
| done   | `ruby-raiders`          | `RubyRaidersNormal`              | Direct debug encounter matched.         |
| done   | `fogmog`                | `FogmogNormal`                   | Direct debug encounter matched.         |
| done   | `living-fog`            | `LivingFogNormal`                | Direct debug encounter matched.         |
| done   | `bowlbugs-weak`         | `BowlbugsWeak`                   | Direct debug encounter matched.         |
| done   | `bowlbugs`              | `BowlbugsNormal`                 | Direct debug encounter matched.         |
| done   | `tunneler`              | `TunnelerWeak`                   | Direct debug encounter matched.         |
| done   | `tunneler-and-chomper`  | `TunnelerNormal`                 | Direct debug encounter matched.         |
| done   | `thieving-hopper`       | `ThievingHopperWeak`             | Direct debug encounter matched.         |
| done   | `mytes`                 | `MytesNormal`                    | Direct debug encounter matched.         |
| done   | `slumbering-beetle`     | `SlumberingBeetleNormal`         | Direct debug encounter matched.         |
| done   | `spiny-toad`            | `SpinyToadNormal`                | Direct debug encounter matched.         |
| done   | `ovicopter`             | `OvicopterNormal`                | Direct debug encounter matched.         |
| done   | `louse-progenitor`      | `LouseProgenitorNormal`          | Direct debug encounter matched.         |
| done   | `hunter-killer`         | `HunterKillerNormal`             | Direct debug encounter matched.         |
| done   | `axebot`                | `AxebotsNormal`                  | Direct debug encounter matched.         |
| done   | `devoted-sculptor`      | `DevotedSculptorWeak`            | Direct debug encounter matched.         |
| done   | `fabricator`            | `FabricatorNormal`               | Direct debug encounter matched.         |
| done   | `frog-knight`           | `FrogKnightNormal`               | Direct debug encounter matched.         |
| done   | `globe-head`            | `GlobeHeadNormal`                | Direct debug encounter matched.         |
| done   | `turret-operator`       | `TurretOperatorWeak`             | Direct debug encounter matched.         |
| done   | `owl-magistrate`        | `OwlMagistrateNormal`            | Direct debug encounter matched.         |
| done   | `scrolls-weak`          | `ScrollsOfBitingWeak`            | Direct debug encounter matched.         |
| done   | `scrolls`               | `ScrollsOfBitingNormal`          | Direct debug encounter matched.         |
| done   | `slimed-berserker`      | `SlimedBerserkerNormal`          | Direct debug encounter matched.         |
| done   | `lost-and-forgotten`    | `TheLostAndForgottenNormal`      | Direct debug encounter matched.         |
| done   | `obscura`               | `TheObscuraNormal`               | Direct debug encounter matched.         |
| done   | `construct-menagerie`   | `ConstructMenagerieNormal`       | Direct debug encounter matched.         |
| done   | `dense-vegetation`      | `DenseVegetationEventEncounter`  | Direct debug encounter matched.         |
| done   | `punch-off`             | `PunchOffEventEncounter`         | Direct debug encounter matched.         |
| done   | `fake-merchant`         | `FakeMerchantEventEncounter`     | Direct debug encounter matched.         |
| done   | `mysterious-knight`     | `MysteriousKnightEventEncounter` | Direct debug encounter matched.         |

### Live fights not modeled yet

`AeonglassBoss`, `BattlewornDummyEventEncounter` (blocked: direct debug start has no dummy setting and immediately ends combat), `BygoneEffigyElite`, `ByrdonisElite`, `CeremonialBeastBoss`, `DecimillipedeElite`, `EntomancerElite`, `InfestedPrismsElite`, `KaiserCrabBoss`, `KnightsElite`, `KnowledgeDemonBoss`, `LagavulinMatriarchBoss`, `MechaKnightElite`, `PhantasmalGardenersElite`, `PhrogParasiteElite`, `QueenBoss`, `SoulFyshBoss`, `SoulNexusElite`, `TerrorEelElite`, `TestSubjectBoss`, `TheArchitectEventEncounter`, `TheInsatiableBoss`, `TheKinBoss`, `VantomBoss`, `WaterfallGiantBoss`.

## Immediate next steps

1. Improve card draw-order parity so hand comparisons can be enabled in trace comparison.
2. Expand validation scripts into repeatable sweeps over known seeds and encounters.
3. Add emulator support for remaining live fights, starting with unmodeled normal fights before elites and bosses.
4. Continue adding map, reward, rest-site, shop, relic, elite, and boss behavior for full-run training.

## Known fidelity gaps

- Card draw order is not yet fully aligned with the real game RNG.
- Some enemy details remain simplified: Gremlin Merc theft/heist rewards, exact Two-Tailed Rat summon constraints, Slithering Strangler's small-slime variant, and mixed attack/buff intent fidelity.
- Neow relics and rewards are only handled enough for validation setup, not fully emulated.
- Full-run systems are incomplete: shops, rests, relic interactions, elites, bosses, events, and richer map routing.
