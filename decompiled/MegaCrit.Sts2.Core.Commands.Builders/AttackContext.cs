using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace MegaCrit.Sts2.Core.Commands.Builders;

public sealed class AttackContext : IAsyncDisposable
{
	private readonly ICombatState _combatState;

	private readonly PlayerChoiceContext _choiceContext;

	private readonly AttackCommand _attackCommand;

	private bool _disposed;

	private AttackContext(ICombatState combatState, PlayerChoiceContext choiceContext, CardModel cardSource)
	{
		_combatState = combatState;
		_choiceContext = choiceContext;
		_attackCommand = new AttackCommand(0m).FromCard(cardSource).TargetingAllOpponents(combatState);
	}

	public static async Task<AttackContext> CreateAsync(ICombatState combatState, PlayerChoiceContext choiceContext, CardModel cardSource)
	{
		AttackContext context = new AttackContext(combatState, choiceContext, cardSource);
		await Hook.BeforeAttack(combatState, context._attackCommand);
		return context;
	}

	public void AddHit(IEnumerable<DamageResult> results)
	{
		_attackCommand.IncrementHitsInternal();
		_attackCommand.AddResultsInternal(results);
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		try
		{
			await Hook.AfterAttack(_combatState, _choiceContext, _attackCommand);
		}
		catch (Exception ex)
		{
			Log.Error(ex.ToString());
		}
	}
}
