using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace PhigrosJiu;

/// <summary>
/// Mod entry point. Registers the Harmony patches that hook the character into the game.
/// Card, power and relic models are discovered by the engine itself: ModelDb.Init scans every
/// AbstractModel subclass in loaded mod assemblies, so no manual registration is required.
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public const string ModId = "PhigrosJiu";
    public const string HarmonyId = "local.phigrosjiu.character";
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        new Harmony(HarmonyId).PatchAll(typeof(Entry).Assembly);
        _initialized = true;
        Log.Info("[PhigrosJiu] 鸠 loaded: 89 cards, 4 relics; repeats, overdraft and signal.");
    }
}
