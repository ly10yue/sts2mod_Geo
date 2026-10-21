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

public sealed class GeopeliaLastWillPower : GeopeliaPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
            await CreatureCmd.Kill(Owner, force: true);
    }
}

public sealed class GeopeliaRestartFuturePower : GeopeliaPower
{
    [SavedProperty] public int Plays { get; set; }
    [SavedProperty] public bool ExtraTurnPending { get; set; }
    private CardModel? _activationSource;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _activationSource = cardSource;
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Player != Owner.Player) return Task.CompletedTask;
        if (ReferenceEquals(play.Card, _activationSource))
        {
            _activationSource = null;
            return Task.CompletedTask;
        }
        Plays++;
        if (Plays >= 12 && !ExtraTurnPending && !CombatManager.Instance.IsOverOrEnding)
        {
            Plays -= 12;
            ExtraTurnPending = true;
            PlayerCmd.EndTurn(play.Player, canBackOut: false);
        }
        return Task.CompletedTask;
    }
    public override bool ShouldTakeExtraTurn(Player player) => player == Owner.Player && ExtraTurnPending;
    public override Task AfterTakingExtraTurn(Player player)
    {
        if (player == Owner.Player) ExtraTurnPending = false;
        return Task.CompletedTask;
    }
}

/// <summary>All per-turn counters are saved, owner-scoped and reset before that owner's turn.</summary>
public abstract class GeopeliaMechanicPower : GeopeliaPower
{
    public virtual string Mode => "";
    [SavedProperty] public int Plays { get; set; }
    [SavedProperty] public int Used { get; set; }
    [SavedProperty] public int Progress { get; set; }
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
            case "Aerial" when !play.IsAutoPlay && play.IsFirstInSeries && play.Card.Type == CardType.Skill && Used < 3:
                Used++; await Runtime.AddSignal(context, Owner.Player!, Amount); break;
            case "EchoGuard" when Runtime.IsRepeat(play) && Used < 6:
                Used++; await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null); break;
            case "EchoSignal" when Runtime.IsRepeat(play) && Used < Amount:
                Used++; await Runtime.AddSignal(context, Owner.Player!, 1); break;
            case "OpeningSignal" when !play.IsAutoPlay && play.IsFirstInSeries && Used < Amount:
                Used++; await Runtime.AddSignal(context, Owner.Player!, 1); break;
        }
    }
}
public sealed class GeopeliaRhythmPower : GeopeliaMechanicPower { public override string Mode => "Rhythm"; }
public sealed class GeopeliaAerialPower : GeopeliaMechanicPower { public override string Mode => "Aerial"; }
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
    [SavedProperty] public bool HasPrevious { get; set; }
    [SavedProperty] public int PreviousCost { get; set; }
    [SavedProperty] public int PreviousRarity { get; set; }
    [SavedProperty] public int PreviousType { get; set; }
    [SavedProperty] public bool PreviousCostsX { get; set; }
    [SavedProperty] public bool EnergyUsed { get; set; }
    [SavedProperty] public bool DrawUsed { get; set; }
    [SavedProperty] public bool TypeUsed { get; set; }
    [SavedProperty] public int PendingDoubles { get; set; }

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
