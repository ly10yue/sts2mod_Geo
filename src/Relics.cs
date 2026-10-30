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
using MegaCrit.Sts2.Core.Saves.Runs;

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
    [SavedProperty] public bool Tracking { get; set; }
    private int _plays;
    [SavedProperty] public int Plays { get => _plays; set { _plays=value; Status=value%3==2?RelicStatus.Active:RelicStatus.Normal; InvokeDisplayAmountChanged(); } }
    public override bool ShowCounter => true;
    public override int DisplayAmount => Plays%3;
    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    { Tracking=false; Plays=0; return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState state)
    { if(side==CombatSide.Player && participants.Contains(Owner.Creature)) Plays=0; return Task.CompletedTask; }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if(play.Player!=Owner)return;
        if(!Tracking)
        {
            var legacy=Owner.Creature.GetPower<GeopeliaRhythmPower>();
            if(legacy is null)return;
            Plays=legacy.Plays; Tracking=true;
        }
        Plays++;
        if(Plays%3!=0)return;
        Flash();
        await Runtime.AddSignal(context,Owner,1);
    }


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
        Tracking=true;
        Plays=0; // The relic owns its trigger and display; no proxy power is applied.
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
    private bool _used;
    [SavedProperty] public bool Used { get=>_used; set { _used=value; InvokeDisplayAmountChanged(); } }
    public override bool ShowCounter=>true;
    public override int DisplayAmount=>Used?0:1;
    public override Task BeforeCombatStart(){ Used=false; return Task.CompletedTask; }
    public override Task AfterModifyingHandDraw(){ Used=true; Flash(); return Task.CompletedTask; }


    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner || Used) return count;
        if (Owner.PlayerCombatState == null) return count;
        if (Owner.PlayerCombatState.TurnNumber > 1) return count;
        return count + 1;
    }
}

/// <summary>Geopelia card.</summary>
public sealed class GeopeliaStardustBadge : GeopeliaRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter=>true;
    public override int DisplayAmount=>Owner is null?0:Math.Min(4,Runtime.Signal(Owner));
    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? source)
    { if(power.Owner==Owner.Creature && power is GeopeliaSignalPower) { Status=Runtime.Signal(Owner)>=4?RelicStatus.Active:RelicStatus.Normal; InvokeDisplayAmountChanged(); } return Task.CompletedTask; }


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

    public override Task AfterModifyingDamageAmount(CardModel? cardSource)
    { if(cardSource?.Owner==Owner)Flash(); return Task.CompletedTask; }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner.Creature) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return DynamicVars["Damage"].BaseValue;
    }
}
