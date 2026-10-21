using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;

namespace PhigrosJiu;

/// <summary>
/// 鸠 (Geopelia) — a playable character built on the Phigros illustration archive.
/// The class name decides the model id ("GEOPELIA") and therefore every vanilla asset path;
/// the few paths that need to differ are redirected in <see cref="GeopeliaCharacterPatches"/>.
/// </summary>
public sealed class Geopelia : CharacterModel
{
    public override Color NameColor => new("e8c47a");

    public override Color MapDrawingColor => new("e8c47a");

    public override CharacterGender Gender => CharacterGender.Feminine;

    public override int StartingHp => 70;

    public override int StartingGold => 99;

    public override int MaxEnergy => 3;

    public override CardPoolModel CardPool => ModelDb.CardPool<GeopeliaCardPool>();

    public override RelicPoolModel RelicPool => ModelDb.RelicPool<GeopeliaRelicPool>();

    // Reuse the Ironclad potion pool: the character does not ship potions of its own yet.
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<IroncladPotionPool>();

    protected override CharacterModel? UnlocksAfterRunAs => null;

    protected override string CharacterSelectIconPath => Content.CharacterIcon;

    protected override string CharacterSelectLockedIconPath => Content.CharacterIcon;

    protected override string IconPath => SceneHelper.GetScenePath("ui/character_icons/silent_icon");

    protected override string MapMarkerPath => ImageHelper.GetImagePath("packed/map/icons/map_marker_silent.png");

    public override IEnumerable<CardModel> StartingDeck => new CardModel[]
    {
        ModelDb.Card<GeopeliaStrike>(),
        ModelDb.Card<GeopeliaStrike>(),
        ModelDb.Card<GeopeliaStrike>(),
        ModelDb.Card<GeopeliaStrike>(),
        ModelDb.Card<GeopeliaDefend>(),
        ModelDb.Card<GeopeliaDefend>(),
        ModelDb.Card<GeopeliaDefend>(),
        ModelDb.Card<GeopeliaDefend>(),
        ModelDb.Card<GeopeliaCounterbeat>(),
        ModelDb.Card<GeopeliaOpeningBeat>(),
    };

    public override IReadOnlyList<RelicModel> StartingRelics => new[]
    {
        ModelDb.Relic<GeopeliaJudgmentLine>(),
    };

    public override float AttackAnimDelay => 0.15f;

    public override float CastAnimDelay => 0.25f;

    public override List<string> GetArchitectAttackVfx() => new()
    {
        "vfx/vfx_attack_slash",
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_bloody_impact",
        "vfx/vfx_rock_shatter",
    };
}
