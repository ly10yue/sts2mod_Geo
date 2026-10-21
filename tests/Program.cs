using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

// Isolated managed-process integration test. Does not open a game or touch player saves.
string game = Path.GetFullPath(args[0]);
bool reverse = args.Contains("reverse");
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(game, "data_sts2_windows_x86_64", name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
string? overrideMain = args.FirstOrDefault(a => a.StartsWith("--main-dll="));
var main = AssemblyLoadContext.Default.LoadFromAssemblyPath(overrideMain is null ? Path.Combine(game, "mods/PhigrosJiu/PhigrosJiu.dll") : Path.GetFullPath(overrideMain["--main-dll=".Length..]));
var peer = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "mods/jiu_2/jiu_2.dll"));
var assemblies = reverse ? new[] { peer, main } : new[] { main, peer };
var owners = new Dictionary<Assembly, string> { [main] = "local.phigrosjiu.character", [peer] = "local.jiu_2.character" };
foreach (var assembly in assemblies) new Harmony(owners[assembly]).PatchAll(assembly);
int checks = 0;
void Check(bool passed, string label)
{
    checks++;
    if (!passed) throw new Exception("FAILED: " + label);
}

Type[] Models(Assembly a) => a.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(AbstractModel))).ToArray();
var mainTypes = Models(main);
Check(main.GetType("PhigrosJiu.GameValidation") is null, "production DLL excludes validation harness");
Check(main.GetType("PhigrosJiu.ValidationSkipShuffleTutorial") is null, "production DLL keeps native tutorials unchanged");
var peerTypes = Models(peer);
var ids = new Dictionary<string, Type>();
foreach (var type in mainTypes.Concat(peerTypes))
{
    string id = ModelDb.GetId(type).ToString();
    Check(!ids.ContainsKey(id), "duplicate model id " + id);
    ids[id] = type;
    Check(ModelDb.GetEntry(type).StartsWith("PHIGROS_JIU_") == (type.Assembly == main), "model prefix ownership " + type.FullName);
}
Check(mainTypes.Length > 0 && peerTypes.Length > 0, "nonempty model sets");
Check(!mainTypes.Select(ModelDb.GetId).Intersect(peerTypes.Select(ModelDb.GetId)).Any(), "disjoint model IDs");

var vanilla = typeof(ModelDb).Assembly;
string[] names = { "Ironclad", "Silent", "Regent", "Necrobinder", "Defect" };
var vanillaCharacters = names.Select(n => vanilla.GetType("MegaCrit.Sts2.Core.Models.Characters." + n)!).ToArray();
Check(ModelDb.GetEntry(vanillaCharacters[0]) == "IRONCLAD", "vanilla identity unchanged");
var initializedTypes = mainTypes.Concat(peerTypes).Concat(vanillaCharacters).ToArray();
AccessTools.Field(typeof(ModelDb), "_allAbstractModelSubtypes").SetValue(null, initializedTypes);
ModelDb.Init(initializedTypes);
var characters = ModelDb.AllCharacters.ToArray();
Check(characters.Count(c => c.GetType().Assembly == main) == 1, "one main character");
Check(characters.Count(c => c.GetType().Assembly == peer) == 1, "one peer character");
Check(characters.Length == 7, "five vanilla plus two mod characters");

foreach (var assembly in new[] { main, peer })
{
    var types = Models(assembly);
    var characterType = types.Single(t => t.IsSubclassOf(typeof(CharacterModel)));
    var character = ModelDb.GetById<CharacterModel>(ModelDb.GetId(characterType));
    Check(character.IsPlayable && character.StartingHp > 0 && character.MaxEnergy > 0, "playable adapter " + assembly.GetName().Name);
    var deck = character.StartingDeck.ToArray();
    Check(deck.Length > 0, "nonempty starter deck");
    Check(deck.All(c => c.GetType().Assembly == assembly), "starter deck isolated");
    Check(character.StartingRelics.All(r => r.GetType().Assembly == assembly), "starter relic isolated");
    var cards = character.CardPool.AllCards.ToArray();
    Check(cards.Length == types.Count(t => t.IsSubclassOf(typeof(CardModel))), "all declared cards belong to the pool");
    Check(cards.All(c => c.GetType().Assembly == assembly && c.Pool == character.CardPool), "card pool isolated");
    var mutable = cards.Select(c => c.ToMutable()).ToArray();
    Check(mutable.All(c => c.IsMutable && c.Id == ModelDb.GetId(c.GetType())), "card clones preserve identity");
    foreach (var card in cards)
    {
        string resource = card.PortraitPath;
        string expected = assembly == main ? "res://PhigrosJiu/" : "res://jiu_2/";
        Check(resource.StartsWith(expected, StringComparison.Ordinal), "portrait namespace " + card.Id);
        Check(card.DynamicVars != null, "dynamic vars instantiate " + card.Id);
    }
    if (assembly == main)
    {
        var expected = new Dictionary<string,int> { ["Basic"] = 4, ["Common"] = 25, ["Uncommon"] = 29, ["Rare"] = 25, ["Ancient"] = 2 };
        foreach (var (rarity, count) in expected) Check(cards.Count(c => c.Rarity.ToString() == rarity) == count, "sample rarity count " + rarity);
        foreach (var copy in mutable)
        {
            copy.UpgradeInternal(); copy.FinalizeUpgradeInternal();
            Check(copy.CurrentUpgradeLevel == 1, "upgrade works " + copy.Id);
            Check(copy.DynamicVars.All(pair => pair.Value.BaseValue >= 0), "nonnegative upgraded variables " + copy.Id);
        }
    }
    foreach (var relic in character.StartingRelics)
        Check(relic.PackedIconPath.StartsWith(assembly == main ? "res://PhigrosJiu/" : "res://jiu_2/"), "relic icon namespace");
    foreach (var powerType in types.Where(t => t.IsSubclassOf(typeof(PowerModel))))
    {
        var power = ModelDb.GetById<PowerModel>(ModelDb.GetId(powerType));
        string icon = (string)AccessTools.Property(typeof(PowerModel), "PackedIconPath").GetValue(power)!;
        Check(icon.StartsWith(assembly == main ? "res://PhigrosJiu/" : "res://jiu_2/"), "power icon namespace");
    }
}
// IDs must survive both save-facing lookups and model discovery without crossing assemblies.
foreach (var type in mainTypes.Concat(peerTypes))
    Check(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)).GetType() == type, "ID round-trip " + type.FullName);
Console.WriteLine(JsonSerializer.Serialize(new { result = "PASS", order = reverse ? "peer-first" : "main-first", checks, models = ids.Count, characters = characters.Select(c => c.Id.Entry).ToArray() }));
