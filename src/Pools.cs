using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace PhigrosJiu;

/// <summary>Geopelia card.</summary>
public sealed class GeopeliaCardPool : CardPoolModel
{
    public override string Title => "phigrosjiu";

    public override string EnergyColorName => "silent";

    public override string CardFrameMaterialPath => "card_frame_green";

    public override Color DeckEntryCardColor => new("4de0a5");

    public override Color EnergyOutlineColor => new("123a2a");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() => Design.Cards.Select(c =>
        ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(Entry).Assembly.GetType("PhigrosJiu." + c.ClassName)!))).ToArray();

}

/// <summary>Geopelia card.</summary>
public sealed class GeopeliaRelicPool : RelicPoolModel
{
    public override string EnergyColorName => "silent";

    public override Color LabOutlineColor => new("4de0a5");

    protected override IEnumerable<RelicModel> GenerateAllRelics() => new RelicModel[]
    {
        ModelDb.Relic<GeopeliaJudgmentLine>(),
        ModelDb.Relic<GeopeliaScoreFragment>(),
        ModelDb.Relic<GeopeliaStardustBadge>(),
        ModelDb.Relic<GeopeliaPerfectFrame>(),
    };
}
