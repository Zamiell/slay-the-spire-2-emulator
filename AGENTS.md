# AGENTS.md

- Read the README.md to get an overview for what this project is.
- Whenever you edit a Markdown file, format it afterward with: `bunx --bun prettier --write foo.md`
- Whenever you edit a Python file, format it afterward with: `black --target-version py314 foo.py`
- To validate code changes, run the combined lint/test script: `bash lint-and-test.sh`

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
