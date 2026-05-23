using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;

namespace Sts2Emulator.Interop;

public static class CombatFactory
{
    // Ironclad starting deck card IDs (from Generated/Cards.g.cs).
    private const int StrikeId = IC.StrikeIronclad; // 472
    private const int DefendId = IC.DefendIronclad;  // 131

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
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        state.PlayerBlock  = 0;
        state.PlayerBuffs  = [];
        state.Hand         = [];
        state.DiscardPile  = [];
        state.ExhaustPile  = [];
        state.PotionSlots  = new int[3];
        state.Turn         = 0;
        state.PlayerTurn   = true;

        // Standard Ironclad starter deck: 5 Strikes + 4 Defends.
        state.DrawPile = [
            .. Enumerable.Repeat(new CardInstance(StrikeId, false), 5),
            .. Enumerable.Repeat(new CardInstance(DefendId, false), 4),
        ];

        // Single CalcifiedCultist encounter (Act 1, normal difficulty).
        int cultistHp = rng.Next(38, 42); // 38–41 inclusive
        state.Enemies =
        [
            new EnemyState
            {
                DefId         = KE.CalcifiedCultist, // 14
                Hp            = cultistHp,
                MaxHp         = cultistHp,
                Block         = 0,
                CurrentIntent = new Intent(IntentType.Buff, 0), // starts with Incantation
                Buffs         = [],
                MoveIndex     = 0,
            }
        ];

        // Shuffle draw pile and deal opening hand of 5.
        state.DrawPile = state.DrawPile.OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < 5 && state.DrawPile.Count > 0; i++)
        {
            state.Hand.Add(state.DrawPile[0]);
            state.DrawPile.RemoveAt(0);
        }
    }
}
