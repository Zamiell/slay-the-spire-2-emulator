# AGENTS.md

- Read the README.md to get an overview for what this project is.
- Whenever you edit a Markdown file, format it afterward with: `bunx --bun prettier --write foo.md`
- Whenever you edit a Python file, format it afterward with: `black foo.py`

## Slay the Spire 2 Launch Instructions

- **Always launch Slay the Spire 2 through Steam**, not by starting `SlayTheSpire2.exe` directly. Otherwise, the game will fail to initialize with the following error: Steam failed to initialize. Make sure you run the game from Steam.
- Correct CLI launch command on Windows: `Start-Process "steam://rungameid/2868840"`
- After launching through Steam, verify STS2MCP with: `Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:15526/"`.
