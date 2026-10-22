using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace PhigrosJiu;

// Native enchantment serialization preserves this additional effect when a generated card is copied or saved.
public sealed class GeopeliaEndlessSignal : EnchantmentModel
{
    private bool InherentEffect => Card is GeopeliaCard own && own.Definition.Steps.Any(s => s.Kind == "EndlessSignal");
    public override bool HasExtraCardText => !HasCard || !InherentEffect;
    public override bool CanEnchantCardType(CardType type) => type == CardType.Skill;
    protected override void OnEnchant()
    {
        Card.AddKeyword(CardKeyword.Exhaust);
        Card.SetToFreeThisCombat();
    }
    public override Task OnPlay(PlayerChoiceContext context, CardPlay? play)
        => InherentEffect ? Task.CompletedTask : Runtime.ApplyPower(context, Card.Owner, typeof(GeopeliaEndlessSignalPower), 1, Card);
}

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.IconPath), MethodType.Getter)]
internal static class EndlessSignalIconPatch
{
    private static bool Prefix(EnchantmentModel __instance, ref string __result)
    {
        if (__instance is not GeopeliaEndlessSignal) return true;
        __result = Content.PowerIcon(ModelDb.GetId<GeopeliaEndlessSignalPower>().Entry);
        return false;
    }
}
