using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace PhigrosJiu;

/// <summary>Permanent identities for the main mod, independent of jiu_2's assembly and saves.</summary>
public static class ModelIdentity
{
    public const string Prefix = "PHIGROS_JIU_";

    public static string AssetEntry(string entry) => entry.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
        ? entry[Prefix.Length..]
        : entry;
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.GetEntry))]
internal static class MainModelIdentityPatch
{
    // Type names alone are not unique: jiu_2 has the same Geopelia* class names.
    // Restrict the prefix to this assembly and preserve every other mod's result.
    private static void Postfix(Type type, ref string __result)
    {
        if (type.Assembly != typeof(Entry).Assembly || !typeof(AbstractModel).IsAssignableFrom(type)) return;
        if (!__result.StartsWith(ModelIdentity.Prefix, StringComparison.Ordinal))
            __result = ModelIdentity.Prefix + __result;
    }
}
