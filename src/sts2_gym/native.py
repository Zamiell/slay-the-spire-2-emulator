"""ctypes bindings to the NativeAOT-compiled Sts2Emulator native library."""

import ctypes
import os
import sys
from pathlib import Path

_LIB_NAMES = {
    "win32": "Sts2Emulator.dll",
    "linux": "Sts2Emulator.so",
    "darwin": "Sts2Emulator.dylib",
}
_ALLOW_STALE_ENV = "STS2_ALLOW_STALE_NATIVE"
_REQUIRED_NATIVE_API_VERSION = 8


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _native_source_paths(repo_root: Path) -> list[Path]:
    source_root = repo_root / "src" / "Sts2Emulator"
    paths = [
        path
        for path in source_root.rglob("*.cs")
        if "bin" not in path.parts and "obj" not in path.parts
    ]
    paths.extend(source_root.rglob("*.csproj"))
    return paths


def _assert_native_library_is_fresh(path: Path) -> None:
    if os.environ.get(_ALLOW_STALE_ENV) == "1":
        return

    source_paths = _native_source_paths(_repo_root())
    if not source_paths:
        return

    newest_source = max(source_paths, key=lambda source: source.stat().st_mtime)
    if path.stat().st_mtime >= newest_source.stat().st_mtime:
        return

    raise RuntimeError(
        f"{path} is older than {newest_source}. Rebuild the native library with "
        f"`bash scripts/build.sh win-x64` or `dotnet publish "
        f'"src\\Sts2Emulator\\Sts2Emulator.csproj" -c Release -r win-x64 '
        f'--self-contained -o "out"`. Set {_ALLOW_STALE_ENV}=1 only when '
        "intentionally testing an older native build."
    )


def _assert_native_api_version(lib: ctypes.CDLL, path: Path) -> None:
    try:
        version_func = lib.Sts2_NativeApiVersion
    except AttributeError as exc:
        raise RuntimeError(
            f"{path} does not export Sts2_NativeApiVersion and is too old for "
            "these Python bindings. Rebuild the native library with "
            "`bash scripts/build.sh win-x64` or `dotnet publish "
            '"src\\Sts2Emulator\\Sts2Emulator.csproj" -c Release -r win-x64 '
            '--self-contained -o "out"`.'
        ) from exc

    version_func.restype = ctypes.c_int
    version_func.argtypes = []
    actual_version = int(version_func())
    if actual_version != _REQUIRED_NATIVE_API_VERSION:
        raise RuntimeError(
            f"{path} exports native API version {actual_version}, but "
            f"sts2_gym requires {_REQUIRED_NATIVE_API_VERSION}. Rebuild the "
            "native library with `bash scripts/build.sh win-x64` or "
            '`dotnet publish "src\\Sts2Emulator\\Sts2Emulator.csproj" '
            '-c Release -r win-x64 --self-contained -o "out"`.'
        )


def _load_lib() -> ctypes.CDLL:
    name = _LIB_NAMES.get(sys.platform)
    if name is None:
        raise RuntimeError(f"Unsupported platform: {sys.platform}")

    search = []
    if "STS2_LIB_PATH" in os.environ:
        search.append(Path(os.environ["STS2_LIB_PATH"]) / name)
    search.append(_repo_root() / "out" / name)
    for path in search:
        if path.exists():
            _assert_native_library_is_fresh(path)
            lib = ctypes.CDLL(str(path))
            _assert_native_api_version(lib, path)
            return lib
    raise FileNotFoundError(
        f"Could not find {name}. Run scripts/build.sh first, or set STS2_LIB_PATH."
    )


_lib = _load_lib()

# ── function signatures ───────────────────────────────────────────────────────

_lib.Sts2_ObsSize.restype = ctypes.c_int
_lib.Sts2_ObsSize.argtypes = []

_lib.Sts2_MaxEnemies.restype = ctypes.c_int
_lib.Sts2_MaxEnemies.argtypes = []

_lib.Sts2_Create.restype = ctypes.c_int
_lib.Sts2_Create.argtypes = [ctypes.c_int]

_lib.Sts2_Reset.restype = None
_lib.Sts2_Reset.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_int)]

