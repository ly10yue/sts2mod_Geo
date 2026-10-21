#if JIU_VALIDATION
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task EditorFeatureCases()
    {
        _current="editor-native-description-and-replay";
        LocManager.Instance.SetLanguage("zhs");
        foreach (var original in ModelDb.CardPool<GeopeliaCardPool>().AllCards.Cast<GeopeliaCard>())
        foreach (bool upgraded in new[]{false,true})
        {
            var card=(GeopeliaCard)original.ToMutable();
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            string text=card.GetDescriptionForPile(PileType.None);
            Check(!text.Contains("{V")&&!text.Contains("IfUpgraded"),"native dynamic description resolves");
            Check(!Regex.IsMatch(text,@"(?<!\d)0(点伤害|点格挡|点信号|张牌)"),"zero effect clauses are omitted");
            Check(card.GetEnchantedReplayCount()==card.Definition.Replays,"fixed replay uses native count exactly once");
            if(card.Definition.Replays>0) Check(text.Contains("重放")&&!text.Contains("额外打出"),"native replay keyword replaces manual duplicate text");
        }
        // Exercise the real formatter's two-state omission, independent of user balance choices.
        const string key="PHIGROS_JIU_EDITOR_ZERO_PROBE";
        LocManager.Instance.GetTable("cards").MergeWith(new(){[key]="{IfUpgraded:show:造成{V0:diff()}点伤害。|}"});
        foreach(var display in new[]{UpgradeDisplay.Normal,UpgradeDisplay.Upgraded,UpgradeDisplay.UpgradePreview})
        {
            var text=new LocString("cards",key);text.Add(new IfUpgradedVar(display));text.Add(new DynamicVar("V0",10));
            Check(display==UpgradeDisplay.Normal?text.GetFormattedText()=="":text.GetFormattedText().Contains("10"),"upgrade-only effect formats natively");
        }
        var f=NewFixture("EDITOR-REPLAY");
        var repeat=f.State.CreateCard(ModelDb.Card<GeopeliaTowerResonance>(),f.Player);
        int expected=1+((GeopeliaCard)repeat).Definition.Replays;
        repeat.BaseReplayCount++;
        Check(repeat.GetEnchantedReplayCount()==expected,"native replay additions do not double the authored count");
        await f.Play(repeat);
        Check(Runtime.History(f.Player).Count(e=>e.CardPlay.Card==repeat)==expected+1,"native replay executes the exact total count");
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="native-replay-and-zero-text",cards=85});

        _current="editor-native-keywords";
        f=NewFixture("EDITOR-KEYWORDS");
        var exhaust=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
        exhaust.AddKeyword(CardKeyword.Exhaust);await f.Play(exhaust);
        Check(exhaust.Pile?.Type==PileType.Exhaust,"added Exhaust executes natively");
        var ordinary=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
        ordinary.AddKeyword(CardKeyword.Exhaust);ordinary.RemoveKeyword(CardKeyword.Exhaust);await f.Play(ordinary);
        Check(ordinary.Pile?.Type==PileType.Discard,"removed Exhaust restores normal discard");
        var ethereal=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
        ethereal.AddKeyword(CardKeyword.Ethereal);await CardPileCmd.Add(ethereal,PileType.Hand,skipVisuals:true);
        var preserved=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
        preserved.AddKeyword(CardKeyword.Ethereal);preserved.RemoveKeyword(CardKeyword.Ethereal);await CardPileCmd.Add(preserved,PileType.Hand,skipVisuals:true);
        var turn=AccessTools.Field(typeof(CombatManager),"_turnState").GetValue(CombatManager.Instance)!;
        await (Task)AccessTools.Method(typeof(CombatManager),"DoTurnEnd").Invoke(CombatManager.Instance,new object[]{turn,f.Player,f.Context})!;
        Check(ethereal.Pile?.Type==PileType.Exhaust,"added Ethereal exhausts at native turn end");
        Check(preserved.Pile?.Type==PileType.Hand,"removed Ethereal no longer exhausts");
        foreach(var keyword in Enum.GetValues<CardKeyword>().Where(k=>k!=CardKeyword.None))
        {preserved.AddKeyword(keyword);Check(preserved.Keywords.Contains(keyword),"native keyword is supported");preserved.RemoveKeyword(keyword);}
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="native-keyword-add-remove"});

        _current="editor-percentage-debt";
        f=NewFixture("EDITOR-PERCENT");
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaDamageDebtPower),30,null);
        await f.Player.Creature.GetPower<GeopeliaDamageDebtPower>()!.AfterEnergyReset(f.Player);
        var strike=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
        await CreatureCmd.Damage(f.Context,f.Enemy,10m,ValueProp.Move,f.Player.Creature,strike,null);
        Check(f.Enemy.CurrentHp==9992,"30 percent damage debt reduces ten damage to seven");
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaBlockDebtPower),30,null);
        await f.Player.Creature.GetPower<GeopeliaBlockDebtPower>()!.AfterEnergyReset(f.Player);
        var blockPlay=new CardPlay{Card=strike,Player=f.Player,Target=f.Player.Creature,ResultPile=PileType.Discard,Resources=new(){EnergySpent=0,EnergyValue=0,StarsSpent=0,StarValue=0},IsAutoPlay=false,PlayIndex=0,PlayCount=1};
        await CreatureCmd.GainBlock(f.Player.Creature,10m,ValueProp.Move,blockPlay);
        Check(f.Player.Creature.Block==7,"30 percent block debt grants seven block");
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="edited-percentage-debt",percentage=30});

        _current="editor-draw-debt-repayment";
        // Change the in-memory probe definition only; never change the user's design file.
        var definition=Design.Get("GeopeliaStrike");
        var originalSteps=definition.Steps;
        try
        {
            foreach(var c in new (int debt,int repay,int left)[]{(6,2,4),(6,20,0),(150,99,0),(0,2,0),(6,0,6)})
            {
                f=NewFixture("EDITOR-DRAW-REPAY");
                definition.Steps=new[]{new EffectStep{Kind="RepayDraw",Value=c.repay,Upgrade=c.repay}};
                var probe=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
                probe.DynamicVars["V0"].BaseValue=c.repay;
                if(c.debt>0) await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaDrawDebtPower),c.debt,null);
                await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaEnergyDebtPower),3,null);
                await f.Play(probe);
                var remaining=f.Player.Creature.GetPower<GeopeliaDrawDebtPower>();
                Check((remaining?.Amount??0)==c.left,"draw debt is reduced or fully cleared without becoming negative");
                Check(c.left!=0||remaining is null,"cleared draw debt power is removed");
                Check(f.Player.Creature.GetPowerAmount<GeopeliaEnergyDebtPower>()==3,"draw repayment preserves unrelated debt");
                if(remaining is not null) remaining.AmountOnTurnStart=remaining.Amount;
                Check(Hook.ModifyHandDraw(f.State,f.Player,5,out _)==Math.Max(0,5-c.left),"next turn hand draw uses only remaining debt");
                RunManager.Instance.CleanUp(false);
            }
            // The same '99 means all' convention must also work for energy debt above 99.
            f=NewFixture("EDITOR-ENERGY-CLEAR");
            definition.Steps=new[]{new EffectStep{Kind="RepayEnergy",Value=99,Upgrade=99}};
            var energyProbe=f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player);
            energyProbe.DynamicVars["V0"].BaseValue=99;
            await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaEnergyDebtPower),150,null);
            await f.Play(energyProbe);
            Check(f.Player.Creature.GetPower<GeopeliaEnergyDebtPower>() is null,"99 clears energy debt above 99 as described");
            RunManager.Instance.CleanUp(false);
        }
        finally { definition.Steps=originalSteps; }
        Rows.Add(new{test="draw-debt-reduce-clear",scenarios=5,energyClearAbove99=true});
    }
}
#endif
