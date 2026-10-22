using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace PhigrosJiu;

public abstract class GeopeliaCard : CardModel
{
    public CardDefinition Definition { get; }
    protected GeopeliaCard(CardDefinition definition) : base(definition.Cost, definition.Type, definition.Rarity, definition.Target)
        => Definition = definition;
    protected override bool HasEnergyCostX => Definition.CostsX;
    public string ArtFile => Definition.Portrait;
    public override string PortraitPath => Content.CardArt(ArtFile);
    protected override string PortraitPngPath => PortraitPath;
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<string> AllPortraitPaths => new[] { PortraitPath };
    public override CardPoolModel Pool => ModelDb.CardPool<GeopeliaCardPool>();
    public override bool GainsBlock => Definition.Steps.Any(s => s.Kind is "Block" or "BlockBonus" or "SignalBlock");
    public override IEnumerable<CardKeyword> CanonicalKeywords => Definition.Keywords.Select(Enum.Parse<CardKeyword>);
    protected override HashSet<CardTag> CanonicalTags => GetType() == typeof(GeopeliaStrike) ? new() { CardTag.Strike }
        : GetType() == typeof(GeopeliaDefend) ? new() { CardTag.Defend } : new();
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            for (int i = 0; i < Definition.Steps.Length; i++)
            {
                var s = Definition.Steps[i];
                yield return s.Kind is "Attack" or "AttackBonus" or "AllAttack" or "SignalAttack" or "DebtAttack" or "FatalRewardAttack"
                    ? new DamageVar("V" + i, s.Value, ValueProp.Move)
                    : s.Kind is "Block" or "BlockBonus" or "SignalBlock" ? new BlockVar("V" + i, s.Value, ValueProp.Move)
                    : new DynamicVar("V" + i, s.Value);
                yield return new DynamicVar("X" + i, s.X);
            }
        }
    }
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if (Definition.Replays == 0 && (Definition.SignalReplayCost > 0 || Definition.PlayedReplayThreshold > 0))
                yield return HoverTipFactory.Static(StaticHoverTip.ReplayDynamic, new DynamicVar("Times", 1));
            if (Definition.Themes.Contains("信号")) yield return HoverTipFactory.FromPower<GeopeliaSignalPower>();
            if (Definition.Themes.Contains("透支")) yield return HoverTipFactory.FromPower<GeopeliaEnergyDebtPower>();
            if (Definition.Steps.Any(s => s.Kind is "Strength" or "SignalStrength")) yield return HoverTipFactory.FromPower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>();
            if (Definition.Steps.Any(s => s.Kind == "Dexterity")) yield return HoverTipFactory.FromPower<MegaCrit.Sts2.Core.Models.Powers.DexterityPower>();
            if (Definition.Steps.Any(s => s.Kind == "EndlessSignal")) yield return ModelDb.Enchantment<GeopeliaEndlessSignal>().HoverTip;
            if (Definition.Steps.Any(s => s.Kind == "FatalRewardAttack")) yield return HoverTipFactory.Static(StaticHoverTip.Fatal);
            if (Definition.Steps.Any(s => s.Kind == "ExhaustDrawReplay"))
            {
                yield return HoverTipFactory.FromKeyword(CardKeyword.Exhaust);
                yield return HoverTipFactory.Static(StaticHoverTip.ReplayDynamic, new DynamicVar("Times", 1));
            }
        }
    }
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (!ReferenceEquals(card, this)) return playCount;
        // Fixed repeats are included by the native replay-count integration below.
        int extra = 0;
        if (Definition.SignalReplayCost > 0 && Runtime.Signal(Owner) >= Definition.SignalReplayCost) extra++;
        if (Definition.PlayedReplayThreshold > 0 && Runtime.Played(Owner) >= Definition.PlayedReplayThreshold) extra++;
        return playCount + extra;
    }
    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (card == this && Definition.SignalReplayCost > 0 && Runtime.Signal(Owner) >= Definition.SignalReplayCost)
            await Runtime.SpendSignal(new ThrowingPlayerChoiceContext(), Owner, Definition.SignalReplayCost, this);
    }
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(Definition.UpgradeCost - Definition.Cost);
        foreach (var keyword in Definition.UpgradeKeywords) AddKeyword(Enum.Parse<CardKeyword>(keyword));
        for (int i = 0; i < Definition.Steps.Length; i++)
        {
            var step = Definition.Steps[i];
            if (step.Upgrade != step.Value) DynamicVars["V" + i].UpgradeValueBy(step.Upgrade - step.Value);
            if ((step.Ux ?? step.X) != step.X) DynamicVars["X" + i].UpgradeValueBy((step.Ux ?? step.X) - step.X);
        }
    }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool incurredDebt = false;
        for (int i = 0; i < Definition.Steps.Length; i++)
        {
            var step = Definition.Steps[i];
            int n = DynamicVars["V" + i].IntValue, x = DynamicVars["X" + i].IntValue;
            bool bonus = step.Kind is "AttackBonus" or "BlockBonus";
            bool signalBonus = step.Kind is "SignalAttack" or "SignalBlock";
            if (n <= 0 && (!(bonus || signalBonus) || x <= 0)) continue;
            bool condition = Runtime.Condition(step, Owner, play);
            if (!bonus && !condition) continue;
            if (bonus)
            {
                if (x > 0 && condition && (step.Pay == 0 || await Runtime.SpendSignal(context, Owner, step.Pay, this))) n += x;
            }
            else if (step.Pay > 0 && !await Runtime.SpendSignal(context, Owner, step.Pay, this)) continue;
            if (n <= 0 && !signalBonus) continue;
            switch (step.Kind)
            {
                case "HandSignalAttack":
                    await ConsumeHandAttack(context, play, ResolveEnergyXValue()); break;
                case "Attack": case "AttackBonus": await Attack(context, play, n); break;
                case "AllAttack": await Attack(context, play, n, true); break;
                case "ExhaustDrawReplay":
                    var selected = (await CardSelectCmd.FromHand(context, Owner,
                        new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1), c => c != this, this)).FirstOrDefault();
                    bool replay = false;
                    if (selected is not null)
                    {
                        bool eligible = selected.Type is CardType.Status or CardType.Curse;
                        var result = await CardCmd.Exhaust(context, selected);
                        replay = eligible && result is { success: true };
                    }
                    await CardPileCmd.Draw(context, n, Owner);
                    if (replay && !CombatManager.Instance.IsOverOrEnding)
                        await CardCmd.AutoPlay(context, this, null, skipCardPileVisuals: true);
                    break;
                case "SignalExecute":
                    int signals = Runtime.Signal(Owner);
                    if (play.Target is { IsAlive: true } victim && victim.CurrentHp < (long)signals * n
                        && await Runtime.SpendSignal(context, Owner, signals, this) && victim.IsAlive)
                        await CreatureCmd.Kill(victim);
                    break;
                case "FatalRewardAttack":
                    if (play.Target is { IsAlive: true } fatalTarget)
                    {
                        var battle = CombatState!;
                        bool fatalAllowed = fatalTarget.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());
                        var result = await DamageCmd.Attack(n).FromCard(this, play).Targeting(fatalTarget)
                            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
                        if (fatalAllowed && result.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
                            FatalCardRewards.Record(battle, Owner);
                    }
                    break;
                case "DebtAttack":
                    int hits = Runtime.DebtCardsPlayed(Owner);
                    if (hits > 0 && play.Target is { IsAlive: true } debtTarget)
                        await DamageCmd.Attack(n).WithHitCount(hits).FromCard(this, play).Targeting(debtTarget)
                            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
                    break;
                case "Block": case "BlockBonus": await Block(play, n); break;
                case "Draw": await CardPileCmd.Draw(context, n, Owner); break;
                case "RareNonPower":
                    var rarePool = Pool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
                        .Where(c => c.Rarity == CardRarity.Rare && c.Type != CardType.Power);
                    var rareCards = MegaCrit.Sts2.Core.Factories.CardFactory.GetDistinctForCombat(Owner, rarePool, n, Owner.RunState.Rng.CombatCardGeneration);
                    await CardPileCmd.AddGeneratedCardsToCombat(rareCards, PileType.Hand, Owner);
                    break;
                case "EndlessSignal":
                    await Runtime.ApplyPower(context, Owner, typeof(GeopeliaEndlessSignalPower), n, this);
                    break;
                case "Energy": await PlayerCmd.GainEnergy(n, Owner); break;
                case "Signal": await Runtime.AddSignal(context, Owner, n, this); break;
                case "Strength": await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(context,Owner.Creature,n,Owner.Creature,this); break;
                case "Dexterity": await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.DexterityPower>(context,Owner.Creature,n,Owner.Creature,this); break;
                case "SignalStrength":
                    int allSignal = Runtime.Signal(Owner);
                    if (allSignal > 0 && await Runtime.SpendSignal(context,Owner,allSignal,this))
                        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(context,Owner.Creature,allSignal,Owner.Creature,this);
                    break;
                case "SignalRare":
                    int spent = Math.Min(step.Cap, Runtime.Signal(Owner));
                    if (spent > 0) await Runtime.SpendSignal(context, Owner, spent, this);
                    var rares = Pool.AllCards.Where(c => c.Rarity == CardRarity.Rare).ToArray();
                    for (int j = 0; j < spent / n && !CombatManager.Instance.IsOverOrEnding; j++)
                    {
                        var chosen = Owner.RunState.Rng.CombatCardGeneration.NextItem(rares);
                        var generated = CombatState!.CreateCard(chosen!, Owner);
                        await CardPileCmd.Add(generated, PileType.Hand, CardPilePosition.Bottom, this);
                    }
                    break;
                case "SignalAttack": case "SignalBlock":
                    int consumed = x > 0 ? Math.Min(step.Cap, Runtime.Signal(Owner)) : 0;
                    if (consumed > 0) await Runtime.SpendSignal(context, Owner, consumed, this);
                    if (step.Kind == "SignalAttack") await Attack(context, play, n + consumed * x);
                    else await Block(play, n + consumed * x);
                    break;
                case "Weak":
                    if (play.Target is { IsAlive: true } t) await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.WeakPower>(context, t, n, Owner.Creature, this);
                    break;
                case "AllWeak": case "AllVulnerable":
                    foreach (var target in CombatState!.HittableEnemies.ToArray())
                        if (step.Kind == "AllWeak") await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.WeakPower>(context, target, n, Owner.Creature, this);
                        else await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>(context, target, n, Owner.Creature, this);
                    break;
                case "EnergyDebt": case "DrawDebt": case "SignalDebt": case "DamageDebt": case "BlockDebt":
                    await Runtime.ApplyPower(context, Owner, Runtime.DebtType(step.Kind), n, this); incurredDebt = true; break;
                case "RepayEnergy": case "RepayDraw":
                    PowerModel? debt = step.Kind == "RepayDraw"
                        ? Owner.Creature.GetPower<GeopeliaDrawDebtPower>()
                        : Owner.Creature.GetPower<GeopeliaEnergyDebtPower>();
                    if (debt is not null)
                    {
                        if (n == 99 || n >= debt.Amount) await PowerCmd.Remove(debt);
                        else await PowerCmd.ModifyAmount(context, debt, -n, Owner.Creature, this);
                    }
                    break;
                case "Power": await Runtime.ApplyPower(context, Owner, Runtime.PowerType(step.Power), n, this); break;
                case "EchoSelf":
                    if (!play.IsAutoPlay)
                    {
                        await PowerCmd.Apply<GeopeliaTapeLoopPower>(context, Owner.Creature, n, Owner.Creature, this);
                        Owner.Creature.GetPower<GeopeliaTapeLoopPower>()!.SourceUpgrade = CurrentUpgradeLevel;
                    }
                    break;
                case "Replay":
                    if (!play.IsAutoPlay)
                        foreach (var previous in Runtime.ReplayableHistory(Owner, this).TakeLast(n).ToArray()) await Runtime.Replay(context, previous, play.Target);
                    break;
                case "Ancient":
                    var choices = Pool.AllCards.Where(c => c.Rarity == CardRarity.Ancient).ToArray();
                    for (int j = 0; j < n; j++)
                    {
                        var chosen = Owner.RunState.Rng.CombatCardGeneration.NextItem(choices);
                        var generated = CombatState!.CreateCard(chosen!, Owner);
                        await CardPileCmd.Add(generated, PileType.Hand, CardPilePosition.Bottom, this);
                    }
                    break;
                default: throw new InvalidOperationException("Unknown card operation " + step.Kind);
            }
        }
        if (incurredDebt && Owner.Creature.GetPower<GeopeliaOverdriveGuardPower>() is { } guard)
            await CreatureCmd.GainBlock(Owner.Creature, guard.Amount, ValueProp.Unpowered, null);
        if (incurredDebt && Owner.Creature.GetPower<GeopeliaDebtDrawPower>() is { } draw)
            await draw.IncurredDebt(context);
    }
    private Task Block(CardPlay play, int value) => CreatureCmd.GainBlock(Owner.Creature, value, ValueProp.Move, play);
    private async Task ConsumeHandAttack(PlayerChoiceContext context, CardPlay play, int energy)
    {
        // Snapshot just as Fiend Fire does: cards created by exhaust/signal hooks are not recursively consumed.
        var hand = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this).ToArray();
        int signal = Runtime.Signal(Owner), exhausted = 0;
        if (signal > 0) await Runtime.SpendSignal(context, Owner, signal, this);
        foreach (var card in hand)
        {
            if (CombatManager.Instance.IsOverOrEnding) return;
            if (card.Pile?.Type == PileType.Hand && await CardCmd.Exhaust(context, card) is { success: true }) exhausted++;
        }
        decimal damage = (decimal)energy * signal * exhausted;
        if (damage > 0 && !CombatManager.Instance.IsOverOrEnding)
            await DamageCmd.Attack(damage).FromCard(this, play).TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }
    private async Task Attack(PlayerChoiceContext context, CardPlay play, int value, bool all = false)
    {
        var attack = DamageCmd.Attack(value).FromCard(this, play).WithHitFx("vfx/vfx_attack_slash");
        if (all) attack.TargetingAllOpponents(CombatState!);
        else
        {
            var target = play.Target is { IsAlive: true } ? play.Target : CombatState?.HittableEnemies.FirstOrDefault();
            if (target is null) return;
            attack.Targeting(target);
        }
        await attack.Execute(context);
    }
}
