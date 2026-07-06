using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using RagsCore.Actions;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class ActionEditorViewModel : ViewModelBase
    {
        public Game Game { get; }
        public Room? CurrentRoom { get; }
        public GameObject? FocusObject { get; }
        public GameAction Action { get; }
        public ObservableCollection<string> AvailableSteps { get; } =
            new([
                "var.equals", "player.inRoom", "room.hasObject",                 "var.set", "var.evaluate", "player.moveTo", "player.screenShake", "room.addObject", "room.removeObject",
                 "object.displayDescription", "object.moveToCharacter", "object.moveToInventory", "object.moveInsideObject",
                 "player.swapCharacter", "ui.showSplashScreen"
            ]);

        private string? _selectedStepKey;
        public string? SelectedStepKey { get => _selectedStepKey; set => SetProperty(ref _selectedStepKey, value); }

        private ActionStep? _selectedStep;
        public ActionStep? SelectedStep
        {
            get => _selectedStep;
            set => SetProperty(ref _selectedStep, value);
        }

        public ICommand AddStepCommand { get; }
        public ICommand RemoveStepCommand { get; }
        public ICommand TestRunCommand { get; }

        public ActionEditorViewModel(Game game, GameAction action, Room? room = null, GameObject? obj = null)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            CurrentRoom = room;
            FocusObject = obj;

            AddStepCommand = new Command(() =>
            {
                if (string.IsNullOrWhiteSpace(SelectedStepKey)) return;
                var newStep = CreateStep(SelectedStepKey);
                if (SelectedStep is not null)
                {
                    var idx = Action.Steps.IndexOf(SelectedStep);
                    if (idx >= 0)
                        Action.Steps.Insert(idx + 1, newStep);
                    else
                        Action.Steps.Add(newStep);
                }
                else
                {
                    Action.Steps.Add(newStep);
                }
                SelectedStepKey = null;
            });

            RemoveStepCommand = new Command<ActionStep>(s =>
            {
                if (s is null) return;
                Action.Steps.Remove(s);
                if (SelectedStep == s) SelectedStep = null;
            });

            TestRunCommand = new Command(() =>
            {
                var ctx = new ActionContext(Game, CurrentRoom, FocusObject);
                if (Action.Steps.OfType<RagsCore.Actions.Condition>().All(c => c.Evaluate(ctx)))
                {
                    foreach (var cmd in Action.Steps.OfType<GameCommand>())
                        cmd.Execute(ctx);
                }
            });
        }

        private static ActionStep CreateStep(string key) => key switch
        {
            // Conditions
            "var.equals" => new VariableEqualsCondition(),
            "player.inRoom" => new PlayerInRoomCondition(),
            "room.hasObject" => new RoomHasObjectCondition(),
            // Commands
            "var.set" => new SetVariableCommand(),
            "var.evaluate" => new EvaluateFormulaCommand(),
            "player.moveTo" => new MovePlayerToRoomCommand(),
            "player.screenShake" => new ScreenShakeCommand(),
            "room.addObject" => new AddObjectToRoomCommand(),
            "room.removeObject" => new RemoveObjectFromRoomCommand(),
            "object.displayDescription" => new ObjectDisplayDescriptionCommand(),
            "object.moveToCharacter" => new ObjectMoveToCharacterCommand(),
            "object.moveToInventory" => new ObjectMoveToInventoryCommand(),
            "object.moveInsideObject" => new ObjectMoveInsideObjectCommand(),
            "player.swapCharacter" => new SwapPlayerCharacterCommand(),
            "ui.showSplashScreen" => new ShowSplashScreenCommand(),
            _ => throw new NotSupportedException(key)
        };
    }
}
