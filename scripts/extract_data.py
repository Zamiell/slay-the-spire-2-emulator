#!/usr/bin/env python3
"""Parse decompiled sts2.dll C# source and emit Generated/*.g.cs files."""

import re
import sys
from pathlib import Path

REPO = Path(__file__).parent.parent
DECOMPILED = REPO / "decompiled"
GENERATED  = REPO / "src" / "Sts2Emulator" / "Generated"

# ── helpers ───────────────────────────────────────────────────────────────────

def find_files(pattern: str) -> list[Path]:
    return sorted(DECOMPILED.rglob(pattern))


def first_int(text: str, name: str, default: int = 0) -> int:
    m = re.search(rf"{re.escape(name)}\s*=\s*(-?\d+)", text)
    return int(m.group(1)) if m else default


def cs_header(script: str) -> str:
    return f"// AUTO-GENERATED — do not edit. Re-run scripts/{script} to update.\n"

# ── card extraction ───────────────────────────────────────────────────────────

def extract_cards() -> str:
    """
    Locate card definition classes in the decompiled source and emit Cards.g.cs.
    Heuristic: look for classes that contain fields named 'damage', 'block', 'cost'
    or inherit from a base card class.  Adjust regexes after first decompile.
    """
    entries: list[str] = []

    # TODO: update class_pattern to match actual STS2 card class names after decompile
    class_pattern = re.compile(
        r"class\s+(\w+Card\w*|Card\w+)\s*[:{]", re.IGNORECASE
    )
    cost_pattern   = re.compile(r"(?:cost|Cost)\s*[=:]\s*(-?\d+)")
    damage_pattern = re.compile(r"(?:damage|Damage)\s*[=:]\s*(-?\d+)")
    block_pattern  = re.compile(r"(?:block|Block)\s*[=:]\s*(-?\d+)")

    card_id = 1
    for f in find_files("*.cs"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for m in class_pattern.finditer(text):
            name = m.group(1)
            start = m.start()
            snippet = text[start:start + 600]
            cost   = first_int(snippet, "cost",   0)  if cost_pattern.search(snippet)   else 1
            damage = first_int(snippet, "damage", 0)  if damage_pattern.search(snippet) else 0
            block  = first_int(snippet, "block",  0)  if block_pattern.search(snippet)  else 0
            entries.append(
                f'        new CardDef(Id: {card_id}, Name: "{name}", '
                f'Cost: {cost}, BaseDamage: {damage}, BaseBlock: {block}, '
                f'Type: CardType.Attack),'
            )
            card_id += 1

    if not entries:
        print("  [extract_cards] No card classes found — decompiled/ may be empty.", file=sys.stderr)
        entries = ["        // No cards extracted yet — run decompile.sh first."]

    lines = "\n".join(entries)
    return f"""{cs_header("extract_data.py")}namespace Sts2Emulator.GeneratedData;

internal static class Cards
{{
    private static readonly CardDef[] _all =
    [
{lines}
    ];

    public static CardDef Get(int id) =>
        Array.Find(_all, c => c.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown card id {{id}}");
}}
"""

# ── enemy extraction ──────────────────────────────────────────────────────────

def extract_enemies() -> str:
    entries: list[str] = []
    # TODO: update to match actual STS2 enemy class structure after decompile
    enemy_pattern = re.compile(r"class\s+(\w+(?:Enemy|Monster|Cultist|Guard|Louse)\w*)\s*[:{]", re.IGNORECASE)
    hp_pattern    = re.compile(r"(?:maxHp|MaxHp|hp|HP)\s*[=:]\s*(-?\d+)")

    enemy_id = 1
    for f in find_files("*.cs"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for m in enemy_pattern.finditer(text):
            name = m.group(1)
            snippet = text[m.start():m.start() + 400]
            hp = first_int(snippet, "maxHp", 48) if hp_pattern.search(snippet) else 48
            entries.append(
                f'        new EnemyDef(Id: {enemy_id}, Name: "{name}", MinHp: {hp}, MaxHp: {hp}),'
            )
            enemy_id += 1

    if not entries:
        entries = ["        // No enemies extracted yet — run decompile.sh first."]

    lines = "\n".join(entries)
    return f"""{cs_header("extract_data.py")}namespace Sts2Emulator.GeneratedData;

internal static class Enemies
{{
    private static readonly EnemyDef[] _all =
    [
{lines}
    ];

    public static EnemyDef Get(int id) =>
        Array.Find(_all, e => e.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown enemy id {{id}}");

    public static Intent ChooseIntent(int enemyId, int moveIndex, int turn, Random rng)
    {{
        // TODO: implement per-enemy move patterns after decompile
        return new Intent(IntentType.Unknown, 0);
    }}

    public static void ApplyBuffIntent(EnemyState enemy, CombatState state, Random rng)
    {{
        // TODO: implement after decompile
    }}
}}
"""

# ── relic extraction ──────────────────────────────────────────────────────────

def extract_relics() -> str:
    entries: list[str] = []
    relic_pattern = re.compile(r"class\s+(\w+(?:Relic)\w*)\s*[:{]", re.IGNORECASE)

    relic_id = 1
    for f in find_files("*.cs"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for m in relic_pattern.finditer(text):
            name = m.group(1)
            entries.append(f'        new RelicDef(Id: {relic_id}, Name: "{name}"),')
            relic_id += 1

    if not entries:
        entries = ["        // No relics extracted yet — run decompile.sh first."]

    lines = "\n".join(entries)
    return f"""{cs_header("extract_data.py")}namespace Sts2Emulator.GeneratedData;

internal static class Relics
{{
    private static readonly RelicDef[] _all =
    [
{lines}
    ];

    public static RelicDef Get(int id) =>
        Array.Find(_all, r => r.Id == id) is {{ Id: > 0 }} def
            ? def
            : throw new ArgumentException($"Unknown relic id {{id}}");
}}
"""

# ── main ──────────────────────────────────────────────────────────────────────

def main() -> None:
    if not DECOMPILED.exists():
        print("decompiled/ directory not found. Run scripts/decompile.sh first.", file=sys.stderr)
        sys.exit(1)

    GENERATED.mkdir(parents=True, exist_ok=True)

    for filename, content in [
        ("Cards.g.cs",   extract_cards()),
        ("Enemies.g.cs", extract_enemies()),
        ("Relics.g.cs",  extract_relics()),
    ]:
        out = GENERATED / filename
        out.write_text(content, encoding="utf-8")
        print(f"  wrote {out.relative_to(REPO)}")

    print("extract_data.py complete.")


if __name__ == "__main__":
    main()
