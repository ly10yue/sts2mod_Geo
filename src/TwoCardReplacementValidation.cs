#if JIU_VALIDATION
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters.Mocks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task TwoCardReplacementCases()
    {
        _current = "spasmodic-x-energy-signal-hand-product";
        foreach (bool upgraded in new[] { false, true })
        foreach (int energy in new[] { 0, 2 })
        foreach (int signal in new[] { 0, 3 })
        foreach (int handSize in new[] { 0, 4 })
        {
            var f = NewFixture("SPASMODIC-PRODUCT");
            var second = f.State.CreateCreature(ModelDb.Monster<MockAttackMonster>().ToMutable(), CombatSide.Enemy, null);
            f.State.AddCreature(second);
            f.Player.PlayerCombatState!.Energy = energy;
            await Runtime.AddSignal(f.Context, f.Player, signal);
            await PowerCmd.Apply<GeopeliaEnergyDebtPower>(f.Context, f.Player.Creature, 3, f.Player.Creature, null);
            var hand = new List<CardModel>();
            for (int i = 0; i < handSize; i++)
            {
                var other = f.State.CreateCard(ModelDb.Card<GeopeliaDefend>(), f.Player);
                hand.Add(other);
                await CardPileCmd.Add(other, PileType.Hand, skipVisuals: true);
            }
            var card = f.State.CreateCard(ModelDb.Card<GeopeliaContinuousTake>(), f.Player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            Check(card.EnergyCost.CostsX && card.TargetType == TargetType.AllEnemies, "X cost and all-enemy targeting");
            await f.Play(card);
            Check(f.Player.PlayerCombatState.Energy == 0 && Runtime.Signal(f.Player) == 0, "all initial energy and signal spent even when damage is zero");
            Check(hand.All(c => c.Pile?.Type == PileType.Exhaust), "all other hand cards exhausted");
            Check(PileType.Hand.GetPile(f.Player).Cards.Count == 0, "played card excluded from hand consumption");
            Check(9999-f.Enemy.CurrentHp == energy*signal*handSize && 9999-second.CurrentHp == energy*signal*handSize,
                "resource product hits every enemy including all zero-factor boundaries");
            Check(card.Pile?.Type == PileType.Discard && Runtime.History(f.Player).Count(e => e.CardPlay.Card == card) == 1,
                "no inherited replay or unrequested self-exhaust");
            Check(f.Player.Creature.GetPowerAmount<GeopeliaEnergyDebtPower>() == 3 && f.Player.Creature.CurrentHp == 70,
                "no debt consumption or death consequence");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new { test = "spasmodic-base-upgraded-x-product-all-enemies-zero-boundaries", combinations = 16 });

        _current = "please-delayed-player-end-and-enemy-skip";
        foreach (bool upgraded in new[] { false, true })
        {
            var f = NewFixture("PLEASE-DELAY");
            var card = f.State.CreateCard(ModelDb.Card<GeopeliaSignalSingularity>(), f.Player);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            Check(card.Type == CardType.Skill && card.EnergyCost.GetResolved() == 1 && card.Keywords.Contains(CardKeyword.Exhaust),
                "PLEASE is a one-energy exhausting skill in both upgrade states");
            await f.Play(card);
            var power = f.Player.Creature.GetPower<GeopeliaFutureRestartPower>()!;
            Check(card.Pile?.Type == PileType.Exhaust && !power.ExtraTurnPending, "cast exhausts and leaves current turn intact");
            Check(!CombatManager.Instance.IsPlayerReadyToEndTurn(f.Player) && !Hook.ShouldTakeExtraTurn(f.State, f.Player),
                "no immediate turn end or enemy skip on cast");
            await power.AfterSideTurnStart(CombatSide.Enemy, new[] { f.Enemy }, f.State);
            Check(!power.ExtraTurnPending, "the intervening enemy turn proceeds normally");
            await BeginTurn(f, draw: false);
            await Hook.AfterSideTurnStart(f.State, CombatSide.Player, new[] { f.Player.Creature });
            Check(CombatManager.Instance.IsPlayerReadyToEndTurn(f.Player), "next own turn is forcibly ended through the native queue");
            Check(Hook.ShouldTakeExtraTurn(f.State, f.Player), "native extra-turn hook skips the immediately following enemy side");
            var manager = CombatManager.Instance;
            var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(manager)!;
            int beforeExtraTurn = f.Player.PlayerCombatState!.TurnNumber;
            await (Task)AccessTools.Method(typeof(CombatManager), "SwitchFromPlayerToEnemySide").Invoke(manager, new[] { turn })!;
            Check(f.State.CurrentSide == CombatSide.Player && f.Player.PlayerCombatState.TurnNumber == beforeExtraTurn + 1,
                "native side transition starts the next own turn without entering the enemy side");
            Check(f.Player.Creature.GetPower<GeopeliaFutureRestartPower>() is null && !Hook.ShouldTakeExtraTurn(f.State, f.Player),
                "effect removed after one player/enemy pair, without an endless skipped-turn loop");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new { test = "please-base-upgraded-delayed-end-native-enemy-skip-one-shot" });
    }
}
#endif
