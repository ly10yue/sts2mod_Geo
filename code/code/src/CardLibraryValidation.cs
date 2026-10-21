#if JIU_VALIDATION
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task CardLibraryCases()
    {
        _current = "card-library-native-ui";
        var discovered = SaveManager.Instance.Progress.DiscoveredCards.ToHashSet();
        var screen = ResourceLoader.Load<PackedScene>("res://scenes/screens/card_library/card_library.tscn").Instantiate<NCardLibrary>();
        NGame.Instance!.AddChild(screen);
        screen.Initialize(NullRunState.Instance);
        screen.OnSubmenuOpened();
        async Task Frames() { for (int i = 0; i < 8; i++) await screen.ToSignal(screen.GetTree(), SceneTree.SignalName.ProcessFrame); }
        await Frames();
        var characters = (Dictionary<CharacterModel, NCardPoolFilter>)AccessTools.Field(typeof(NCardLibrary), "_cardPoolFilters").GetValue(screen)!;
        var filter = characters[ModelDb.Character<Geopelia>()];
        Check(filter.Visible && filter.GetNode<TextureRect>("Image").Texture is not null, "Jiu filter and icon exist");
        Check(characters.Keys.Count(c => c is Geopelia) == 1, "exactly one Jiu filter");
        filter.IsSelected = true;
        filter.EmitSignal(NCardPoolFilter.SignalName.Toggled, filter);
        await Frames();
        var grid = (NCardLibraryGrid)AccessTools.Field(typeof(NCardLibrary), "_grid").GetValue(screen)!;
        Check(grid.VisibleCards.Count() == Design.Cards.Count && grid.VisibleCards.All(c => c is GeopeliaCard), "Jiu filter displays all main-pool cards without peer cards");
        var seen = (HashSet<ModelId>)AccessTools.Field(typeof(NCardLibraryGrid), "_seenCards").GetValue(grid)!;
        var unlocked = (HashSet<CardModel>)AccessTools.Field(typeof(NCardLibraryGrid), "_unlockedCards").GetValue(grid)!;
        Check(grid.VisibleCards.All(c => seen.Contains(c.Id) && unlocked.Contains(c)), "all Jiu cards show their full artwork");
        var search = (NSearchBar)AccessTools.Field(typeof(NCardLibrary), "_searchBar").GetValue(screen)!;
        search.TextArea.Text = "dB doll";
        AccessTools.Method(typeof(NCardLibrary), "UpdateFilter").Invoke(screen, new object[] { false });
        await Frames();
        Check(grid.VisibleCards.Count() == 1 && grid.VisibleCards.Single() is GeopeliaStrike, "undiscovered card can be searched by song title");
        var rows = (List<List<NGridCardHolder>>)AccessTools.Field(grid.GetType().BaseType!, "_cardRows").GetValue(grid)!;
        var holder = rows.SelectMany(r => r).First(h => h.CardModel is GeopeliaStrike);
        AccessTools.Method(typeof(NCardLibrary), "ShowCardDetail").Invoke(screen, new object[] { holder });
        var inspect = NGame.Instance.GetInspectCardScreen();
        Check(inspect.Visible, "undiscovered card opens in native inspector");
        inspect.Close();
        var upgrades = (NLibraryStatTickbox)AccessTools.Field(typeof(NCardLibrary), "_viewUpgrades").GetValue(screen)!;
        upgrades.IsTicked = true;
        AccessTools.Method(typeof(NCardLibrary), "ToggleShowUpgrades").Invoke(screen, new object[] { upgrades });
        await Frames();
        Check(grid.VisibleCards.Count() == 1 && grid.IsShowingUpgrades, "upgrade preview works with search");
        search.ClearText();
        var common = (NCardRarityTickbox)AccessTools.Field(typeof(NCardLibrary), "_commonFilter").GetValue(screen)!;
        common.IsTicked = true;
        AccessTools.Method(typeof(NCardLibrary), "UpdateRarityFilter").Invoke(screen, new object[] { common });
        await Frames();
        Check(grid.VisibleCards.Count() == 25, "rarity filter includes all 25 common cards");
        Check(discovered.SetEquals(SaveManager.Instance.Progress.DiscoveredCards), "browsing never changes persistent discovery progress");
        Benchmarks.Add(new { test = "card-library", totalCards = Design.Cards.Count, commonCards = 25, search = "PASS", inspect = "PASS", upgrade = "PASS", discoveryUnchanged = true });
        screen.OnSubmenuClosed();
        // Keep the test node alive until process shutdown; native grid animations can still be pending.
        screen.Visible=false;
        await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
#endif