_lib.Sts2_ResetEncounter.restype = None
_lib.Sts2_ResetEncounter.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeck.restype = None
_lib.Sts2_ResetWithDeck.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeckAndEncounter.restype = None
_lib.Sts2_ResetWithDeckAndEncounter.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeckEncounterAndRelics.restype = None
_lib.Sts2_ResetWithDeckEncounterAndRelics.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetRunCombat.restype = None
_lib.Sts2_ResetRunCombat.argtypes = [
    ctypes.c_int,  # handle
    ctypes.POINTER(ctypes.c_int),  # deckIds
    ctypes.c_int,  # deckLen
    ctypes.c_int,  # encounterId
    ctypes.POINTER(ctypes.c_int),  # relicIds
    ctypes.c_int,  # relicLen
    ctypes.c_int,  # playerHp
    ctypes.c_int,  # playerMaxHp
    ctypes.POINTER(ctypes.c_int),  # potionIds
    ctypes.c_int,  # potionLen
    ctypes.c_int,  # playerGold
    ctypes.c_int,  # encounterRngSeed
    ctypes.POINTER(ctypes.c_int),  # obsBuf
]

_lib.Sts2_ResetRunCombatPreShuffled.restype = None
_lib.Sts2_ResetRunCombatPreShuffled.argtypes = [
    ctypes.c_int,  # handle
    ctypes.POINTER(ctypes.c_int),  # deckIds
    ctypes.c_int,  # deckLen
    ctypes.c_int,  # encounterId
    ctypes.POINTER(ctypes.c_int),  # relicIds
    ctypes.c_int,  # relicLen
    ctypes.c_int,  # playerHp
    ctypes.c_int,  # playerMaxHp
    ctypes.POINTER(ctypes.c_int),  # potionIds
    ctypes.c_int,  # potionLen
    ctypes.c_int,  # playerGold
    ctypes.c_int,  # shuffleRngSeed
    ctypes.c_int,  # shufflePreSkip
    ctypes.c_int,  # nicheSkipCount
    ctypes.c_int,  # encounterRngSeed
    ctypes.c_int,  # monsterAiRngSeed
    ctypes.POINTER(ctypes.c_int),  # obsBuf
]

_lib.Sts2_Step.restype = ctypes.c_int
_lib.Sts2_Step.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.POINTER(ctypes.c_float),
]

_lib.Sts2_PlayerWon.restype = ctypes.c_int
_lib.Sts2_PlayerWon.argtypes = [ctypes.c_int]

_lib.Sts2_EncounterId.restype = ctypes.c_int
_lib.Sts2_EncounterId.argtypes = [ctypes.c_int]

_lib.Sts2_GetShuffleRngCallCount.restype = ctypes.c_int
_lib.Sts2_GetShuffleRngCallCount.argtypes = [ctypes.c_int]

_lib.Sts2_GetNicheRngCallCount.restype = ctypes.c_int
_lib.Sts2_GetNicheRngCallCount.argtypes = [ctypes.c_int]

_lib.Sts2_ActionCount.restype = ctypes.c_int
_lib.Sts2_ActionCount.argtypes = [ctypes.c_int]

_lib.Sts2_ValidActions.restype = None
_lib.Sts2_ValidActions.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
]

_lib.Sts2_Destroy.restype = None
_lib.Sts2_Destroy.argtypes = [ctypes.c_int]

# ── public wrappers ───────────────────────────────────────────────────────────

OBS_SIZE: int = _lib.Sts2_ObsSize()
MAX_ENEMIES: int = _lib.Sts2_MaxEnemies()


def create(seed: int) -> int:
    return _lib.Sts2_Create(seed)


def reset(handle: int, obs_buf: ctypes.Array) -> None:
    _lib.Sts2_Reset(handle, obs_buf)


def reset_encounter(handle: int, encounter_id: int, obs_buf: ctypes.Array) -> None:
    _lib.Sts2_ResetEncounter(handle, encounter_id, obs_buf)


def reset_with_deck(handle: int, deck_ids: list[int], obs_buf: ctypes.Array) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    _lib.Sts2_ResetWithDeck(handle, deck_buf, len(deck_ids), obs_buf)


def reset_with_deck_and_encounter(
    handle: int,
    deck_ids: list[int],
    encounter_id: int,
    obs_buf: ctypes.Array,
) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    _lib.Sts2_ResetWithDeckAndEncounter(
        handle, deck_buf, len(deck_ids), encounter_id, obs_buf
    )


