using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Runs;

namespace MegaCrit.Sts2.Core.Combat;

public interface ICombatState
{
	IRunState RunState { get; }

	IReadOnlyList<Creature> Allies { get; }

	IReadOnlyList<Creature> Enemies { get; }

	IReadOnlyList<Creature> Creatures { get; }

	IReadOnlyList<Creature> PlayerCreatures { get; }

	IReadOnlyList<Player> Players { get; }

	IReadOnlyList<ModifierModel> Modifiers { get; }

	MultiplayerScalingModel? MultiplayerScalingModel { get; }

	int RoundNumber { get; set; }

	CombatSide CurrentSide { get; set; }

	EncounterModel? Encounter { get; }

	IReadOnlyList<Creature> EscapedCreatures { get; }

	IReadOnlyList<Creature> CreaturesOnCurrentSide { get; }

	IReadOnlyList<Creature> HittableEnemies { get; }

	event Action<ICombatState>? CreaturesChanged;

	T CreateCard<T>(Player owner) where T : CardModel;

	CardModel CreateCard(CardModel canonicalCard, Player owner);

	CardModel CloneCard(CardModel mutableCard);

	void AddCard(CardModel card, Player owner);

	void RemoveCard(CardModel card);

	bool ContainsCard(CardModel card);

	void AddPlayer(Player player);

	Creature CreateCreature(MonsterModel monster, CombatSide side, string? slot);

	void CreatureEscaped(Creature creature);

	void RemoveCreature(Creature creature, bool unattach = true);

	bool ContainsCreature(Creature creature);

	bool ContainsMonster<T>() where T : MonsterModel;

	Creature? GetCreature(uint? combatId);

	Task<Creature?> GetCreatureAsync(uint? combatId, double timeoutSec);

	IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side);

	IReadOnlyList<Creature> GetOpponentsOf(Creature creature);

	IReadOnlyList<Creature> GetTeammatesOf(Creature creature);

	Player? GetPlayer(ulong playerId);

	IEnumerable<AbstractModel> IterateHookListeners();

	void SortEnemiesBySlotName();

	void SetEnemyIndex(Creature creature, int index);

	void AddCreature(Creature creature);

	bool IsLiveCombat();
}
