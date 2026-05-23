"""ctypes bindings to the NativeAOT-compiled Sts2Emulator native library."""

import ctypes
import os
import sys
from pathlib import Path

_LIB_NAMES = {
    "win32":  "Sts2Emulator.dll",
    "linux":  "Sts2Emulator.so",
    "darwin": "Sts2Emulator.dylib",
}

def _load_lib() -> ctypes.CDLL:
    name = _LIB_NAMES.get(sys.platform)
    if name is None:
        raise RuntimeError(f"Unsupported platform: {sys.platform}")

    search = [
        Path(__file__).parent.parent.parent / "out" / name,
        Path(os.environ.get("STS2_LIB_PATH", "")) / name,
    ]
    for path in search:
        if path.exists():
            return ctypes.CDLL(str(path))
    raise FileNotFoundError(
        f"Could not find {name}. Run scripts/build.sh first, or set STS2_LIB_PATH."
    )

_lib = _load_lib()

# ── function signatures ───────────────────────────────────────────────────────

_lib.Sts2_ObsSize.restype       = ctypes.c_int
_lib.Sts2_ObsSize.argtypes      = []

_lib.Sts2_Create.restype        = ctypes.c_int
_lib.Sts2_Create.argtypes       = [ctypes.c_int]

_lib.Sts2_Reset.restype         = None
_lib.Sts2_Reset.argtypes        = [ctypes.c_int, ctypes.POINTER(ctypes.c_int)]

_lib.Sts2_ResetEncounter.restype  = None
_lib.Sts2_ResetEncounter.argtypes = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeck.restype  = None
_lib.Sts2_ResetWithDeck.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_ResetWithDeckAndEncounter.restype  = None
_lib.Sts2_ResetWithDeckAndEncounter.argtypes = [
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
]

_lib.Sts2_Step.restype          = ctypes.c_int
_lib.Sts2_Step.argtypes         = [
    ctypes.c_int,
    ctypes.c_int,
    ctypes.POINTER(ctypes.c_int),
    ctypes.POINTER(ctypes.c_float),
]

_lib.Sts2_PlayerWon.restype     = ctypes.c_int
_lib.Sts2_PlayerWon.argtypes    = [ctypes.c_int]

_lib.Sts2_EncounterId.restype    = ctypes.c_int
_lib.Sts2_EncounterId.argtypes   = [ctypes.c_int]

_lib.Sts2_ActionCount.restype   = ctypes.c_int
_lib.Sts2_ActionCount.argtypes  = [ctypes.c_int]

_lib.Sts2_ValidActions.restype  = None
_lib.Sts2_ValidActions.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_int), ctypes.c_int]

_lib.Sts2_Destroy.restype       = None
_lib.Sts2_Destroy.argtypes      = [ctypes.c_int]

# ── public wrappers ───────────────────────────────────────────────────────────

OBS_SIZE: int = _lib.Sts2_ObsSize()


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


def destroy(handle: int) -> None:
    _lib.Sts2_Destroy(handle)
