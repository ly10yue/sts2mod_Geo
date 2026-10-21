using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace PhigrosJiu;

/// <summary>
/// CharacterModel derives almost every asset path and sound event from the model id, which for
/// this character is "geopelia" — a name the base game has no assets for. These prefixes point
/// every missing path at the vanilla Silent assets, while the character-select artwork comes
/// from the mod's own pck. Nothing else about Silent is used: stats, deck, pool and relics are
/// all defined by <see cref="Geopelia"/>.
/// </summary>
internal static class GeopeliaCharacterPatches
{
    internal static bool IsGeopelia(CharacterModel model) => model is Geopelia;

    internal static bool Redirect(CharacterModel model, ref string result, string value)
    {
        if (!IsGeopelia(model)) return true;
        result = value;
        return false;
    }
}

[HarmonyPatch(typeof(ModelDb), "AllCharacters", MethodType.Getter)]
internal static class GeopeliaAllCharactersPatch
{
    /// <summary>Appends the mod character to the game's character list (character select, unlock state, random picks).</summary>
    private static void Postfix(ref IEnumerable<CharacterModel> __result)
    {
        CharacterModel? character = ModelDb.GetByIdOrNull<CharacterModel>(ModelDb.GetId<Geopelia>());
        if (character == null) return;
        List<CharacterModel> list = __result.ToList();
        if (list.Contains(character)) return;
        list.Add(character);
        __result = list;
    }
}

[HarmonyPatch(typeof(CharacterModel), "VisualsPath", MethodType.Getter)]
internal static class GeopeliaVisualsPathPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, Content.CharacterVisuals);
}

[HarmonyPatch(typeof(CharacterModel), "IconTexturePath", MethodType.Getter)]
internal static class GeopeliaIconTexturePatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, Content.CharacterIcon);
}

[HarmonyPatch(typeof(CharacterModel), "IconOutlineTexturePath", MethodType.Getter)]
internal static class GeopeliaIconOutlineTexturePatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, Content.CharacterIcon);
}

[HarmonyPatch(typeof(CharacterModel), "EnergyCounterPath", MethodType.Getter)]
internal static class GeopeliaEnergyCounterPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, SceneHelper.GetScenePath("combat/energy_counters/silent_energy_counter"));
}

[HarmonyPatch(typeof(CharacterModel), "MerchantAnimPath", MethodType.Getter)]
internal static class GeopeliaMerchantPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, SceneHelper.GetScenePath("merchant/characters/silent_merchant"));
}

[HarmonyPatch(typeof(CharacterModel), "RestSiteAnimPath", MethodType.Getter)]
internal static class GeopeliaRestSitePatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, SceneHelper.GetScenePath("rest_site/characters/silent_rest_site"));
}

[HarmonyPatch(typeof(CharacterModel), "TrailPath", MethodType.Getter)]
internal static class GeopeliaTrailPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, SceneHelper.GetScenePath("vfx/card_trail_silent"));
}

[HarmonyPatch(typeof(CharacterModel), "CharacterSelectBg", MethodType.Getter)]
internal static class GeopeliaCharacterSelectBgPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, Content.CharacterSelectBg);
}

[HarmonyPatch(typeof(CharacterModel), "CharacterSelectTransitionPath", MethodType.Getter)]
internal static class GeopeliaTransitionPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, "res://materials/transitions/silent_transition_mat.tres");
}

[HarmonyPatch(typeof(CharacterModel), "ArmPointingTexturePath", MethodType.Getter)]
internal static class GeopeliaArmPointPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, ImageHelper.GetImagePath("ui/hands/multiplayer_hand_silent_point.png"));
}

[HarmonyPatch(typeof(CharacterModel), "ArmRockTexturePath", MethodType.Getter)]
internal static class GeopeliaArmRockPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, ImageHelper.GetImagePath("ui/hands/multiplayer_hand_silent_rock.png"));
}

[HarmonyPatch(typeof(CharacterModel), "ArmPaperTexturePath", MethodType.Getter)]
internal static class GeopeliaArmPaperPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, ImageHelper.GetImagePath("ui/hands/multiplayer_hand_silent_paper.png"));
}

[HarmonyPatch(typeof(CharacterModel), "ArmScissorsTexturePath", MethodType.Getter)]
internal static class GeopeliaArmScissorsPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, ImageHelper.GetImagePath("ui/hands/multiplayer_hand_silent_scissors.png"));
}

[HarmonyPatch(typeof(CharacterModel), "CharacterSelectSfx", MethodType.Getter)]
internal static class GeopeliaSelectSfxPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, "event:/sfx/characters/silent/silent_select");
}

[HarmonyPatch(typeof(CharacterModel), "AttackSfx", MethodType.Getter)]
internal static class GeopeliaAttackSfxPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, "event:/sfx/characters/silent/silent_attack");
}

[HarmonyPatch(typeof(CharacterModel), "CastSfx", MethodType.Getter)]
internal static class GeopeliaCastSfxPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, "event:/sfx/characters/silent/silent_cast");
}

[HarmonyPatch(typeof(CharacterModel), "DeathSfx", MethodType.Getter)]
internal static class GeopeliaDeathSfxPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, "event:/sfx/characters/silent/silent_die");
}

[HarmonyPatch(typeof(CharacterModel), "CharacterTransitionSfx", MethodType.Getter)]
internal static class GeopeliaTransitionSfxPatch
{
    private static bool Prefix(CharacterModel __instance, ref string __result) =>
        GeopeliaCharacterPatches.Redirect(__instance, ref __result, "event:/sfx/ui/wipe_silent");
}

/// <summary>Powers use the vanilla sprite atlas by default; point ours at the mod's png icons.</summary>
[HarmonyPatch(typeof(PowerModel), "PackedIconPath", MethodType.Getter)]
internal static class GeopeliaPowerIconPatch
{
    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (__instance is not GeopeliaPower) return true;
        __result = Content.PowerIcon(__instance.Id.Entry.ToLowerInvariant());
        return false;
    }
}

[HarmonyPatch(typeof(PowerModel), "ResolvedBigIconPath", MethodType.Getter)]
internal static class GeopeliaPowerBigIconPatch
{
    private static bool Prefix(PowerModel __instance, ref string __result)
    {
        if (__instance is not GeopeliaPower) return true;
        __result = Content.PowerIcon(__instance.Id.Entry.ToLowerInvariant());
        return false;
    }
}
