using Sts2Emulator.Core;

namespace Sts2Emulator.Interop;

public static class CombatFactory
{
    public static CombatState NewCombat(int seed)
    {
        var state = new CombatState
        {
            PlayerHp    = 80,
            PlayerMaxHp = 80,
            Energy      = 3,
            MaxEnergy   = 3,
        };
        Reset(state, seed);
        return state;
    }

    public static void Reset(CombatState state, int? seed = null)
    {
        // Placeholder: build a minimal starting combat state.
        // Will be replaced with proper encounter selection once generated data is available.
        state.PlayerBlock  = 0;
        state.PlayerBuffs  = [];
        state.Hand         = [];
        state.DiscardPile  = [];
        state.ExhaustPile  = [];
        state.PotionSlots  = new int[3];
        state.Turn         = 0;
        state.PlayerTurn   = true;

        // Stub draw pile: 5 Strikes + 4 Defends
        state.DrawPile = [
            .. Enumerable.Repeat(new CardInstance(1, false), 5),
            .. Enumerable.Repeat(new CardInstance(2, false), 4),
        ];

        // Stub enemy: single Cultist
        state.Enemies = [
            new EnemyState
            {
                DefId          = 1,
                Hp             = 48,
                MaxHp          = 48,
                Block          = 0,
                CurrentIntent  = new Intent(IntentType.Buff, 0),
                Buffs          = [],
            }
        ];

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        // Initial draw
        var shuffled = state.DrawPile.OrderBy(_ => rng.Next()).ToList();
        state.DrawPile = shuffled;
        for (int i = 0; i < 5 && state.DrawPile.Count > 0; i++)
        {
            state.Hand.Add(state.DrawPile[0]);
            state.DrawPile.RemoveAt(0);
        }
    }
}
