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

    [Fact]
    public void Shrink_ReducesPoweredAttackDamageByThirtyPercent()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 56,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 0),
                Buffs = [],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Shrink, 1);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(16, state.Enemies[0].Hp);
    }

    [Fact]
    public void Thorns_RetaliatesAgainstPoweredAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.PlayerHp = 64;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 93,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 0),
                Buffs = [new BuffState(BuffId.Thorns, 2)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(62, state.PlayerHp);
        Assert.Equal(14, state.Enemies[0].Hp);
    }

    [Fact]
    public void Toadpole_SpikeSpitConsumesThornsBeforeAttacking()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 0;
        state.PlayerHp = 64;
        var enemy = new EnemyState
        {
            DefId = 93,
            Hp = 22,
            MaxHp = 22,
            CurrentIntent = new Intent(IntentType.Attack, 12),
            Buffs = [new BuffState(BuffId.Thorns, 2)],
            MoveIndex = 1,
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(0, BuffSystem.Get(enemy.Buffs, BuffId.Thorns));
        Assert.Equal(52, state.PlayerHp);
    }

    [Fact]
    public void Ravenous_StrengthensAndStunsCorpseSlugWhenAllyDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 17,
                Hp = 1,
                MaxHp = 25,
                CurrentIntent = new Intent(IntentType.Attack, 6),
                Buffs = [new BuffState(BuffId.Ravenous, 5)],
            },
            new EnemyState
            {
                DefId = 17,
                Hp = 25,
                MaxHp = 25,
                CurrentIntent = new Intent(IntentType.Attack, 6),
                Buffs = [new BuffState(BuffId.Ravenous, 5)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, state.Enemies[0].Hp);
        Assert.Equal(5, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Stunned));

        EnemyAI.ExecuteIntent(state.Enemies[1], state, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.Enemies[1].Buffs, BuffId.Stunned));
        Assert.Equal(64, state.PlayerHp);
    }

    [Fact]
    public void Slippery_CapsOneUnblockedHitThenExpires()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 42,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 4),
                Buffs = [new BuffState(BuffId.Slippery, 1)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(13, state.Enemies[0].Hp);
        Assert.Equal(0, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Slippery));
    }

    [Fact]
    public void Surprise_SpawnsGremlinsWhenMercDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 37,
                Hp = 1,
                MaxHp = 47,
                CurrentIntent = new Intent(IntentType.Attack, 16),
                Buffs = [new BuffState(BuffId.Surprise, 1)],
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        var result = CombatEngine.Step(state, 0, new Random(0));

        Assert.False(result.Terminal);
        Assert.Contains(state.Enemies, e => e.DefId == 78 && e.Hp > 0);
        Assert.Contains(state.Enemies, e => e.DefId == 28 && e.Hp > 0);
        Assert.Contains(state.Enemies, e => e.DefId == 78 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1);
        Assert.Contains(state.Enemies, e => e.DefId == 28 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1);
    }

    [Fact]
    public void TwoTailedRat_CallForBackupSummonsRatAndTracksLimit()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 101,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Buff, 0),
                Buffs = [],
            },
        ];

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Equal(2, state.Enemies.Count(e => e.DefId == 101));
        Assert.All(state.Enemies.Where(e => e.DefId == 101),
            rat => Assert.Equal(1, BuffSystem.Get(rat.Buffs, BuffId.BackupCount)));
        Assert.Contains(state.Enemies, e => e.DefId == 101 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1);
    }

    [Fact]
    public void ForcedNormalEncounters_CreateExpectedShapes()
    {
        var expectedEnemyIds = new Dictionary<int, int>
        {
            [14] = 53,  // Mawler
            [15] = 56,  // Nibbits
            [16] = 47,  // Large slimes include LeafSlimeM
            [17] = 30,  // Flyconid encounter
            [18] = 77,  // Snapping Jaxfruit
            [19] = 20,  // Cubex Construct
            [20] = 103, // Vine Shambler
            [21] = 71,  // Shrinker Beetle + Fuzzy
            [22] = 14,  // Calcified Cultist + Seapunk
            [23] = 32,  // Fossil Stalker
            [24] = 65,  // Punch Construct
            [25] = 70,  // Sewer Clam
            [26] = 39,  // Haunted Ship
            [27] = 74,  // Slithering Strangler
            [28] = 5,   // Ruby Raiders
            [29] = 31,  // Fogmog
            [30] = 49,  // Living Fog
        };

        foreach (var (encounterId, enemyId) in expectedEnemyIds)
        {
            var state = CombatFactory.NewCombat(seed: encounterId);

            CombatFactory.Reset(state, new Random(encounterId), StarterDeckIds, encounterId);

            Assert.Equal(encounterId, state.EncounterId);
            Assert.Contains(state.Enemies, enemy => enemy.DefId == enemyId);
        }
    }

    [Fact]
    public void SewerClam_PlatingAddsBlockAndDecays()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 70,
            Hp = 45,
            MaxHp = 45,
            CurrentIntent = new Intent(IntentType.Attack, 11),
            Buffs = [new BuffState(BuffId.Plating, 9)],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(9, enemy.Block);
        Assert.Equal(8, BuffSystem.Get(enemy.Buffs, BuffId.Plating));
    }

    [Fact]
    public void HauntedShip_HauntAppliesWeakAndDazed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 39,
            Hp = 67,
            MaxHp = 67,
            CurrentIntent = new Intent(IntentType.Debuff, 5),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Weak));
        Assert.Equal(5, state.DiscardPile.Count(c => c.DefId == ST.Dazed));
    }

    [Fact]
    public void VineShambler_GraspingVinesAppliesTangled()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerBlock = 0;
        var enemy = new EnemyState
        {
            DefId = 103,
            Hp = 65,
            MaxHp = 65,
            CurrentIntent = new Intent(IntentType.Debuff, 9),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(55, state.PlayerHp);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Tangled));
    }

    [Fact]
    public void Fogmog_IllusionSummonsEyeWithTeeth()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 31,
                Hp = 78,
                MaxHp = 78,
                CurrentIntent = new Intent(IntentType.Buff, 0),
                Buffs = [],
            },
        ];

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Contains(state.Enemies, e => e.DefId == 26 && BuffSystem.Get(e.Buffs, BuffId.Illusion) == 1);
        Assert.Contains(state.Enemies, e => e.DefId == 26 && BuffSystem.Get(e.Buffs, BuffId.Stunned) == 1);
    }

    [Fact]
    public void LivingFog_AdvancedGasAppliesSmoggy()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 49,
            Hp = 82,
            MaxHp = 82,
            CurrentIntent = new Intent(IntentType.Debuff, 9),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(55, state.PlayerHp);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Smoggy));
    }

    [Fact]
    public void LivingFog_BloatSummonsGasBombAndAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 49,
            Hp = 82,
            MaxHp = 82,
            CurrentIntent = new Intent(IntentType.Buff, 6),
            Buffs = [],
            MoveIndex = 1,
        };

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(58, state.PlayerHp);
        Assert.Contains(state.Enemies, e => e.DefId == 35 && BuffSystem.Get(e.Buffs, BuffId.Minion) == 1);
    }

    [Fact]
    public void Tangled_IncreasesAttackEnergyCostUntilNextTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 1;
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Tangled, 1);

        Assert.DoesNotContain(0, CombatEngine.ValidActions(state));
        Assert.Equal(StepResult.Invalid, CombatEngine.Step(state, 0, new Random(0)));
    }

    [Fact]
    public void Smoggy_BlocksAdditionalSkillsAfterSkillPlayed()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false));
        state.Hand.Add(new CardInstance(IC.DefendIronclad, false));
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Smoggy, 1);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.DoesNotContain(0, CombatEngine.ValidActions(state));
        Assert.Equal(StepResult.Invalid, CombatEngine.Step(state, 0, new Random(0)));
    }

    [Fact]
    public void Constrict_DamagesAtEndTurnAndExpiresWhenStranglerDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies.Clear();
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Constrict, 3);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(61, state.PlayerHp);

        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 74,
                Hp = 1,
                MaxHp = 56,
                CurrentIntent = new Intent(IntentType.Debuff, 3),
                Buffs = [],
            },
        ];
        state.Energy = 3;
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Constrict, 3);

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Constrict));
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
