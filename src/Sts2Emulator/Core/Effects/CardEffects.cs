namespace Sts2Emulator.Core.Effects;

public static class CardEffects
{
    public static void Apply(CardDef def, bool upgraded, CombatState state, Random rng)
    {
        switch (def.Id)
        {
            case ST.Slimed:
                DrawCards(state, 1, rng);
                break;

            case ST.FranticEscape: // 1-cost status, increments Sandpit on enemy; cost increases per play
            {
                var target = FirstEnemy(state);
                if (target != null)
                    BuffSystem.Apply(target.Buffs, BuffId.Sandpit, 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FranticEscapePlayedCount, 1);
                break;
            }

            // ── Ironclad Attacks ─────────────────────────────────────────────────

            case IC.Break: // 1-cost, 20/30 dmg + Vulnerable 5/7
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded));
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 7 : 5,
                        rng
                    );
                }
                break;
            }

            case IC.Bludgeon: // 3-cost, 32/42 dmg
                DealDamage(state, Dmg(def, upgraded));
                break;

            case IC.Anger: // 0-cost, 6/8 dmg + add copy to discard
                DealDamage(state, Dmg(def, upgraded));
                state.DiscardPile.Add(new CardInstance(def.Id, upgraded));
                break;

            case IC.Bash: // 2-cost, 8/10 dmg + Vulnerable 2/3
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded));
                    ApplyEnemyDebuffToTarget(
                        state,
                        target,
                        BuffId.Vulnerable,
                        upgraded ? 3 : 2,
                        rng
                    );
                }
                break;
            }

            case IC.BodySlam: // 1/0-cost, dmg = player's current block
                DealDamage(state, state.PlayerBlock);
                break;

            case IC.IronWave: // 1-cost, gain 5/7 block, then deal 5/7 damage
                GainBlock(state, Blk(def, upgraded));
                DealDamage(state, Dmg(def, upgraded));
                break;

            case IC.Breakthrough: // 1-cost, lose 1 HP + 9/13 dmg to ALL enemies
                LoseHp(state, 1);
                DealDamageToAll(state, Dmg(def, upgraded));
                break;

            case IC.AshenStrike: // 1-cost, 6 + 3/4 per exhausted card
                DealDamage(state, 6 + state.ExhaustPile.Count * (upgraded ? 4 : 3));
                break;

            case IC.Bully: // 0-cost, 4 + 2/3 * target's Vulnerable stacks
            {
                var t = FirstEnemy(state);
                int vuln = t != null ? BuffSystem.Get(t.Buffs, BuffId.Vulnerable) : 0;
                DealDamage(state, 4 + (upgraded ? 3 : 2) * vuln);
                break;
            }

            case IC.Cinder: // 2-cost, 18/24 dmg + exhaust a random card from hand
                DealDamage(state, Dmg(def, upgraded));
                ExhaustRandomCardFromHand(state, rng);
                break;

            case IC.Conflagration: // 1-cost, 2 dmg × 4/5 hits to ALL enemies
                DealDamageToAllMultiHit(state, 2, upgraded ? 5 : 4);
                break;

            case CL.DramaticEntrance: // 0-cost, 11/15 damage to ALL enemies, exhaust
                DealDamageToAll(state, Dmg(def, upgraded));
                break;

            case CL.Bolas: // 0-cost, 3/4 damage; returns to hand before next turn's draw
                DealDamage(state, Dmg(def, upgraded));
                break;

            case CL.DarkShackles: // 0-cost, enemy loses 9/15 Strength this turn, exhaust
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 15 : 9);
                break;

            case CL.Volley: // X-cost, 10/14 damage X times to random enemies
            {
                int x = state.Energy;
                state.Energy = 0;
                DealDamageToRandomEnemiesMultiHit(state, Dmg(def, upgraded), x, rng);
                break;
            }

            case CL.Omnislice: // 0-cost, 8/11 damage + splash effective first-hit damage to other enemies
                DealOmnislice(state, Dmg(def, upgraded));
                break;

            case CL.Prolong: // 0-cost, gain current block again next turn; upgrade removes exhaust
                BuffSystem.Apply(state.PlayerBuffs, BuffId.BlockNextTurn, state.PlayerBlock);
                break;

            case CL.Salvo: // 1-cost, 12/16 damage + retain remaining hand this turn
                DealDamage(state, Dmg(def, upgraded));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RetainHand, 1);
                break;

            case AN.NeowsFury: // 1-cost, 10/14 damage + move 2/3 discard cards to hand, exhaust
                DealDamage(state, Dmg(def, upgraded));
                MoveDiscardCardsToHand(state, upgraded ? 3 : 2);
                break;

            case IC.Dismantle: // 1-cost, 8/10 dmg, hits twice if target is Vulnerable
            {
                var t = FirstEnemy(state);
                int hits = (t != null && BuffSystem.Get(t.Buffs, BuffId.Vulnerable) > 0) ? 2 : 1;
                DealDamageMultiHit(state, Dmg(def, upgraded), hits, rng);
                break;
            }

            case IC.FiendFire: // 2-cost, exhaust hand, deal 7/10 dmg per card exhausted
            {
                int count = state.Hand.Count;
                while (state.Hand.Count > 0)
                {
                    ExhaustCard(state, state.Hand[0], rng: rng);
                    state.Hand.RemoveAt(0);
                }
                DealDamageMultiHit(state, Dmg(def, upgraded), count, rng);
                break;
            }

            case IC.FightMe: // 2-cost, 5/6 dmg twice, gain 3/4 Strength, enemy gains 1 Strength
                DealDamageMultiHit(state, Dmg(def, upgraded), 2, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 4 : 3);
                ApplyEnemyDebuff(state, BuffId.Strength, 1, rng);
                break;

            case IC.Headbutt: // 1-cost, 9/12 dmg + put top of discard on top of draw
                DealDamage(state, Dmg(def, upgraded));
                if (state.DiscardPile.Count > 0)
                {
                    var card = state.DiscardPile[^1];
                    state.DiscardPile.RemoveAt(state.DiscardPile.Count - 1);
                    state.DrawPile.Insert(0, card);
                }
                break;

            case IC.Hemokinesis: // 1-cost, lose 2 HP then deal 15/20 dmg
                LoseHp(state, 2);
                DealDamage(state, Dmg(def, upgraded));
                break;

            case IC.MoltenFist: // 1-cost, 10/14 dmg + reapply target's Vulnerable if it survives
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    DealDamageToEnemy(state, target, Dmg(def, upgraded));
                    int vulnerable =
                        target.Hp > 0 ? BuffSystem.Get(target.Buffs, BuffId.Vulnerable) : 0;
                    if (vulnerable > 0)
                    {
                        int before = vulnerable;
                        BuffSystem.Apply(target.Buffs, BuffId.Vulnerable, vulnerable);
                        DrawForVicious(
                            state,
                            BuffId.Vulnerable,
                            before,
                            BuffSystem.Get(target.Buffs, BuffId.Vulnerable),
                            rng
                        );
                    }
                }
                break;
            }

            case IC.Feed: // 1-cost, 10/12 dmg; if kills gain 3/4 max HP; exhaust
            {
                var feedTarget = FirstEnemy(state);
                if (feedTarget != null)
                {
                    DealDamageToEnemy(state, feedTarget, Dmg(def, upgraded));
                    if (feedTarget.Hp <= 0)
                        state.PlayerMaxHp += upgraded ? 4 : 3;
                }
                break;
            }

            case IC.Mangle: // 3-cost, 15/20 dmg + enemy loses Strength 10/15 this turn
                DealDamage(state, Dmg(def, upgraded));
                ApplyTemporaryStrengthDownToEnemy(state, upgraded ? 15 : 10);
                break;

            case IC.HowlFromBeyond: // 3-cost, 16/21 dmg to ALL enemies
                DealDamageToAll(state, Dmg(def, upgraded));
                break;

            case IC.PactsEnd: // 0-cost, 17/23 dmg to ALL enemies
                DealDamageToAll(state, Dmg(def, upgraded));
                break;

            case IC.Pillage: // 1-cost, 6/9 dmg + draw until drawing a non-Attack
                DealDamage(state, Dmg(def, upgraded));
                DrawUntilNonAttack(state, rng);
                break;

            case IC.PerfectedStrike: // 2-cost, 6 + 2/3 per Strike card in all piles
                DealDamage(state, 6 + CountStrikeCards(state) * (upgraded ? 3 : 2));
                break;

            case IC.PommelStrike: // 1-cost, 9/10 dmg + draw 1/2
                DealDamage(state, Dmg(def, upgraded));
                DrawCards(state, upgraded ? 2 : 1, rng);
                break;

            case IC.PrimalForce: // 0-cost, transform all Attacks in hand to GiantRocks
            {
                for (int i = 0; i < state.Hand.Count; i++)
                {
                    var card = state.Hand[i];
                    if (GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack)
                        state.Hand[i] = new CardInstance(IC.GiantRock, upgraded);
                }
                break;
            }

            case IC.SetupStrike: // 1-cost, 7/9 dmg + 2/3 temporary Strength
            {
                DealDamage(state, Dmg(def, upgraded));
                int strength = upgraded ? 3 : 2;
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, strength);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.TemporaryStrength, strength);
                break;
            }

            case IC.Spite: // 0-cost, 5 dmg; 2/3 hits if the player lost HP this turn
                DealDamageMultiHit(
                    state,
                    Dmg(def, upgraded),
                    state.PlayerHpLostThisTurn > 0 ? (upgraded ? 3 : 2) : 1,
                    rng
                );
                break;

            case IC.Stomp: // 3-cost, reduced by Attacks played this turn, 12/15 dmg to ALL enemies
                DealDamageToAll(state, Dmg(def, upgraded));
                break;

            case IC.Stoke: // 1-cost, exhaust hand and add random cards
            {
                int count = state.Hand.Count;
                foreach (var card in state.Hand.ToArray())
                    ExhaustCard(state, card, rng: rng);
                state.Hand.Clear();
                for (int i = 0; i < count; i++)
                {
                    if (state.Hand.Count < MaxCardsInHand)
                    {
                        int defId = _ironcladPool[rng.Next(_ironcladPool.Length)];
                        state.Hand.Add(new CardInstance(defId, upgraded));
                    }
                }
                break;
            }

            case IC.SwordBoomerang: // 1-cost, 3 dmg × 3/4 hits to random enemies
                DealDamageMultiHit(state, 3, upgraded ? 4 : 3, rng);
                break;

            case IC.Tank: // 1/0-cost, apply TankPower (multiplayer only)
                break;

            case IC.Thunderclap: // 1-cost, 4/7 dmg to ALL + Vulnerable 1 to ALL
                DealDamageToAll(state, Dmg(def, upgraded));
                ApplyAllEnemyDebuff(state, BuffId.Vulnerable, 1, rng);
                break;

            case IC.Unmovable: // 2/1-cost, double first block gain each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.UnmovablePower, 1);
                break;

            case IC.TwinStrike: // 1-cost, 5/7 dmg × 2 hits
                DealDamageMultiHit(state, Dmg(def, upgraded), 2, rng);
                break;

            case IC.Unrelenting: // 2-cost, 14/20 dmg + FreeAttackPower 1 (next Attack costs 0)
                DealDamage(state, Dmg(def, upgraded));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FreeAttackPower, 1);
                break;

            case IC.Rampage: // 1-cost, 9 dmg + gains 5/6 more damage per prior play (approx: static base)
                DealDamage(state, Dmg(def, upgraded));
                break;

            case IC.TearAsunder: // 2-cost, 5/7 dmg × (1 + unblocked damage hits received this combat)
            {
                int hits = 1 + state.UnblockedDamageHitCount;
                DealDamageMultiHit(state, Dmg(def, upgraded), hits, rng);
                break;
            }

            case IC.Thrash: // 1-cost, 4/6 dmg × 2 + exhaust a random Attack from hand
                DealDamageMultiHit(state, Dmg(def, upgraded), 2, rng);
                ExhaustRandomCardOfTypeFromHand(state, CardType.Attack, rng);
                break;

            case IC.Uppercut: // 2-cost, 13/13 dmg + Weak 1/2 + Vulnerable 1/2
                DealDamage(state, Dmg(def, upgraded));
                ApplyEnemyDebuff(state, BuffId.Weak, upgraded ? 2 : 1, rng);
                ApplyEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 2 : 1, rng);
                break;

            case IC.Whirlwind: // X-cost, 5/8 dmg × (energy spent) to ALL enemies
            {
                int x = state.Energy;
                state.Energy = 0;
                DealDamageToAllMultiHit(state, Dmg(def, upgraded), x);
                break;
            }

            // ── Ironclad Skills ──────────────────────────────────────────────────

            case IC.Armaments: // 1-cost, gain 5 block + upgrade 1 card/all cards if upgraded
                GainBlock(state, Blk(def, upgraded));
                if (upgraded)
                    UpgradeAllCardsInHand(state);
                else
                    UpgradeFirstCardInHand(state);
                break;

            case IC.Brand: // 0-cost, lose 1 HP, exhaust a card, gain 1/2 Strength
                LoseHp(state, 1);
                ExhaustRandomCardFromHand(state, rng);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 2 : 1);
                break;

            case IC.BattleTrance: // 0-cost, draw 3/4 (NoDraw omitted)
                DrawCards(state, upgraded ? 4 : 3, rng);
                break;

            case IC.BloodWall: // 2-cost, lose 2 HP + gain 16/20 block
                LoseHp(state, 2);
                GainBlock(state, Blk(def, upgraded));
                break;

            case IC.Bloodletting: // 0-cost, lose 3 HP + gain 2/3 energy
                LoseHp(state, 3);
                state.Energy += upgraded ? 3 : 2;
                break;

            case IC.BurningPact: // 1-cost, draw 2/3 (exhaust-a-card choice omitted)
                DrawCards(state, upgraded ? 3 : 2, rng);
                break;

            case IC.Colossus: // 1-cost, gain 5/8 block; Vulnerable enemies deal half attack damage this turn
                GainBlock(state, Blk(def, upgraded));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Colossus, 1);
                break;

            case IC.Dominate: // 1-cost, Vulnerable 1 to enemy, gain Strength = total Vulnerable
            {
                ApplyEnemyDebuff(state, BuffId.Vulnerable, 1, rng);
                var t = FirstEnemy(state);
                if (t != null)
                    BuffSystem.Apply(
                        state.PlayerBuffs,
                        BuffId.Strength,
                        BuffSystem.Get(t.Buffs, BuffId.Vulnerable)
                    );
                break;
            }

            case IC.DrumOfBattle: // 1-cost, draw 2; on self-exhaust gain 2/3 energy
                DrawCards(state, 2, rng);
                break;

            case IC.EvilEye: // 1-cost, gain block twice if a card exhausted this turn
                GainBlock(state, Blk(def, upgraded));
                if (state.CardsExhaustedThisTurn > 0)
                    GainBlock(state, Blk(def, upgraded));
                break;

            case IC.ExpectAFight: // 2/1-cost, gain 1 energy per Attack in hand
            {
                int attackCount = state.Hand.Count(card =>
                    GeneratedData.Cards.Get(card.DefId).Type == CardType.Attack
                );
                state.Energy += attackCount;
                break;
            }

            case IC.FlameBarrier: // 2-cost, 12/16 block + FlameBarrier 4/6
                GainBlock(state, Blk(def, upgraded));
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FlameBarrier, upgraded ? 6 : 4);
                break;

            case IC.ForgottenRitual: // 1-cost, gain 3/4 energy only if a card exhausted this turn
                if (state.CardsExhaustedThisTurn > 0)
                    state.Energy += upgraded ? 4 : 3;
                break;

            case IC.Havoc: // 1/0-cost, play top card of draw pile and exhaust it
            {
                if (state.DrawPile.Count > 0)
                {
                    var top = state.DrawPile[0];
                    state.DrawPile.RemoveAt(0);
                    var topDef = GeneratedData.Cards.Get(top.DefId);
                    Apply(topDef, top.Upgraded, state, rng);
                    ExhaustCard(state, top, rng: rng);
                }
                break;
            }

            case IC.InfernalBlade: // 1/0-cost, add a random Ironclad Attack to hand free this turn
                AddRandomInfernalBladeAttack(state, rng);
                break;

            case IC.NotYet: // 2-cost, heal 10/13 HP
                state.PlayerHp = Math.Min(state.PlayerHp + (upgraded ? 13 : 10), state.PlayerMaxHp);
                break;

            case IC.OneTwoPunch: // 1-cost, the next 1/2 Attack cards are played twice this turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.OneTwoPunch, upgraded ? 2 : 1);
                break;

            case IC.Offering: // 0-cost, lose 6 HP + gain 2 energy + draw 3/5
                LoseHp(state, 6);
                state.Energy += 2;
                DrawCards(state, upgraded ? 5 : 3, rng);
                break;

            case IC.SecondWind: // 1-cost, exhaust non-Attacks, gain 5/7 block per
            {
                int blockEach = upgraded ? 7 : 5;
                var nonAtk = state
                    .Hand.Where(c => GeneratedData.Cards.Get(c.DefId).Type != CardType.Attack)
                    .ToList();
                foreach (var c in nonAtk)
                {
                    state.Hand.Remove(c);
                    ExhaustCard(state, c, rng: rng);
                    GainBlock(state, blockEach);
                }
                break;
            }

            case IC.Restlessness: // 0-cost, if this was the only card in hand, draw and gain energy
                if (state.Hand.Count == 0)
                {
                    DrawCards(state, upgraded ? 3 : 2, rng);
                    state.Energy += upgraded ? 3 : 2;
                }
                break;

            case IC.ShrugItOff: // 1-cost, 8/11 block + draw 1
                GainBlock(state, Blk(def, upgraded));
                DrawCards(state, 1, rng);
                break;

            case IC.UltimateDefend: // 1-cost, 11/15 block
                GainBlock(state, Blk(def, upgraded));
                break;

            case IC.Impervious: // 2-cost, 30/40 block + exhaust (Exhaust handled by CardDef)
                GainBlock(state, Blk(def, upgraded));
                break;

            case IC.Splash: // 1-cost, approximate generated off-character attack with a free Strike
                state.Hand.Add(new CardInstance(IC.StrikeIronclad, upgraded));
                break;

            case IC.Stampede: // 2/1-cost, auto-play random Attacks at play-phase start (tracked)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Stampede, 1);
                break;

            case IC.Taunt: // 1-cost, 7/8 block + Vulnerable 1 to enemy
                GainBlock(state, Blk(def, upgraded));
                ApplyEnemyDebuff(state, BuffId.Vulnerable, 1, rng);
                break;

            case IC.Tremble: // 1-cost, Vulnerable 3/4 to enemy
                ApplyEnemyDebuff(state, BuffId.Vulnerable, upgraded ? 4 : 3, rng);
                break;
            case IC.TrueGrit: // 1-cost, gain 7/9 block; exhaust a random card
            {
                GainBlock(state, Blk(def, upgraded));
                if (state.Hand.Count > 0)
                {
                    int index = rng.Next(state.Hand.Count);
                    var c = state.Hand[index];
                    state.Hand.RemoveAt(index);
                    ExhaustCard(state, c, rng: rng);
                }
                break;
            }

            // ── Ironclad Power Cards ─────────────────────────────────────────────

            case IC.Barricade: // 3/2-cost, block no longer expires
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Barricade, 1);
                break;

            case IC.Aggression: // 1-cost, start of turn add a random upgraded Ironclad card (Innate when upgraded)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Aggression, 1);
                break;

            case IC.Corruption: // 3/2-cost, Skills cost 0 and exhaust
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Corruption, 1);
                break;

            case IC.CrimsonMantle: // 1-cost, start of turn lose N HP and gain 8/10 block; increment N when played
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CrimsonMantleSelfDamage, 1);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CrimsonMantleBlock, upgraded ? 10 : 8);
                break;

            case IC.Pyre: // 2-cost, gain 1/2 Max Energy
                BuffSystem.Apply(state.PlayerBuffs, BuffId.PyrePower, upgraded ? 2 : 1);
                break;

            case IC.Cruelty: // 1-cost, increase Vulnerable multiplier by 25/50%
                BuffSystem.Apply(state.PlayerBuffs, BuffId.CrueltyPower, upgraded ? 50 : 25);
                break;

            case IC.DarkEmbrace: // 2-cost, draw 1 card when a card is exhausted (upgraded costs 1)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.DarkEmbrace, 1);
                break;

            case IC.DemonicShield: // 0-cost, lose 1 HP, double target ally's block (self in SP)
                LoseHp(state, 1);
                GainBlock(state, state.PlayerBlock);
                break;

            case IC.DemonForm: // 3-cost, gain 2/3 Strength each player turn start
                BuffSystem.Apply(state.PlayerBuffs, BuffId.DemonForm, upgraded ? 3 : 2);
                break;

            case IC.FeelNoPain: // 1-cost, gain 3/4 block when exhausting cards
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FeelNoPain, upgraded ? 4 : 3);
                break;

            case IC.Hellraiser: // 2-cost, whenever you draw a Strike, play it (upgraded costs 1)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Hellraiser, 1);
                break;

            case IC.Inflame: // 1-cost, immediately gain 2/3 Strength
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, upgraded ? 3 : 2);
                break;

            case IC.Inferno: // 1-cost, self-damage each turn; taking unblocked self-damage burns all enemies
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Inferno, upgraded ? 9 : 6);
                BuffSystem.Apply(state.PlayerBuffs, BuffId.InfernoSelfDamage, 1);
                break;

            case IC.Juggernaut: // 2-cost, deal 5/7 dmg when gaining block
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Juggernaut, upgraded ? 7 : 5);
                break;

            case IC.Juggling: // 1-cost, copy the third Attack played each turn into hand
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Juggling, 1);
                break;

            case IC.Nostalgia: // 1/0-cost, first Attack/Skill each turn goes on top of draw pile
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Nostalgia, 1);
                break;

            case IC.Rage: // 0-cost, gain 3/5 block when playing an Attack this turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Rage, upgraded ? 5 : 3);
                break;

            case IC.Rupture: // 1-cost, gain 1/2 Strength when losing HP via card effects
                BuffSystem.Apply(state.PlayerBuffs, BuffId.RupturePower, upgraded ? 2 : 1);
                break;

            case IC.StoneArmor: // 1-cost, gain 4/6 Plating (block each end of turn, decays 1/turn)
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Plating, upgraded ? 6 : 4);
                break;

            case IC.Vicious: // 1-cost, draw 1/2 whenever you apply Vulnerable
                BuffSystem.Apply(state.PlayerBuffs, BuffId.Vicious, upgraded ? 2 : 1);
                break;

            // ── Colorless ────────────────────────────────────────────────────────

            case CL.Alchemize: // 1/0-cost, gain random potion
                ProcureRandomPotion(state, rng);
                break;

            case CL.Anointed: // 1-cost, draw all Rare cards from draw pile
                DrawRareCards(state, upgraded, rng);
                break;

            case CL.Discovery: // 1-cost, choose a card to add to hand; it's free this turn
            {
                int defId = _ironcladPool[rng.Next(_ironcladPool.Length)];
                if (state.Hand.Count < MaxCardsInHand)
                    state.Hand.Add(new CardInstance(defId, false, FreeThisTurn: true));
                break;
            }

            case CL.Finesse: // 0-cost, 2/4 block + draw 1
                GainBlock(state, upgraded ? 4 : 2);
                DrawCards(state, 1, rng);
                break;

            case CL.FlashOfSteel: // 0-cost, 3/6 dmg + draw 1
                DealDamage(state, upgraded ? 6 : 3);
                DrawCards(state, 1, rng);
                break;

            case CL.HandOfGreed: // 2-cost, 20/25 dmg; gain 20/25 gold on fatal
            {
                var target = FirstEnemy(state);
                if (target != null)
                {
                    int hpBefore = target.Hp;
                    DealDamageToEnemy(state, target, upgraded ? 25 : 20);
                    if (
                        target.Hp <= 0
                        && hpBefore > 0
                        && !BuffSystem.Has(target.Buffs, BuffId.Minion)
                    )
                        state.PlayerGold += upgraded ? 25 : 20;
                }
                break;
            }

            case CL.Entropy: // 2-cost, transform 1/2 cards in hand each turn
                BuffSystem.Apply(state.PlayerBuffs, BuffId.EntropyPower, upgraded ? 2 : 1);
                break;

            case CL.Fasten: // 1-cost, Defend cards give 4/6 extra block
                BuffSystem.Apply(state.PlayerBuffs, BuffId.FastenPower, upgraded ? 6 : 4);
                break;

            // ── Fallback ─────────────────────────────────────────────────────────

            default:
                if (def.Type != CardType.Status && def.Type != CardType.Curse)
                {
                    int dmg = upgraded ? def.BaseDamage + def.UpgradeDamage : def.BaseDamage;
                    int blk = upgraded ? def.BaseBlock + def.UpgradeBlock : def.BaseBlock;
                    if (dmg > 0)
                        DealDamage(state, dmg);
                    if (blk > 0)
                        GainBlock(state, blk, isDefend: def.Name.Contains("Defend"));
                }
                break;
        }
    }

    // ── Card-pile helpers ─────────────────────────────────────────────────────

    private static void DrawRareCards(CombatState state, bool retain, Random rng)
    {
        var rareIndices = state
            .DrawPile.Select((c, i) => new { Card = c, Index = i })
            .Where(x => GeneratedData.Cards.Get(x.Card.DefId).Rarity == CardRarity.Rare)
            .OrderByDescending(x => x.Index)
            .ToList();

        foreach (var item in rareIndices)
        {
            if (state.Hand.Count >= MaxCardsInHand)
                break;
            state.Hand.Add(item.Card with { Retain = retain });
            state.DrawPile.RemoveAt(item.Index);
        }
    }

    private static void ProcureRandomPotion(CombatState state, Random rng)
    {
        for (int i = 0; i < state.MaxPotionSlots; i++)
        {
            if (state.PotionSlots[i] == 0)
            {
                state.PotionSlots[i] = rng.Next(1, 64); // 1 to 63
                break;
            }
        }
    }

    internal static void TransformRandomCardInHand(CombatState state, Random rng)
    {
        if (state.Hand.Count == 0)
            return;
        int idx = rng.Next(state.Hand.Count);
        int defId = _ironcladPool[rng.Next(_ironcladPool.Length)];
        state.Hand[idx] = new CardInstance(defId, false);
    }

    public static void DrawCards(CombatState state, int count, Random rng)
    {
        for (int i = 0; i < count; i++)
        {
            if (state.DrawPile.Count == 0)
            {
                // StableShuffle: sort then FY-shuffle (matches STS2 CardPileCmd.Shuffle).
                state.DrawPile = state
                    .DiscardPile.OrderBy(c => GeneratedData.Cards.Get(c.DefId).Name)
                    .ThenBy(c => c.Upgraded ? 1 : 0)
                    .ToList();
                ShufflePile(state.DrawPile, state.ShuffleRng ?? rng);
                state.DiscardPile.Clear();
            }
            if (state.DrawPile.Count == 0)
                break;

            var card = state.DrawPile[0];
            state.DrawPile.RemoveAt(0);

            if (
                BuffSystem.Get(state.PlayerBuffs, BuffId.Hellraiser) > 0
                && IsStrikeCard(card.DefId)
            )
            {
                state.AutoPlayQueue.Add(card);
            }
            else if (state.Hand.Count < MaxCardsInHand)
            {
                state.Hand.Add(card);
            }
            else
            {
                state.DiscardPile.Add(card);
            }
        }
    }

    private static bool IsStrikeCard(int defId)
    {
        var name = GeneratedData.Cards.Get(defId).Name;
        return name.Contains("Strike", StringComparison.OrdinalIgnoreCase);
    }

    public static void ShufflePile<T>(IList<T> pile, Random rng)
    {
        for (int i = pile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pile[i], pile[j]) = (pile[j], pile[i]);
        }
    }

    // Adds card to exhaust pile and triggers exhaust hooks.
    public static void ExhaustCard(
        CombatState state,
        CardInstance card,
        bool causedByEthereal = false,
        Random? rng = null
    )
    {
        state.ExhaustPile.Add(card with { FreeThisTurn = false });
        state.CardsExhaustedThisTurn++;
        if (card.DefId == IC.DrumOfBattle)
            state.Energy += card.Upgraded ? 3 : 2;

        int fnp = BuffSystem.Get(state.PlayerBuffs, BuffId.FeelNoPain);
        if (fnp > 0)
            state.PlayerBlock += BuffSystem.IncomingBlock(fnp, state.PlayerBuffs);

        int de = BuffSystem.Get(state.PlayerBuffs, BuffId.DarkEmbrace);
        if (de > 0)
        {
            if (causedByEthereal)
                state.EtherealExhaustCount++;
            else if (rng != null)
                DrawCards(state, de, rng);
        }
    }

    // ── Combat helpers ────────────────────────────────────────────────────────

    public static void DealDamage(CombatState state, int amount)
    {
        var target = FirstEnemy(state);
        if (target != null)
            DealDamageToEnemy(state, target, amount);
    }

    public static void DealDamageToPlayer(CombatState state, int amount)
    {
        int absorbed = Math.Min(state.PlayerBlock, amount);
        state.PlayerBlock -= absorbed;
        int hpLoss = amount - absorbed;
        if (hpLoss > 0)
        {
            state.PlayerHp -= hpLoss;
            state.PlayerHpLostThisTurn += hpLoss;
        }
    }

    public static void DealDamageToAll(CombatState state, int amount)
    {
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
            DealDamageToEnemy(state, enemy, amount);
    }

    // Deals `amount` to first living enemy, `hits` times.
    public static void DealDamageMultiHit(CombatState state, int amount, int hits, Random rng)
    {
        for (int i = 0; i < hits; i++)
        {
            var target = FirstEnemy(state);
            if (target is null)
                break;
            DealDamageToEnemy(state, target, amount);
        }
    }

    // Deals `amount` to every living enemy, repeated `hits` times.
    public static void DealDamageToAllMultiHit(CombatState state, int amount, int hits)
    {
        for (int i = 0; i < hits; i++)
            foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
                DealDamageToEnemy(state, enemy, amount);
    }

    private static void DealDamageToRandomEnemiesMultiHit(
        CombatState state,
        int amount,
        int hits,
        Random rng
    )
    {
        for (int i = 0; i < hits; i++)
        {
            var livingEnemies = state.Enemies.Where(e => e.Hp > 0).ToList();
            if (livingEnemies.Count == 0)
                return;

            DealDamageToEnemy(state, livingEnemies[rng.Next(livingEnemies.Count)], amount);
        }
    }

    private static int DealDamageToEnemy(CombatState state, EnemyState target, int amount)
    {
        TriggerEnemyThorns(state, target);

        int damage = BuffSystem.IncomingDamage(amount, state.PlayerBuffs, target.Buffs);
        int cap = BuffSystem.Get(target.Buffs, BuffId.HardToKill);
        if (cap > 0)
            damage = Math.Min(damage, cap);
        int absorbed = Math.Min(target.Block, damage);
        target.Block -= absorbed;
        int hpLoss = damage - absorbed;

        int hardened = BuffSystem.Get(target.Buffs, BuffId.HardenedShell);
        if (hardened > 0)
        {
            hpLoss = Math.Min(hpLoss, hardened);
            BuffSystem.Apply(target.Buffs, BuffId.HardenedShell, -hpLoss);
        }

        int slippery = BuffSystem.Get(target.Buffs, BuffId.Slippery);
        if (slippery > 0 && hpLoss >= 1)
        {
            hpLoss = 1;
            BuffSystem.Apply(target.Buffs, BuffId.Slippery, -1);
        }
        target.Hp = Math.Max(0, target.Hp - hpLoss);
        if (target.Hp == 0)
            OnEnemyDeath(state, target);
        return hpLoss;
    }

    private static void OnEnemyDeath(CombatState state, EnemyState enemy)
    {
        // ShrinkerBeetle: permanent Shrink (ShrinkPower) is removed when its applier dies.
        if (enemy.DefId == KE.ShrinkerBeetle)
            BuffSystem.Remove(state.PlayerBuffs, BuffId.Shrink);
    }

    public static void GainBlock(CombatState state, int amount) =>
        GainBlock(state, amount, powered: true);

    public static void GainUnpoweredBlock(CombatState state, int amount) =>
        GainBlock(state, amount, powered: false);

    private static void GainBlock(
        CombatState state,
        int amount,
        bool powered = true,
        bool isDefend = false
    )
    {
        int effective = powered
            ? BuffSystem.IncomingBlock(amount, state.PlayerBuffs, isDefend)
            : amount;
        if (effective <= 0)
            return;

        int unmovable = BuffSystem.Get(state.PlayerBuffs, BuffId.UnmovablePower);
        if (unmovable > state.BlockGainsThisTurn)
        {
            effective *= 2;
            state.BlockGainsThisTurn++;
        }

        state.PlayerBlock += effective;

        // Juggernaut: deal unpowered damage to a random enemy when block is gained.
        int jug = BuffSystem.Get(state.PlayerBuffs, BuffId.Juggernaut);
        if (jug > 0)
        {
            var target = FirstEnemy(state);
            if (target != null)
            {
                int abs = Math.Min(target.Block, jug);
                target.Block -= abs;
                target.Hp = Math.Max(0, target.Hp - (jug - abs));
            }
        }
    }

    // Deals unblockable, unpowered HP loss to the player and triggers Rupture.
    public static void LoseHp(CombatState state, int amount)
    {
        int hpBefore = state.PlayerHp;
        state.PlayerHp = Math.Max(0, state.PlayerHp - amount);
        state.PlayerHpLostThisTurn += Math.Max(0, hpBefore - state.PlayerHp);

        int rupt = BuffSystem.Get(state.PlayerBuffs, BuffId.RupturePower);
        if (rupt > 0 && hpBefore > state.PlayerHp)
            BuffSystem.Apply(state.PlayerBuffs, BuffId.Strength, rupt);

        TriggerInfernoAfterPlayerSelfDamage(state, hpBefore - state.PlayerHp);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int Dmg(CardDef def, bool upgraded) =>
        upgraded ? def.BaseDamage + def.UpgradeDamage : def.BaseDamage;

    private static int Blk(CardDef def, bool upgraded) =>
        upgraded ? def.BaseBlock + def.UpgradeBlock : def.BaseBlock;

    private static EnemyState? FirstEnemy(CombatState state)
    {
        int idx = state.TargetEnemyIndex;
        if (idx >= 0 && idx < state.Enemies.Count && state.Enemies[idx].Hp > 0)
            return state.Enemies[idx];
        return state.Enemies.FirstOrDefault(e => e.Hp > 0);
    }

    private static void ExhaustRandomCardFromHand(CombatState state, Random rng)
    {
        if (state.Hand.Count == 0)
            return;

        int index = rng.Next(state.Hand.Count);
        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        ExhaustCard(state, card, rng: rng);
    }

    private static void ExhaustRandomCardOfTypeFromHand(
        CombatState state,
        CardType type,
        Random rng
    )
    {
        var candidates = state
            .Hand.Select((c, i) => (card: c, idx: i))
            .Where(t => GeneratedData.Cards.Get(t.card.DefId).Type == type)
            .ToList();
        if (candidates.Count == 0)
            return;

        var chosen = candidates[rng.Next(candidates.Count)];
        state.Hand.RemoveAt(chosen.idx);
        ExhaustCard(state, chosen.card, rng: rng);
    }

    private static void UpgradeFirstCardInHand(CombatState state)
    {
        for (int i = 0; i < state.Hand.Count; i++)
        {
            if (!IsUpgradable(state.Hand[i]))
                continue;

            state.Hand[i] = state.Hand[i] with { Upgraded = true };
            return;
        }
    }

    private static void UpgradeAllCardsInHand(CombatState state)
    {
        for (int i = 0; i < state.Hand.Count; i++)
        {
            if (IsUpgradable(state.Hand[i]))
                state.Hand[i] = state.Hand[i] with { Upgraded = true };
        }
    }

    private static bool IsUpgradable(CardInstance card)
    {
        if (card.Upgraded)
            return false;

        var def = GeneratedData.Cards.Get(card.DefId);
        return def.Type is not (CardType.Status or CardType.Curse);
    }

    private const int MaxCardsInHand = 10;

    private static void DrawUntilNonAttack(CombatState state, Random rng)
    {
        while (state.Hand.Count < MaxCardsInHand)
        {
            int handCountBefore = state.Hand.Count;
            DrawCards(state, 1, rng);
            if (state.Hand.Count == handCountBefore)
                return;

            var drawnCard = state.Hand[^1];
            if (GeneratedData.Cards.Get(drawnCard.DefId).Type != CardType.Attack)
                return;
        }
    }

    private static void MoveDiscardCardsToHand(CombatState state, int count)
    {
        int cardsToMove = Math.Min(count, MaxCardsInHand - state.Hand.Count);
        for (int i = 0; i < cardsToMove && state.DiscardPile.Count > 0; i++)
        {
            var card = state.DiscardPile[0];
            state.DiscardPile.RemoveAt(0);
            state.Hand.Add(card with { FreeThisTurn = false });
        }
    }

    private static void TriggerInfernoAfterPlayerSelfDamage(CombatState state, int unblockedDamage)
    {
        int inferno = BuffSystem.Get(state.PlayerBuffs, BuffId.Inferno);
        if (!state.PlayerTurn || unblockedDamage <= 0 || inferno <= 0)
            return;

        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0).ToList())
            DealUnpoweredDamageToEnemy(enemy, inferno);
    }

    private static void DealOmnislice(CombatState state, int amount)
    {
        var target = FirstEnemy(state);
        if (target is null)
            return;

        int splashDamage = DealDamageToEnemy(state, target, amount);
        if (splashDamage <= 0)
            return;

        foreach (
            var enemy in state.Enemies.Where(e => e.Hp > 0 && !ReferenceEquals(e, target)).ToList()
        )
            DealUnpoweredDamageToEnemy(state, enemy, splashDamage, triggerThorns: true);
    }

    private static void AddRandomInfernalBladeAttack(CombatState state, Random rng)
    {
        if (state.Hand.Count >= MaxCardsInHand)
            return;

        int[] options = [.. _infernalBladeAttackPool];
        for (int i = options.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (options[i], options[j]) = (options[j], options[i]);
        }

        state.Hand.Add(new CardInstance(options[0], false, FreeThisTurn: true));
    }

    private static void DealUnpoweredDamageToEnemy(EnemyState target, int amount) =>
        DealUnpoweredDamageToEnemy(null, target, amount, triggerThorns: false);

    private static void DealUnpoweredDamageToEnemy(
        CombatState? state,
        EnemyState target,
        int amount,
        bool triggerThorns
    )
    {
        if (triggerThorns && state != null)
            TriggerEnemyThorns(state, target);

        int damage = Math.Max(0, amount);
        int cap = BuffSystem.Get(target.Buffs, BuffId.HardToKill);
        if (cap > 0)
            damage = Math.Min(damage, cap);
        int absorbed = Math.Min(target.Block, damage);
        target.Block -= absorbed;
        int hpLoss = damage - absorbed;
        int slippery = BuffSystem.Get(target.Buffs, BuffId.Slippery);
        if (slippery > 0 && hpLoss >= 1)
        {
            hpLoss = 1;
            BuffSystem.Apply(target.Buffs, BuffId.Slippery, -1);
        }
        target.Hp = Math.Max(0, target.Hp - hpLoss);
    }

    private static void TriggerEnemyThorns(CombatState state, EnemyState target)
    {
        int thorns = BuffSystem.Get(target.Buffs, BuffId.Thorns);
        if (thorns <= 0)
            return;

        int hpBeforeThorns = state.PlayerHp;
        state.PlayerHp = Math.Max(0, state.PlayerHp - thorns);
        state.PlayerHpLostThisTurn += Math.Max(0, hpBeforeThorns - state.PlayerHp);
    }

    private static void ApplyEnemyDebuff(CombatState state, BuffId id, int magnitude, Random rng)
    {
        var target = FirstEnemy(state);
        if (target == null)
            return;

        ApplyEnemyDebuffToTarget(state, target, id, magnitude, rng);
    }

    private static void ApplyEnemyDebuffToTarget(
        CombatState state,
        EnemyState target,
        BuffId id,
        int magnitude,
        Random rng
    )
    {
        if (target.Hp <= 0)
            return;

        int before = BuffSystem.Get(target.Buffs, id);
        BuffSystem.Apply(target.Buffs, id, magnitude);
        DrawForVicious(state, id, before, BuffSystem.Get(target.Buffs, id), rng);
    }

    private static void ApplyTemporaryStrengthDownToEnemy(CombatState state, int amount)
    {
        var target = FirstEnemy(state);
        if (target == null)
            return;
        if (BuffSystem.TryConsumeArtifact(target.Buffs))
            return;

        BuffSystem.Apply(target.Buffs, BuffId.Strength, -amount);
        BuffSystem.Apply(target.Buffs, BuffId.TemporaryStrength, amount);
    }

    private static void ApplyAllEnemyDebuff(CombatState state, BuffId id, int magnitude, Random rng)
    {
        foreach (var enemy in state.Enemies.Where(e => e.Hp > 0))
        {
            int before = BuffSystem.Get(enemy.Buffs, id);
            BuffSystem.Apply(enemy.Buffs, id, magnitude);
            DrawForVicious(state, id, before, BuffSystem.Get(enemy.Buffs, id), rng);
        }
    }

    private static void DrawForVicious(
        CombatState state,
        BuffId id,
        int before,
        int after,
        Random rng
    )
    {
        int vicious = BuffSystem.Get(state.PlayerBuffs, BuffId.Vicious);
        if (id == BuffId.Vulnerable && vicious > 0 && after > before)
            DrawCards(state, vicious, rng);
    }

    private static readonly HashSet<string> _strikeNames = new(StringComparer.Ordinal)
    {
        "StrikeIronclad",
        "StrikeSilent",
        "StrikeDefect",
        "StrikeRegent",
        "StrikeNecrobinder",
        "TwinStrike",
        "PommelStrike",
        "PerfectedStrike",
        "SetupStrike",
        "AshenStrike",
        "AdaptiveStrike",
        "BlightStrike",
        "FocusedStrike",
        "LeadingStrike",
        "MeteorStrike",
        "MinionStrike",
        "MomentumStrike",
        "SculptingStrike",
        "SeekerStrike",
        "ShiningStrike",
        "SolarStrike",
        "UltimateStrike",
    };

    internal static void AddRandomUpgradedIroncladCardToHand(
        CombatState state,
        int count,
        Random rng
    )
    {
        for (int i = 0; i < count; i++)
        {
            if (state.Hand.Count >= MaxCardsInHand)
                break;

            int defId = _ironcladPool[rng.Next(_ironcladPool.Length)];
            state.Hand.Add(new CardInstance(defId, true));
        }
    }

    private static readonly int[] _ironcladPool =
    [
        IC.Aggression,
        IC.Anger,
        IC.Armaments,
        IC.AshenStrike,
        IC.Barricade,
        IC.BattleTrance,
        IC.BloodWall,
        IC.Bloodletting,
        IC.Bludgeon,
        IC.BodySlam,
        IC.Brand,
        IC.Break,
        IC.Breakthrough,
        IC.Bully,
        IC.BurningPact,
        IC.Cinder,
        IC.Colossus,
        IC.Conflagration,
        IC.Corruption,
        IC.CrimsonMantle,
        IC.Cruelty,
        IC.DarkEmbrace,
        IC.DemonForm,
        IC.DemonicShield,
        IC.Dismantle,
        IC.Dominate,
        IC.DrumOfBattle,
        IC.EvilEye,
        IC.ExpectAFight,
        IC.Feed,
        IC.FeelNoPain,
        IC.FiendFire,
        IC.FightMe,
        IC.FlameBarrier,
        IC.ForgottenRitual,
        IC.Havoc,
        IC.Headbutt,
        IC.Hellraiser,
        IC.Hemokinesis,
        IC.HowlFromBeyond,
        IC.Impervious,
        IC.InfernalBlade,
        IC.Inferno,
        IC.Inflame,
        IC.IronWave,
        IC.Juggernaut,
        IC.Juggling,
        IC.Mangle,
        IC.MoltenFist,
        IC.NotYet,
        IC.Offering,
        IC.OneTwoPunch,
        IC.PactsEnd,
        IC.PerfectedStrike,
        IC.Pillage,
        IC.PommelStrike,
        IC.PrimalForce,
        IC.Pyre,
        IC.Rage,
        IC.Rampage,
        IC.Rupture,
        IC.SecondWind,
        IC.SetupStrike,
        IC.ShrugItOff,
        IC.Spite,
        IC.Stampede,
        IC.Stoke,
        IC.Stomp,
        IC.StoneArmor,
        IC.SwordBoomerang,
        IC.Tank,
        IC.Taunt,
        IC.TearAsunder,
        IC.Thrash,
        IC.Thunderclap,
        IC.Tremble,
        IC.TrueGrit,
        IC.TwinStrike,
        IC.Unmovable,
        IC.Unrelenting,
        IC.Uppercut,
        IC.Vicious,
        IC.Whirlwind,
    ];

    private static readonly int[] _infernalBladeAttackPool =
    [
        IC.Anger,
        IC.AshenStrike,
        IC.BodySlam,
        IC.Break,
        IC.Breakthrough,
        IC.Bludgeon,
        IC.Bully,
        IC.Cinder,
        IC.Conflagration,
        IC.Dismantle,
        IC.FiendFire,
        IC.FightMe,
        IC.Headbutt,
        IC.Hemokinesis,
        IC.HowlFromBeyond,
        IC.Mangle,
        IC.MoltenFist,
        IC.PactsEnd,
        IC.PerfectedStrike,
        IC.Pillage,
        IC.PommelStrike,
        IC.Rampage,
        IC.SetupStrike,
        IC.Spite,
        IC.Stomp,
        IC.SwordBoomerang,
        IC.TearAsunder,
        IC.Thrash,
        IC.Thunderclap,
        IC.TwinStrike,
        IC.Unrelenting,
        IC.Uppercut,
        IC.Whirlwind,
    ];

    private static int CountStrikeCards(CombatState state)
    {
        int count = 0;
        foreach (
            var pile in new[] { state.Hand, state.DrawPile, state.DiscardPile, state.ExhaustPile }
        )
        foreach (var c in pile)
            if (_strikeNames.Contains(GeneratedData.Cards.Get(c.DefId).Name))
                count++;
        return count;
    }
}

