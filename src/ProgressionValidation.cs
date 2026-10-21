#if JIU_VALIDATION
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;

namespace PhigrosJiu;

internal static partial class GameValidation
{
    private static async Task ProgressionCases()
    {
        var characters = new[] { ModelDb.Character<Geopelia>(), ModelDb.AllCharacters.Single(c => c.GetType().Assembly.GetName().Name == "jiu_2") };
        Check(!JiuProgression.IsJiu(ModelDb.Character<Silent>()), "vanilla epoch checks are preserved");
        foreach (var character in characters)
        {
            _current = character.Id.Entry + ":dialogues";
            string languageBefore = LocManager.Instance.Language;
            foreach (string language in new[] { "zhs", "eng" })
            {
            LocManager.Instance.SetLanguage(language);
            foreach (var set in new[] { ModelDb.Event<Neow>().DialogueSet, ModelDb.Event<TheArchitect>().DialogueSet })
            foreach (int visits in new[] { 0, 1, 4, 20 })
            {
                var dialogues = set.GetValidDialogues(character.Id, visits, 25, false).ToArray();
                Check(dialogues.Length > 0, "dialogue exists on first and repeated visits");
                foreach (var dialogue in dialogues)
                foreach (var line in dialogue.Lines)
                {
                    Check(line.LineText?.Exists() == true && !string.IsNullOrWhiteSpace(line.LineText.GetFormattedText()), "dialogue text resolves");
                    Check(line.NextButtonText is null || line.NextButtonText.Exists(), "continue label resolves");
                }
            }
            }
            LocManager.Instance.SetLanguage(languageBefore);
            foreach (var encounter in new EncounterModel[] { ModelDb.Encounter<ToadpolesWeak>(), ModelDb.Encounter<BygoneEffigyElite>(), ModelDb.Encounter<WaterfallGiantBoss>() })
            {
                _current = character.Id.Entry + ":victory:" + encounter.Id.Entry;
                var player = Player.CreateForNewRun(character, UnlockState.all, 1);
                var run = RunState.CreateForTest(new[] { player }, seed: "JIU-VICTORY");
                RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService(), shouldSave: false);
                LocalContext.NetId = player.NetId;
                var room = new CombatRoom(encounter.ToMutable(), run);
                run.PushRoom(room);
                var state = room.CombatState;
                state.AddPlayer(player);
                room.Encounter.GenerateMonstersWithSlots(run);
                foreach (var (monster, slot) in room.Encounter.MonstersWithSlots)
                    state.AddCreature(state.CreateCreature(monster, CombatSide.Enemy, slot));
                CombatManager.Instance.SetUpCombat(state);
                var turn = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
                AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
                player.PlayerCombatState!.Phase = PlayerTurnPhase.Play;
                int won = 0, ended = 0;
                void OnWon(CombatRoom finished) { if (finished == room) won++; }
                void OnEnded(CombatRoom finished) { if (finished == room) ended++; }
                CombatManager.Instance.CombatWon += OnWon;
                CombatManager.Instance.CombatEnded += OnEnded;
                try
                {
                    int before = SaveManager.Instance.Progress.GetOrCreateEncounterStats(encounter.Id).FightStats.FirstOrDefault(f => f.Character == character.Id)?.Wins ?? 0;
                    await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), state.Enemies.ToArray(), 999999m, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
                    await CombatManager.Instance.CheckWinCondition();
                    Check(!CombatManager.Instance.IsInProgress && room.IsPreFinished, "combat completes after lethal damage");
                    Check(won == 1 && ended == 1, "native victory and end notifications each fire once");
                    Check(SaveManager.Instance.Progress.GetOrCreateEncounterStats(encounter.Id).FightStats.Single(f => f.Character == character.Id).Wins == before + 1, "fight statistics are still saved for the correct character");
                    var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
                    await rewards.GenerateWithoutOffering();
                    Check(rewards.Rewards.Count > 0 && rewards.Rewards.All(r => r.IsPopulated), "native post-combat rewards populate");
                    Benchmarks.Add(new { test = "progression-victory", character = character.Id.Entry, encounter = encounter.Id.Entry, won, ended, rewards = rewards.Rewards.Count });
                }
                finally
                {
                    CombatManager.Instance.CombatWon -= OnWon;
                    CombatManager.Instance.CombatEnded -= OnEnded;
                    RunManager.Instance.CleanUp(false);
                }
            }
            // Exercise the actual event option callbacks through the final WinRun call.
            _current = character.Id.Entry + ":architect-proceed";
            var owner = Player.CreateForNewRun(character, UnlockState.all, 1);
            var finalRun = RunState.CreateForTest(new[] { owner }, seed: "JIU-ARCHITECT");
            RunManager.Instance.SetUpTest(finalRun, new NetSingleplayerGameService(), shouldSave: false);
            LocalContext.NetId = owner.NetId;
            var architect = ModelDb.Event<TheArchitect>().ToMutable();
            await architect.BeginEvent(owner, new EventCombatSynchronizer(finalRun, finalRun), false);
            for (int i = 0; i < 3; i++)
            {
                Check(architect.CurrentOptions.Count == 1 && !architect.CurrentOptions[0].IsLocked, "architect presents an actionable option");
                await architect.CurrentOptions[0].Chosen();
            }
            Check(architect.CurrentOptions.Count == 0, "architect final choice completes without null dialogue");
            Benchmarks.Add(new { test = "architect-proceed", character = character.Id.Entry, result = "PASS" });
            RunManager.Instance.CleanUp(false);
        }
    }
}
#endif
