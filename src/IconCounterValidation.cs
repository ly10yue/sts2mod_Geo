#if JIU_VALIDATION
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace PhigrosJiu;
internal static partial class GameValidation
{
    private static async Task IconCounterCases()
    {
        _current="relic-owned-recorder-counter-and-legacy-save";
        var f=NewFixture("RELIC-COUNTER");
        var relic=f.Player.GetRelic<GeopeliaJudgmentLine>();
        if(relic is null){ relic=(GeopeliaJudgmentLine)ModelDb.Relic<GeopeliaJudgmentLine>().ToMutable(); f.Player.AddRelicInternal(relic); }
        int updates=0,flashes=0;relic.DisplayAmountChanged+=()=>updates++;relic.Flashed+=(_,_)=>flashes++;
        await relic.BeforeCombatStart();
        Check(relic.ShowCounter && relic.DisplayAmount==0 && Runtime.Signal(f.Player)==1,"recorder grants initial signal and owns its native counter");
        Check(f.Player.Creature.GetPower<GeopeliaRhythmPower>() is null,"new combats do not apply the old proxy buff");
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaEltaw>(),f.Player));
        Check(relic.DisplayAmount==2 && Runtime.Signal(f.Player)==1,"both native replay resolutions advance the relic counter");
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaRhythmPower),1,null);
        Check(!f.Player.Creature.GetPower<GeopeliaRhythmPower>()!.IsVisible,"legacy recorder power is hidden when its owning relic exists");
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
        Check(relic.DisplayAmount==0 && Runtime.Signal(f.Player)==2 && updates>=4 && flashes>=2,$"recorder third: count={relic.DisplayAmount}, signal={Runtime.Signal(f.Player)}, events={updates}, flashes={flashes}");
        await BeginTurn(f,false);Check(relic.DisplayAmount==0,"recorder resets each owner turn");
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="recorder-native-relic-counter-flash-replay-reset-and-legacy-deduplication"});

        _current="native-power-counter-label-updates";
        f=NewFixture("POWER-COUNTER");
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaBelieveLight>(),f.Player));
        var horizon=f.Player.Creature.GetPower<GeopeliaAerialPower>()!;
        var node=NPower.Create(horizon);node.Visible=false;NGame.Instance!.AddChild(node);
        await node.ToSignal(node.GetTree(),SceneTree.SignalName.ProcessFrame);
        var label=node.GetNode("%AmountLabel");
        int interval=horizon.Amount;
        Check(horizon.DisplayAmount==interval && label.Get("text").AsString()==interval.ToString(),$"native counter initial: value={horizon.DisplayAmount}, label={label.Get("text").AsString()}");
        int events=0;horizon.DisplayAmountChanged+=()=>events++;
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
        Check(horizon.DisplayAmount==interval-1 && events>0 && label.Get("text").AsString()==(interval-1).ToString(),"native label updates immediately through DisplayAmountChanged");
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaPulseGuardPower),3,null);
        var pulse=f.Player.Creature.GetPower<GeopeliaPulseGuardPower>()!;
        Check(pulse.DisplayAmount==3,"three-card block engine starts at three remaining");
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
        Check(pulse.DisplayAmount==2,"block engine decrements its visible counter");
        await BeginTurn(f,false);Check(pulse.DisplayAmount==3 && horizon.DisplayAmount==interval-2,"per-turn counters reset while Horizon progress carries across turns");
        var copy=(GeopeliaAerialPower)ModelDb.Power<GeopeliaAerialPower>().ToMutable();
        SavedProperties.From(horizon)!.Fill(copy);
        Check(copy.Progress==horizon.Progress,"native saved properties retain visible progress");
        node.Visible=false;RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="real-native-power-label-live-events-per-turn-and-persistent-progress"});
    }
}
#endif
