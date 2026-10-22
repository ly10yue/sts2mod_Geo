#if JIU_VALIDATION
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters.Mocks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static void AddSpareEnemy(Fixture f)
        => f.State.AddCreature(f.State.CreateCreature(ModelDb.Monster<MockAttackMonster>().ToMutable(), CombatSide.Enemy, null));
    private static async Task FatalReworkCases()
    {
        _current="upgrade-preview-only-changed-values-green";
        foreach(var original in ModelDb.CardPool<GeopeliaCardPool>().AllCards.Cast<GeopeliaCard>())
        {
            var card=(GeopeliaCard)original.ToMutable();card.UpgradeInternal();
            for(int i=0;i<card.Definition.Steps.Length;i++)
            {
                var s=card.Definition.Steps[i];
                Check(card.DynamicVars["V"+i].WasJustUpgraded==(s.Upgrade!=s.Value),"only changed V values are marked as upgrades");
                Check(card.DynamicVars["X"+i].WasJustUpgraded==((s.Ux??s.X)!=s.X),"unchanged X parameters are not marked green");
            }
            Check(card.EnergyCost.WasJustUpgraded==(!card.Definition.CostsX && card.Definition.Cost!=card.Definition.UpgradeCost),"only changed energy costs are marked green");
        }
        var unchanged=(GeopeliaCard)ModelDb.Card<GeopeliaRest>().ToMutable();unchanged.UpgradeInternal();
        Check(!unchanged.GetDescriptionForUpgradePreview().Contains("#7fff"),"cost-only upgrade does not highlight unchanged effect numbers");
        Rows.Add(new{test="all-85-native-upgrade-preview-variable-flags"});

        _current="radiance-exhaust-draw-native-replay";
        foreach(bool upgraded in new[]{false,true})
        foreach(var kind in new[]{"normal","status","curse","empty","chain"})
        {
            var f=NewFixture("RADIANCE-"+kind);await StockDrawPile(f);
            var choices=new List<CardModel>();
            if(kind!="empty")
            {
                CardModel template=kind is "status" or "chain"?ModelDb.Card<Dazed>():kind=="curse"?ModelDb.Card<Regret>():ModelDb.Card<GeopeliaDefend>();
                choices.Add(f.State.CreateCard(template,f.Player));
                if(kind=="chain") {choices.Add(f.State.CreateCard(ModelDb.Card<Regret>(),f.Player));choices.Add(f.State.CreateCard(ModelDb.Card<GeopeliaDefend>(),f.Player));}
                foreach(var choice in choices) await CardPileCmd.Add(choice,PileType.Hand,skipVisuals:true);
            }
            var selector=new TestCardSelector();
            foreach(var choice in choices) selector.PrepareToSelect(new[]{0});
            if(kind is "status" or "curse") selector.PrepareToSelect(new[]{0});
            var radiance=f.State.CreateCard(ModelDb.Card<GeopeliaGraceNote>(),f.Player);
            if(upgraded){radiance.UpgradeInternal();radiance.FinalizeUpgradeInternal();}
            using(CardSelectCmd.UseSelector(selector)) await f.Play(radiance);
            int executions=kind=="chain"?3:kind is "status" or "curse"?2:1;
            Check(Runtime.History(f.Player).Count(e=>e.CardPlay.Card==radiance)==executions,"normal, status, curse and chained selections trigger the exact replay count");
            Check(f.Player.PlayerCombatState!.Energy==98,"Radiance and its replays spend only one energy");
            Check(radiance.Pile?.Type==PileType.Discard && !radiance.Keywords.Contains(CardKeyword.Exhaust),"Radiance itself is not given Exhaust");
            Check(choices.All(c=>c.Pile?.Type==PileType.Exhaust),"selected cards are actually exhausted");
            int expectedExhaust=choices.Count+(kind is "status" or "curse"?1:0);
            int expectedHand=(upgraded?2:1)*executions-expectedExhaust+choices.Count;
            Check(PileType.Hand.GetPile(f.Player).Cards.Count==expectedHand,"each resolution draws 1/2 cards, with the selected cards removed");
            Check(Runtime.Signal(f.Player)==0 && Runtime.Debt(f.Player)==0,"old signal and signal debt effects are removed");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="radiance-base-upgraded-normal-status-curse-empty-and-chain"});
        {
            var f=NewFixture("RADIANCE-BURST");await StockDrawPile(f);
            await CardPileCmd.Add(f.State.CreateCard(ModelDb.Card<Dazed>(),f.Player),PileType.Hand,skipVisuals:true);
            await PowerCmd.Apply<BurstPower>(f.Context,f.Player.Creature,1,f.Player.Creature,null);
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaGraceNote>(),f.Player);
            var selector=new TestCardSelector();for(int i=0;i<3;i++) selector.PrepareToSelect(new[]{0});
            using(CardSelectCmd.UseSelector(selector)) await f.Play(card);
            Check(Runtime.History(f.Player).Count(e=>e.CardPlay.Card==card)==3,"Radiance conditional replay stacks with native Burst");
            Check(f.Player.PlayerCombatState!.Energy==98 && PileType.Hand.GetPile(f.Player).Cards.Count==1,"nested replay and Burst each resolve without charging extra energy");
            Check(card.Pile?.Type==PileType.Discard,"nested native replays return Radiance to its normal result pile");
            RunManager.Instance.CleanUp(false);
        }

        _current="qzkago-strict-signal-execution";
        foreach(bool upgraded in new[]{false,true})
        foreach(int signals in new[]{0,3})
        foreach(int delta in new[]{-1,0,1})
        {
            var f=NewFixture("QZKAGO-BOUNDARY");AddSpareEnemy(f);
            int hp=Math.Max(1,signals*(upgraded?4:3)+delta);f.Enemy.SetCurrentHpInternal(hp);
            await CreatureCmd.GainBlock(f.Enemy,100,MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,null);
            await Runtime.AddSignal(f.Context,f.Player,signals);
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaOverdraftSuite>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            await f.Play(card);
            bool shouldDie=hp<signals*(upgraded?4:3);
            Check(f.Enemy.IsDead==shouldDie,"execution uses strict HP threshold and bypasses block");
            Check(Runtime.Signal(f.Player)==(shouldDie?0:signals),"failed threshold preserves all signal; successful execution spends all");
            Check(f.Player.PlayerCombatState!.Energy==98 && Runtime.Debt(f.Player)==0,"QZKago costs one and adds no old debt");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="qzkago-base-upgraded-strict-threshold-zero-signal-and-block"});

        _current="absolute-fatal-reward-tiers";
        foreach(bool upgraded in new[]{false,true})
        foreach(bool minion in new[]{false,true})
        foreach(bool lethal in new[]{false,true})
        {
            var f=NewFixture("ABSOLUTE-FATAL");AddSpareEnemy(f);
            f.Enemy.SetCurrentHpInternal(lethal?1:100);
            if(minion) await PowerCmd.Apply<MinionPower>(f.Context,f.Enemy,1,f.Enemy,null);
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaAbsoluteDisorder>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            await f.Play(card);
            Check(FatalCardRewards.Count(f.State,f.Player)==(lethal&&!minion?1:0),"only native Fatal-eligible lethal hits grant a bonus");
            Check(lethal||f.Enemy.CurrentHp==100-(upgraded?14:10),"damage is 10/14 to one target");
            Check(card.Pile?.Type==PileType.Exhaust && card.EnergyCost.GetResolved()==2 && card.GetEnchantedReplayCount()==0,"Absolute costs two, exhausts, and loses old repeats");
            RunManager.Instance.CleanUp(false);
        }
        {
            var f=NewFixture("REWARD-PROMOTION");
            ((RunState)f.Player.RunState).PushRoom(new CombatRoom(f.State));
            FatalCardRewards.Record(f.State,f.Player);
            var options=CardCreationOptions.ForRoom(f.Player,RoomType.Monster);
            var offered=new[]{f.Player.RunState.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player),f.Player.RunState.CreateCard(ModelDb.Card<GeopeliaRest>(),f.Player),f.Player.RunState.CreateCard(ModelDb.Card<GeopeliaAfterglow>(),f.Player),f.Player.RunState.CreateCard(ModelDb.Card<GeopeliaFullPlayback>(),f.Player)};
            offered[1].UpgradeInternal();offered[1].FinalizeUpgradeInternal();
            var reward=new CardReward(offered,CardCreationSource.Encounter,f.Player,options);reward.Populate();
            var cards=reward.Cards.ToArray();
            Check(cards.Select(c=>c.Rarity).SequenceEqual(new[]{CardRarity.Basic,CardRarity.Uncommon,CardRarity.Rare,CardRarity.Rare}),"common and uncommon rewards each rise one tier; Basic and Rare remain unchanged");
            Check(cards[1].IsUpgraded && ReferenceEquals(cards[3],offered[3]),"upgrade level is retained and already-rare reward stays the same card");
            reward.Populate();Check(reward.Cards.SequenceEqual(cards),"repeated Populate cannot promote a reward twice");
            FatalCardRewards.Record(f.State,f.Player);
            var twice=new CardReward(offered,CardCreationSource.Encounter,f.Player,options);twice.Populate();
            Check(twice.Cards.Skip(1).All(c=>c.Rarity==CardRarity.Rare),"two Fatal kills stack to Rare and never exceed it");
            var other=new CardReward(offered,CardCreationSource.Other,f.Player,options);other.Populate();
            Check(other.Cards.SequenceEqual(offered),"non-combat card rewards are unchanged");
            var peer=Player.CreateForNewRun(ModelDb.Character<Geopelia>(),UnlockState.none,2);
            Check(FatalCardRewards.Count(f.State,peer)==0,"reward bonus belongs only to the killing player");
            var nextBattle=new CombatState(runState:f.Player.RunState);
            Check(FatalCardRewards.Count(nextBattle,f.Player)==0,"reward bonus cannot leak into the next combat");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="absolute-fatal-minion-damage-reward-tier-upgrade-idempotence-and-scope"});
        await FatalVictoryCase(false);await FatalVictoryCase(true);
    }
    private static async Task FatalVictoryCase(bool execute)
    {
        _current=execute?"qzkago-final-kill-victory":"absolute-final-kill-and-real-rewards";
        var player=Player.CreateForNewRun(ModelDb.Character<Geopelia>(),UnlockState.all,1);
        var run=RunState.CreateForTest(new[]{player},seed:"FATAL-VICTORY");
        RunManager.Instance.SetUpTest(run,new NetSingleplayerGameService(),shouldSave:false);LocalContext.NetId=player.NetId;
        var room=new CombatRoom(ModelDb.Encounter<ToadpolesWeak>().ToMutable(),run);run.PushRoom(room);
        var state=room.CombatState;state.AddPlayer(player);room.Encounter.GenerateMonstersWithSlots(run);
        foreach(var (monster,slot) in room.Encounter.MonstersWithSlots) state.AddCreature(state.CreateCreature(monster,CombatSide.Enemy,slot));
        CombatManager.Instance.SetUpCombat(state);
        var turn=AccessTools.Field(typeof(CombatManager),"_turnState").GetValue(CombatManager.Instance)!;
        AccessTools.Property(turn.GetType(),"IsInProgress").SetValue(turn,true);
        player.PlayerCombatState!.Phase=PlayerTurnPhase.Play;player.PlayerCombatState.Energy=99;
        var last=state.Enemies.First();
        await CreatureCmd.Damage(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),state.Enemies.Where(e=>e!=last).ToArray(),999999,MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable|MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,player.Creature);
        last.SetCurrentHpInternal(1);var f=new Fixture(state,player,last);
        if(execute) await Runtime.AddSignal(f.Context,player,1);
        CardModel card=execute?state.CreateCard(ModelDb.Card<GeopeliaOverdraftSuite>(),player):state.CreateCard(ModelDb.Card<GeopeliaAbsoluteDisorder>(),player);
        await f.Play(card);await CombatManager.Instance.CheckWinCondition();
        Check(!CombatManager.Instance.IsInProgress && room.IsPreFinished,"last-enemy execution completes native victory normally");
        var rewards=new RewardsSet(player).WithRewardsFromRoom(room);await rewards.GenerateWithoutOffering();
        Check(rewards.Rewards.All(r=>r.IsPopulated),"native combat rewards populate after the killing card");
        if(!execute) Check(rewards.Rewards.OfType<CardReward>().SelectMany(r=>r.Cards).All(c=>c.Rarity is CardRarity.Uncommon or CardRarity.Rare),"real post-combat card rewards receive the Fatal bonus");
        if(!execute)
        {
            var saved=room.ToSerializable();
            var restored=CombatRoom.FromSerializable(saved,run);
            Check(FatalCardRewards.Count(restored.CombatState,player)==1,"finished-room save and reload preserves the Fatal reward bonus");
            run.PushRoom(restored);
            var restoredRewards=new RewardsSet(player).WithRewardsFromRoom(restored);await restoredRewards.GenerateWithoutOffering();
            Check(restoredRewards.Rewards.OfType<CardReward>().SelectMany(r=>r.Cards).All(c=>c.Rarity is CardRarity.Uncommon or CardRarity.Rare),"reloaded combat card rewards retain the rarity promotion");
        }
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test=execute?"qzkago-native-final-kill-victory":"absolute-native-victory-and-promoted-combat-rewards"});
    }
}
#endif
