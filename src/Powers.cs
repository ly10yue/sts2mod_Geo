using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace PhigrosJiu;

public abstract class GeopeliaPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class GeopeliaSignalPower : GeopeliaPower
{
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? source)
    {
        if (power == this && Amount > Runtime.SignalLimit(Owner.Player!)) SetAmount(Runtime.SignalLimit(Owner.Player!));
        return Task.CompletedTask;
    }
}

public sealed class GeopeliaSignalCapacityPower : GeopeliaPower { }

public sealed class GeopeliaEndlessSignalPower : GeopeliaPower
{
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combatState)
    {
        if (player != Owner.Player || AmountOnTurnStart <= 0 || CombatManager.Instance.IsOverOrEnding) return;
        int count = Math.Min(Amount, AmountOnTurnStart);
        // Consume only charges present at turn start, before any newly generated card can add a charge.
        await PowerCmd.ModifyAmount(context, this, -count, Owner, null);
        var pool = ModelDb.CardPool<GeopeliaCardPool>().GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c.Type == CardType.Skill);
        for (int i = 0; i < count && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            var card = MegaCrit.Sts2.Core.Factories.CardFactory.GetDistinctForCombat(player, pool, 1, player.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (card is null) continue;
            CardCmd.Enchant<GeopeliaEndlessSignal>(card, 1);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        }
    }
}

public sealed class GeopeliaFutureRestartPower : GeopeliaPower
{
    [SavedProperty] public int AppliedOnTurn { get; set; }
    [SavedProperty] public bool ExtraTurnPending { get; set; }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        AppliedOnTurn = Owner.Player!.PlayerCombatState!.TurnNumber;
        return Task.CompletedTask;
    }
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        var player = Owner.Player!;
        if (side == CombatSide.Player && participants.Contains(Owner) && !ExtraTurnPending
            && player.PlayerCombatState!.TurnNumber > AppliedOnTurn && !CombatManager.Instance.IsOverOrEnding)
        {
            // End through the native turn queue; the native extra-turn hook then skips the enemy side.
            ExtraTurnPending = true;
            PlayerCmd.EndTurn(player, canBackOut: false);
        }
        return Task.CompletedTask;
    }
    public override bool ShouldTakeExtraTurn(Player player) => player == Owner.Player && ExtraTurnPending;
    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (player != Owner.Player || !ExtraTurnPending) return;
        ExtraTurnPending = false;
        if (Amount <= 1) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1, Owner, null);
    }
}

