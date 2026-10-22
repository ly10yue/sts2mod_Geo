#if JIU_VALIDATION
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task SignalReplayReworkCases()
    {
        _current="signal-capacity-and-reset";
        var f=NewFixture("SIGNAL-CAPACITY");
        await Runtime.AddSignal(f.Context,f.Player,99);
        Check(Runtime.Signal(f.Player)==24,"initial signal cap is 24");
        await PowerCmd.Apply<GeopeliaSignalPower>(f.Context,f.Player.Creature,99,f.Player.Creature,null);
        Check(Runtime.Signal(f.Player)==24,"direct native signal applications also clamp");
        foreach(bool upgraded in new[]{false,true})
        {
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaAfterglow>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            Check(card.EnergyCost.GetResolved()==1 && card.Type==CardType.Power,"Evanescent is a one-cost power");
            await f.Play(card);
            int cap=upgraded?54:36;
            Check(Runtime.SignalLimit(f.Player)==cap && Runtime.Signal(f.Player)==(upgraded?36:24),"capacity stacks without granting signal");
            await Runtime.AddSignal(f.Context,f.Player,99);
            Check(Runtime.Signal(f.Player)==cap,"signal can fill the expanded cap");
        }
        await BeginTurn(f,false);
        Check(Runtime.SignalLimit(f.Player)==54 && Runtime.Signal(f.Player)==54,"expanded capacity persists across turns");
        RunManager.Instance.CleanUp(false);
        f=NewFixture("SIGNAL-CAPACITY-NEW-COMBAT");
        Check(Runtime.SignalLimit(f.Player)==24 && Runtime.Signal(f.Player)==0,"new combat resets capacity and signal");
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="signal-default-24-stacking-capacity-and-combat-reset"});

        _current="opia-generation";
        foreach(bool upgraded in new[]{false,true})
        foreach(int signal in new[]{0,2,3,4,11,12,24})
        {
            f=NewFixture("OPIA-"+signal);
            await Runtime.AddSignal(f.Context,f.Player,signal);
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaShieldArray>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            await f.Play(card);
            var generated=PileType.Hand.GetPile(f.Player).Cards.ToArray();
            Check(generated.Length==Math.Min(12,signal)/(upgraded?3:4),"opia rounds down after spending at most twelve");
            Check(generated.All(c=>c.Rarity==CardRarity.Rare && c.Pool is GeopeliaCardPool),"opia generates only Jiu rare cards");
            Check(Runtime.Signal(f.Player)==Math.Max(0,signal-12),"opia spends the remainder too, but never more than twelve");
            Check(card.EnergyCost.GetResolved()==1,"opia costs one");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="opia-base-upgraded-signal-boundaries",signals=new[]{0,2,3,4,11,12,24}});

        _current="condor-combat-history-multihit";
        foreach(bool upgraded in new[]{false,true})
        {
            f=NewFixture("CONDOR");
            var empty=f.State.CreateCard(ModelDb.Card<GeopeliaDeadline>(),f.Player);
            await f.Play(empty);
            Check(f.Enemy.CurrentHp==9999,"Condor deals no hits with empty debt history");
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaBorrowedLight>(),f.Player));
            var replay=f.State.CreateCard(ModelDb.Card<GeopeliaBorrowedLight>(),f.Player);
            await CardCmd.AutoPlay(f.Context,replay,null);
            Check(Runtime.DebtCardsPlayed(f.Player)==2,"manual and native automatic debt plays both count");
            await BeginTurn(f,false);
            Check(Runtime.DebtCardsPlayed(f.Player)==2,"combat history survives next-turn debt repayment");
            await PowerCmd.Apply<StrengthPower>(f.Context,f.Player.Creature,2,f.Player.Creature,null);
            var condor=f.State.CreateCard(ModelDb.Card<GeopeliaDeadline>(),f.Player);
            if(upgraded){condor.UpgradeInternal();condor.FinalizeUpgradeInternal();}
            await f.Play(condor);
            Check(9999-f.Enemy.CurrentHp==2*((upgraded?11:9)+2),"Condor hits once per debt play and Strength applies to each hit");
            Check(condor.EnergyCost.GetResolved()==1 && Runtime.DebtCardsPlayed(f.Player)==2,"Condor costs one and adds no debt of its own");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="condor-combat-history-native-multihit-strength"});

        _current="historical-replay-native-card-identity-and-hooks";
        foreach(var type in new[]{typeof(GeopeliaReplayBuffer),typeof(GeopeliaChristmasCarol),typeof(GeopeliaPhantomEnsemble),typeof(GeopeliaFullPlayback),typeof(GeopeliaSafetyCopy)})
        foreach(bool upgraded in new[]{false,true})
        {
            f=NewFixture("HISTORICAL-REPLAY");await StockDrawPile(f);
            var prior=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
            prior.UpgradeInternal();prior.FinalizeUpgradeInternal();prior.BaseReplayCount=1;
            await f.Play(prior);
            await PowerCmd.Apply<BurstPower>(f.Context,f.Player.Creature,1,f.Player.Creature,null);
            var engine=f.State.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)),f.Player);
            if(upgraded){engine.UpgradeInternal();engine.FinalizeUpgradeInternal();}
            int energy=f.Player.PlayerCombatState!.Energy;
            await f.Play(engine);
            var automatic=Runtime.History(f.Player).Where(e=>e.CardPlay.IsAutoPlay).ToArray();
            Check(automatic.Length==4 && automatic.All(e=>ReferenceEquals(e.CardPlay.Card,prior)),"history replay uses the original card and native replay plus Burst hooks");
            Check(f.Enemy.CurrentHp==9939,"upgraded Strike deals ten on each of six native resolutions");
            Check(prior.Pile?.Type==PileType.Discard && !prior.Keywords.Contains(CardKeyword.Exhaust),"history replay does not add Exhaust or leave a temporary clone");
            Check(f.Player.PlayerCombatState.Energy==energy-engine.EnergyCost.GetResolved(),"history replay pays only the engine's cost");
            Check(f.Player.Creature.CurrentHp==70,"historical attack never targets its caster");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="all-five-history-replay-cards-base-upgraded-native-identity-and-burst"});

        _current="history-replay-order-and-limit";
        foreach(bool upgraded in new[]{false,true})
        {
            f=NewFixture("HISTORY-ORDER");
            var previous=new List<CardModel>();
            for(int i=0;i<6;i++)
            {
                var strike=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
                previous.Add(strike);await f.Play(strike);
            }
            var engine=f.State.CreateCard(ModelDb.Card<GeopeliaFullPlayback>(),f.Player);
            if(upgraded){engine.UpgradeInternal();engine.FinalizeUpgradeInternal();}
            var replayStep=((GeopeliaCard)engine).Definition.Steps.Single(s=>s.Kind=="Replay");
            int replayCount=upgraded?replayStep.Upgrade:replayStep.Value;
            await f.Play(engine);
            var actual=Runtime.History(f.Player).Where(e=>e.CardPlay.IsAutoPlay).Select(e=>e.CardPlay.Card).ToArray();
            Check(actual.SequenceEqual(previous.TakeLast(replayCount)),"Re:End replays the authored number of previous manual cards in chronological order");
            Check(PileType.Exhaust.GetPile(f.Player).Cards.Count==0,"history playback creates no synthetic exhausted cards");
            await BeginTurn(f,false);
            Check(!Runtime.ReplayableHistory(f.Player,engine).Any(),"history replay remains restricted to the current turn");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="re-end-authored-count-order-and-turn-scope"});

        _current="power-card-description-prefix";
        foreach(var power in ModelDb.CardPool<GeopeliaCardPool>().AllCards.Cast<GeopeliaCard>().Where(c=>c.Type==CardType.Power))
            foreach(var step in power.Definition.Steps.Where(s=>s.Kind=="Power"))
                Check(!power.GetDescriptionForPile(PileType.None).Contains("获得[gold]"+power.Definition.Name),"power card shows effect directly without acquiring its own name");
        Rows.Add(new{test="power-cards-show-effects-directly"});
    }
}
#endif
