#if JIU_VALIDATION
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters.Mocks;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;

namespace PhigrosJiu;

// Compiled into a separate validation DLL only; never shipped in the installed mod.
[HarmonyPatch(typeof(NGame), "LaunchMainMenu")]
internal static class ValidationStartup
{
    private static bool _started;
    private static void Postfix(Task __result)
    {
        if (_started || !CommandLineHelper.HasArg("jiu-validation")) return;
        _started = true;
        _ = GameValidation.Run(__result);
    }
}

// The native tutorial expects a save-backed interactive room; the combat commands below do not.
[HarmonyPatch(typeof(CardPileCmd), "ShuffleFtueCheck")]
internal static class ValidationSkipShuffleTutorial
{
    private static bool Prefix(ref Task __result)
    {
        if (!CommandLineHelper.HasArg("jiu-validation")) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

internal static partial class GameValidation
{
    private static readonly List<object> Rows = new();
    private static readonly List<object> Benchmarks = new();
    private static readonly List<EconomyRow> Economy = new();
    private sealed record EconomyRow(string Model, string Name, bool Upgraded, int ImmediateEnergy, int Drawn,
        int EnergyDebt, int DrawDebt, int NetEnergy, int NetDraw);
    private static string _current = "startup";
    private static int _checks;
    internal static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new InvalidOperationException(_current + ": " + message);
    }
    internal static async Task Run(Task menu)
    {
        string output = CommandLineHelper.GetValue("jiu-report") ?? "jiu-validation.json";
        try
        {
            await menu;
            await NGame.Instance!.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
            await CardLibraryCases();
            TestMode.IsOn = true;
            await ProgressionCases();
            await VisualCases();
            if (CommandLineHelper.HasArg("jiu-editor-validation"))
            {
                await EditorFeatureCases();
                await SongReworkCases();
                await SignalReplayReworkCases();
            }
            else
            {
            foreach (var definition in Design.Cards)
            foreach (bool upgraded in new[] { false, true })
            {
                _current = definition.ClassName + (upgraded ? "+" : "");
                var f = NewFixture("JIU-" + definition.ClassName);
                await Runtime.AddSignal(f.Context, f.Player, 6);
                for (int i = 0; i < 30; i++) await CardPileCmd.Add(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(), f.Player), PileType.Draw, skipVisuals: true);
                var type = typeof(Entry).Assembly.GetType("PhigrosJiu." + definition.ClassName)!;
                var card = f.State.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)), f.Player);
                if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
                Check(card.Portrait is AtlasTexture, "native portrait is an atlas view");
                var portrait = (AtlasTexture)card.Portrait;
                Check(portrait.Atlas.ResourcePath == Content.Root + "cards/originals/" + definition.SongId + ".png", "native portrait matches exact song");
                string description = card.GetDescriptionForPile(PileType.Hand);
                Check(!string.IsNullOrWhiteSpace(description) && !description.Contains("{V") && !description.Contains("{X"), "native localized dynamic description");
                Check(card.Title.Contains(definition.Name), "native localized title");
                await f.Play(card);
                Check(Runtime.Signal(f.Player) >= 0 && Runtime.Signal(f.Player) <= Runtime.SignalLimit(f.Player), "signal cap");
                Check(f.Player.Creature.CurrentHp == 70, "self-targeting or unexpected health loss");
                int executions = Runtime.History(f.Player).Count(e => e.CardPlay.Card == card);
                int expected = 1 + definition.Replays + (definition.SignalReplayCost > 0 ? 1 : 0);
                Check(executions == expected, "native execution count " + executions + " expected " + expected);
                int debtBefore = Runtime.Debt(f.Player);
                Rows.Add(new { card = definition.Name, model = definition.Entry, upgraded, damage = 9999 - f.Enemy.CurrentHp,
                    block = f.Player.Creature.Block, signal = Runtime.Signal(f.Player), drawn = f.TotalCardsDrawn, energyGained = f.TotalEnergyGained, debt = debtBefore, executions });
                // Force the next-turn hooks to ensure pending debt is paid, then removed.
                f.Player.PlayerCombatState!.Energy = 3;
                foreach (var debt in f.Player.Creature.Powers.OfType<GeopeliaDebtPower>().ToArray())
                {
                    debt.AmountOnTurnStart = debt.Amount;
                    await debt.AfterEnergyReset(f.Player);
                    await debt.AfterSideTurnStart(CombatSide.Player, new[] { f.Player.Creature }, f.State);
                }
                Check(!f.Player.Creature.Powers.Any(p => p is GeopeliaDebtPower && p is not GeopeliaTurnPenaltyPower), "pending debts clear next turn");
                RunManager.Instance.CleanUp(false);
            }
            await ReplayCases();
            await MechanicCases();
            await EconomyCases();
            await ControlledCombats();
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new { result = "PASS", checks = _checks, cases = Rows.Count, rows = Rows, benchmarks = Benchmarks, economy = Economy }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print("JIU_NATIVE_VALIDATION=PASS cases=" + Rows.Count + " checks=" + _checks);
            NGame.Instance.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(output, JsonSerializer.Serialize(new { result = "FAIL", current = _current, completed = Rows.Count, error = error.ToString(), rows = Rows }, new JsonSerializerOptions { WriteIndented = true }));
            GD.PrintErr("JIU_NATIVE_VALIDATION=FAIL " + _current + " " + error);
            NGame.Instance!.GetTree().Quit(1);
        }
    }
    private static async Task VisualCases()
    {
        _current = "peer-model-at-85-percent";
        Check((string)AccessTools.Property(typeof(CharacterModel), "VisualsPath").GetValue(ModelDb.Character<Geopelia>())! == Content.CharacterVisuals, "independent main character visuals");
        var scene = ResourceLoader.Load<PackedScene>(Content.CharacterVisuals);
        var node = scene.Instantiate<NCreatureVisuals>();
        NGame.Instance!.AddChild(node);
        await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        var body = node.GetNode<Sprite2D>("Visuals");
        Check(body.Texture is not null, "peer character texture loaded");
        Check(body.Scale.IsEqualApprox(new Vector2(0.85f, 0.85f)), "body scaled to 85 percent");
        Check(node.Bounds.Size.IsEqualApprox(new Vector2(425, 340)), "hit area scales with model");
        node.SetScaleAndHue(1f, 0f);
        Check(body.Scale.IsEqualApprox(new Vector2(0.85f, 0.85f)), "native scale reset preserves smaller body");
        Check(body.GetNode<AnimationPlayer>("Idle").IsPlaying(), "peer idle animation active");
        node.QueueFree();
    }
    private static async Task ReplayCases()
    {
        _current = "replay-target-and-payment";
        var f = NewFixture("JIU-REPLAY");
        var strike = f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(), f.Player);
        await f.Play(strike);
        var replay = f.State.CreateCard(ModelDb.Card<GeopeliaReplayBuffer>(), f.Player);
        await f.Play(replay);
        Check(f.Enemy.CurrentHp == 9999 - 14, "replay deals original damage to enemy");
        Check(f.Player.Creature.CurrentHp == 70, "replay never attacks its caster");
        Check(Runtime.History(f.Player).Count(e => e.CardPlay.IsAutoPlay) == 1, "one automatic replay");
        Check(f.Player.PlayerCombatState!.Energy == 98, "replay pays only the manual strike, buffer costs zero");
        RunManager.Instance.CleanUp(false);

        _current = "echo-does-not-recurse";
        f = NewFixture("JIU-TAPE");
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaRehearsal>(), f.Player));
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(), f.Player));
        Check(Runtime.History(f.Player).Count(e => e.CardPlay.IsAutoPlay) == 1, "one tape replay, no recursion");
        Check(f.Player.Creature.GetPowerAmount<GeopeliaTapeLoopPower>() == 1, "tape charge consumed once");
        RunManager.Instance.CleanUp(false);

        _current = "shared-signal-budget";
        f = NewFixture("JIU-SIGNAL");
        await Runtime.AddSignal(f.Context, f.Player, 5);
        await Runtime.ApplyPower(f.Context, f.Player, typeof(GeopeliaSignalEchoPower), 2, null);
        await Runtime.ApplyPower(f.Context, f.Player, typeof(GeopeliaWorldEchoPower), 2, null);
        await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaRetribution>(), f.Player));
        Check(Runtime.History(f.Player).Length == 3, "native plus card repeat plus one affordable power repeat");
        Check(Runtime.Signal(f.Player) == 0, "all signal paid exactly once");
        RunManager.Instance.CleanUp(false);
    }

    private static async Task MechanicCases()
    {
        _current = "signal-boundaries-and-native-strength";
        foreach (int signal in new[] { 0, 1, 2, 3, 12 })
        {
            var f = NewFixture("BOUNDARY" + signal);
            await Runtime.AddSignal(f.Context, f.Player, signal);
            await f.Play(f.State.CreateCard(ModelDb.Card<GeopeliaCounterbeat>(), f.Player));
            Check(9999 - f.Enemy.CurrentHp == 8 + 2 * Math.Min(signal, 2), "signal spending on each repeat");
            Check(Runtime.Signal(f.Player) == Math.Max(0, signal - 2), "partial budget never goes negative");
            RunManager.Instance.CleanUp(false);
        }
        var g = NewFixture("STRENGTH");
        await PowerCmd.Apply<StrengthPower>(g.Context, g.Player.Creature, 2, g.Player.Creature, null);
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaEchoNeedle>(), g.Player));
        Check(9999 - g.Enemy.CurrentHp == 12, "native strength applies to all three real plays");
        RunManager.Instance.CleanUp(false);

        _current = "debt-next-turn-and-expiry";
        g = NewFixture("DEBT");
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaEnergyDebtPower), 9, null);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaDrawDebtPower), 9, null);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaDamageDebtPower), 50, null);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaDamageDebtPower), 50, null);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaBlockDebtPower), 50, null);
        Check(g.Player.Creature.GetPowerAmount<GeopeliaDamageDebtPower>() == 50, "percentage debt never stacks");
        await BeginTurn(g, draw: false);
        Check(g.Player.PlayerCombatState!.Energy == 0, "overdraft energy floors at zero");
        Check(Hook.ModifyHandDraw(g.State, g.Player, 5, out _) == 0, "draw debt floors at zero");
        await Hook.AfterSideTurnStart(g.State, CombatSide.Player, new[] { g.Player.Creature });
        Check(Runtime.Debt(g.Player) == 0, "active penalties are no longer unpaid debt");
        g.Player.PlayerCombatState.Energy = 99;
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaStrike>(), g.Player));
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaDefend>(), g.Player));
        Check(9999 - g.Enemy.CurrentHp == 3 && g.Player.Creature.Block == 3, "native rounding for 50 percent damage and block");
        await Hook.AfterSideTurnEnd(g.State, CombatSide.Player, new[] { g.Player.Creature });
        Check(!g.Player.Creature.Powers.Any(p => p is GeopeliaTurnPenaltyPower), "penalties expire after exactly one turn");
        RunManager.Instance.CleanUp(false);

        _current = "trigger-caps-and-turn-reset";
        g = NewFixture("CAPS");
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaEchoGuardPower), 2, null);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaEchoSignalPower), 3, null);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaPulseGuardPower), 3, null);
        for (int i = 0; i < 8; i++) await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaEchoNeedle>(), g.Player));
        Check(g.Player.Creature.Block == 24, "repeat guard and three-card guard have independent caps");
        Check(Runtime.Signal(g.Player) == 3, "repeat signal caps at three");
        await BeginTurn(g, draw: false);
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaEchoNeedle>(), g.Player));
        Check(g.Player.Creature.Block == 7 && Runtime.Signal(g.Player) == 5, "caps reset at next turn");
        RunManager.Instance.CleanUp(false);

        _current = "spend-reward-and-active-repayment";
        g = NewFixture("REPAY");
        for (int i = 0; i < 20; i++) await CardPileCmd.Add(g.State.CreateCard(ModelDb.Card<GeopeliaStrike>(), g.Player), PileType.Draw, skipVisuals: true);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaSignalDrawPower), 1, null);
        await Runtime.AddSignal(g.Context, g.Player, 99);
        Check(Runtime.Signal(g.Player) == 12, "signal hard cap at twelve");
        await Runtime.SpendSignal(g.Context, g.Player, 12, null);
        Check(PileType.Hand.GetPile(g.Player).Cards.Count == 3, "signal draw capped at three triggers even for twelve spent");
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaDebtDrawPower), 1, null);
        await Runtime.ApplyPower(g.Context, g.Player, typeof(GeopeliaEnergyDebtPower), 2, null);
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaRecharge>(), g.Player));
        Check(g.Player.Creature.GetPowerAmount<GeopeliaEnergyDebtPower>() == 1, "partial repayment removes exactly one energy debt");
        Check(g.Player.Creature.GetPower<GeopeliaDebtDrawPower>()!.Used == 0, "repayment grants no deferred benefit");
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaRush>(), g.Player));
        Check(g.Player.Creature.GetPower<GeopeliaDebtDrawPower>()!.Used == 1, "debt generation grants immediate reward");
        int handBefore = PileType.Hand.GetPile(g.Player).Cards.Count;
        await BeginTurn(g, draw: false);
        await Hook.AfterPlayerTurnStart(g.State, g.Context, g.Player);
        Check(PileType.Hand.GetPile(g.Player).Cards.Count == handBefore, "natural repayment and turn start grant no draw");
        RunManager.Instance.CleanUp(false);

        _current = "immediate-signal-and-energy-engines";
        g = NewFixture("IMMEDIATE");
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaBelieveLight>(), g.Player));
        Check(Runtime.Signal(g.Player) == 3, "signal power gives signal on the turn it is played");
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaLowpass>(), g.Player));
        Check(Runtime.Signal(g.Player) == 4, "manual skill immediately gives signal");
        await g.Play(g.State.CreateCard(ModelDb.Card<GeopeliaRainbowSignal>(), g.Player));
        int energyBefore = g.Player.PlayerCombatState!.Energy;
        await Runtime.SpendSignal(g.Context, g.Player, 2, null);
        Check(g.Player.PlayerCombatState.Energy == energyBefore + 1, "signal-spend energy is immediate");
        await Runtime.SpendSignal(g.Context, g.Player, 2, null);
        Check(g.Player.PlayerCombatState.Energy == energyBefore + 1, "signal energy limited to once per turn");
        int signalBefore = Runtime.Signal(g.Player);
        await BeginTurn(g, draw: false);
        await Hook.AfterPlayerTurnStart(g.State, g.Context, g.Player);
        Check(Runtime.Signal(g.Player) == signalBefore && g.Player.PlayerCombatState.Energy == 3, "no turn-start signal or energy rewards");
        RunManager.Instance.CleanUp(false);
    }

    private static async Task EconomyCases()
    {
        CardModel[] cards = { ModelDb.Card<Adrenaline>(), ModelDb.Card<MasterOfStrategy>(), ModelDb.Card<Finesse>(),
            ModelDb.Card<Backflip>(), ModelDb.Card<Bloodletting>(), ModelDb.Card<Offering>(),
            ModelDb.Card<GeopeliaAdvance>(), ModelDb.Card<GeopeliaBorrowedLight>(), ModelDb.Card<GeopeliaMomentarySunrise>(),
            ModelDb.Card<GeopeliaScavenge>(), ModelDb.Card<GeopeliaDanceWithSilence>(), ModelDb.Card<GeopeliaGraceNote>(),
            ModelDb.Card<GeopeliaRebirth>(), ModelDb.Card<GeopeliaQuietCoda>(), ModelDb.Card<GeopeliaRest>(), ModelDb.Card<GeopeliaRefinance>() };
        foreach (var canonical in cards)
        foreach (bool upgraded in new[] { false, true })
        {
            _current = "economy-" + canonical.GetType().Name + (upgraded ? "+" : "");
            var f = NewFixture("ECONOMY");
            for (int i = 0; i < 30; i++) await CardPileCmd.Add(f.State.CreateCard(ModelDb.Card<GeopeliaStrike>(), f.Player), PileType.Draw, skipVisuals: true);
            f.Player.PlayerCombatState!.Energy = 10;
            var card = f.State.CreateCard(canonical, f.Player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await f.Play(card);
            int energy = f.Player.PlayerCombatState.Energy - 10;
            int drawn = PileType.Hand.GetPile(f.Player).Cards.Count;
            int energyDebt = f.Player.Creature.GetPowerAmount<GeopeliaEnergyDebtPower>();
            int drawDebt = f.Player.Creature.GetPowerAmount<GeopeliaDrawDebtPower>();
            await BeginTurn(f, draw: false);
            Check(f.Player.PlayerCombatState.Energy == Math.Max(0, 3 - energyDebt), "actual next-turn energy payment");
            Check(Hook.ModifyHandDraw(f.State, f.Player, 5, out _) == Math.Max(0, 5 - drawDebt), "actual next-turn draw payment");
            Economy.Add(new(canonical.GetType().Name, card.Title, upgraded, energy, drawn, energyDebt, drawDebt,
                energy - energyDebt, drawn - 1 - drawDebt));
            RunManager.Instance.CleanUp(false);
        }
        foreach (bool upgraded in new[] { false, true })
        {
            EconomyRow Get(string name) => Economy.Single(r => r.Model == name && r.Upgraded == upgraded);
            var adrenaline = Get(nameof(Adrenaline));
            var master = Get(nameof(MasterOfStrategy));
            var blood = Get(nameof(Bloodletting));
            Check(Get(nameof(GeopeliaAdvance)).NetEnergy >= blood.NetEnergy, "common energy card pays full debt and matches native energy baseline");
            Check(Get(nameof(GeopeliaBorrowedLight)).NetEnergy >= adrenaline.NetEnergy && Get(nameof(GeopeliaBorrowedLight)).NetDraw >= adrenaline.NetDraw, "uncommon burst beats adrenaline net budget");
            Check(Get(nameof(GeopeliaMomentarySunrise)).NetEnergy > adrenaline.NetEnergy && Get(nameof(GeopeliaMomentarySunrise)).NetDraw >= master.NetDraw, "rare burst matches colorless draw and exceeds adrenaline energy after debt");
            Check(Get(nameof(GeopeliaDanceWithSilence)).NetDraw > master.NetDraw, "draw premium compensates for next-turn block penalty");
            Check(Get(nameof(GeopeliaRebirth)).NetEnergy == Get(nameof(Backflip)).NetEnergy && Get(nameof(GeopeliaRebirth)).NetDraw > Get(nameof(Backflip)).NetDraw, "defensive overdraft does not lose to backflip after debt");
        }
    }

    private static async Task BeginTurn(Fixture f, bool draw)
    {
        f.State.RoundNumber++;
        f.State.CurrentSide = CombatSide.Player;
        f.Player.PlayerCombatState!.IncrementTurnNumber();
        f.Player.Creature.BeforeTurnStart(CombatSide.Player);
        await CreatureCmd.LoseBlock(f.Context, f.Player.Creature, f.Player.Creature.Block, null);
        await Hook.BeforeSideTurnStart(f.State, CombatSide.Player, new[] { f.Player.Creature });
        f.Player.PlayerCombatState.Energy = 3;
        await Hook.AfterEnergyReset(f.State, f.Player);
        if (!draw) return;
        decimal n = Hook.ModifyHandDraw(f.State, f.Player, 5, out _);
        await CardPileCmd.Draw(f.Context, n, f.Player, fromHandDraw: true);
        await Hook.AfterPlayerTurnStart(f.State, f.Context, f.Player);
        await Hook.AfterSideTurnStart(f.State, CombatSide.Player, new[] { f.Player.Creature });
    }

    // Controlled six-turn encounters, using real piles, native effects, three energy and five draw.
    // The simple public scoring policy is shared across decks; these are calibration data, not win-rate claims.
    private static async Task ControlledCombats()
    {
        var decks = new (string name, bool vanilla, CardModel[] additions)[]
        {
            ("Ironclad starter", true, Array.Empty<CardModel>()),
            ("Jiu starter", false, Array.Empty<CardModel>()),
            ("Jiu repeats", false, new CardModel[] { ModelDb.Card<GeopeliaEltaw>(), ModelDb.Card<GeopeliaRehearsal>(), ModelDb.Card<GeopeliaPolyphony>() }),
            ("Jiu signal", false, new CardModel[] { ModelDb.Card<GeopeliaDiamondEyes>(), ModelDb.Card<GeopeliaCarrier>(), ModelDb.Card<GeopeliaSignalBurst>() }),
            ("Jiu overdraft", false, new CardModel[] { ModelDb.Card<GeopeliaRush>(), ModelDb.Card<GeopeliaRebirth>(), ModelDb.Card<GeopeliaRecharge>() }),
            ("Vanilla additions", true, new CardModel[] { ModelDb.Card<TwinStrike>(), ModelDb.Card<Backflip>(), ModelDb.Card<Dash>() })
            ,("Jiu flow", false, new CardModel[] { ModelDb.Card<GeopeliaAdvance>(), ModelDb.Card<GeopeliaSync>(), ModelDb.Card<GeopeliaRebirth>() })
            ,("Jiu rare flow", false, new CardModel[] { ModelDb.Card<GeopeliaMomentarySunrise>(), ModelDb.Card<GeopeliaHappyDisease>(), ModelDb.Card<GeopeliaSustainAmplifier>() })
            ,("Colorless additions", true, new CardModel[] { ModelDb.Card<Finesse>(), ModelDb.Card<Finesse>(), ModelDb.Card<MasterOfStrategy>() })
            ,("Vanilla burst", true, new CardModel[] { ModelDb.Card<Adrenaline>(), ModelDb.Card<Bloodletting>(), ModelDb.Card<Backflip>() })
        };
        foreach (var deck in decks)
        {
            _current = "controlled-combat-" + deck.name;
            decimal damage = 0, blocked = 0, incoming = 0;
            int plays = 0, turns = 0, drawn = 0, energyGained = 0, boundedTurns = 0;
            for (int seed = 0; seed < 16; seed++)
            {
                var f = NewFixture("CALIBRATION-" + seed, deck.vanilla ? ModelDb.Character<Ironclad>() : null);
                var random = new Random(seed);
                var cards = f.Player.Character.StartingDeck.Concat(deck.additions).OrderBy(_ => random.Next()).ToArray();
                foreach (var c in cards) await CardPileCmd.Add(f.State.CreateCard(c, f.Player), PileType.Draw, skipVisuals: true);
                if (!deck.vanilla)
                {
                    await Runtime.ApplyPower(f.Context, f.Player, typeof(GeopeliaRhythmPower), 1, null);
                    await Runtime.AddSignal(f.Context, f.Player, 1);
                }
                for (int turn = 0; turn < 6; turn++)
                {
                    int initialHand = PileType.Hand.GetPile(f.Player).Cards.Count;
                    await BeginTurn(f, draw: true);
                    drawn += PileType.Hand.GetPile(f.Player).Cards.Count - initialHand;
                    int attack = new[] { 8, 12, 0, 16, 10, 14 }[turn];
                    int guard = 0;
                    for (int action = 0; action < 80; action++)
                    {
                        var next = PileType.Hand.GetPile(f.Player).Cards
                            .Where(c => c.EnergyCost.GetResolved() <= f.Player.PlayerCombatState!.Energy)
                            .OrderByDescending(c => Score(c, attack - (int)f.Player.Creature.Block)).FirstOrDefault();
                        if (next is null) break;
                        await f.Play(next);
                        guard++;
                    }
                    // Observation limit only. Repeated free-card cycles are allowed by the design.
                    if (guard == 80) boundedTurns++;
                    Check(f.Player.PlayerCombatState!.Energy >= 0, "actual energy remains nonnegative");
                    blocked += Math.Min(attack, f.Player.Creature.Block);
                    incoming += attack;
                    plays += Runtime.Played(f.Player);
                    turns++;
                    await Hook.AfterSideTurnEnd(f.State, CombatSide.Player, new[] { f.Player.Creature });
                    await CardCmd.Discard(f.Context, PileType.Hand.GetPile(f.Player).Cards.ToArray());
                    f.State.CurrentSide = CombatSide.Enemy;
                    await Hook.AfterSideTurnEnd(f.State, CombatSide.Enemy, new[] { f.Enemy });
                }
                damage += 9999 - f.Enemy.CurrentHp;
                drawn += f.TotalCardsDrawn;
                energyGained += f.TotalEnergyGained;
                RunManager.Instance.CleanUp(false);
            }
            Benchmarks.Add(new { deck = deck.name, seeds = 16, turns, damagePerTurn = Math.Round(damage / turns, 2),
                blockPerTurn = Math.Round(blocked / turns, 2), unblockedPerTurn = Math.Round((incoming - blocked) / turns, 2), playsPerTurn = Math.Round((decimal)plays / turns, 2),
                cardsDrawnPerTurn = Math.Round((decimal)drawn / turns, 2), energyGainedPerTurn = Math.Round((decimal)energyGained / turns, 2), boundedTurns });
        }
    }

    private static decimal Score(CardModel c, int missingBlock)
    {
        decimal damage = 0, block = 0, utility = 0;
        if (c is GeopeliaCard own)
        {
            for (int i = 0; i < own.Definition.Steps.Length; i++)
            {
                var s = own.Definition.Steps[i];
                int value = c.DynamicVars["V" + i].IntValue;
                bool condition = s.Condition switch
                {
                    "lastSkill" => Runtime.History(c.Owner).LastOrDefault()?.CardPlay.Card.Type == CardType.Skill,
                    "lastAttack" => Runtime.History(c.Owner).LastOrDefault()?.CardPlay.Card.Type == CardType.Attack,
                    "plays" => Runtime.Played(c.Owner) >= s.Threshold,
                    "debt" => Runtime.Debt(c.Owner) > 0,
                    "replay" => own.Definition.Replays > 0 || own.Definition.SignalReplayCost > 0 && Runtime.Signal(c.Owner) >= own.Definition.SignalReplayCost,
                    _ => true
                };
                if (s.Pay > 0 && Runtime.Signal(c.Owner) < s.Pay) condition = false;
                if (s.Kind is "Attack" or "AllAttack" or "AttackBonus" or "SignalAttack") damage += value;
                if (s.Kind is "Block" or "BlockBonus" or "SignalBlock") block += value;
                if (s.Kind.EndsWith("Bonus") && s.Pay > 0 && Runtime.Signal(c.Owner) >= s.Pay)
                { if (s.Kind == "AttackBonus") damage += s.X; else block += s.X; }
                if (s.Kind == "SignalAttack") damage += Math.Min(s.Cap, Runtime.Signal(c.Owner)) * s.X;
                if (!condition) continue;
                if (s.Kind == "Signal") utility += Math.Min(value, 12 - Runtime.Signal(c.Owner)) * 0.7m;
                if (s.Kind == "Draw") utility += value * 2;
                if (s.Kind == "Energy") utility += value * 5;
                if (s.Kind == "EnergyDebt") utility -= value * 4;
                if (s.Kind == "DrawDebt") utility -= value * 2;
                if (s.Kind == "Power") utility += 9;
            }
            damage *= 1 + own.Definition.Replays;
            block *= 1 + own.Definition.Replays;
        }
        else
        {
            if (c.DynamicVars.TryGetValue("Damage", out var d)) damage = d.BaseValue;
            if (c.DynamicVars.TryGetValue("Block", out var b)) block = b.BaseValue;
            if (c is TwinStrike) damage *= 2;
            if (c.DynamicVars.TryGetValue("Cards", out var cards)) utility += cards.IntValue * 2;
            if (c.DynamicVars.TryGetValue("Energy", out var energy)) utility += energy.IntValue * 5;
            if (c is Bash) utility += 5;
        }
        return (damage + Math.Min(Math.Max(0, missingBlock), block) * 1.2m + utility) / Math.Max(1, c.EnergyCost.GetResolved());
    }

    private static Fixture NewFixture(string seed, CharacterModel? character = null)
    {
        var player = Player.CreateForNewRun(character ?? ModelDb.Character<Geopelia>(), UnlockState.none, 1);
        var run = RunState.CreateForTest(new[] { player }, seed: seed);
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService(), shouldSave: false);
        LocalContext.NetId = player.NetId;
        var state = new CombatState(runState: run);
        state.AddPlayer(player);
        ModelDb.Inject(typeof(MockAttackMonster));
        var enemy = state.CreateCreature(ModelDb.Monster<MockAttackMonster>().ToMutable(), CombatSide.Enemy, null);
        state.AddCreature(enemy);
        CombatManager.Instance.SetUpCombat(state);
        var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
        AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
        player.PlayerCombatState!.Phase = PlayerTurnPhase.Play;
        player.PlayerCombatState.Energy = 99;
        return new Fixture(state, player, enemy);
    }

    private sealed record Fixture(CombatState State, Player Player, Creature Enemy)
    {
        public ThrowingPlayerChoiceContext Context { get; } = new();
        public int TotalCardsDrawn { get; private set; }
        public int TotalEnergyGained { get; private set; }
        public async Task Play(CardModel card)
        {
            if (card.Pile?.Type != PileType.Hand) await CardPileCmd.Add(card, PileType.Hand, skipVisuals: true);
            int handBefore = PileType.Hand.GetPile(Player).Cards.Count;
            int energyBefore = Player.PlayerCombatState!.Energy;
            var (energy, stars) = await card.SpendResources();
            Creature? target = card.TargetType == TargetType.AnyEnemy ? Enemy : card.TargetType == TargetType.Self ? Player.Creature : null;
            await card.OnPlayWrapper(Context, target, false, new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars }, true);
            TotalCardsDrawn += PileType.Hand.GetPile(Player).Cards.Count - handBefore + 1;
            TotalEnergyGained += Player.PlayerCombatState.Energy - energyBefore + energy;
        }
    }
}
#endif
