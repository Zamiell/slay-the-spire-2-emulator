using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace MegaCrit.Sts2.Core.GameActions;

public class UndoEndPlayerTurnAction : GameAction
{
    private readonly Player _player;

    private readonly int _turnNumber;

    public override ulong OwnerId => _player.NetId;

    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

    public UndoEndPlayerTurnAction(Player player, int turnNumber)
    {
        _player = player;
        _turnNumber = turnNumber;
    }

    protected override Task ExecuteAction()
    {
        int turnNumber = _player.PlayerCombatState.TurnNumber;
        if (turnNumber == _turnNumber)
        {
            CombatManager.Instance.UndoReadyToEndTurn(_player);
        }
        else
        {
            Log.Info(
                $"Ignoring undo end turn action. Current turn number: {turnNumber} action turn number: {_turnNumber} CombatState: {RunManager.Instance.ActionQueueSynchronizer.CombatState}"
            );
        }
        return Task.CompletedTask;
    }

    public override INetAction ToNetAction()
    {
        return new NetUndoEndPlayerTurnAction { turnNumber = _turnNumber };
    }

    public override string ToString()
    {
        return $"{"UndoEndPlayerTurnAction"} for player {_player.NetId} turn {_turnNumber}";
    }
}
