#if JIU_VALIDATION
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task StasisCases()
    {
        _current="stasis-native-additive-replay";
        foreach(bool upgraded in new[]{false,true})
        foreach(bool native in new[]{false,true})
        {
            var f=NewFixture("STASIS-ADDITIVE");
            CardModel target=native?f.State.CreateCard(ModelDb.Card<Bash>(),f.Player):f.State.CreateCard(ModelDb.Card<GeopeliaEltaw>(),f.Player);
            target.BaseReplayCount=1;
            await CardPileCmd.Add(target,PileType.Hand,skipVisuals:true);
            int before=target.GetEnchantedReplayCount(),cost=target.EnergyCost.GetResolved();
            var stasis=f.State.CreateCard(ModelDb.Card<GeopeliaQuietCoda>(),f.Player);
            if(upgraded){stasis.UpgradeInternal();stasis.FinalizeUpgradeInternal();}
            using(CardSelectCmd.UseSelector(new TestCardSelector())) await f.Play(stasis);
            int addition=upgraded?3:2;
            Check(target.GetEnchantedReplayCount()==before+addition,"Stasis adds 2/3 to authored and native replay counts exactly once");
            Check(target.EnergyCost.GetResolved()==cost,"Stasis does not copy Transfigure's energy-cost penalty");
            Check(stasis.EnergyCost.GetResolved()==1 && stasis.Pile?.Type==PileType.Discard && !stasis.Keywords.Contains(CardKeyword.Exhaust),"Stasis costs one and has no inherited Exhaust");
            Check(f.Player.Creature.Block==0 && Runtime.Debt(f.Player)==0,"old block and draw debt effects removed");
            using(CardSelectCmd.UseSelector(new TestCardSelector())) await f.Play(stasis);
            Check(target.GetEnchantedReplayCount()==before+2*addition,"two casts stack additively");
            await BeginTurn(f,false);
            Check(target.GetEnchantedReplayCount()==before+2*addition,"added replay persists into later turns of the combat");
            var clone=f.State.CloneCard(target);
            Check(clone.GetEnchantedReplayCount()==target.GetEnchantedReplayCount(),"native copying preserves the added replay count");
            using(CardSelectCmd.UseSelector(new TestCardSelector())) await f.Play(f.State.CreateCard(ModelDb.Card<Transfigure>(),f.Player));
            int count=before+2*addition+1;
            Check(target.GetEnchantedReplayCount()==count && target.EnergyCost.GetResolved()==cost+1,"Stasis stacks with the original Transfigure through the same native counter");
            f.Player.PlayerCombatState!.Energy=99;
            await f.Play(target);
            Check(Runtime.History(f.Player).Count(e=>e.CardPlay.Card==target)==count+1,"native engine resolves the exact new total number of plays");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="stasis-base-upgraded-main-and-vanilla-additive-stacking-native-transfigure-and-copy"});

        _current="stasis-eligibility-and-enchantments";
        {
            var f=NewFixture("STASIS-NO-TARGET");
            var plain=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
            var conditional=f.State.CreateCard(ModelDb.Card<GeopeliaRetribution>(),f.Player);
            await CardPileCmd.Add(plain,PileType.Hand,skipVisuals:true);
            await CardPileCmd.Add(conditional,PileType.Hand,skipVisuals:true);
            using(CardSelectCmd.UseSelector(new TestCardSelector())) await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaQuietCoda>(),f.Player));
            Check(plain.GetEnchantedReplayCount()==0 && conditional.GetEnchantedReplayCount()==0,"cards without the native replay keyword are not selectable");
            RunManager.Instance.CleanUp(false);

            f=NewFixture("STASIS-ENCHANTMENT");
            var enchanted=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
            var enchantment=CardCmd.Enchant<Spiral>(enchanted,1);
            int before=enchanted.GetEnchantedReplayCount();
            Check(before>0,"native enchantment supplies existing replay");
            await CardPileCmd.Add(enchanted,PileType.Hand,skipVisuals:true);
            using(CardSelectCmd.UseSelector(new TestCardSelector())) await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaQuietCoda>(),f.Player));
            Check(enchanted.GetEnchantedReplayCount()==before+2 && ReferenceEquals(enchanted.Enchantment,enchantment),"enchantment-supplied replay qualifies and the enchantment is preserved");
            RunManager.Instance.CleanUp(false);

            f=NewFixture("STASIS-NEW-COMBAT");
            var fresh=f.State.CreateCard(ModelDb.Card<GeopeliaEltaw>(),f.Player);
            Check(fresh.GetEnchantedReplayCount()==1,"canonical cards do not inherit temporary combat additions");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="stasis-native-keyword-filter-enchantment-and-new-combat"});
    }
}
#endif
