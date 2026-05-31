using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace MegaCrit.Sts2.Core.Multiplayer.Game;

public class RewardsSetSynchronizer : IDisposable
{
    private enum RewardSetCompleteState
    {
        None,
        Completed,
        Skipped,
    }

    private record struct BufferedMessage
    {
        public int SetId =>
            selectedMessage?.setId
            ?? (skippedMessage ?? throw new InvalidOperationException()).setId;

        public RewardSelectedMessage? selectedMessage;

        public RewardSetSkippedMessage? skippedMessage;

        public ulong senderId;
    }

    private class PlayerRewardState
    {
        public required List<RewardsSetState> rewardsStack;

        public required List<BufferedMessage> bufferedMessages;

        public readonly Dictionary<int, RewardSetCompleteState> completedRewards =
            new Dictionary<int, RewardSetCompleteState>();

        public int nextId;
    }

    private class RewardsSetState
    {
        public required RewardsSet set;

        public required TaskCompletionSource completionSource;
    }

    private readonly RunLocationTargetedMessageBuffer _messageBuffer;

    private readonly INetGameService _netService;

    private readonly IPlayerCollection _playerCollection;

    private readonly ulong _localPlayerId;

    private readonly Logger _logger = new Logger("RewardsSetSynchronizer", LogType.GameSync);

    private readonly List<PlayerRewardState> _rewardStates = new List<PlayerRewardState>();

    private Player LocalPlayer => _playerCollection.GetPlayer(_localPlayerId);

    public RewardsSetSynchronizer(
        RunLocationTargetedMessageBuffer messageBuffer,
        INetGameService netService,
        IPlayerCollection playerCollection,
        ulong localPlayerId
    )
    {
        _netService = netService;
        _playerCollection = playerCollection;
        _localPlayerId = localPlayerId;
        foreach (Player player in playerCollection.Players)
        {
            _rewardStates.Add(
                new PlayerRewardState
                {
                    rewardsStack = new List<RewardsSetState>(),
                    bufferedMessages = new List<BufferedMessage>(),
                }
            );
        }
        _messageBuffer = messageBuffer;
        _messageBuffer.RegisterMessageHandler<RewardSelectedMessage>(HandleRewardSelectedMessage);
        _messageBuffer.RegisterMessageHandler<RewardSetSkippedMessage>(
            HandleRewardSetSkippedMessage
        );
    }

    public void Dispose()
    {
        _messageBuffer.UnregisterMessageHandler<RewardSelectedMessage>(HandleRewardSelectedMessage);
        _messageBuffer.UnregisterMessageHandler<RewardSetSkippedMessage>(
            HandleRewardSetSkippedMessage
        );
    }

    private PlayerRewardState GetRewardStateForPlayer(Player player)
    {
        return _rewardStates[_playerCollection.GetPlayerSlotIndex(player)];
    }

    public Task BeginRewardsSet(RewardsSet set)
    {
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(set.Player);
        set.Id = rewardStateForPlayer.nextId;
        rewardStateForPlayer.nextId++;
        _logger.Debug($"Beginning rewards set {set}");
        TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
        RewardsSetState rewardsSetState = new RewardsSetState
        {
            set = set,
            completionSource = taskCompletionSource,
        };
        rewardStateForPlayer.rewardsStack.Add(rewardsSetState);
        foreach (BufferedMessage item in rewardStateForPlayer.bufferedMessages.ToList())
        {
            if (set.Id == item.SetId)
            {
                if (item.selectedMessage != null)
                {
                    _logger.Debug("Handling buffered RewardSelectedMessage");
                    HandleRewardSelectedMessage(item.selectedMessage, item.senderId);
                }
                else if (item.skippedMessage != null)
                {
                    _logger.Debug("Handling buffered RewardSetSkippedMessage");
                    HandleRewardSetSkippedMessage(item.skippedMessage, item.senderId);
                }
                rewardStateForPlayer.bufferedMessages.Remove(item);
            }
        }
        CompleteRewardsSetIfNecessary(rewardsSetState);
        return taskCompletionSource.Task;
    }

    public async Task<bool> SelectLocalReward(Reward reward)
    {
        if (reward.Player != LocalPlayer)
        {
            throw new InvalidOperationException(
                $"{"SelectLocalReward"} called for reward {reward} with non-local player {reward.Player.NetId}! This is not allowed"
            );
        }
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(LocalPlayer);
        if (rewardStateForPlayer.rewardsStack.Count <= 0)
        {
            throw new InvalidOperationException(
                "Tried to sync reward for local player, but they are not currently viewing any reward set!"
            );
        }
        RewardsSetState rewardsSetState = rewardStateForPlayer.rewardsStack.Last();
        RewardsSet set = rewardsSetState.set;
        int rewardIndex = set.Rewards.IndexOf(reward);
        RewardSelectedMessage message = new RewardSelectedMessage
        {
            location = _messageBuffer.CurrentLocation,
            setId = rewardsSetState.set.Id,
            rewardIndex = rewardIndex,
        };
        _netService.SendMessage(message);
        return await SelectRewardForPlayer(rewardsSetState, reward);
    }

    public void SkipLocalRewardsSet()
    {
        _logger.Debug("Skipping local RewardsSet");
        RewardsSetState rewardsSetState = SkipRewardsSetOnStackTopForPlayer(LocalPlayer);
        RewardSetSkippedMessage message = new RewardSetSkippedMessage
        {
            location = _messageBuffer.CurrentLocation,
            setId = rewardsSetState.set.Id,
        };
        _netService.SendMessage(message);
    }

