    using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagsCore.Models;

namespace RagNext.ViewModels
{
    public class ActionEditorViewModel : BaseViewModel
    {
        public Game Game { get; }
        public Room? CurrentRoom { get; }
        public GameObject? FocusObject { get; }
        public GameAction Action { get; }

        public ObservableCollection<string> AvailableConditions { get; } =
            new(["var.equals","player.inRoom","room.hasObject"]);

        public ObservableCollection<string> AvailableCommands { get; } =
            new(["var.set","player.moveTo","room.addObject","room.removeObject"]);

        private string? _selectedConditionKey;
        public string? SelectedConditionKey { get => _selectedConditionKey; set => SetProperty(ref _selectedConditionKey, value); }

        private string? _selectedCommandKey;
        public string? SelectedCommandKey { get => _selectedCommandKey; set => SetProperty(ref _selectedCommandKey, value); }

        // Add this new property so the UI can bind the currently selected command.
        private GameCommand? _selectedCommand;
        public GameCommand? SelectedCommand
        {
            get => _selectedCommand;
            set => SetProperty(ref _selectedCommand, value);
        }

        public ICommand AddConditionCommand { get; }
        public ICommand AddCommandCommand { get; }
        public ICommand RemoveConditionCommand { get; }
        public ICommand RemoveCommandCommand { get; }
        public ICommand TestRunCommand { get; }

        public ActionEditorViewModel(Game game, GameAction action, Room? room = null, GameObject? obj = null)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            CurrentRoom = room;
            FocusObject = obj;

            AddConditionCommand = new Command(() =>
            {
                if (string.IsNullOrWhiteSpace(SelectedConditionKey)) return;
                Action.Conditions.Add(CreateCondition(SelectedConditionKey));
                SelectedConditionKey = null;
            });

            // (Optional) if you later unify conditions + commands into one ordered list,
            // you'd introduce a single ObservableCollection<object> Steps and insert both there.

            // Replace the existing AddCommandCommand initialization with this:
            AddCommandCommand = new Command(() =>
            {
                if (string.IsNullOrWhiteSpace(SelectedCommandKey)) return;
                var newCmd = CreateCommand(SelectedCommandKey);

                if (SelectedCommand is not null)
                {
                    var idx = Action.Commands.IndexOf(SelectedCommand);
                    if (idx >= 0)
                        Action.Commands.Insert(idx + 1, newCmd); // insert as peer after selection
                    else
                        Action.Commands.Add(newCmd);
                }
                else
                {
                    Action.Commands.Add(newCmd); // fallback to append
                }

                SelectedCommandKey = null;
            });

            RemoveConditionCommand = new Command<RagsCore.Actions.Condition>(c =>
            {
                if (c is null) return;
                Action.Conditions.Remove(c);
            });

            RemoveCommandCommand = new Command<GameCommand>(c =>
            {
                if (c is null) return;
                Action.Commands.Remove(c);
            });

            TestRunCommand = new Command(() =>
            {
                var ctx = new ActionContext(Game, CurrentRoom, FocusObject);
                _ = ActionEngine.Execute(Action, ctx);
            });
        }

        private static RagsCore.Actions.Condition CreateCondition(string key) => key switch
        {
            "var.equals"    => new VariableEqualsCondition(),
            "player.inRoom" => new PlayerInRoomCondition(),
            "room.hasObject"=> new RoomHasObjectCondition(),
            _ => throw new NotSupportedException(key)
        };

        private static GameCommand CreateCommand(string key) => key switch
        {
            "var.set"          => new SetVariableCommand(),
            "player.moveTo"    => new MovePlayerToRoomCommand(),
            "room.addObject"   => new AddObjectToRoomCommand(),
            "room.removeObject"=> new RemoveObjectFromRoomCommand(),
            _ => throw new NotSupportedException(key)
        };
    }
}