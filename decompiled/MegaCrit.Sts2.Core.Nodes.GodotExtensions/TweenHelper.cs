using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace MegaCrit.Sts2.Core.Nodes.GodotExtensions;

public static class TweenHelper
{
	public static void FastForwardToCompletion(this Tween t)
	{
		t.CustomStep(999999999.0);
	}

	public static async Task<bool> AwaitFinished(this Tween tween, Node owner)
	{
		if (!(await AwaitFinishedInternal(tween, owner)))
		{
			return false;
		}
		return GodotObject.IsInstanceValid(owner) && owner.IsInsideTree();
	}

	private static Task<bool> AwaitFinishedInternal(Tween tween, Node owner)
	{
		if (!tween.IsValid() || !tween.IsRunning())
		{
			return Task.FromResult(result: true);
		}
		TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		bool resolved = false;
		tween.Finished += OnFinished;
		owner.TreeExiting += OnExiting;
		if (!tween.IsValid() || !tween.IsRunning())
		{
			OnFinished();
		}
		else if (!owner.IsInsideTree())
		{
			OnExiting();
		}
		return tcs.Task;
		void OnExiting()
		{
			if (!resolved)
			{
				resolved = true;
				if (tween.IsValid())
				{
					tween.Finished -= OnFinished;
				}
				if (GodotObject.IsInstanceValid(owner))
				{
					owner.TreeExiting -= OnExiting;
				}
				tcs.TrySetResult(result: false);
			}
		}
		void OnFinished()
		{
			if (!resolved)
			{
				resolved = true;
				if (tween.IsValid())
				{
					tween.Finished -= OnFinished;
				}
				if (GodotObject.IsInstanceValid(owner))
				{
					owner.TreeExiting -= OnExiting;
				}
				tcs.TrySetResult(result: true);
			}
		}
	}

	public static Task AwaitFinished(this Tween tween, CancellationToken ct)
	{
		TaskCompletionSource tcs = new TaskCompletionSource();
		int unsubscribed = 0;
		CancellationTokenRegistration ctr = default(CancellationTokenRegistration);
		tween.Finished += OnFinished;
		if (ct.CanBeCanceled)
		{
			ctr = ct.Register(delegate
			{
				if (Interlocked.Exchange(ref unsubscribed, 1) == 0)
				{
					tween.Finished -= OnFinished;
				}
				tcs.TrySetCanceled(ct);
			});
		}
		return tcs.Task;
		void OnFinished()
		{
			if (Interlocked.Exchange(ref unsubscribed, 1) == 0)
			{
				tween.Finished -= OnFinished;
			}
			ctr.Dispose();
			tcs.TrySetResult();
		}
	}
}