/// <summary>All per-turn counters are saved, owner-scoped and reset before that owner's turn.</summary>
public abstract class GeopeliaMechanicPower : GeopeliaPower
{
    public virtual string Mode => "";
    public override int DisplayAmount => Mode switch {
        "Rhythm"=>3-Plays%3, "PulseGuard"=>Used>=4?0:3-Plays%3, "RhythmDraw"=>Used>=3?0:3-Plays%3,
        "EchoGuard"=>Math.Max(0,6-Used), "EchoSignal" or "OpeningSignal"=>Math.Max(0,Amount-Used),
        _=>this switch { GeopeliaSignalDrawPower=>Used>=3?0:3-Progress%3, GeopeliaDebtDrawPower=>Math.Max(0,2-Used),
        GeopeliaBeaconPower or GeopeliaSignalEchoPower=>Used>0?0:1, GeopeliaWorldEchoPower=>Math.Max(0,Amount-Used), _=>base.DisplayAmount }
    };
    private int _counterPlays;
    [SavedProperty] public int Plays { get=>_counterPlays; set { _counterPlays=value; InvokeDisplayAmountChanged(); } }
    private int _counterUsed;
    [SavedProperty] public int Used { get=>_counterUsed; set { _counterUsed=value; InvokeDisplayAmountChanged(); } }
    private int _counterProgress;
    [SavedProperty] public int Progress { get=>_counterProgress; set { _counterProgress=value; InvokeDisplayAmountChanged(); } }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) { Plays = Used = Progress = 0; }
        return Task.CompletedTask;
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Player != Owner.Player) return;
        Plays++;
        switch (Mode)
        {
            case "Rhythm" when Plays % 3 == 0:
                await Runtime.AddSignal(context, Owner.Player!, 1); break;
            case "PulseGuard" when Plays % 3 == 0 && Used < 4:
                Used++; await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null); break;
            case "RhythmDraw" when Plays % 3 == 0 && Used < 3:
                Used++; await CardPileCmd.Draw(context, Amount, Owner.Player!); break;
            case "EchoGuard" when Runtime.IsRepeat(play) && Used < 6:
                Used++; await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null); break;
            case "EchoSignal" when Runtime.IsRepeat(play) && Used < Amount:
                Used++; await Runtime.AddSignal(context, Owner.Player!, 1); break;
            case "OpeningSignal" when !play.IsAutoPlay && play.IsFirstInSeries && Used < Amount:
                Used++; await Runtime.AddSignal(context, Owner.Player!, 1); break;
        }
    }
}
public sealed class GeopeliaRhythmPower : GeopeliaMechanicPower
{
    public override string Mode=>"Rhythm";
    protected override bool IsVisibleInternal=>Owner?.Player?.GetRelic<GeopeliaJudgmentLine>() is null;
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
        =>Owner.Player?.GetRelic<GeopeliaJudgmentLine>() is not null?Task.CompletedTask:base.AfterCardPlayed(context,play);
}
// Separate native instances preserve each copy's interval and progress, including mixed upgrades.
public sealed class GeopeliaAerialPower : GeopeliaPower
{
    public override int DisplayAmount=>Math.Max(0,Amount-Progress);
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    private int _counterProgress;
    [SavedProperty] public int Progress { get=>_counterProgress; set { _counterProgress=value; InvokeDisplayAmountChanged(); } }
    [SavedProperty] public int AppliedAfterPlayCount { get; set; }
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        AppliedAfterPlayCount = CombatManager.Instance.History.CardPlaysStarted.Count(e => e.CardPlay.Player == Owner.Player);
        return Task.CompletedTask;
    }
    public override async Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Player != Owner.Player || CombatManager.Instance.IsOverOrEnding) return;
        var started = CombatManager.Instance.History.CardPlaysStarted.Where(e => e.CardPlay.Player == Owner.Player).ToArray();
        int index = Array.FindIndex(started, e => ReferenceEquals(e.CardPlay, play));
        // A newly applied copy begins after its source resolution. Existing copies still count that card.
        if (index >= 0 && index < AppliedAfterPlayCount) return;
        Progress++;
        if (Progress < Math.Max(1, Amount)) return;
        Progress -= Math.Max(1, Amount); // Commit before native autoplay can reenter this hook.
        Flash();
        var target = play.Target is { IsAlive: true } ? play.Target : null;
        // Supports attack, skill, power, X-cost and existing replay/enchantment effects.
        // X repeats use the already-paid captured value; replay itself spends no resources.
        await CardCmd.AutoPlay(context, play.Card, target, skipXCapture: true);
    }
}

