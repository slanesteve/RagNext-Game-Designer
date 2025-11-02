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

            AddCommandCommand = new Command(() =>
            {
                if (string.IsNullOrWhiteSpace(SelectedCommandKey)) return;
                Action.Commands.Add(CreateCommand(SelectedCommandKey));
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