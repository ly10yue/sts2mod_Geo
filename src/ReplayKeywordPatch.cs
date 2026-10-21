using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace PhigrosJiu;

// Use native replay text, hover tips, play-count generation and enchantment scaling.
// BaseReplayCount remains the native mutable field, so Transfigure/Hidden Gem still add once.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetEnchantedReplayCount))]
internal static class JiuReplayKeywordPatch
{
    private static bool Prefix(CardModel __instance, ref int __result)
    {
        if (__instance is not GeopeliaCard card) return true;
        int count = card.BaseReplayCount + card.Definition.Replays;
        __result = card.Enchantment?.EnchantPlayCount(count) ?? count;
        return false;
    }
}
