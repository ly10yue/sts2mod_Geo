using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace PhigrosJiu;

internal static class FatalCardRewards
{
    internal const string SaveKey = "PhigrosJiu.FatalCardRewardTiers";
    private sealed class BattleBonus { public Dictionary<ulong, int> Tiers { get; } = new(); }
    private static readonly ConditionalWeakTable<ICombatState, BattleBonus> Bonuses = new();
    // Results already processed must not be promoted again when Populate is called a second time.
    private static readonly ConditionalWeakTable<CardReward, HashSet<CardCreationResult>> Processed = new();
    internal static void Record(ICombatState battle, Player player)
    {
        var tiers = Bonuses.GetOrCreateValue(battle).Tiers;
        tiers[player.NetId] = Math.Min(2, tiers.GetValueOrDefault(player.NetId) + 1);
    }
    internal static int Count(ICombatState battle, Player player)
        => Bonuses.TryGetValue(battle, out var bonus) ? bonus.Tiers.GetValueOrDefault(player.NetId) : 0;
    internal static void Save(CombatRoom room, SerializableRoom save)
    {
        if (room.IsPreFinished && Bonuses.TryGetValue(room.CombatState, out var bonus))
            save.EncounterState[SaveKey] = JsonSerializer.Serialize(bonus.Tiers);
    }
    internal static void Restore(CombatRoom room, Dictionary<string, string> state)
    {
        if (!room.IsPreFinished || !state.TryGetValue(SaveKey, out string? json)) return;
        foreach (var entry in JsonSerializer.Deserialize<Dictionary<ulong, int>>(json) ?? new())
            Bonuses.GetOrCreateValue(room.CombatState).Tiers[entry.Key] = Math.Clamp(entry.Value, 0, 2);
    }
    internal static void Promote(CardReward reward, CardCreationOptions options, List<CardCreationResult> cards)
    {
        var player = reward.Player;
        if (options.Source != CardCreationSource.Encounter || player.RunState.CurrentRoom is not CombatRoom room) return;
        int tiers = Count(room.CombatState, player);
        if (tiers == 0) return;
        var processed = Processed.GetOrCreateValue(reward);
        var pools = options.CardPools.Count > 0 ? options.CardPools : new[] { player.Character.CardPool };
        var choices = pools.SelectMany(p => p.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)).ToArray();
        var used = cards.Select(c => c.Card.Id).ToHashSet();
        foreach (var result in cards)
        {
            if (!processed.Add(result)) continue;
            var original = result.Card;
            var rarity = original.Rarity;
            for (int i = 0; i < tiers; i++) rarity = rarity switch
            { CardRarity.Common => CardRarity.Uncommon, CardRarity.Uncommon => CardRarity.Rare, _ => rarity };
            if (rarity == original.Rarity) continue;
            var pool = choices.Where(c => c.Rarity == rarity && !used.Contains(c.Id)).ToArray();
            if (pool.Length == 0) pool = choices.Where(c => c.Rarity == rarity).ToArray();
            if (pool.Length == 0) continue;
            var selected = (options.RngOverride ?? player.PlayerRng.Rewards).NextItem(pool)!;
            var promoted = player.RunState.CreateCard(selected, player);
            for (int i = 0; i < original.CurrentUpgradeLevel; i++) CardCmd.Upgrade(promoted);
            result.ModifyCard(promoted);
            used.Add(promoted.Id);
        }
    }
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.ToSerializable))]
internal static class FatalRewardSavePatch
{
    private static void Postfix(CombatRoom __instance, SerializableRoom __result) => FatalCardRewards.Save(__instance, __result);
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.FromSerializable))]
internal static class FatalRewardRestorePatch
{
    // Keep the namespaced extra data out of each vanilla encounter's own LoadCustomState implementation.
    private static void Prefix(SerializableRoom serializableRoom, out Dictionary<string, string> __state)
    {
        __state = serializableRoom.EncounterState;
        if (__state.ContainsKey(FatalCardRewards.SaveKey))
            serializableRoom.EncounterState = __state.Where(e => e.Key != FatalCardRewards.SaveKey).ToDictionary();
    }
    private static void Postfix(SerializableRoom serializableRoom, CombatRoom __result, Dictionary<string, string> __state)
    {
        serializableRoom.EncounterState = __state;
        FatalCardRewards.Restore(__result, __state);
    }
}

[HarmonyPatch(typeof(CardReward), nameof(CardReward.Populate))]
internal static class FatalCardRewardPatch
{
    private static void Postfix(CardReward __instance, List<CardCreationResult> ____cards)
    {
        var options = (CardCreationOptions)AccessTools.Property(typeof(CardReward), "Options").GetValue(__instance)!;
        FatalCardRewards.Promote(__instance, options, ____cards);
    }
}
