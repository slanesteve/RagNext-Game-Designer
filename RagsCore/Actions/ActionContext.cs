using System;
using System.Linq;
using RagsCore.Models;

namespace RagsCore.Actions
{
    // Execution context passed to conditions and commands.
    public sealed class ActionContext
    {
        public Game Game { get; }
        public Room? CurrentRoom { get; }
        public Player Player => Game.Player;
        public GameObject? FocusObject { get; }

        public ActionContext(Game game, Room? currentRoom = null, GameObject? focusObject = null)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            CurrentRoom = currentRoom;
            FocusObject = focusObject;
        }

        public GameVariable? GetVariable(string name) =>
            Game.Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.Ordinal));

        public void SetVariable(string name, string? value)
        {
            var v = GetVariable(name);
            if (v is null)
            {
                v = new GameVariable { Name = name, Value = value };
                Game.Variables.Add(v);
            }
            else
            {
                v.Value = value;
            }
        }
    }
}