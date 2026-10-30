#if JIU_VALIDATION
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task HateHorizonGrowthCases()
    {
        _current="hate-next-turn-damage-window-and-growing-threshold";
        foreach(bool upgraded in new[]{false,true})
        {
            var f=NewFixture("HATE-WINDOW");
            var hate=f.State.CreateCard(ModelDb.Card<GeopeliaHate>(),f.Player);
            if(upgraded){hate.UpgradeInternal();hate.FinalizeUpgradeInternal();}
            Check(hate.Rarity==CardRarity.Rare && hate.Type==CardType.Power && hate.EnergyCost.GetResolved()==(upgraded?2:3),"HATE is a 3/2-cost rare power");
            await f.Play(hate);
            var power=f.Player.Creature.GetPower<GeopeliaHatePower>()!;
            Check(!power.Active && power.Amount==1,"cast does not activate until the next owner turn");
            for(int threshold=1;threshold<=3;threshold++)
            {
                await BeginTurn(f,false);f.Player.PlayerCombatState!.Energy=99;
                Check(power.Active && power.Plays==0 && power.Amount==threshold,"next turn starts the double-damage window at its growing threshold");
                int hp=f.Player.Creature.CurrentHp;
                await CreatureCmd.Damage(f.Context,f.Player.Creature,2,ValueProp.Move,f.Enemy);
                Check(f.Player.Creature.CurrentHp==hp-4,"incoming damage is doubled while active");
                await CreatureCmd.Damage(f.Context,f.Player.Creature,1,ValueProp.Unblockable|ValueProp.Unpowered,f.Player.Creature);
                Check(f.Player.Creature.CurrentHp==hp-5,"direct HP loss is not doubled");
                for(int i=0;i<threshold;i++)
                {
                    var strike=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
                    int before=f.Enemy.CurrentHp;
                    await f.Play(strike);
                    Check(before-f.Enemy.CurrentHp==2*strike.DynamicVars["V0"].IntValue,"all threshold attacks, including the final one, deal double damage");
                    Check(power.Active==(i+1<threshold),"state exits only after the threshold resolution");
                }
                Check(power.Amount==threshold+1,"exiting increases the future card threshold by exactly one");
                var ordinary=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
                int beforeHp=f.Enemy.CurrentHp;await f.Play(ordinary);
                Check(beforeHp-f.Enemy.CurrentHp==ordinary.DynamicVars["V0"].IntValue && power.Amount==threshold+1,"later cards are normal and do not keep increasing the threshold");
                var restored=ModelDb.Power<GeopeliaHatePower>().ToMutable();
                SavedProperties.From(power)!.Fill(restored);
                Check(((GeopeliaHatePower)restored).Active==power.Active && ((GeopeliaHatePower)restored).Plays==power.Plays,"native saved properties preserve HATE's state and progress");
            }
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="hate-base-upgraded-next-turn-double-outgoing-incoming-and-growing-1-2-3-window"});

        _current="horizon-counts-all-resolutions-and-persists-between-turns";
        foreach(bool upgraded in new[]{false,true})
        {
            var f=NewFixture("HORIZON-COUNT");
            var source=f.State.CreateCard(ModelDb.Card<GeopeliaBelieveLight>(),f.Player);
            if(upgraded){source.UpgradeInternal();source.FinalizeUpgradeInternal();}
            await f.Play(source);
            var power=f.Player.Creature.GetPower<GeopeliaAerialPower>()!;
            int interval=source.DynamicVars["V0"].IntValue;
            Check(source.EnergyCost.GetResolved()==2 && power.Amount==interval && power.Progress==0,"Horizon costs two and starts its 5/4 interval after the source resolution");
            for(int i=0;i<interval-1;i++)await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
            Check(power.Progress==interval-1,"all manual resolutions counted");
            await BeginTurn(f,false);f.Player.PlayerCombatState!.Energy=99;
            Check(power.Progress==interval-1,"progress carries into the next turn");
            var last=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
            int hp=f.Enemy.CurrentHp,energy=f.Player.PlayerCombatState.Energy;
            await f.Play(last);
            var history=Runtime.History(f.Player);
            Check(hp-f.Enemy.CurrentHp==2*last.DynamicVars["V0"].IntValue && history.Count(e=>e.CardPlay.IsAutoPlay)==1,"threshold card is replayed natively exactly once");
            Check(history.All(e=>ReferenceEquals(e.CardPlay.Card,last)) && last.Pile?.Type==PileType.Discard,"replay retains the original card identity and normal result pile");
            Check(power.Progress==1 && f.Player.PlayerCombatState.Energy==energy-last.EnergyCost.GetResolved(),"automatic replay counts toward the next threshold and spends no additional energy");
            var copy=(GeopeliaAerialPower)ModelDb.Power<GeopeliaAerialPower>().ToMutable();
            SavedProperties.From(power)!.Fill(copy);
            Check(copy.Progress==power.Progress && copy.AppliedAfterPlayCount==power.AppliedAfterPlayCount,"native serialization retains Horizon progress");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="horizon-base-upgraded-native-replay-free-same-card-count-includes-auto-and-cross-turn"});

        _current="horizon-native-keywords-powers-x-and-hate-order";
        foreach(var kind in new[]{"power","exhaust","x","replays","hate"})
        {
            var f=NewFixture("HORIZON-"+kind);
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaBelieveLight>(),f.Player));
            var horizon=f.Player.Creature.GetPower<GeopeliaAerialPower>()!;
            CardModel card=kind switch
            {
                "power"=>f.State.CreateCard(ModelDb.Card<Inflame>(),f.Player),
                "exhaust"=>f.State.CreateCard(ModelDb.Card<Impervious>(),f.Player),
                "x"=>f.State.CreateCard(ModelDb.Card<Whirlwind>(),f.Player),
                _=>f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player)
            };
            if(kind=="hate")
            {
                await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaHate>(),f.Player));
                await BeginTurn(f,false);
            }
            horizon.Progress=kind=="replays"?horizon.Amount-2:horizon.Amount-1;
            if(kind=="replays")card.BaseReplayCount=1;
            f.Player.PlayerCombatState!.Energy=kind=="x"?2:99;
            int before=f.Enemy.CurrentHp;
            await f.Play(card);
            if(kind=="power")Check(f.Player.Creature.GetPowerAmount<StrengthPower>()==4 && card.Pile is null,"native power replay applies twice and is removed normally");
            if(kind=="exhaust")Check(f.Player.Creature.Block==60 && card.Pile?.Type==PileType.Exhaust,"Exhaust skill resolves twice without losing its keyword");
            if(kind=="x")Check(before-f.Enemy.CurrentHp==20 && f.Player.PlayerCombatState.Energy==0,"X replay preserves the already-paid captured X value");
            if(kind=="replays")Check(before-f.Enemy.CurrentHp==4*card.DynamicVars["V0"].IntValue && horizon.Progress==2,"existing native replay resolutions count and survive the automatic play");
            if(kind=="hate")Check(before-f.Enemy.CurrentHp==3*card.DynamicVars["V0"].IntValue && !f.Player.Creature.GetPower<GeopeliaHatePower>()!.Active,"HATE exits after the threshold card before Horizon's automatic replay");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="horizon-power-exhaust-x-existing-replays-and-hate-order"});

        _current="break-over-get-back-per-prior-resolution-growth";
        foreach(bool upgraded in new[]{false,true})
        foreach(int prior in new[]{0,3})
        foreach(bool block in new[]{false,true})
        {
            var f=NewFixture("PER-PLAY-GROWTH");
            for(int i=0;i<prior;i++)await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
            CardModel card=block?f.State.CreateCard(ModelDb.Card<GeopeliaScavenge>(),f.Player):f.State.CreateCard(ModelDb.Card<GeopeliaRedline>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            card.BaseReplayCount=1;
            int before=block?f.Player.Creature.Block:f.Enemy.CurrentHp;
            await f.Play(card);
            int baseAmount=block?8:9,bonus=block?(upgraded?5:3):(upgraded?6:4);
            int result=block?f.Player.Creature.Block-before:before-f.Enemy.CurrentHp;
            Check(result==2*baseAmount+bonus*(2*prior+1),"each resolution excludes itself and includes previous native replays when calculating scaling");
            Check(Runtime.Debt(f.Player)==0 && Runtime.Signal(f.Player)==0 && f.TotalCardsDrawn==0,"Get Back's old draw/signal/debt effects are removed");
            await BeginTurn(f,false);
            var fresh=block?f.State.CreateCard(ModelDb.Card<GeopeliaScavenge>(),f.Player):f.State.CreateCard(ModelDb.Card<GeopeliaRedline>(),f.Player);
            before=block?f.Player.Creature.Block:f.Enemy.CurrentHp;
            await f.Play(fresh);
            Check((block?f.Player.Creature.Block-before:before-f.Enemy.CurrentHp)==baseAmount,"growth resets each owner turn");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="break-over-get-back-base-upgraded-previous-count-0-3-replay-growth-and-turn-reset"});
    }
}
#endif
