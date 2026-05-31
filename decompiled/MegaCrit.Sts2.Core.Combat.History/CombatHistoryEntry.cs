using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace MegaCrit.Sts2.Core.Combat.History;

public abstract class CombatHistoryEntry
{
    private readonly Dictionary<ulong, int> _playerTurnNumbers = new Dictionary<ulong, int>();

    public Creature Actor { get; }

    private int RoundNumber { get; }

    private CombatSide CurrentSide { get; }

    public CombatHistory History { get; }

    public string HumanReadableString => $"Rd {RoundNumber} ({CurrentSide} turn): {Description}.";

    public abstract string Description { get; }

    protected CombatHistoryEntry(
        Creature actor,
        int roundNumber,
        CombatSide currentSide,
        CombatHistory history,
        IEnumerable<Player> players
    )
    {
        Actor = actor;
        RoundNumber = roundNumber;
        CurrentSide = currentSide;
        History = history;
        foreach (Player player in players)
        {
            _playerTurnNumbers[player.NetId] = player.PlayerCombatState.TurnNumber;
        }
    }

    public bool HappenedThisTurn(ICombatState? state)
    {
        if (state == null)
        {
            return false;
        }
        if (RoundNumber != state.RoundNumber)
        {
            return false;
        }
        if (CurrentSide != state.CurrentSide)
        {
            return false;
        }
        foreach (KeyValuePair<ulong, int> playerTurnNumber in _playerTurnNumbers)
        {
            playerTurnNumber.Deconstruct(out var key, out var value);
            ulong playerId = key;
            int num = value;
            Player? player = state.GetPlayer(playerId);
            if (player != null)
            {
                int? num2 = player.PlayerCombatState?.TurnNumber;
                value = num;
                if (num2 == value)
                {
                    continue;
                }
            }
            return false;
        }
        return true;
    }

    public bool HappenedLastPlayerTurn(Player player)
    {
        if (!_playerTurnNumbers.TryGetValue(player.NetId, out var value))
        {
            return false;
        }
        int num = value;
        PlayerCombatState? playerCombatState = player.PlayerCombatState;
        return num
            == (
                (playerCombatState != null)
                    ? new int?(playerCombatState.TurnNumber - 1)
                    : ((int?)null)
            );
    }
}
