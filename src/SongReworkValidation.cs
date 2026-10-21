#if JIU_VALIDATION
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task StockDrawPile(Fixture f,int count=30)
    {
        for(int i=0;i<count;i++)
            await CardPileCmd.Add(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player),PileType.Draw,skipVisuals:true);
    }
    private static async Task SongReworkCases()
    {
        _current="song-description-and-keywords";
        foreach(var original in ModelDb.CardPool<GeopeliaCardPool>().AllCards.Cast<GeopeliaCard>())
        {
            string text=original.GetDescriptionForPile(PileType.None);
            if(original is GeopeliaOldRecording)
                Check(text.Contains("不息的信号")&&!text.Contains("下回合")&&!text.Contains("给予"),"Desultory has the short description");
            if(original.Type==CardType.Power && original is not GeopeliaSustainAmplifier)
                Check(!text.Contains("每回合最多"),"old power cards hide per-turn cap clauses");
        }
        var f=NewFixture("SONG-DESULTORY");await StockDrawPile(f);
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaEnergyDebtPower),5,null);
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaDrawDebtPower),5,null);
        var old=f.State.CreateCard(ModelDb.Card<GeopeliaOldRecording>(),f.Player);
        await f.Play(old);
        Check(Runtime.Signal(f.Player)==2 && f.TotalCardsDrawn==2 && f.TotalEnergyGained==1,"Desultory resources remain unchanged");
        Check(f.Enemy.CurrentHp==9997 && f.Player.Creature.Block==2,"Desultory damage and block remain unchanged");
        Check(f.Enemy.GetPowerAmount<WeakPower>()==2 && f.Enemy.GetPowerAmount<VulnerablePower>()==2,"Desultory enemy debuffs remain unchanged");
        Check(f.Player.Creature.GetPowerAmount<GeopeliaEnergyDebtPower>()==3 && f.Player.Creature.GetPowerAmount<GeopeliaDrawDebtPower>()==3,"Desultory repays both debts");
        Check(old.Pile?.Type==PileType.Exhaust && old.Keywords.Contains(CardKeyword.Sly),"Desultory native keywords remain unchanged");
        RunManager.Instance.CleanUp(false);

        _current="song-nhelv";
        foreach(bool upgraded in new[]{false,true})
        foreach(int signals in new[]{0,7,12})
        {
            f=NewFixture("SONG-NHELV");
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaCounterpoint>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            Check(card.EnergyCost.GetResolved()==1,"Nhelv costs one in both versions");
            Check(card.Keywords.Contains(CardKeyword.Exhaust) && card.Keywords.Contains(CardKeyword.Retain)==upgraded,"Nhelv gains Retain only on upgrade");
            await Runtime.AddSignal(f.Context,f.Player,signals);
            await f.Play(card);
            Check(Runtime.Signal(f.Player)==0 && f.Player.Creature.GetPowerAmount<StrengthPower>()==signals,"Nhelv converts all signal one-for-one");
            Check(card.Pile?.Type==PileType.Exhaust,"Nhelv exhausts after play");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="nhelv-base-upgraded",signalAmounts=new[]{0,7,12}});

        _current="song-der-richter";
        foreach(bool upgraded in new[]{false,true})
        {
            f=NewFixture("SONG-RICHTER");
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaOpeningTheme>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            Check(card.EnergyCost.GetResolved()==2 && !card.Keywords.Contains(CardKeyword.Innate),"Richter is a two-cost power without old Innate");
            await f.Play(card);
            int amount=upgraded?5:3;
            Check(f.Player.Creature.GetPowerAmount<StrengthPower>()==amount && f.Player.Creature.GetPowerAmount<DexterityPower>()==amount,"Richter grants permanent Strength and Dexterity");
            Check(f.Player.PlayerCombatState!.Energy==97,"Richter does not charge its turn-start penalty on play");
            for(int turn=0;turn<2;turn++)
            {await BeginTurn(f,false);Check(f.Player.PlayerCombatState.Energy==2,"Richter loses one after each native energy reset");}
            Check(f.Player.Creature.GetPower<GeopeliaRichterPower>() is not null,"Richter penalty remains active across turns");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="richter-stats-and-repeated-turn-penalty"});

        _current="song-archidoxen-independent-matches";
        foreach(var c in new (int cost,CardRarity rarity,CardType type,int energy,int draw,int pending)[]{
            (1,CardRarity.Rare,CardType.Attack,1,0,0),
            (0,CardRarity.Basic,CardType.Attack,0,1,0),
            (0,CardRarity.Rare,CardType.Skill,0,0,1),
            (1,CardRarity.Basic,CardType.Skill,2,1,0),
            (0,CardRarity.Rare,CardType.Attack,0,0,0)})
        {
            f=NewFixture("SONG-ARCH-MATCH");await StockDrawPile(f);
            await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaArchidoxenPower),1,null);
            var power=f.Player.Creature.GetPower<GeopeliaArchidoxenPower>()!;
            power.HasPrevious=true;power.PreviousCost=c.cost;power.PreviousRarity=(int)c.rarity;power.PreviousType=(int)c.type;
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaDefend>(),f.Player));
            Check(f.TotalEnergyGained==c.energy && f.TotalCardsDrawn==c.draw && power.PendingDoubles==c.pending,"Archidoxen criteria operate independently and type doubles the next benefit");
            Check(power.PreviousCost==1 && power.PreviousRarity==(int)CardRarity.Basic && power.PreviousType==(int)CardType.Skill,"Archidoxen records the latest card values");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="archidoxen-independent-criteria",cases=5});

        _current="song-archidoxen-real-power-and-caps";
        foreach(bool upgraded in new[]{false,true})
        {
            f=NewFixture("SONG-ARCH-CAPS");await StockDrawPile(f);
            var card=f.State.CreateCard(ModelDb.Card<GeopeliaSustainAmplifier>(),f.Player);
            if(upgraded){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
            int cost=upgraded?1:2;
            Check(card.EnergyCost.GetResolved()==cost,"Archidoxen costs two or upgraded one");
            await f.Play(card);
            var power=f.Player.Creature.GetPower<GeopeliaArchidoxenPower>()!;
            Check(power.HasPrevious && power.PreviousCost==cost && !power.EnergyUsed && !power.DrawUsed && !power.TypeUsed,"playing Archidoxen seeds its own record without triggering itself");
            // Reset with a simple predecessor, then repeatedly play identical native cards.
            power.PreviousCost=1;power.PreviousRarity=(int)CardRarity.Basic;power.PreviousType=(int)CardType.Attack;
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
            int energy=f.TotalEnergyGained,draw=f.TotalCardsDrawn;
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
            Check(energy==2 && draw==1 && f.TotalEnergyGained==energy && f.TotalCardsDrawn==draw,"each effect triggers only once in a turn");
            await BeginTurn(f,false);
            Check(!power.EnergyUsed && !power.DrawUsed && !power.TypeUsed && power.HasPrevious,"allowances reset and prior card survives the turn boundary");
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
            Check(f.TotalEnergyGained==energy+2 && f.TotalCardsDrawn==draw+1,"all three effects can trigger again next turn");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="archidoxen-cost-record-and-turn-caps"});

        _current="song-archidoxen-native-benefits";
        f=NewFixture("SONG-ARCH-NATIVE");await StockDrawPile(f);
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaArchidoxenPower),1,null);
        var arch=f.Player.Creature.GetPower<GeopeliaArchidoxenPower>()!;
        arch.HasPrevious=true;arch.PreviousType=(int)CardType.Skill;arch.EnergyUsed=arch.DrawUsed=true;
        await f.Play(f.State.CreateCard(ModelDb.Card<Backflip>(),f.Player));
        Check(f.TotalCardsDrawn==4 && arch.PendingDoubles==0,"native Backflip draws four with one pending double");
        arch.PendingDoubles=1;
        int energyBefore=f.Player.PlayerCombatState!.Energy;
        await PlayerCmd.GainEnergy(3,f.Player);await PlayerCmd.GainEnergy(3,f.Player);
        Check(f.Player.PlayerCombatState.Energy==energyBefore+9 && arch.PendingDoubles==0,"energy doubles exactly one gain event");
        await CardCmd.Discard(f.Context,PileType.Hand.GetPile(f.Player).Cards.ToArray());
        arch.PendingDoubles=1;
        var handDraw=await CardPileCmd.Draw(f.Context,2,f.Player,fromHandDraw:true);
        Check(handDraw.Count()==4 && arch.PendingDoubles==0,"next draw also doubles when it is the initial hand draw");
        arch.PendingDoubles=1;
        await PowerCmd.Apply<NoDrawPower>(f.Context,f.Player.Creature,1,f.Player.Creature,null);
        var prevented=await CardPileCmd.Draw(f.Context,2,f.Player);
        Check(!prevented.Any() && arch.PendingDoubles==1,"blocked draw preserves the pending double");
        await PowerCmd.Remove(f.Player.Creature.GetPower<NoDrawPower>());
        var ordinaryDraw=await CardPileCmd.Draw(f.Context,2,f.Player);
        Check(ordinaryDraw.Count()==4 && arch.PendingDoubles==0,"the next successful draw consumes the double");
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="archidoxen-native-draw-energy-blocked-and-hand-draw"});

        _current="song-hidden-old-limit-preserved";
        f=NewFixture("SONG-OLD-CAP");await StockDrawPile(f);
        await Runtime.ApplyPower(f.Context,f.Player,typeof(GeopeliaRhythmDrawPower),1,null);
        for(int i=0;i<12;i++) await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(),f.Player));
        Check(f.TotalCardsDrawn==3,"removing cap text preserves the old three-trigger gameplay limit");
        RunManager.Instance.CleanUp(false);
        Rows.Add(new{test="old-hidden-caps-preserved"});
    }
}
#endif