public sealed class GeopeliaHatePower : GeopeliaPower
{
    public override int DisplayAmount=>Active?Math.Max(0,Amount-Plays):0;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    private bool _counterActive;
    [SavedProperty] public bool Active { get=>_counterActive; set { _counterActive=value; InvokeDisplayAmountChanged(); } }
    private int _counterPlays;
    [SavedProperty] public int Plays { get=>_counterPlays; set { _counterPlays=value; InvokeDisplayAmountChanged(); } }
    [SavedProperty] public int ActivatedOnTurn { get; set; } = -1;
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // The cast grants the permanent engine; its damage state starts on the next owner turn.
        ActivatedOnTurn = Owner.Player!.PlayerCombatState!.TurnNumber;
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        int turn = Owner.Player!.PlayerCombatState!.TurnNumber;
        if (side == CombatSide.Player && participants.Contains(Owner) && turn > ActivatedOnTurn)
        {
            Active = true; Plays = 0; ActivatedOnTurn = turn;
            Flash();
        }
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? play)
        => Active && !props.HasFlag(ValueProp.Unblockable) && (dealer == Owner || target == Owner) ? 2m : 1m;
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (!Active || play.Player != Owner.Player) return;
        Plays++;
        if (Plays < Amount) return;
        Active = false; // The threshold card gets doubled; subsequent replays do not.
        await PowerCmd.ModifyAmount(context, this, 1, Owner, null);
    }
}

public sealed class GeopeliaBeaconPower : GeopeliaMechanicPower
{
    public async Task Spent(PlayerChoiceContext context)
    {
        if (Used > 0) return;
        Used++;
        await PlayerCmd.GainEnergy(Amount, Owner.Player!);
    }
}
public sealed class GeopeliaPulseGuardPower : GeopeliaMechanicPower { public override string Mode => "PulseGuard"; }
public sealed class GeopeliaRhythmDrawPower : GeopeliaMechanicPower { public override string Mode => "RhythmDraw"; }
public sealed class GeopeliaEchoGuardPower : GeopeliaMechanicPower { public override string Mode => "EchoGuard"; }
public sealed class GeopeliaEchoSignalPower : GeopeliaMechanicPower { public override string Mode => "EchoSignal"; }
public sealed class GeopeliaOpeningSignalPower : GeopeliaMechanicPower { public override string Mode => "OpeningSignal"; }
public sealed class GeopeliaOverdriveGuardPower : GeopeliaPower { }

public sealed class GeopeliaRichterPower : GeopeliaPower
{
    public override PowerType Type => PowerType.Debuff;
    public override async Task AfterEnergyReset(Player player)
    {
        if (player == Owner.Player) await PlayerCmd.LoseEnergy(Amount,player);
    }
}

public sealed class GeopeliaArchidoxenPower : GeopeliaPower
{
    public override int DisplayAmount=>3-(EnergyUsed?1:0)-(DrawUsed?1:0)-(TypeUsed?1:0);
    [SavedProperty] public bool HasPrevious { get; set; }
    [SavedProperty] public int PreviousCost { get; set; }
    [SavedProperty] public int PreviousRarity { get; set; }
    [SavedProperty] public int PreviousType { get; set; }
    [SavedProperty] public bool PreviousCostsX { get; set; }
    private bool _counterEnergyUsed;
    [SavedProperty] public bool EnergyUsed { get=>_counterEnergyUsed; set { _counterEnergyUsed=value; InvokeDisplayAmountChanged(); } }
    private bool _counterDrawUsed;
    [SavedProperty] public bool DrawUsed { get=>_counterDrawUsed; set { _counterDrawUsed=value; InvokeDisplayAmountChanged(); } }
    private bool _counterTypeUsed;
    [SavedProperty] public bool TypeUsed { get=>_counterTypeUsed; set { _counterTypeUsed=value; InvokeDisplayAmountChanged(); } }
    private int _counterPendingDoubles;
    [SavedProperty] public int PendingDoubles { get=>_counterPendingDoubles; set { _counterPendingDoubles=value; InvokeDisplayAmountChanged(); } }