    public void HandleRewardSelectedMessage(RewardSelectedMessage message, ulong senderId)
    {
        _logger.Debug(
            $"Received {"RewardSelectedMessage"} from player {senderId}, set id: {message.setId} reward index: {message.rewardIndex}"
        );
        Player player = _playerCollection.GetPlayer(senderId);
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(player);
        if (rewardStateForPlayer.nextId <= message.setId)
        {
            _logger.Debug(
                $"Buffering {"RewardSelectedMessage"} because RewardsSet id {message.setId} hasn't been created yet"
            );
            rewardStateForPlayer.bufferedMessages.Add(
                new BufferedMessage { selectedMessage = message, senderId = senderId }
            );
        }
        else
        {
            TaskHelper.RunSafely(SelectRewardForPlayer(player, message.rewardIndex));
        }
    }

    public void HandleRewardSetSkippedMessage(RewardSetSkippedMessage message, ulong senderId)
    {
        _logger.Debug(
            $"Received {"RewardSetSkippedMessage"} from player {senderId}, set id: {message.setId}"
        );
        Player player = _playerCollection.GetPlayer(senderId);
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(player);
        if (rewardStateForPlayer.nextId <= message.setId)
        {
            _logger.Debug(
                $"Buffering {"RewardSetSkippedMessage"} because RewardsSet id {message.setId} hasn't been created yet"
            );
            rewardStateForPlayer.bufferedMessages.Add(
                new BufferedMessage { skippedMessage = message, senderId = senderId }
            );
        }
        else
        {
            SkipRewardsSetOnStackTopForPlayer(_playerCollection.GetPlayer(senderId));
        }
    }

    private async Task SelectRewardForPlayer(Player player, int rewardIndex)
    {
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(player);
        if (rewardStateForPlayer.rewardsStack.Count <= 0)
        {
            throw new InvalidOperationException(
                $"Tried to select reward for player {player.NetId}, but they are not currently viewing any reward set!"
            );
        }
        RewardsSetState rewardsSetState = rewardStateForPlayer.rewardsStack.Last();
        RewardsSet set = rewardsSetState.set;
        if (rewardIndex < 0 || rewardIndex >= set.Rewards.Count)
        {
            throw new InvalidOperationException(
                $"Tried to select reward index {rewardIndex} for player {player.NetId}, but it is out of bounds in their current rewards set {set}!"
            );
        }
        Reward reward = set.Rewards[rewardIndex];
        await SelectRewardForPlayer(rewardsSetState, reward);
    }

    private async Task<bool> SelectRewardForPlayer(RewardsSetState setState, Reward reward)
    {
        _logger.Debug($"Selecting reward {reward} for player {reward.Player.NetId}");
        bool result = await reward.SelectUnsynchronized();
        CompleteRewardsSetIfNecessary(setState);
        return result;
    }

    private void CompleteRewardsSetIfNecessary(RewardsSetState setState)
    {
        if (setState.set.AllRewardsSuccessfullySelected)
        {
            CompleteRewardsSet(setState, RewardSetCompleteState.Completed);
        }
    }

    private RewardsSetState SkipRewardsSetOnStackTopForPlayer(Player player)
    {
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(player);
        if (rewardStateForPlayer.rewardsStack.Count <= 0)
        {
            throw new InvalidOperationException(
                $"Tried to skip reward set for player {player.NetId}, but they are not currently viewing any reward set!"
            );
        }
        List<RewardsSetState> rewardsStack = rewardStateForPlayer.rewardsStack;
        RewardsSetState rewardsSetState = rewardsStack[rewardsStack.Count - 1];
        SkipRewardsSet(rewardsSetState);
        return rewardsSetState;
    }

    private void SkipRewardsSet(RewardsSetState setState)
    {
        foreach (Reward reward in setState.set.Rewards)
        {
            if (!reward.SuccessfullySelected)
            {
                reward.OnSkipped();
            }
        }
        CompleteRewardsSet(setState, RewardSetCompleteState.Skipped);
    }

    private void CompleteRewardsSet(RewardsSetState setState, RewardSetCompleteState completeState)
    {
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(setState.set.Player);
        if (IsRewardsSetCompleted(setState.set))
        {
            _logger.Error(
                $"Reward set with id {setState.set} is already finished (state {rewardStateForPlayer.completedRewards[setState.set.Id]})!"
            );
        }
        else
        {
            rewardStateForPlayer.rewardsStack.Remove(setState);
            rewardStateForPlayer.completedRewards[setState.set.Id] = completeState;
            setState.completionSource.SetResult();
            _logger.Debug($"Reward set {setState.set} completed with state: {completeState}");
        }
    }

    public bool IsRewardsSetCompleted(RewardsSet set)
    {
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(set.Player);
        if (set.Id < 0)
        {
            return false;
        }
        return rewardStateForPlayer.completedRewards.ContainsKey(set.Id);
    }

    public bool IsRewardsSetCompleted(Player player, int id)
    {
        PlayerRewardState rewardStateForPlayer = GetRewardStateForPlayer(player);
        return rewardStateForPlayer.completedRewards.ContainsKey(id);
    }

    public void BeforeLeavingRoom()
    {
        for (int i = 0; i < _rewardStates.Count; i++)
        {
            PlayerRewardState playerRewardState = _rewardStates[i];
            if (playerRewardState.rewardsStack.Count > 0)
            {
                _logger.Debug(
                    $"Skipping remaining rewards for player {_playerCollection.Players[i].NetId} because we're exiting the room"
                );
            }
            for (int num = playerRewardState.rewardsStack.Count - 1; num >= 0; num--)
            {
                SkipRewardsSet(playerRewardState.rewardsStack[num]);
            }
            playerRewardState.rewardsStack.Clear();
        }
    }

    public IEnumerable<int> GetNextRewardIds()
    {
        foreach (PlayerRewardState rewardState in _rewardStates)
        {
            yield return rewardState.nextId;
        }
    }
}
