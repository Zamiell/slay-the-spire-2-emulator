"""Game-compatible RNG implementation matching STS2's System.Random seeding."""

from __future__ import annotations

import ctypes
import re


def _int32(x: int) -> int:
    return ctypes.c_int32(x).value


def _uint32(x: int) -> int:
    return ctypes.c_uint32(x).value


def get_deterministic_hash_code(s: str) -> int:
    """Matches C# StringHelper.GetDeterministicHashCode (int32 overflow semantics)."""
    num = _int32(352654597)
    num2 = _int32(352654597)
    for i in range(0, len(s), 2):
        num = _int32(_int32(num << 5) + num ^ ord(s[i]))
        if i == len(s) - 1:
            break
        num2 = _int32(_int32(num2 << 5) + num2 ^ ord(s[i + 1]))
    return _int32(num + _int32(num2 * 1566083941))


_SNAKE_CASE_RE = re.compile(r"([a-z0-9])([A-Z])")


def snake_case(s: str) -> str:
    """Matches C# StringHelper.SnakeCase (CamelCase → snake_case)."""
    return _SNAKE_CASE_RE.sub(r"\1_\2", s.strip()).lower()


_MBIG = 2147483647  # int.MaxValue
_MSEED = 161803398


def _init_seed_array(seed: int) -> list[int]:
    if seed == -2147483648:
        seed = _MBIG
    else:
        seed = abs(seed)
    arr = [0] * 56
    mj = _MSEED - seed
    arr[55] = mj
    mk = 1
    ii = 0
    for i in range(1, 55):
        ii += 21
        if ii >= 55:
            ii -= 55
        arr[ii] = mk
        mk = mj - mk
        if mk < 0:
            mk += _MBIG
        mj = arr[ii]
    for _ in range(1, 5):
        for i in range(1, 56):
            n = i + 30
            if n >= 55:
                n -= 55
            # C# uses int32 (unchecked overflow) arithmetic here
            arr[i] = _int32(arr[i] - arr[1 + n])
            if arr[i] < 0:
                arr[i] += _MBIG
    return arr


class DotNetRandom:
    """System.Random-compatible seeded PRNG using the legacy Knuth subtractive algorithm."""

    def __init__(self, seed: int) -> None:
        self._inext = 0
        self._inextp = 21
        self._arr = _init_seed_array(seed)

    def _sample(self) -> int:
        inext = self._inext + 1
        if inext >= 56:
            inext = 1
        inextp = self._inextp + 1
        if inextp >= 56:
            inextp = 1
        ret = self._arr[inext] - self._arr[inextp]
        if ret == _MBIG:
            ret -= 1
        if ret < 0:
            ret += _MBIG
        self._arr[inext] = ret
        self._inext = inext
        self._inextp = inextp
        return ret

    def next_int(self, max_exclusive: int) -> int:
        return int(self._sample() * (1.0 / _MBIG) * max_exclusive)

    def next_double(self) -> float:
        return self._sample() * (1.0 / _MBIG)

    def next_bool(self) -> bool:
        return self.next_int(2) == 0


class GameRng:
    """Wrapper matching the Rng class interface from STS2."""

    def __init__(self, seed: int, name: str = "") -> None:
        if name:
            raw_seed = _uint32(seed + _uint32(get_deterministic_hash_code(name)))
        else:
            raw_seed = _uint32(seed)
        self._rng = DotNetRandom(_int32(raw_seed))

    def next_int(self, max_exclusive: int) -> int:
        return self._rng.next_int(max_exclusive)

    def next_bool(self) -> bool:
        return self._rng.next_bool()

    def next_double(self) -> float:
        return self._rng.next_double()

    def next_item(self, items: list) -> object:
        n = len(items)
        if n == 0:
            return None
        return items[self._rng.next_int(n)]

    def next_gaussian_int(self, mean: int, std_dev: int, min_val: int, max_val: int) -> int:
        """Box-Muller Gaussian sample clamped to [min_val, max_val], matching Rng.NextGaussianInt."""
        import math
        while True:
            d = 1.0 - self._rng.next_double()
            num = 1.0 - self._rng.next_double()
            num2 = math.sqrt(-2.0 * math.log(d)) * math.sin(math.pi * 2.0 * num)
            result = int(round(mean + std_dev * num2))
            if min_val <= result <= max_val:
                return result

    def shuffle(self, lst: list) -> None:
        """Fisher-Yates in-place shuffle matching Rng.Shuffle / UnstableShuffle."""
        for i in range(len(lst) - 1, 0, -1):
            j = self.next_int(i + 1)
            lst[i], lst[j] = lst[j], lst[i]

    def stable_shuffle(self, lst: list, key=None) -> None:
        """Sort then Fisher-Yates in-place, matching StableShuffle<T>."""
        lst.sort(key=key)
        self.shuffle(lst)

    def shuffled(self, lst: list) -> list:
        result = list(lst)
        self.shuffle(result)
        return result


class RunRngSet:
    """Creates the named subsystem RNGs from a string seed, matching RunRngSet."""

    _RNG_NAMES = [
        "up_front",
        "shuffle",
        "unknown_map_point",
        "combat_card_generation",
        "combat_potion_generation",
        "combat_card_selection",
        "combat_energy_costs",
        "combat_targets",
        "monster_ai",
        "niche",
        "combat_orbs",
        "treasure_room_relics",
    ]

    def __init__(self, string_seed: str) -> None:
        self.string_seed = string_seed
        self.seed = _uint32(get_deterministic_hash_code(string_seed))
        self._rngs: dict[str, GameRng] = {}
        for name in self._RNG_NAMES:
            self._rngs[name] = GameRng(self.seed, name)

    def act_map_rng(self, act_index: int = 0) -> GameRng:
        """Returns a fresh act-map RNG matching new Rng(seed, 'act_N_map')."""
        name = f"act_{act_index + 1}_map"
        return GameRng(self.seed, name)

    @property
    def up_front(self) -> GameRng:
        return self._rngs["up_front"]

    @property
    def shuffle(self) -> GameRng:
        return self._rngs["shuffle"]

    def neow_rng(self, net_id: int = 1) -> GameRng:
        """Returns a fresh Neow-specific RNG matching EventModel's per-event RNG.

        net_id is 1 for singleplayer (NetSingleplayerGameService.defaultNetId).
        """
        neow_hash = _uint32(get_deterministic_hash_code("NEOW"))
        seed = _uint32(self.seed + net_id + neow_hash)
        raw = GameRng.__new__(GameRng)
        raw._rng = DotNetRandom(_int32(seed))
        return raw


class PlayerRngSet:
    """Per-player RNG subsystems matching PlayerRngSet in the game.

    Seeded as (uint)((ulong)GetDeterministicHashCode(string_seed) + net_id).
    Each subsystem is a GameRng(player_seed, name) where name is snake_case of
    the PlayerRngType enum value.
    """

    def __init__(self, run_rng_set: RunRngSet, net_id: int = 1) -> None:
        player_seed = _uint32(run_rng_set.seed + net_id)
        self.rewards = GameRng(player_seed, "rewards")
