#if JIU_VALIDATION
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task RecursiveSignalCases()
    {
        _current="whole-rest-rare-non-power";
        foreach(bool upgraded in new[]{false,true})
        for(int seed=0;seed<12;seed++)
        {
            var f=NewFixture("REST-"+seed);
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaRest>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            await f.Play(card);
            var generated=PileType.Hand.GetPile(f.Player).Cards.Single();
            Check(generated.Rarity==CardRarity.Rare && generated.Type!=CardType.Power && generated.Pool is GeopeliaCardPool,"Whole Rest generates one Jiu rare non-power");
            Check(card.EnergyCost.GetResolved()==(upgraded?0:1) && card.Pile?.Type==PileType.Discard,"Whole Rest costs 1/0 without new Exhaust");
            Check(f.Player.Creature.Block==0 && Runtime.Signal(f.Player)==0 && f.TotalEnergyGained==0,"old block and energy effects removed");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="whole-rest-base-upgraded-native-random-generation",seeds=12});

        _current="debt-clearing-upgrade-retain";
        foreach(bool upgraded in new[]{false,true})
        {
            var f=NewFixture("DEBT-CLEARING-RETAIN");
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaDebtClearing>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            Check(card.EnergyCost.GetResolved()==1 && card.Keywords.Contains(CardKeyword.Retain)==upgraded && card.Keywords.Contains(CardKeyword.Exhaust),"debt clearing only gains Retain on upgrade");
            await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaEnergyDebtPower),150,null);
            await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaDrawDebtPower),150,null);
            await f.Play(card);
            Check(Runtime.Debt(f.Player)==0 && card.Pile?.Type==PileType.Exhaust,"both versions retain full debt clearing and Exhaust");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="debt-clearing-upgrade-retain-preserves-current-design"});

        _current="endless-signal-recursive-next-turn-free-skill";
        foreach(bool upgraded in new[]{false,true})
        {
            var f=NewFixture("ENDLESS-CHAIN");
            var original=f.State.CreateCard(ModelDb.Card<GeopeliaHappyDisease>(),f.Player);
            if(upgraded){original.UpgradeInternal();original.FinalizeUpgradeInternal();}
            Check(original.Type==CardType.Skill && original.Rarity==CardRarity.Rare && original.EnergyCost.GetResolved()==(upgraded?0:1),"Der Schneid is a 1/0-cost rare skill");
            await f.Play(original);
            Check(original.Pile?.Type==PileType.Exhaust && PileType.Hand.GetPile(f.Player).Cards.Count==0,"Der Schneid exhausts without generating immediately");
            await Hook.BeforeHandDraw(f.State,f.Player,f.Context);
            Check(PileType.Hand.GetPile(f.Player).Cards.Count==0,"a same-turn start hook cannot mature new charges");
            for(int generation=0;generation<4;generation++)
            {
                await BeginTurn(f,false);
                var next=f.State.CreateCard(ModelDb.Card<GeopeliaHold>(),f.Player);
                TestRngInjector.SetCombatCardGenerationOverride(new(){next});
                await Hook.BeforeHandDraw(f.State,f.Player,f.Context);
                Check(PileType.Hand.GetPile(f.Player).Cards.Single()==next && next.Type==CardType.Skill,"each next own turn supplies exactly the requested skill");
                Check(next.EnergyCost.GetResolved()==0 && next.Keywords.Contains(CardKeyword.Exhaust) && next.Enchantment is GeopeliaEndlessSignal,"generated skill is free and inherits native Exhaust plus the recursive effect");
                Check(next.GetDescriptionForPile(PileType.Hand).Contains("不息的信号"),"generated card displays the inherited effect");
                Check(next.Enchantment!.HoverTip.Description.Contains("下个"),"native enchantment hover tip explains recurrence");
                var restored=CardModel.FromSerializable(next.ToSerializable());
                Check(restored.Enchantment is GeopeliaEndlessSignal && restored.Keywords.Contains(CardKeyword.Exhaust),"native serialization preserves inherited effect and Exhaust");
                var cloned=f.State.CloneCard(next);
                Check(cloned.Enchantment is GeopeliaEndlessSignal && cloned.EnergyCost.GetResolved()==0,"native copy preserves recursive effect and free cost");
                int energy=f.Player.PlayerCombatState!.Energy;
                await f.Play(next);
                Check(f.Player.Creature.Block==8 && f.Player.PlayerCombatState.Energy==energy,"generated skill keeps its original effect and spends no energy");
                Check(next.Pile?.Type==PileType.Exhaust && f.Player.Creature.GetPowerAmount<GeopeliaEndlessSignalPower>()==1,"playing inherited skill exhausts it and queues exactly one successor");
                await Hook.BeforeHandDraw(f.State,f.Player,f.Context);
                Check(PileType.Hand.GetPile(f.Player).Cards.Count==0,"successor cannot appear during the current turn");
            }
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="endless-signal-base-upgraded-four-generations-native-copy-save-and-original-effect"});

        _current="endless-signal-native-burst-and-self-generation";
        {
            var f=NewFixture("ENDLESS-BURST");
            await PowerCmd.Apply<BurstPower>(f.Context,f.Player.Creature,1,f.Player.Creature,null);
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaHappyDisease>(),f.Player));
            Check(f.Player.Creature.GetPowerAmount<GeopeliaEndlessSignalPower>()==2,"native Burst queues two successors without an artificial cap");
            await BeginTurn(f,false);
            await Hook.BeforeHandDraw(f.State,f.Player,f.Context);
            var generated=PileType.Hand.GetPile(f.Player).Cards.ToArray();
            Check(generated.Length==2 && generated.All(c=>c.Type==CardType.Skill && c.EnergyCost.GetResolved()==0 && c.Enchantment is GeopeliaEndlessSignal),"two mature charges create two random free inheriting skills");
            await Hook.BeforeHandDraw(f.State,f.Player,f.Context);
            Check(PileType.Hand.GetPile(f.Player).Cards.Count==2 && f.Player.Creature.GetPower<GeopeliaEndlessSignalPower>() is null,"mature charges are consumed only once");
            RunManager.Instance.CleanUp(false);

            f=NewFixture("ENDLESS-SELF");
            var self=f.State.CreateCard(ModelDb.Card<GeopeliaHappyDisease>(),f.Player);
            CardCmd.Enchant<GeopeliaEndlessSignal>(self,1);
            await f.Play(self);
            Check(f.Player.Creature.GetPowerAmount<GeopeliaEndlessSignalPower>()==1,"a card already containing Endless Signal does not duplicate the same named effect");
            RunManager.Instance.CleanUp(false);
            f=NewFixture("ENDLESS-NEW-COMBAT");
            Check(f.Player.Creature.GetPower<GeopeliaEndlessSignalPower>() is null,"new combat has no inherited pending generation");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="endless-signal-burst-stacking-native-random-skills-dedup-and-combat-reset"});
    }
}
#endif