    private void Record(CardModel card,int cost)
    {
        HasPrevious=true;PreviousCost=cost;PreviousRarity=(int)card.Rarity;
        PreviousType=(int)card.Type;PreviousCostsX=card.EnergyCost.CostsX;
    }
    public override Task AfterApplied(Creature? applier,CardModel? cardSource)
    {
        if (!HasPrevious && cardSource is not null && cardSource.Owner == Owner.Player)
            Record(cardSource,cardSource.EnergyCost.GetResolved());
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context,CombatSide side,IReadOnlyList<Creature> participants,ICombatState combatState)
    {
        if (participants.Contains(Owner)) EnergyUsed=DrawUsed=TypeUsed=false;
        return Task.CompletedTask;
    }
    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Player != Owner.Player) return;
        var card=play.Card;
        bool energy=HasPrevious && !EnergyUsed && PreviousCost==play.Resources.EnergyValue && PreviousCostsX==card.EnergyCost.CostsX;
        bool draw=HasPrevious && !DrawUsed && PreviousRarity==(int)card.Rarity;
        bool repeat=HasPrevious && !TypeUsed && PreviousType==(int)card.Type;
        // Reserve each allowance before awaiting; draws can start nested card plays.
        Record(card,play.Resources.EnergyValue);
        if (repeat) { TypeUsed=true;PendingDoubles++; }
        if (energy) EnergyUsed=true;
        if (draw) DrawUsed=true;
        if (energy) await PlayerCmd.GainEnergy(Amount,play.Player);
        if (draw) await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(),Amount,play.Player);
    }
    public decimal DoubleNextBenefit(decimal amount)
    {
        if (PendingDoubles<=0 || amount<=0) return amount;
        PendingDoubles--;return amount*2;
    }
    public override decimal ModifyEnergyGain(Player player,decimal amount)
        => player==Owner.Player && PendingDoubles>0 && amount>0 ? amount*2 : amount;
    public override Task AfterModifyingEnergyGain()
    {
        if (PendingDoubles>0) PendingDoubles--;
        return Task.CompletedTask;
    }
}

public sealed class GeopeliaSignalDrawPower : GeopeliaMechanicPower
{
    public async Task Spent(PlayerChoiceContext context, int n)
    {
        Progress += n;
        while (Progress >= 3 && Used < 3)
        {
            Progress -= 3;
            Used++; // Consume the allowance before drawing: auto-drawn cards may trigger hooks.
            await CardPileCmd.Draw(context, Amount, Owner.Player!);
        }
    }
}
public sealed class GeopeliaDebtDrawPower : GeopeliaMechanicPower
{
    public async Task IncurredDebt(PlayerChoiceContext context)
    {
        if (Used >= 2) return;
        Used++;
        await CardPileCmd.Draw(context, Amount, Owner.Player!);
    }
}

public sealed class GeopeliaNextRepeatPower : GeopeliaPower
{
    private bool Eligible(CardModel card) => card.Owner.Creature == Owner && card.Type == CardType.Attack && !PlayFlags.IsAuto(card);
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int count) => Eligible(card) ? count + 1 : count;
    public override async Task AfterModifyingCardPlayCount(CardModel card) { if (Eligible(card)) await PowerCmd.Decrement(this); }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    { if (participants.Contains(Owner)) await PowerCmd.Remove(this); }
}

public sealed class GeopeliaSignalEchoPower : GeopeliaMechanicPower
{
    public bool Eligible(CardModel card) => card.Owner.Creature == Owner && !PlayFlags.IsAuto(card) && card.Type == CardType.Attack
        && Used == 0 && Runtime.Signal(card.Owner) - Runtime.CardSignalReservation(card) >= Amount;
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int count) => Eligible(card) ? count + 1 : count;
    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        Used++;
        await Runtime.SpendSignal(new ThrowingPlayerChoiceContext(), card.Owner, Amount, card);
    }
}
public sealed class GeopeliaWorldEchoPower : GeopeliaMechanicPower
{
    private bool Eligible(CardModel card)
    {
        if (card.Owner.Creature != Owner || PlayFlags.IsAuto(card) || !Runtime.Replayable(card) || Used >= Amount) return false;
        int reserved = Runtime.CardSignalReservation(card);
        var other = Owner.GetPower<GeopeliaSignalEchoPower>();
        if (other?.Eligible(card) == true) reserved += other.Amount;
        return Runtime.Signal(card.Owner) - reserved >= 2;
    }
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int count) => Eligible(card) ? count + 1 : count;
    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        Used++;
        await Runtime.SpendSignal(new ThrowingPlayerChoiceContext(), card.Owner, 2, card);
    }
}

