using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace PhigrosJiu;

public sealed class EffectStep
{
    public string Kind { get; set; } = "";
    public int Value { get; set; }
    public int Upgrade { get; set; }
    public int X { get; set; }
    public int? Ux { get; set; }
    public int Cap { get; set; }
    public int Pay { get; set; }
    public string Condition { get; set; } = "";
    public int Threshold { get; set; }
    public string Power { get; set; } = "";
}

public sealed class CardDefinition
{
    public string ClassName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Entry { get; set; } = "";
    public CardRarity Rarity { get; set; }
    public CardType Type { get; set; }
    public int Cost { get; set; }
    public int UpgradeCost { get; set; }
    public int Replays { get; set; }
    public int SignalReplayCost { get; set; }
    public int PlayedReplayThreshold { get; set; }
    public string Portrait { get; set; } = "";
    public string SongId { get; set; } = "";
    public decimal ChartConstant { get; set; }
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public string[] UpgradeKeywords { get; set; } = Array.Empty<string>();
    public EffectStep[] Steps { get; set; } = Array.Empty<EffectStep>();
    public string[] Themes { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = "";
    public bool HasReplayAction => Steps.Any(s => s.Kind is "Replay" or "EchoSelf");
    public TargetType Target => Steps.Any(s => s.Kind is "Attack" or "AttackBonus" or "SignalAttack" or "Weak")
        ? TargetType.AnyEnemy : Steps.Any(s => s.Kind is "AllAttack" or "AllWeak" or "AllVulnerable") ? TargetType.AllEnemies : TargetType.Self;
}

public static class Design
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    public static readonly IReadOnlyList<CardDefinition> Cards = JsonSerializer.Deserialize<List<CardDefinition>>(DesignJson.Value, Options)!;
    private static readonly Dictionary<string, CardDefinition> ByName = Cards.ToDictionary(c => c.ClassName);
    public static CardDefinition Get(string name) => ByName[name];
}