// Card IDs for all Ironclad cards (from Generated/Cards.g.cs).
public static class IC
{
    // Basic
    public const int AscendersBane = 10001;
    public const int StrikeIronclad = 472;
    public const int DefendIronclad = 131;

    // Common Attacks
    public const int Anger = 13;
    public const int AshenStrike = 20;
    public const int Bash = 30;
    public const int BodySlam = 50;
    public const int Break = 59;
    public const int Breakthrough = 60;
    public const int Headbutt = 240;
    public const int IronWave = 268;
    public const int MoltenFist = 313;
    public const int PommelStrike = 358;
    public const int SetupStrike = 421;
    public const int Spite = 454;
    public const int Thunderclap = 508;
    public const int TwinStrike = 519;

    // Common Skills
    public const int Armaments = 18;
    public const int BloodWall = 46;
    public const int Bloodletting = 45;
    public const int ShrugItOff = 433;
    public const int Tremble = 516;
    public const int TrueGrit = 517;

    // Uncommon Attacks
    public const int Bludgeon = 47;
    public const int Bully = 66;
    public const int Cinder = 87;
    public const int Dismantle = 147;
    public const int FightMe = 189;
    public const int Hemokinesis = 247;
    public const int Pillage = 353;
    public const int Rampage = 381;
    public const int Stomp = 465;
    public const int SwordBoomerang = 486;
    public const int Unrelenting = 526;
    public const int Uppercut = 529;

