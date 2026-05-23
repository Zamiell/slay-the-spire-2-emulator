using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sts2Emulator.Core;

namespace Sts2Emulator.Interop;

// Observation vector layout (OBS_SIZE ints):
//   [0]        player_hp
//   [1]        player_max_hp
//   [2]        player_block
//   [3]        energy
//   [4]        max_energy
//   [5]        draw_pile_size
//   [6]        discard_pile_size
//   [7]        exhaust_pile_size
//   [8..17]    hand slots: card_def_id (0 = empty), 5 cards × 2 ints (id, upgraded)
//   [18..23]   potion slots: potion_def_id (0 = empty), 3 slots × 2 ints (id, has_potion)
//   [34..53]   player buffs: 10 slots × 2 ints (buff_id, magnitude)
//   [54..68]   enemy 0: hp, max_hp, block, intent_type, intent_mag, 5 buff slots × 2 ints
//   [69..83]   enemy 1 (same layout)
//   [84..98]   enemy 2 (same layout)
//   [99..163]  reserved
//
// Total: 164 ints. Enemies beyond index 2 are ignored for now.

public static class NativeExports
{
    public const int OBS_SIZE = 164;
    public const int MAX_HAND = 10;
    public const int MAX_ENEMIES = 3;
    public const int MAX_PLAYER_BUFFS = 10;
    public const int MAX_ENEMY_BUFFS = 5;
    private static ReadOnlySpan<int> StarterDeckIds =>
    [
        472, 472, 472, 472, 472,
        131, 131, 131, 131,
        30,
        10001,
    ];

    private sealed class NativeCombat
    {
        public readonly int Seed;
        public readonly CombatState State = new();
        public Random Rng { get; private set; }
        public bool LastPlayerWon { get; set; }

        public NativeCombat(int seed)
        {
            Seed = seed;
            Rng = new Random(seed);
            CombatFactory.Reset(State, Rng);
        }

        public void Reset()
        {
            Rng = new Random(Seed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng);
        }

        public void Reset(ReadOnlySpan<int> deckIds)
        {
            Rng = new Random(Seed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng, deckIds);
        }

        public void Reset(ReadOnlySpan<int> deckIds, int encounterId)
        {
            Rng = new Random(Seed);
            LastPlayerWon = false;
            CombatFactory.Reset(State, Rng, deckIds, encounterId);
        }
    }

    private static readonly NativeCombat?[] _pool = new NativeCombat?[256];

    [UnmanagedCallersOnly]
    public static int Sts2_ObsSize() => OBS_SIZE;