public sealed class GeopeliaTapeLoopPower : GeopeliaPower
{
    [SavedProperty] public int SourceUpgrade { get; set; }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Player != Owner.Player || play.IsAutoPlay || !play.IsLastInSeries || play.Card is GeopeliaRehearsal) return;
        int upgrade = SourceUpgrade;
        await PowerCmd.Decrement(this);
        var source = Owner.CombatState!.CreateCard(ModelDb.Card<GeopeliaRehearsal>(), Owner.Player!);
        if (upgrade > 0) { source.UpgradeInternal(); source.FinalizeUpgradeInternal(); }
        source.AddKeyword(CardKeyword.Exhaust);
        await CardCmd.AutoPlay(context, source, null);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    { if (participants.Contains(Owner)) await PowerCmd.Remove(this); }
}

public abstract class GeopeliaDebtPower : GeopeliaPower { public override PowerType Type => PowerType.Debuff; }
public sealed class GeopeliaEnergyDebtPower : GeopeliaDebtPower
{
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player) return;
        await PlayerCmd.LoseEnergy(Amount, player);
        await PowerCmd.Remove(this);
    }
}
public sealed class GeopeliaSignalDebtPower : GeopeliaDebtPower
{
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player) return;
        int n = Math.Min(Amount, Runtime.Signal(player));
        // Losing signal is debt repayment, not an active spend and does not trigger spend rewards.
        if (n > 0) await PowerCmd.Apply<GeopeliaSignalPower>(new ThrowingPlayerChoiceContext(), Owner, -n, Owner, null);
        await PowerCmd.Remove(this);
    }
}
public sealed class GeopeliaDrawDebtPower : GeopeliaDebtPower
{
    public override decimal ModifyHandDraw(Player player, decimal count) => player == Owner.Player && AmountOnTurnStart > 0 ? Math.Max(0, count - Amount) : count;
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || AmountOnTurnStart == 0) return;
        await PowerCmd.Remove(this);
    }
}
public abstract class GeopeliaPercentageDebtPower : GeopeliaDebtPower
{
    public override PowerStackType StackType => PowerStackType.Single;
    public abstract Type ActiveType { get; }
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player) return;
        await Runtime.ApplyPower(new ThrowingPlayerChoiceContext(), player, ActiveType, Math.Clamp(Amount, 0, 100), null);
        await PowerCmd.Remove(this);
    }
}
public sealed class GeopeliaDamageDebtPower : GeopeliaPercentageDebtPower { public override Type ActiveType => typeof(GeopeliaMutedPower); }
public sealed class GeopeliaBlockDebtPower : GeopeliaPercentageDebtPower { public override Type ActiveType => typeof(GeopeliaFragileGuardPower); }
public abstract class GeopeliaTurnPenaltyPower : GeopeliaDebtPower
{
    public override PowerStackType StackType => PowerStackType.Single;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    { if (participants.Contains(Owner)) await PowerCmd.Remove(this); }
}
public sealed class GeopeliaMutedPower : GeopeliaTurnPenaltyPower
{
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? play)
        => dealer == Owner && cardSource is not null && props.IsPoweredAttack() ? Math.Max(0m, 1m - Amount / 100m) : 1m;
}
public sealed class GeopeliaFragileGuardPower : GeopeliaTurnPenaltyPower
{
    public override decimal ModifyBlockMultiplicative(Creature target, decimal amount, ValueProp props, CardModel? cardSource, CardPlay? play)
        => target == Owner && cardSource is not null ? Math.Max(0m, 1m - Amount / 100m) : 1m;
}