    // Uncommon Skills
    public const int BattleTrance = 31;
    public const int BurningPact = 69;
    public const int Colossus = 95;
    public const int Dominate = 150;
    public const int DrumOfBattle = 155;
    public const int EvilEye = 174;
    public const int ExpectAFight = 175;
    public const int FlameBarrier = 195;
    public const int ForgottenRitual = 205;
    public const int Havoc = 238;
    public const int InfernalBlade = 262;
    public const int Nostalgia = 327;
    public const int Rage = 378;
    public const int Restlessness = 396;
    public const int SecondWind = 414;
    public const int Splash = 455;
    public const int Taunt = 493;
    public const int UltimateDefend = 521;

    // Rare Attacks
    public const int FiendFire = 188;
    public const int Feed = 183;
    public const int HowlFromBeyond = 254;
    public const int Mangle = 295;
    public const int PactsEnd = 339;
    public const int PerfectedStrike = 349;
    public const int TearAsunder = 494;
    public const int Thrash = 505;
    public const int Whirlwind = 538;

    // Missing
    public const int Brand = 58;
    public const int CrimsonMantle = 113;
    public const int Cruelty = 114;
    public const int DemonicShield = 142;
    public const int Hellraiser = 246;
    public const int GiantRock = 217;
    public const int PrimalForce = 364;
    public const int Pyre = 374;
    public const int Stoke = 464;
    public const int Tank = 492;
    public const int Unmovable = 525;

