using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace PhigrosJiu;

internal static class CustomCardEffects
{
    // Percentage penalties store percentage points, not units of resource debt.
    internal static PowerModel[] ResourceDebts(Player player) => player.Creature.Powers
        .Where(p => p.Amount > 0 && (p is GeopeliaEnergyDebtPower or GeopeliaDrawDebtPower or GeopeliaSignalDebtPower))
        .ToArray();

    internal static async Task LastWill(PlayerChoiceContext context, CardModel card, CardPlay play, int energy)
    {
        var player = card.Owner;
        int signal = Runtime.Signal(player);
        var debts = ResourceDebts(player);
        decimal damage = (decimal)energy * signal * debts.Sum(p => p.Amount);
        // Register the consequence even at zero damage. Removal is consumption, not repayment.
        await PowerCmd.Apply<GeopeliaLastWillPower>(context, player.Creature, 1, player.Creature, card);
        foreach (var debt in debts) await PowerCmd.Remove(debt);
        if (signal > 0) await Runtime.SpendSignal(context, player, signal, card);
        // Spending signal can generate energy through other powers; the X payment has already happened.
        if (damage > 0)
            await DamageCmd.Attack(damage).FromCard(card, play).TargetingAllOpponents(card.CombatState!)
                .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    internal static async Task LetGo(PlayerChoiceContext context, CardModel card)
    {
        var player = card.Owner;
        var debts = ResourceDebts(player);
        if (debts.Length > 0)
        {
            var chosen = player.RunState.Rng.CombatCardSelection.NextItem(debts)!;
            if (chosen.Amount == 1) await PowerCmd.Remove(chosen);
            else await PowerCmd.ModifyAmount(context, chosen, -1, player.Creature, card);
        }
        var selected = (await CardSelectCmd.FromHand(context, player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1), c => c != card, card)).FirstOrDefault();
        if (selected is not null) await CardCmd.Exhaust(context, selected);
    }
}
