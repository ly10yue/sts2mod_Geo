using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace PhigrosJiu;

public static class Runtime
{
    public const int SignalCap = 24;
    public static int SignalLimit(Player player) => SignalCap + player.Creature.GetPowerAmount<GeopeliaSignalCapacityPower>();
    public static int Signal(Player player) => player.Creature.GetPowerAmount<GeopeliaSignalPower>();
    public static bool IsRepeat(CardPlay play) => play.IsAutoPlay || !play.IsFirstInSeries;
    public static CardPlayFinishedEntry[] History(Player player) => CombatManager.Instance.History.CardPlaysFinished
        .Where(e => e.CardPlay.Player == player && e.HappenedThisTurn(player.Creature.CombatState)).ToArray();
    public static int Played(Player player) => History(player).Length;
    // Like PullFromBelow, count each completed resolution across the whole combat, including replays.
    public static int DebtCardsPlayed(Player player) => CombatManager.Instance.History.CardPlaysFinished.Count(e =>
        e.CardPlay.Player == player && e.CardPlay.Card is GeopeliaCard card && card.Definition.Steps
            .Select((s, i) => (s, i)).Any(p => p.s.Kind is "EnergyDebt" or "DrawDebt" or "SignalDebt" or "DamageDebt" or "BlockDebt"
                && card.DynamicVars["V" + p.i].IntValue > 0));
    public static int Debt(Player player) => player.Creature.Powers
        .Where(p => p is GeopeliaDebtPower && p is not GeopeliaTurnPenaltyPower).Sum(p => p.Amount);
    public static bool Condition(EffectStep step, Player player, CardPlay play) => step.Condition switch
    {
        "plays" => Played(player) >= step.Threshold,
        "signal" => Signal(player) >= step.Threshold,
        "replay" => IsRepeat(play),
        "debt" => Debt(player) > 0,
        "lastAttack" => History(player).LastOrDefault()?.CardPlay.Card.Type == CardType.Attack,
        "lastSkill" => History(player).LastOrDefault()?.CardPlay.Card.Type == CardType.Skill,
        _ => true
    };
    public static async Task AddSignal(PlayerChoiceContext context, Player player, int n, CardModel? source = null)
    {
        int amount = Math.Min(Math.Max(0, n), SignalLimit(player) - Signal(player));
        if (amount > 0) await PowerCmd.Apply<GeopeliaSignalPower>(context, player.Creature, amount, player.Creature, source);
    }
    public static async Task<bool> SpendSignal(PlayerChoiceContext context, Player player, int n, CardModel? source)
    {
        if (n <= 0) return true;
        if (Signal(player) < n) return false;
        await PowerCmd.Apply<GeopeliaSignalPower>(context, player.Creature, -n, player.Creature, source);
        if (player.Creature.GetPower<GeopeliaSignalDrawPower>() is { } amplifier) await amplifier.Spent(context, n);
        if (player.Creature.GetPower<GeopeliaBeaconPower>() is { } beacon) await beacon.Spent(context);
        return true;
    }
    public static Task ApplyPower(PlayerChoiceContext context, Player player, Type type, int amount, CardModel? source)
    {
        if (type == typeof(GeopeliaSignalEchoPower) && player.Creature.GetPower<GeopeliaSignalEchoPower>() is { } old)
        { old.SetAmount(Math.Min(old.Amount, amount)); return Task.CompletedTask; }
        if ((typeof(GeopeliaPercentageDebtPower).IsAssignableFrom(type) || typeof(GeopeliaTurnPenaltyPower).IsAssignableFrom(type))
            && player.Creature.Powers.Any(p => p.GetType() == type)) return Task.CompletedTask;
        return PowerCmd.Apply(context, ModelDb.GetById<PowerModel>(ModelDb.GetId(type)).ToMutable(), player.Creature, amount, player.Creature, source);
    }
    public static Type DebtType(string kind) => kind switch
    {
        "EnergyDebt" => typeof(GeopeliaEnergyDebtPower), "DrawDebt" => typeof(GeopeliaDrawDebtPower),
        "SignalDebt" => typeof(GeopeliaSignalDebtPower), "DamageDebt" => typeof(GeopeliaDamageDebtPower),
        "BlockDebt" => typeof(GeopeliaBlockDebtPower), _ => throw new ArgumentException(kind)
    };
    public static Type PowerType(string kind) => kind switch
    {
        "FutureRestart" => typeof(GeopeliaFutureRestartPower),
        "Aerial" => typeof(GeopeliaAerialPower), "NextRepeat" => typeof(GeopeliaNextRepeatPower),
        "PulseGuard" => typeof(GeopeliaPulseGuardPower), "OverdriveGuard" => typeof(GeopeliaOverdriveGuardPower),
        "EchoGuard" => typeof(GeopeliaEchoGuardPower), "SignalEcho" => typeof(GeopeliaSignalEchoPower),
        "RhythmDraw" => typeof(GeopeliaRhythmDrawPower), "Beacon" => typeof(GeopeliaBeaconPower),
        "SignalDraw" => typeof(GeopeliaSignalDrawPower), "DebtDraw" => typeof(GeopeliaDebtDrawPower),
        "OpeningSignal" => typeof(GeopeliaOpeningSignalPower), "EchoSignal" => typeof(GeopeliaEchoSignalPower),
        "WorldEcho" => typeof(GeopeliaWorldEchoPower), "Archidoxen" => typeof(GeopeliaArchidoxenPower),
        "Richter" => typeof(GeopeliaRichterPower), "SignalCapacity" => typeof(GeopeliaSignalCapacityPower), _ => throw new ArgumentException(kind)
    };
    public static bool Replayable(CardModel card) => card.Type is CardType.Attack or CardType.Skill
        && !card.EnergyCost.CostsX && !card.HasStarCostX && (card is not GeopeliaCard own || !own.Definition.HasReplayAction);
    public static IEnumerable<CardModel> ReplayableHistory(Player player, CardModel excluded) => History(player)
        .Where(e => !e.CardPlay.IsAutoPlay && e.CardPlay.IsFirstInSeries && e.CardPlay.Card != excluded && Replayable(e.CardPlay.Card))
        .Select(e => e.CardPlay.Card);
    public static async Task Replay(PlayerChoiceContext context, CardModel source, Creature? target)
    {
        if (!Replayable(source) || CombatManager.Instance.IsOverOrEnding) return;
        // Use the original mutable card and native play-count/result-pile hooks, just as native autoplay does.
        // No clone or synthetic Exhaust: upgrades, enchantments and original keywords remain intact.
        Creature? replayTarget = source.TargetType == TargetType.AnyEnemy && target is { IsAlive: true }
            && target.Side != source.Owner.Creature.Side ? target : null;
        await CardCmd.AutoPlay(context, source, replayTarget);
    }
    public static int CardSignalReservation(CardModel card) => card is GeopeliaCard own
        && own.Definition.SignalReplayCost > 0 && Signal(card.Owner) >= own.Definition.SignalReplayCost ? own.Definition.SignalReplayCost : 0;
}

internal static class PlayFlags
{
    internal sealed class Flag { public bool Auto; }
    private static readonly ConditionalWeakTable<CardModel, Flag> Flags = new();
    public static bool IsAuto(CardModel card) => Flags.TryGetValue(card, out var flag) && flag.Auto;
    public static void Set(CardModel card, bool value) => Flags.GetOrCreateValue(card).Auto = value;
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class RecordAutoPlayFlag
{
    // Observation only: native replay generation has no isAutoPlay parameter.
    private static void Prefix(CardModel __instance, bool isAutoPlay) => PlayFlags.Set(__instance, isAutoPlay);
}