    // Rare Skills
    public const int Conflagration = 99;
    public const int Impervious = 261;
    public const int NotYet = 328;
    public const int OneTwoPunch = 334;
    public const int Offering = 332;

    // Powers
    public const int Barricade = 29;
    public const int Aggression = 9;
    public const int Corruption = 107;
    public const int DarkEmbrace = 119;
    public const int DemonForm = 141;
    public const int FeelNoPain = 185;
    public const int Inflame = 265;
    public const int Inferno = 263;
    public const int Juggling = 273;
    public const int Juggernaut = 272;
    public const int Rupture = 404;
    public const int Stampede = 462;
    public const int StoneArmor = 466;
    public const int Vicious = 533;
}

public static class CL
{
    public const int Alchemize = 10;
    public const int Anointed = 14;
    public const int Bolas = 51;
    public const int DarkShackles = 121;
    public const int Discovery = 146;
    public const int DramaticEntrance = 153;
    public const int Entropy = 219;
    public const int Fasten = 232;
    public const int Omnislice = 333;

    public const int Prolong = 366;
    public const int Salvo = 406;
    public const int Finesse = 191;
    public const int FlashOfSteel = 197;
    public const int HandOfGreed = 234;
    public const int Volley = 535;
}

public static class AN
{
    public const int NeowsFury = 321;
}

public static class ST
{
    public const int Dazed = 10002;
    public const int Slimed = 440;
    public const int Toxic = 512;
    public const int Infection = 10008;
    public const int Burn = 10009;
    public const int Beckon = 36;
    public const int Disintegration = 10010;
    public const int FranticEscape = 206;
    public const int Wound = 10011;
    public const int Wither = 10012;
}
