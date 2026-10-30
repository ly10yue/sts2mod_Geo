#if JIU_VALIDATION
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task EndlessSignalAssetCases()
    {
        _current="endless-signal-enchantment-icon-asset-path";
        var canonical=ModelDb.Enchantment<GeopeliaEndlessSignal>();
        string path=canonical.IconPath;
        Check(ResourceLoader.Exists(path),"Endless Signal enchantment icon path must exist in the mounted pack: "+path);
        Check(path==Content.Root+"icons/powers/geopelia_endless_signal_enchantment.png","icon path uses the exact lowercase packed filename");
        Check(ResourceLoader.Exists(Content.PowerIcon(ModelDb.GetId<GeopeliaEndlessSignalPower>().Entry)),"power icon helper normalizes raw ModelId entries");
        var session=PreloadManager.Cache.CreateSession("JIU_ENDLESS_SIGNAL_ICON",new[]{path});
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(!session.IsCompleted && clock.ElapsedMilliseconds<5000)
        {
            session.Process();
            await NGame.Instance!.ToSignal(NGame.Instance.GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        Check(session.IsCompleted && await session.Task,"real asset preloading completes");
        Check(canonical.Icon is not null,"native cache returns a real enchantment icon");
        foreach(bool upgraded in new[]{false,true})
        {
            _current="endless-signal-next-turn-real-card-visuals";
            var f=NewFixture("ENDLESS-SIGNAL-UI");
            var source=f.State.CreateCard(ModelDb.Card<GeopeliaHappyDisease>(),f.Player);
            if(upgraded){source.UpgradeInternal();source.FinalizeUpgradeInternal();}
            await f.Play(source);await BeginTurn(f,false);
            var generated=f.State.CreateCard(ModelDb.Card<GeopeliaHold>(),f.Player);
            TestRngInjector.SetCombatCardGenerationOverride(new(){generated});
            await Hook.BeforeHandDraw(f.State,f.Player,f.Context);
            Check(generated.Enchantment?.Icon is not null && generated.EnergyCost.GetResolved()==0,"next-turn card has an actual icon and is free");
            bool previous=TestMode.IsOn;
            try
            {
                // Unlike logic-only fixtures, create and update the real NCard with TestMode disabled.
                TestMode.IsOn=false;
                var node=NCard.Create(generated) ?? throw new InvalidOperationException("native visible card was not instantiated");
                NGame.Instance!.AddChild(node);
                await node.ToSignal(node.GetTree(),SceneTree.SignalName.ProcessFrame);
                node.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);
                var icon=(TextureRect)AccessTools.Field(typeof(NCard),"_enchantmentIcon").GetValue(node)!;
                Check(icon.Texture is not null,"real card UpdateEnchantmentVisuals resolves its texture without throwing");
                node.Visible=false;
            }
            finally {TestMode.IsOn=previous;}
            // Start-of-turn draw and the following player action still finish after the visual update.
            await StockDrawPile(f,6);await CardPileCmd.Draw(f.Context,5,f.Player,fromHandDraw:true);
            Check(PileType.Hand.GetPile(f.Player).Cards.Count==6,"turn-start draw continues after generated card visuals");
            await f.Play(generated);
            Check(f.Player.Creature.GetPowerAmount<GeopeliaEndlessSignalPower>()==1,"visible generated skill plays and passes its effect to the next turn");
            RunManager.Instance.CleanUp(false);
        }
        Rows.Add(new{test="endless-signal-pack-preload-and-real-card-visuals-base-upgraded",icon=path});
    }
}
#endif
