using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using RagsCore.Actions;

namespace RagNext.Views.Controls
{
    public partial class ActionCommandsEditorView : ContentView
    {
        public ActionCommandsEditorView()
        {
            InitializeComponent();
        }

        public static readonly BindableProperty CommandsProperty =
            BindableProperty.Create(nameof(Commands), typeof(ObservableCollection<GameCommand>), typeof(ActionCommandsEditorView));

        public ObservableCollection<GameCommand>? Commands
        {
            get => (ObservableCollection<GameCommand>?)GetValue(CommandsProperty);
            set => SetValue(CommandsProperty, value);
        }

        public static readonly BindableProperty GameProperty =
            BindableProperty.Create(nameof(Game), typeof(RagsCore.Models.Game), typeof(ActionCommandsEditorView));

        public RagsCore.Models.Game? Game
        {
            get => (RagsCore.Models.Game?)GetValue(GameProperty);
            set => SetValue(GameProperty, value);
        }

        public ObservableCollection<string> AvailableCommandKeys { get; } =
            new(["var.set","player.moveTo","room.addObject","room.removeObject"]);

        public string? SelectedCommandKey { get; set; }

        void OnAddCommandClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedCommandKey)) return;
            if (Commands is null)
            {
                // Create and push back to source via TwoWay binding
                Commands = new ObservableCollection<GameCommand>();
            }
            Commands.Add(CreateCommand(SelectedCommandKey));
            SelectedCommandKey = null;
        }

        void OnRemoveCommandClicked(object? sender, EventArgs e)
        {
            if (Commands is null) return;
            if (sender is not Button b) return;
            if (b.CommandParameter is GameCommand cmd)
                Commands.Remove(cmd);
        }

        private static GameCommand CreateCommand(string key) => key switch
        {
            "var.set"            => new SetVariableCommand(),
            "player.moveTo"      => new MovePlayerToRoomCommand(),
            "room.addObject"     => new AddObjectToRoomCommand(),
            "room.removeObject"  => new RemoveObjectFromRoomCommand(),
            _ => throw new NotSupportedException(key)
        };
    }
}