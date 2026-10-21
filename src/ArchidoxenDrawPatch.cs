using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

namespace PhigrosJiu;

// This hook covers native cards, relics and other sources as well as this mod's cards.
[HarmonyPatch(typeof(CardPileCmd),nameof(CardPileCmd.Draw),new[]{typeof(PlayerChoiceContext),typeof(decimal),typeof(Player),typeof(bool)})]
internal static class ArchidoxenDrawPatch
{
    private static void Prefix(ref decimal count,Player player,bool fromHandDraw)
    {
        if (count<=0 || CombatManager.Instance.IsOverOrEnding
            || player.Creature.GetPower<GeopeliaArchidoxenPower>() is not { PendingDoubles: > 0 } power) return;
        var state=player.Creature.CombatState;
        if (state is null || !Hook.ShouldDraw(state,player,fromHandDraw,out _)
            || PileType.Hand.GetPile(player).Cards.Count>=CardPile.MaxCardsInHand
            || PileType.Draw.GetPile(player).Cards.Count+PileType.Discard.GetPile(player).Cards.Count==0) return;
        count=power.DoubleNextBenefit(count);
    }
}
