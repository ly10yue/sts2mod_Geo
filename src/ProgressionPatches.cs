using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace PhigrosJiu;

internal static class JiuProgression
{
    // The peer has the same vanilla integration gaps. Identify it by both assembly and ID;
    // neither patch changes its DLL, resources, model identity, or another mod's character.
    internal static bool IsJiu(CharacterModel character) => character is Geopelia ||
        (character.GetType().Assembly.GetName().Name == "jiu_2" && character.Id.Entry == "GEOPELIA");

    internal static void AddDialogue(AncientDialogueSet set, string ancient, bool architect)
    {
        foreach (string entry in new[] { ModelDb.GetId<Geopelia>().Entry, "GEOPELIA" })
        {
            if (set.CharacterDialogues.ContainsKey(entry)) continue;
            var dialogue = new AncientDialogue(architect ? "" : "event:/sfx/npcs/neow/neow_curious", "", "")
            {
                IsRepeating = true,
                EndAttackers = architect ? ArchitectAttackers.Both : ArchitectAttackers.None,
            };
            // Both versions share these authored lines, while keeping separate visit statistics.
            dialogue.PopulateLines(ancient, ModelDb.GetId<Geopelia>().Entry, 0);
            for (int i = 0; i < dialogue.Lines.Count - 1; i++)
            {
                string key = dialogue.Lines[i].LineText!.LocEntryKey;
                dialogue.Lines[i].NextButtonText = new("ancients", key[..key.LastIndexOf('.')] + ".next");
            }
            set.CharacterDialogues.Add(entry, new[] { dialogue });
        }
    }
}

// These three routines grant vanilla-only character epochs. Keep the surrounding native
// combat completion, fight statistics, rewards and save operations running normally.
[HarmonyPatch]
internal static class JiuVanillaEpochPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "CheckFifteenElitesDefeatedEpoch", "CheckFifteenBossesDefeatedEpoch", "ObtainCharUnlockEpoch" })
            yield return AccessTools.Method(typeof(ProgressSaveManager), name);
    }

    private static bool Prefix(Player localPlayer) => !JiuProgression.IsJiu(localPlayer.Character);
}

[HarmonyPatch(typeof(AncientEventModel), nameof(AncientEventModel.DialogueSet), MethodType.Getter)]
internal static class JiuNeowDialoguePatch
{
    private static void Postfix(AncientEventModel __instance, AncientDialogueSet __result)
    {
        if (__instance is Neow) JiuProgression.AddDialogue(__result, __instance.Id.Entry, false);
    }
}

[HarmonyPatch(typeof(TheArchitect), nameof(TheArchitect.DialogueSet), MethodType.Getter)]
internal static class JiuArchitectDialoguePatch
{
    private static void Postfix(TheArchitect __instance, AncientDialogueSet __result) =>
        JiuProgression.AddDialogue(__result, __instance.Id.Entry, true);
}
