using Sts2Emulator.Core;
using Sts2Emulator.Core.Effects;
using Sts2Emulator.Interop;
using Xunit;

namespace Sts2Emulator.Tests;

public class CombatEngineTests
{
    [Fact]
    public void NewCombat_StartsAtHighestDifficultyHp()
    {
        var state = CombatFactory.NewCombat(seed: 0);

        Assert.Equal(64, state.PlayerHp);
        Assert.Equal(80, state.PlayerMaxHp);
    }

    [Fact]
    public void NewCombat_StartsWithHighestDifficultyStarterDeck()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var cards = state.Hand
            .Concat(state.DrawPile)
            .Concat(state.DiscardPile)
            .Concat(state.ExhaustPile)
            .ToList();

        Assert.Equal(11, cards.Count);
        Assert.Equal(5, cards.Count(c => c.DefId == IC.StrikeIronclad));
        Assert.Equal(4, cards.Count(c => c.DefId == IC.DefendIronclad));
        Assert.Equal(1, cards.Count(c => c.DefId == IC.Bash));
        Assert.Equal(1, cards.Count(c => c.DefId == IC.AscendersBane));
    }

    [Fact]
    public void AscendersBane_IsNotPlayable()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.AscendersBane, false));

        var actions = CombatEngine.ValidActions(state);
        var result = CombatEngine.Step(state, 0, new Random(0));

        Assert.DoesNotContain(0, actions);
        Assert.Contains(1, actions);
        Assert.Equal(StepResult.Invalid, result);
    }

    [Fact]
    public void NewCombat_IsDeterministicForSameSeed()
    {
        var first = CombatFactory.NewCombat(seed: 123);
        var second = CombatFactory.NewCombat(seed: 123);

        Assert.Equal(first.Enemies[0].Hp, second.Enemies[0].Hp);
        Assert.Equal(first.Hand.Select(c => c.DefId), second.Hand.Select(c => c.DefId));
        Assert.Equal(first.DrawPile.Select(c => c.DefId), second.DrawPile.Select(c => c.DefId));
    }

    [Fact]
    public void Reset_RestoresHighestDifficultyStartingState()
    {
        var state = CombatFactory.NewCombat(seed: 123);
        state.PlayerHp = 1;
        state.Energy = 0;
        state.Hand.Clear();

        CombatFactory.Reset(state, seed: 123);

        Assert.Equal(64, state.PlayerHp);
        Assert.Equal(80, state.PlayerMaxHp);
        Assert.Equal(3, state.Energy);
        Assert.Equal(3, state.MaxEnergy);
        Assert.Equal(5, state.Hand.Count);
        Assert.Equal(6, state.DrawPile.Count);
    }

    [Fact]
    public void NewCombat_SamplesActOneWeakEncounterPools()
    {
        var states = Enumerable.Range(0, 64)
            .Select(seed => CombatFactory.NewCombat(seed))
            .ToList();
        var shapes = states
            .Select(state => (
                state.EncounterId,
                Count: state.Enemies.Count,
                Intents: string.Join(",", state.Enemies.Select(e => e.CurrentIntent.Type))
            ))
            .Distinct()
            .ToList();

        Assert.True(shapes.Count >= 6);
        Assert.DoesNotContain(states, s => s.Enemies.Any(e => e.DefId == 16)); // Chomper is not an opening easy encounter.
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 56)); // Nibbit
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 69)); // Seapunk
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 71)); // ShrinkerBeetle
        Assert.Contains(states, s => s.Enemies.Any(e => e.DefId == 93)); // Toadpole
    }

    [Fact]
    public void ChomperDebuff_AddsDazedToDiscard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 16,
            Hp = 60,
            MaxHp = 60,
            CurrentIntent = new Intent(IntentType.Debuff, 3),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(3, state.DiscardPile.Count(c => c.DefId == ST.Dazed));
    }

    [Fact]
    public void ForcedChompers_MatchesDecompiledOpening()
    {
        var state = new CombatState();
        CombatFactory.Reset(state, new Random(0), StarterDeckIds, encounterId: 1);

        Assert.Equal(1, state.EncounterId);
        Assert.Equal(2, state.Enemies.Count);
        Assert.All(state.Enemies, enemy =>
        {
            Assert.Equal(16, enemy.DefId);
            Assert.Equal(2, BuffSystem.Get(enemy.Buffs, BuffId.Artifact));
        });
        Assert.Equal(IntentType.Attack, state.Enemies[0].CurrentIntent.Type);
        Assert.Equal(18, state.Enemies[0].CurrentIntent.Magnitude);
        Assert.Equal(IntentType.Debuff, state.Enemies[1].CurrentIntent.Type);
        Assert.Equal(3, state.Enemies[1].CurrentIntent.Magnitude);
    }

    [Fact]
    public void ForcedCultists_MatchesDecompiledOpeningAndRitual()
    {
        var state = new CombatState();
        CombatFactory.Reset(state, new Random(0), StarterDeckIds, encounterId: 0);

        Assert.Equal(0, state.EncounterId);
        Assert.Collection(
            state.Enemies,
            enemy =>
            {
                Assert.Equal(14, enemy.DefId);
                Assert.Equal(IntentType.Buff, enemy.CurrentIntent.Type);
                EnemyAI.ExecuteIntent(enemy, state, new Random(0));
                Assert.Equal(2, BuffSystem.Get(enemy.Buffs, BuffId.Ritual));
                Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Strength));
            },
            enemy =>
            {
                Assert.Equal(21, enemy.DefId);
                Assert.Equal(IntentType.Buff, enemy.CurrentIntent.Type);
                EnemyAI.ExecuteIntent(enemy, state, new Random(0));
                Assert.Equal(6, BuffSystem.Get(enemy.Buffs, BuffId.Ritual));
                Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Strength));
            }
        );
    }

    [Fact]
    public void SlimeDebuff_AddsSlimedToDiscard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 47,
            Hp = 32,
            MaxHp = 32,
            CurrentIntent = new Intent(IntentType.Debuff, 2),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(2, state.DiscardPile.Count(c => c.DefId == ST.Slimed));
    }

    private static ReadOnlySpan<int> StarterDeckIds =>
    [
        IC.StrikeIronclad, IC.StrikeIronclad, IC.StrikeIronclad, IC.StrikeIronclad, IC.StrikeIronclad,
        IC.DefendIronclad, IC.DefendIronclad, IC.DefendIronclad, IC.DefendIronclad,
        IC.Bash,
        IC.AscendersBane,
    ];

    [Fact]
    public void Dazed_ExhaustsAtEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(ST.Dazed, false));

        CombatEngine.Step(state, 1, new Random(0));

        Assert.DoesNotContain(state.Hand, c => c.DefId == ST.Dazed);
        Assert.Contains(state.ExhaustPile, c => c.DefId == ST.Dazed);
        Assert.DoesNotContain(state.DiscardPile, c => c.DefId == ST.Dazed);
    }

    [Fact]
    public void Slimed_DrawsOneAndExhaustsWhenPlayed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Hand.Add(new CardInstance(ST.Slimed, false));
        state.DrawPile.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Contains(state.Hand, c => c.DefId == IC.StrikeIronclad);
        Assert.Contains(state.ExhaustPile, c => c.DefId == ST.Slimed);
    }

    [Fact]
    public void Artifact_PreventsEnemyDebuff()
    {
        var enemy = new EnemyState { Buffs = [] };
        BuffSystem.Apply(enemy.Buffs, BuffId.Artifact, 2);

        BuffSystem.Apply(enemy.Buffs, BuffId.Vulnerable, 2);

        Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Artifact));
        Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable));
    }

    [Fact]
    public void NibbitSlice_GainsBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 56,
            Hp = 44,
            MaxHp = 44,
            CurrentIntent = new Intent(IntentType.Attack, 7),
            Buffs = [],
            MoveIndex = 1,
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(6, enemy.Block);
    }

    [Fact]
    public void HardToKill_CapsDamagePerHit()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 25,
                Hp = 24,
                MaxHp = 24,
                CurrentIntent = new Intent(IntentType.Attack, 4),
                Buffs = [new BuffState(BuffId.HardToKill, 9)],
            },
        ];

        CardEffects.DealDamage(state, 50);

        Assert.Equal(15, state.Enemies[0].Hp);
    }

    [Fact]
    public void EndTurn_AdvancesTurnCounter()
    {
        var state = CombatFactory.NewCombat(seed: 42);
        int endTurn = state.Hand.Count;
        var rng = new Random(42);

        CombatEngine.Step(state, endTurn, rng);

        Assert.Equal(1, state.Turn);
    }

    [Fact]
    public void ValidActions_AlwaysIncludesEndTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        int endTurn = state.Hand.Count;

        var actions = CombatEngine.ValidActions(state);

        Assert.Contains(endTurn, actions);
    }

    [Fact]
    public void PlayCard_CostsEnergy()
    {
        var state = CombatFactory.NewCombat(seed: 1);
        var rng = new Random(1);
        int before = state.Energy;

        int action = CombatEngine.ValidActions(state).First(a => a < state.Hand.Count);
        CombatEngine.Step(state, action, rng);

        Assert.True(state.Energy < before);
    }

    [Fact]
    public void Strike_DealsDamageToEnemy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 14,
                Hp = 30,
                MaxHp = 30,
                CurrentIntent = new Intent(IntentType.Attack, 9),
                Buffs = [],
            },
        ];
        int enemyHp = state.Enemies[0].Hp;

        // Force Strike into hand for determinism
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));

        CombatEngine.Step(state, 0, rng); // play Strike

        Assert.True(state.Enemies[0].Hp < enemyHp);
    }

    [Fact]
    public void Defend_GainsBlock()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false));

        CombatEngine.Step(state, 0, rng); // play Defend

        Assert.True(state.PlayerBlock > 0);
    }

    [Fact]
    public void Bash_AppliesVulnerableToEnemy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Bash, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        Assert.True(BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable) > 0);
    }

    [Fact]
    public void Inflame_GrantsStrengthToPlayer()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.Inflame, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void CalcifiedCultist_BuffsOnTurn1_AttacksAfter()
    {
        var state = new CombatState();
        var rng = new Random(0);
        CombatFactory.Reset(state, rng, StarterDeckIds, encounterId: 0);

        Assert.All(state.Enemies, enemy =>
            Assert.Equal(IntentType.Buff, enemy.CurrentIntent.Type));

        CombatEngine.Step(state, state.Hand.Count, rng); // end turn

        Assert.All(state.Enemies, enemy =>
            Assert.Equal(IntentType.Attack, enemy.CurrentIntent.Type));
    }

    [Fact]
    public void Barricade_BlockPersistsAcrossTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        // Give player Barricade and some block
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Barricade, 1);
        state.PlayerBlock = 15;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 56,
                Hp = 1,
                MaxHp = 1,
                CurrentIntent = new Intent(IntentType.Defend, 0),
            },
        ];

        // End turn (don't play anything)
        state.Hand.Clear();
        CombatEngine.Step(state, 0, rng); // 0 = end turn when hand is empty

        // Block should NOT have been reset to 0
        Assert.True(state.PlayerBlock > 0);
    }

    [Fact]
    public void DemonForm_GrantsStrengthEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        // Apply DemonForm (2 Strength per turn)
        BuffSystem.Apply(state.PlayerBuffs, BuffId.DemonForm, 2);

        // End turn
        state.Hand.Clear();
        CombatEngine.Step(state, 0, rng);

        // Player should have gained 2 Strength
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
    }

    [Fact]
    public void TwinStrike_HitsTwice()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 14,
                Hp = 30,
                MaxHp = 30,
                CurrentIntent = new Intent(IntentType.Attack, 9),
                Buffs = [],
            },
        ];
        int enemyHp = state.Enemies[0].Hp;

        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.TwinStrike, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, rng);

        // TwinStrike deals 5×2 = 10 damage (no buffs)
        Assert.Equal(enemyHp - 10, state.Enemies[0].Hp);
    }

    [Fact]
    public void Corruption_MakesSkillsFree()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var rng = new Random(0);

        BuffSystem.Apply(state.PlayerBuffs, BuffId.Corruption, 1);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false)); // Skill, cost 1
        state.Energy = 0; // not enough energy normally

        var actions = CombatEngine.ValidActions(state);
        Assert.Contains(0, actions); // card 0 should be playable despite 0 energy
    }
}