def reset_with_deck_encounter_and_relics(
    handle: int,
    deck_ids: list[int],
    encounter_id: int,
    relic_ids: list[int],
    obs_buf: ctypes.Array,
) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    relic_buf = (ctypes.c_int * len(relic_ids))(*relic_ids)
    _lib.Sts2_ResetWithDeckEncounterAndRelics(
        handle,
        deck_buf,
        len(deck_ids),
        encounter_id,
        relic_buf,
        len(relic_ids),
        obs_buf,
    )


def reset_run_combat(
    handle: int,
    deck_ids: list[int],
    encounter_id: int,
    relic_ids: list[int],
    player_hp: int,
    player_max_hp: int,
    potion_ids: list[int],
    player_gold: int,
    encounter_rng_seed: int,
    obs_buf: ctypes.Array,
) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    relic_buf = (ctypes.c_int * len(relic_ids))(*relic_ids)
    potion_buf = (ctypes.c_int * len(potion_ids))(*potion_ids)
    _lib.Sts2_ResetRunCombat(
        handle,
        deck_buf,
        len(deck_ids),
        encounter_id,
        relic_buf,
        len(relic_ids),
        player_hp,
        player_max_hp,
        potion_buf,
        len(potion_ids),
        player_gold,
        encounter_rng_seed,
        obs_buf,
    )


def reset_run_combat_pre_shuffled(
    handle: int,
    deck_ids: list[int],
    encounter_id: int,
    relic_ids: list[int],
    player_hp: int,
    player_max_hp: int,
    potion_ids: list[int],
    player_gold: int,
    shuffle_rng_seed: int,
    shuffle_pre_skip: int,
    niche_skip_count: int,
    encounter_rng_seed: int,
    monster_ai_rng_seed: int,
    obs_buf: ctypes.Array,
) -> None:
    deck_buf = (ctypes.c_int * len(deck_ids))(*deck_ids)
    relic_buf = (ctypes.c_int * len(relic_ids))(*relic_ids)
    potion_buf = (ctypes.c_int * len(potion_ids))(*potion_ids)
    _lib.Sts2_ResetRunCombatPreShuffled(
        handle,
        deck_buf,
        len(deck_ids),
        encounter_id,
        relic_buf,
        len(relic_ids),
        player_hp,
        player_max_hp,
        potion_buf,
        len(potion_ids),
        player_gold,
        shuffle_rng_seed,
        shuffle_pre_skip,
        niche_skip_count,
        encounter_rng_seed,
        monster_ai_rng_seed,
        obs_buf,
    )


def step(
    handle: int,
    action: int,
    obs_buf: ctypes.Array,
    reward_buf: ctypes.Array,
) -> bool:
    terminal = _lib.Sts2_Step(handle, action, obs_buf, reward_buf)
    return bool(terminal)


def valid_actions(handle: int, max_actions: int) -> ctypes.Array:
    buf = (ctypes.c_int * max_actions)()
    _lib.Sts2_ValidActions(handle, buf, max_actions)
    return buf


def player_won(handle: int) -> bool:
    return bool(_lib.Sts2_PlayerWon(handle))


def encounter_id(handle: int) -> int:
    return int(_lib.Sts2_EncounterId(handle))


def get_niche_rng_call_count(handle: int) -> int:
    """Return the total Next() calls on the niche (main combat) RNG during this combat.

    The caller should accumulate this across combats to compute nicheSkipCount
    for the next combat, so each combat's HP and intent rolls use the correct
    position in the shared niche RNG stream.
    """
    return int(_lib.Sts2_GetNicheRngCallCount(handle))


def get_shuffle_rng_call_count(handle: int) -> int:
    """Return the total Next() calls on the shuffle RNG since combat started.

    This includes the deckLen-1 initial skip consumed during pre-shuffle setup.
    The caller should subtract (deck_len - 1) to get only the mid-combat extra
    advances, then advance the Python-side _run_rng_set.shuffle GameRng by that
    amount so subsequent combats start from the correct shuffle RNG position.
    """
    return int(_lib.Sts2_GetShuffleRngCallCount(handle))


def destroy(handle: int) -> None:
    _lib.Sts2_Destroy(handle)
