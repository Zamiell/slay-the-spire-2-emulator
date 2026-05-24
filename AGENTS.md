# AGENTS.md

- Read the README.md to get an overview for what this project is.
- Whenever you edit a Markdown file, format it afterward with: `bunx --bun prettier --write foo.md`
- Whenever you edit a Python file, format it afterward with: `black --target-version py314 foo.py`
- To validate code changes, run the combined lint/test script: `bash lint-and-test.sh`
- For `Sts2RunEnv` run-level reward logic, prefer decompiled source under `decompiled\` (for example `PotionRewardOdds`, `PotionFactory`, and merchant entry classes) over inferred deterministic shortcuts.
- Native card effects that cause player HP loss should use `CardEffects.LoseHp` so Rupture and Inferno hooks stay consistent with other card-effect self-damage.
- Native card effects that exhaust another card from hand should select from `state.Hand` after the played card has already been removed, then call `CardEffects.ExhaustCard` so exhaust hooks stay consistent.
- Native card effects that upgrade hand cards should replace `CardInstance` values in `state.Hand` with upgraded copies; the played card has already been removed before `CardEffects.Apply` runs.
- Native card effects that conditionally draw multiple cards should draw one card at a time and respect the 10-card hand cap so the newly drawn card controls whether drawing continues.
- Native card powers that modify attack play count should live in `CombatEngine.PlayCard`, apply one extra `CardEffects.Apply` for affected Attack cards, decrement their counter per Attack, and expire at end of player turn.
- Native card powers with extra dynamic variables can be represented with companion `BuffId` entries when `BuffState` needs to track both the visible counter and hidden per-power state.
- Full-run replay diagnostics should report available boundary diffs before stopping on unsupported trace actions, and unsupported action errors should include the reference step, state type, and floor.

## STS2MCP

- This project uses a fork of STS2MCP that is located here: `D:\Repositories\STS2MCP`
- Sometimes, we might need to update the mod in order to add/fix API functionality. If we make updates to the mod, we need to:
  - Recompile the mod.
  - Close the running Slay the Spire 2 instance, if it is running.
  - Copy over the DLL files to this directory: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods`

## Slay the Spire 2 Launch Instructions

- **Always launch Slay the Spire 2 through Steam**, not by starting `SlayTheSpire2.exe` directly. Otherwise, the game will fail to initialize with the following error: Steam failed to initialize. Make sure you run the game from Steam.
- Correct CLI launch command on Windows: `Start-Process "steam://rungameid/2868840"`
- After launching through Steam, verify STS2MCP with: `Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:15526/"`
