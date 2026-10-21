using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace PhigrosJiu;

/// <summary>All Geopelia relics read their icons from the mod folder.</summary>
public abstract class GeopeliaRelic : RelicModel
{
    public override string PackedIconPath => Content.RelicIcon(Id.Entry.ToLowerInvariant());

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;
}

/// <summary>Geopelia card.</summary>
public sealed class GeopeliaJudgmentLine : GeopeliaRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<GeopeliaSignalPower>(1m),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.FromPower<GeopeliaSignalPower>(),
    };

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<GeopeliaRhythmPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1, Owner.Creature, null);
        await PowerCmd.Apply<GeopeliaSignalPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            DynamicVars["GeopeliaSignalPower"].BaseValue,
            Owner.Creature,
            null);
    }
}

/// <summary>Geopelia card.</summary>
public sealed class GeopeliaScoreFragment : GeopeliaRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner) return count;
        if (Owner.PlayerCombatState == null) return count;
        if (Owner.PlayerCombatState.TurnNumber > 1) return count;
        return count + 1;
    }
}

/// <summary>Geopelia card.</summary>
public sealed class GeopeliaStardustBadge : GeopeliaRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(4m, ValueProp.Unpowered),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[]
    {
        HoverTipFactory.Static(StaticHoverTip.Block),
    };

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player) return;
        if (!participants.Contains(Owner.Creature)) return;
        if (Owner.Creature.GetPowerAmount<GeopeliaSignalPower>() < 4) return;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
    }
}

/// <summary>Geopelia card.</summary>
public sealed class GeopeliaPerfectFrame : GeopeliaRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("Damage", 2m),
    };

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner.Creature) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return DynamicVars["Damage"].BaseValue;
    }
}
