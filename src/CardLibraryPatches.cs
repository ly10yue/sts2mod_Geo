using Godot;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace PhigrosJiu;

// Follow Saya's integration: register an independent filter with the native library.
// Keep discovery changes local to this screen; browsing must not modify save progress.
[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
internal static class JiuCardLibraryFilterPatch
{
    private static void Postfix(NCardLibrary __instance)
    {
        var filters = (Dictionary<NCardPoolFilter, Func<CardModel, bool>>)AccessTools.Field(typeof(NCardLibrary), "_poolFilters").GetValue(__instance)!;
        var characters = (Dictionary<CharacterModel, NCardPoolFilter>)AccessTools.Field(typeof(NCardLibrary), "_cardPoolFilters").GetValue(__instance)!;
        var character = ModelDb.Character<Geopelia>();
        if (characters.ContainsKey(character)) return;
        var template = (NCardPoolFilter)AccessTools.Field(typeof(NCardLibrary), "_miscPoolFilter").GetValue(__instance)!;
        var hsv = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://shaders/hsv.gdshader") };
        hsv.SetShaderParameter("h", 1f);
        hsv.SetShaderParameter("s", 0.3f);
        hsv.SetShaderParameter("v", 0.55f);
        var filter = new NCardPoolFilter
        {
            Name = "PhigrosJiuPool", Loc = character.Title,
            CustomMinimumSize = new Vector2(64, 64), Size = new Vector2(64, 64),
        };
        var image = new TextureRect
        {
            Name = "Image", Texture = character.IconTexture, Material = hsv,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(56, 56), Position = new Vector2(4, 4), PivotOffset = new Vector2(28, 28),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var reticle = ResourceLoader.Load<PackedScene>("res://scenes/ui/selection_reticle.tscn").Instantiate<NSelectionReticle>();
        reticle.Name = "SelectionReticle";
        filter.AddChild(image);
        filter.AddChild(reticle);
        reticle.Owner = filter;
        reticle.UniqueNameInOwner = true;
        template.GetParent().AddChild(filter);
        characters.Add(character, filter);
        filters.Add(filter, card => card is GeopeliaCard);
        filter.Connect(NCardPoolFilter.SignalName.Toggled, Callable.From<NCardPoolFilter>(selected =>
            AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter").Invoke(__instance, new object[] { selected })));
        filter.Connect(Control.SignalName.FocusEntered, Callable.From(() =>
            AccessTools.Field(typeof(NCardLibrary), "_lastHoveredControl").SetValue(__instance, filter)));
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.RefreshVisibility))]
internal static class JiuCardLibraryVisibilityPatch
{
    private static void Postfix(NCardLibraryGrid __instance)
    {
        var seen = (HashSet<ModelId>)AccessTools.Field(typeof(NCardLibraryGrid), "_seenCards").GetValue(__instance)!;
        var unlocked = (HashSet<CardModel>)AccessTools.Field(typeof(NCardLibraryGrid), "_unlockedCards").GetValue(__instance)!;
        foreach (var card in ModelDb.CardPool<GeopeliaCardPool>().AllCards)
        {
            seen.Add(card.Id);
            unlocked.Add(card);
        }
    }
}

[HarmonyPatch]
internal static class JiuCardLibraryDiscoveryPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        MethodsIn(typeof(NCardLibrary)).Where(method => method.Name.Contains("TextFilter") || method.Name.Contains("ShowCardDetail"));

    private static IEnumerable<MethodInfo> MethodsIn(Type type) =>
        AccessTools.GetDeclaredMethods(type).Where(method => method.GetMethodBody() is not null)
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(MethodsIn));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var contains = AccessTools.Method(typeof(IReadOnlySet<ModelId>), nameof(IReadOnlySet<ModelId>.Contains));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(contains))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(JiuCardLibraryDiscoveryPatch), nameof(IsVisible));
            }
            yield return instruction;
        }
    }

    // Used only by the library's search and inspection checks, including its compiler-generated
    // navigation predicate. The original save-backed discovered set is never changed.
    internal static bool IsVisible(IReadOnlySet<ModelId> discovered, ModelId id) =>
        discovered.Contains(id) || ModelDb.GetByIdOrNull<CardModel>(id) is GeopeliaCard;
}