    [UnmanagedCallersOnly]
    public static int Sts2_Create(int seed)
    {
        var combat = new NativeCombat(seed);
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i] is null)
            {
                _pool[i] = combat;
                return i;
            }
        }
        return -1; // pool exhausted
    }

    [UnmanagedCallersOnly]
    public static unsafe void Sts2_Reset(int handle, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset();
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly]
    public static unsafe void Sts2_ResetEncounter(int handle, int encounterId, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset(StarterDeckIds, encounterId);
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly]
    public static unsafe void Sts2_ResetWithDeck(int handle, int* deckIds, int deckLen, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset(new ReadOnlySpan<int>(deckIds, deckLen));
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly]
    public static unsafe void Sts2_ResetWithDeckAndEncounter(
        int handle, int* deckIds, int deckLen, int encounterId, int* obsBuf)
    {
        var combat = _pool[handle]!;
        combat.Reset(new ReadOnlySpan<int>(deckIds, deckLen), encounterId);
        WriteObs(combat.State, obsBuf);
    }

    [UnmanagedCallersOnly]
    public static unsafe int Sts2_Step(int handle, int action, int* obsBuf, float* rewardOut)
    {
        var combat = _pool[handle]!;
        var result = CombatEngine.Step(combat.State, action, combat.Rng);
        combat.LastPlayerWon = result.Terminal && result.PlayerWon;
        WriteObs(combat.State, obsBuf);
        *rewardOut = result.Reward;
        return result.Terminal ? 1 : 0;
    }

    [UnmanagedCallersOnly]
    public static int Sts2_PlayerWon(int handle)
    {
        return _pool[handle]!.LastPlayerWon ? 1 : 0;
    }

    [UnmanagedCallersOnly]
    public static int Sts2_EncounterId(int handle)
    {
        return _pool[handle]!.State.EncounterId;
    }

    [UnmanagedCallersOnly]
    public static int Sts2_ActionCount(int handle)
    {
        return CombatEngine.ValidActions(_pool[handle]!.State).Length;
    }

    [UnmanagedCallersOnly]
    public static unsafe void Sts2_ValidActions(int handle, int* maskBuf, int maxActions)
    {
        var valid = CombatEngine.ValidActions(_pool[handle]!.State);
        for (int i = 0; i < maxActions; i++)
            maskBuf[i] = 0;
        foreach (int a in valid)
            if (a < maxActions) maskBuf[a] = 1;
    }

    [UnmanagedCallersOnly]
    public static void Sts2_Destroy(int handle)
    {
        _pool[handle] = null;
    }

    // ── observation serialisation ─────────────────────────────────────────────

    private static unsafe void WriteObs(CombatState s, int* o)
    {
        o[0] = s.PlayerHp;
        o[1] = s.PlayerMaxHp;
        o[2] = s.PlayerBlock;
        o[3] = s.Energy;
        o[4] = s.MaxEnergy;
        o[5] = s.DrawPile.Count;
        o[6] = s.DiscardPile.Count;
        o[7] = s.ExhaustPile.Count;

        // Hand (up to MAX_HAND cards, each 2 ints)
        int base_ = 8;
        for (int i = 0; i < MAX_HAND; i++)
        {
            if (i < s.Hand.Count)
            {
                o[base_ + i * 2]     = s.Hand[i].DefId;
                o[base_ + i * 2 + 1] = s.Hand[i].Upgraded ? 1 : 0;
            }
            else
            {
                o[base_ + i * 2]     = 0;
                o[base_ + i * 2 + 1] = 0;
            }
        }

        // Potions (3 slots, each 2 ints: id, has_potion)
        base_ = 8 + MAX_HAND * 2;
        for (int i = 0; i < 3; i++)
        {
            o[base_ + i * 2]     = s.PotionSlots[i];
            o[base_ + i * 2 + 1] = s.PotionSlots[i] != 0 ? 1 : 0;
        }

        // Player buffs (MAX_PLAYER_BUFFS slots, each 2 ints: buff_id, magnitude)
        base_ = 8 + MAX_HAND * 2 + 6;
        for (int i = 0; i < MAX_PLAYER_BUFFS; i++)
        {
            if (i < s.PlayerBuffs.Count)
            {
                o[base_ + i * 2]     = (int)s.PlayerBuffs[i].Id;
                o[base_ + i * 2 + 1] = s.PlayerBuffs[i].Magnitude;
            }
            else
            {
                o[base_ + i * 2]     = 0;
                o[base_ + i * 2 + 1] = 0;
            }
        }

        // Enemies
        base_ = 8 + MAX_HAND * 2 + 6 + MAX_PLAYER_BUFFS * 2;
        int slotSize = 5 + MAX_ENEMY_BUFFS * 2;
        for (int e = 0; e < MAX_ENEMIES; e++)
        {
            int b = base_ + e * slotSize;
            if (e < s.Enemies.Count)
            {
                var en = s.Enemies[e];
                o[b]     = en.Hp;
                o[b + 1] = en.MaxHp;
                o[b + 2] = en.Block;
                o[b + 3] = (int)en.CurrentIntent.Type;
                o[b + 4] = en.CurrentIntent.Magnitude;
                for (int bi = 0; bi < MAX_ENEMY_BUFFS; bi++)
                {
                    if (bi < en.Buffs.Count)
                    {
                        o[b + 5 + bi * 2]     = (int)en.Buffs[bi].Id;
                        o[b + 5 + bi * 2 + 1] = en.Buffs[bi].Magnitude;
                    }
                    else
                    {
                        o[b + 5 + bi * 2]     = 0;
                        o[b + 5 + bi * 2 + 1] = 0;
                    }
                }
            }
            else
            {
                for (int j = 0; j < slotSize; j++) o[b + j] = 0;
            }
        }
    }
}
