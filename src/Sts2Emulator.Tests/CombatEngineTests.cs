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
    public void DrawCards_DrawsFromTopOfDrawPile()
    {
        var state = new CombatState
        {
            DrawPile =
            [
                new CardInstance(IC.StrikeIronclad, false),
                new CardInstance(IC.Bash, false),
                new CardInstance(IC.DefendIronclad, false),
            ],
        };

        CardEffects.DrawCards(state, 2, new Random(0));

        Assert.Equal(
            [IC.StrikeIronclad, IC.Bash],
            state.Hand.Select(card => card.DefId)
        );
        Assert.Equal([IC.DefendIronclad], state.DrawPile.Select(card => card.DefId));
    }

    [Fact]
    public void ResetWithDeck_NegativeCardIdsEncodeUpgradedCards()
    {
        var state = new CombatState();

        CombatFactory.Reset(state, new Random(0), [-IC.Bash], encounterId: 1);

        Assert.Single(state.Hand.Concat(state.DrawPile));
        Assert.All(state.Hand.Concat(state.DrawPile), card =>
        {
            Assert.Equal(IC.Bash, card.DefId);
            Assert.True(card.Upgraded);
        });
    }

    [Fact]
    public void ResetWithRelics_AppliesCombatStartRelics()
    {
        var state = new CombatState();

        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds:
            [
                RelicEffects.Anchor,
                RelicEffects.BagOfPreparation,
                RelicEffects.BloodVial,
                RelicEffects.BronzeScales,
                RelicEffects.OddlySmoothStone,
                RelicEffects.Vajra,
            ]
        );

        Assert.Equal(7, state.Hand.Count);
        Assert.Equal(66, state.PlayerHp);
        Assert.Equal(10, state.PlayerBlock);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Dexterity));
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Thorns));
    }

    [Fact]
    public void BoomingConch_DrawsAndGivesEnergyInEliteCombatOnly()
    {
        var elite = new CombatState();
        CombatFactory.Reset(
            elite,
            new Random(0),
            StarterDeckIds,
            encounterId: 82,
            relicIds: [RelicEffects.BoomingConch],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.True(elite.IsEliteCombat);
        Assert.Equal(7, elite.Hand.Count);
        Assert.Equal(4, elite.Energy);
        Assert.Equal(3, elite.MaxEnergy);

        var normal = new CombatState();
        CombatFactory.Reset(
            normal,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [RelicEffects.BoomingConch],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.False(normal.IsEliteCombat);
        Assert.Equal(5, normal.Hand.Count);
        Assert.Equal(3, normal.Energy);
    }

    [Fact]
    public void HappyFlower_GivesEnergyEveryThirdPlayerTurn()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [RelicEffects.HappyFlower],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.Equal(1, state.Relics.Single().Counter);
        Assert.Equal(3, state.Energy);

        state.Energy = 3;
        RelicEffects.ApplyStartOfPlayerTurn(state);

        Assert.Equal(2, state.Relics.Single().Counter);
        Assert.Equal(3, state.Energy);

        RelicEffects.ApplyStartOfPlayerTurn(state);

        Assert.Equal(0, state.Relics.Single().Counter);
        Assert.Equal(4, state.Energy);
    }

    [Fact]
    public void FirstTurnRelics_ApplyLanternEnergyAndBagOfMarblesVulnerable()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 2,
            relicIds: [RelicEffects.Lantern, RelicEffects.BagOfMarbles],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.Equal(4, state.Energy);
        Assert.All(
            state.Enemies.Where(enemy => enemy.Hp > 0),
            enemy => Assert.Equal(1, BuffSystem.Get(enemy.Buffs, BuffId.Vulnerable))
        );
    }

    [Fact]
    public void VenerableTeaSetActive_GivesTwoEnergyOnFirstTurn()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 2,
            relicIds: [RelicEffects.VenerableTeaSetActive],
            playerHp: 64,
            playerMaxHp: 80
        );

        Assert.Equal(5, state.Energy);
    }

    [Fact]
    public void Armaments_GainsBlockAndUpgradesFirstCardInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.DoesNotContain(state.Hand, card => card.DefId == IC.StrikeIronclad && !card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.DefendIronclad && !card.Upgraded);
    }

    [Fact]
    public void Armaments_UpgradedUpgradesAllCardsInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Armaments, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(ST.Slimed, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == IC.DefendIronclad && card.Upgraded);
        Assert.Contains(state.Hand, card => card.DefId == ST.Slimed && !card.Upgraded);
    }

    [Fact]
    public void ExpectAFight_GainsEnergyForAttacksInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.ExpectAFight, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.SwordBoomerang, false),
        ];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 100, MaxHp = 100, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(2, state.Energy);
    }

    [Fact]
    public void Juggling_CopiesThirdAttackPlayedEachTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Juggling, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 4;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 100, MaxHp = 100, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Juggling));
        Assert.Single(state.Hand);
        Assert.Equal(IC.StrikeIronclad, state.Hand[0].DefId);
    }

    [Fact]
    public void Restlessness_DrawsAndGainsEnergyWhenOnlyCardInHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Restlessness, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 0;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(2, state.Energy);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
    }

    [Fact]
    public void DrumOfBattle_DrawsAndGainsEnergyWhenExhausted()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.DrumOfBattle, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(2, state.Energy);
        Assert.Equal([IC.StrikeIronclad, IC.DefendIronclad], state.Hand.Select(card => card.DefId));
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DrumOfBattle);
    }

    [Fact]
    public void FightMe_HitsTwiceAndAppliesStrengthToBothSides()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.FightMe, false)];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 100, MaxHp = 100, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(90, state.Enemies[0].Hp);
        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Strength));
    }

    [Fact]
    public void Pillage_DamagesAndDrawsUntilNonAttack()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Pillage, false)];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.DefendIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 50, MaxHp = 50, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(44, state.Enemies[0].Hp);
        Assert.Equal(
            [IC.StrikeIronclad, IC.Bash, IC.DefendIronclad],
            state.Hand.Select(card => card.DefId)
        );
        Assert.Equal([IC.StrikeIronclad], state.DrawPile.Select(card => card.DefId));
    }

    [Fact]
    public void Pillage_UpgradedUsesUpgradedDamageAndStopsAtFullHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Pillage, true),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.StrikeIronclad, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.Bash, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 50, MaxHp = 50, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(41, state.Enemies[0].Hp);
        Assert.Equal(10, state.Hand.Count);
        Assert.Equal([IC.DefendIronclad], state.DrawPile.Select(card => card.DefId));
    }

    [Fact]
    public void Breakthrough_LosesHpAndDamagesAllEnemies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, false)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 30, MaxHp = 30, Buffs = [] },
            new EnemyState { DefId = 16, Hp = 30, MaxHp = 30, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal([21, 21], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void Breakthrough_UpgradedUsesUpgradedAllEnemyDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Breakthrough, true)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 30, MaxHp = 30, Buffs = [] },
            new EnemyState { DefId = 16, Hp = 30, MaxHp = 30, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal([17, 17], state.Enemies.Select(enemy => enemy.Hp));
    }

    [Fact]
    public void Cinder_DamagesTargetAndExhaustsRandomCardFromHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Cinder, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 50, MaxHp = 50, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(32, state.Enemies[0].Hp);
        Assert.Empty(state.Hand);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.Cinder);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DefendIronclad);
    }

    [Fact]
    public void Cinder_UpgradedUsesUpgradedDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Cinder, true)];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 50, MaxHp = 50, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(26, state.Enemies[0].Hp);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.Cinder);
    }

    [Fact]
    public void Havoc_PlaysAndExhaustsTopDrawPileCard()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Havoc, false)];
        state.DrawPile = [new CardInstance(IC.DefendIronclad, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.Havoc);
        Assert.Contains(state.ExhaustPile, card => card.DefId == IC.DefendIronclad);
    }

    [Fact]
    public void Splash_AddsGeneratedAttackToHand()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Splash, false)];
        state.Energy = 1;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Single(state.Hand);
        Assert.Equal(IC.StrikeIronclad, state.Hand[0].DefId);
    }

    [Fact]
    public void Stampede_AppliesTrackedPower()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.Stampede, false)];
        state.Energy = 2;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Stampede));
    }

    [Fact]
    public void Vicious_DrawsWhenPlayerAppliesVulnerable()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand =
        [
            new CardInstance(IC.Vicious, false),
            new CardInstance(IC.Taunt, false),
        ];
        state.DrawPile =
        [
            new CardInstance(IC.StrikeIronclad, false),
            new CardInstance(IC.DefendIronclad, false),
        ];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 100, MaxHp = 100, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Vicious));
        Assert.Equal(1, BuffSystem.Get(state.Enemies[0].Buffs, BuffId.Vulnerable));
        Assert.Contains(state.Hand, card => card.DefId == IC.StrikeIronclad);
    }

    [Fact]
    public void Colossus_GainsBlockAndHalvesVulnerableEnemyAttackDamage()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 100;
        state.PlayerMaxHp = 100;
        state.Hand = [new CardInstance(IC.Colossus, false)];
        state.DrawPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Attack, 20),
                Buffs = [new BuffState(BuffId.Vulnerable, 2)],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(5, state.PlayerBlock);
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(95, state.PlayerHp);
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Colossus));
    }

    [Fact]
    public void Inferno_TriggersWhenPlayerLosesHpOnPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand =
        [
            new CardInstance(IC.Inferno, false),
            new CardInstance(IC.Hemokinesis, false),
        ];
        state.Energy = 2;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 100, MaxHp = 100, Buffs = [] },
            new EnemyState { DefId = 16, Hp = 100, MaxHp = 100, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(48, state.PlayerHp);
        Assert.Equal(6, BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno));
        Assert.Equal(1, BuffSystem.Get(state.PlayerBuffs, BuffId.InfernoSelfDamage));
        Assert.Equal(79, state.Enemies[0].Hp);
        Assert.Equal(94, state.Enemies[1].Hp);
    }

    [Fact]
    public void Inferno_DamagesPlayerAndEnemiesAtStartOfPlayerTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerHp = 50;
        state.Hand = [new CardInstance(IC.Inferno, true)];
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
            new EnemyState
            {
                DefId = 16,
                Hp = 100,
                MaxHp = 100,
                CurrentIntent = new Intent(IntentType.Defend, 0),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));
        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(49, state.PlayerHp);
        Assert.Equal(9, BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno));
        Assert.Equal(91, state.Enemies[0].Hp);
        Assert.Equal(91, state.Enemies[1].Hp);
    }

    [Fact]
    public void SetupStrike_AppliesTemporaryStrengthUntilEndOfTurn()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Hand = [new CardInstance(IC.SetupStrike, false)];
        state.Energy = 1;
        state.Enemies =
        [
            new EnemyState { DefId = 16, Hp = 100, MaxHp = 100, Buffs = [] },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(93, state.Enemies[0].Hp);
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(2, BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength));

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.TemporaryStrength));
    }

    [Fact]
    public void TurnBlockRelics_ApplyHornCleatAndCaptainsWheel()
    {
        var state = new CombatState
        {
            Relics =
            [
                new RelicInstance(RelicEffects.HornCleat),
                new RelicInstance(RelicEffects.CaptainsWheel),
            ],
        };

        state.Turn = 1;
        RelicEffects.ApplyStartOfPlayerTurn(state);
        Assert.Equal(14, state.PlayerBlock);

        state.PlayerBlock = 0;
        state.Turn = 2;
        RelicEffects.ApplyStartOfPlayerTurn(state);
        Assert.Equal(18, state.PlayerBlock);
    }

    [Fact]
    public void RedSkull_TracksLowHpStrength()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 2,
            relicIds: [RelicEffects.RedSkull],
            playerHp: 40,
            playerMaxHp: 80
        );

        Assert.Equal(3, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(1, state.Relics.Single().Counter);

        state.PlayerHp = 41;
        RelicEffects.ApplyAfterPlayerHpChanged(state);

        Assert.Equal(0, BuffSystem.Get(state.PlayerBuffs, BuffId.Strength));
        Assert.Equal(0, state.Relics.Single().Counter);
    }

    [Fact]
    public void Orichalcum_GainsBlockWhenEndingTurnWithoutBlock()
    {
        var state = new CombatState();
        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [RelicEffects.Orichalcum],
            playerHp: 64,
            playerMaxHp: 80
        );
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 16,
                Hp = 20,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Attack, 1),
                Buffs = [],
            },
        ];

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(64, state.PlayerHp);
    }

    [Fact]
    public void ResetWithRunHp_PreservesCurrentRunHp()
    {
        var state = new CombatState();

        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [],
            playerHp: 37,
            playerMaxHp: 80
        );

        Assert.Equal(37, state.PlayerHp);
        Assert.Equal(80, state.PlayerMaxHp);
    }

    [Fact]
    public void ResetWithPotions_PreservesRunPotionSlots()
    {
        var state = new CombatState();

        CombatFactory.Reset(
            state,
            new Random(0),
            StarterDeckIds,
            encounterId: 1,
            relicIds: [],
            playerHp: 37,
            playerMaxHp: 80,
            potionIds: [1, 0, 2]
        );

        Assert.Equal(new[] { 1, 0, 2 }, state.PotionSlots);
    }

    [Fact]
    public void PlayerThorns_RetaliatesAgainstEnemyAttacks()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        var enemy = new EnemyState
        {
            DefId = 16,
            Hp = 20,
            MaxHp = 20,
            CurrentIntent = new Intent(IntentType.Attack, 1),
            Buffs = [],
        };
        BuffSystem.Apply(state.PlayerBuffs, BuffId.Thorns, 3);

        EnemyAI.ExecuteIntent(enemy, state, new Random(0));

        Assert.Equal(17, enemy.Hp);
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
    public void GremlinMerc_AttacksStealGold()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerGold = 99;
        state.PlayerBlock = 99;
        var merc = new EnemyState
        {
            DefId = 37,
            Hp = 53,
            MaxHp = 53,
            CurrentIntent = new Intent(IntentType.Attack, 16),
            Buffs = [],
        };

        EnemyAI.ExecuteIntent(merc, state, new Random(0));

        Assert.Equal(79, state.PlayerGold);
        Assert.Equal(20, merc.StolenGold);
    }

    [Fact]
    public void GremlinMerc_TransfersStolenGoldToFatGremlinHeist()
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
                StolenGold = 40,
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Contains(state.Enemies, e => e.DefId == 28 && e.HeistGold == 40);
    }

    [Fact]
    public void HeistGold_ReturnsWhenFatGremlinDies()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.PlayerGold = 59;
        state.Hand.Clear();
        state.DrawPile.Clear();
        state.DiscardPile.Clear();
        state.ExhaustPile.Clear();
        state.Enemies =
        [
            new EnemyState
            {
                DefId = 28,
                Hp = 1,
                MaxHp = 13,
                CurrentIntent = new Intent(IntentType.Unknown, 0),
                Buffs = [],
                HeistGold = 40,
            },
        ];
        state.Hand.Add(new CardInstance(IC.StrikeIronclad, false));
        state.Energy = 3;

        CombatEngine.Step(state, 0, new Random(0));

        Assert.Equal(99, state.PlayerGold);
        Assert.Equal(0, state.Enemies[0].HeistGold);
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
    public void TwoTailedRat_CallForBackupRespectsTotalSlotLimit()
    {
        var state = CombatFactory.NewCombat(seed: 0);
        state.Enemies = Enumerable.Range(0, 6)
            .Select(i => new EnemyState
            {
                DefId = 101,
                Hp = i == 0 ? 20 : 0,
                MaxHp = 20,
                CurrentIntent = new Intent(IntentType.Buff, 0),
                Buffs = [],
            })
            .ToList();

        EnemyAI.ExecuteIntent(state.Enemies[0], state, new Random(0));

        Assert.Equal(6, state.Enemies.Count(e => e.DefId == 101));
    }

    [Fact]
    public void MixedEnemyIntents_ExposeSecondaryAttackMetadata()
    {
        var state = new CombatState();

        CombatFactory.Reset(state, new Random(0), StarterDeckIds, encounterId: 7);
        var merc = Assert.Single(state.Enemies);
        merc.MoveIndex = 1;
        merc.CurrentIntent = new Intent(IntentType.Debuff, 14);
        EnemyAI.UpdateSecondaryIntents(state.Enemies);

        Assert.Equal(IntentType.Attack, merc.SecondaryIntent?.Type);
        Assert.Equal(14, merc.SecondaryIntent?.Magnitude);
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
