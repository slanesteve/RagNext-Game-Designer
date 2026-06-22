using NUnit.Framework;
using RagNextPlayer.Runtime;
using RagNextPlayer.Runtime.Models;
using System.Collections.Generic;

namespace RagNextPlayer.Tests
{
    public class CommandExecutionTests
    {
        private GameData _game;
        private GameExecutionContext _ctx;

        [SetUp]
        public void Setup()
        {
            _game = new GameData
            {
                Title = "Test Game",
                Variables = new List<GameVariableData>(),
                Rooms = new List<RoomData>(),
                Characters = new List<GameObjectData>(),
                Objects = new List<GameObjectData>(),
                Player = new PlayerData()
            };

            _ctx = new GameExecutionContext(_game);
        }

        [Test]
        public void Execute_SetVariableCommand_UpdatesValue()
        {
            _game.Variables.Add(new GameVariableData { Name = "score", Value = "0", Type = "int" });
            var cmd = new SetVariableCommandData { Name = "score", Value = "10" };
            ActionExecutor.ExecuteCommand(cmd, _ctx);
            Assert.AreEqual("10", _ctx.GetVariable("score")?.Value);
        }

        [Test]
        public void Execute_IncrementVariableCommand_IncreasesValue()
        {
            _game.Variables.Add(new GameVariableData { Name = "counter", Value = "5", Type = "int" });
            var cmd = new VariableIncrementCommandData { Name = "counter", Value = "3" };
            ActionExecutor.ExecuteCommand(cmd, _ctx);
            Assert.AreEqual("8", _ctx.GetVariable("counter")?.Value);
        }

        [Test]
        public void Execute_DecrementVariableCommand_DecreasesValue()
        {
            _game.Variables.Add(new GameVariableData { Name = "counter", Value = "10", Type = "int" });
            var cmd = new VariableDecrementCommandData { Name = "counter", Value = "2" };
            ActionExecutor.ExecuteCommand(cmd, _ctx);
            Assert.AreEqual("8", _ctx.GetVariable("counter")?.Value);
        }

        [Test]
        public void Execute_MovePlayerCommand_SetsVariable()
        {
            var room = new RoomData { Id = "room_42", Name = "Milestone 42" };
            _game.Rooms.Add(room);
            var cmd = new MovePlayerToRoomCommandData { RoomId = "room_42" };
            ActionExecutor.ExecuteCommand(cmd, _ctx);
            Assert.AreEqual("room_42", _ctx.GetVariable("player.currentRoomId")?.Value);
        }

        [Test]
        public void Execute_LockUnlockExitCommands_TogglesLockedState()
        {
            var room = new RoomData { Id = "room_a", Name = "Room A" };
            room.LockedExits["North"] = false;
            _game.Rooms.Add(room);
            _ctx.CurrentRoom = room;

            var lockCmd = new LockRoomExitCommandData { RoomId = "room_a", Direction = "North" };
            ActionExecutor.ExecuteCommand(lockCmd, _ctx);
            Assert.IsTrue(room.LockedExits["North"]);

            var unlockCmd = new UnlockRoomExitCommandData { RoomId = "room_a", Direction = "North" };
            ActionExecutor.ExecuteCommand(unlockCmd, _ctx);
            Assert.IsFalse(room.LockedExits["North"]);
        }

        [Test]
        public void Execute_AppendTextAndLine_AppendsCorrectly()
        {
            _game.Variables.Add(new GameVariableData { Name = "story_log", Value = "Once upon a time", Type = "string" });

            var appendText = new AppendTextCommandData { VariableName = "story_log", Text = ", in a galaxy far away" };
            ActionExecutor.ExecuteCommand(appendText, _ctx);
            Assert.AreEqual("Once upon a time, in a galaxy far away", _ctx.GetVariable("story_log")?.Value);

            var appendLine = new AppendLineCommandData { VariableName = "story_log", Text = "The end." };
            ActionExecutor.ExecuteCommand(appendLine, _ctx);
            Assert.AreEqual("Once upon a time, in a galaxy far away\nThe end.\n", _ctx.GetVariable("story_log")?.Value);
        }

        [Test]
        public void Execute_PlayerProfileCommands_MutatesPlayerAttributes()
        {
            var nameCmd = new PlayerSetNameCommandData { Name = "Hero" };
            ActionExecutor.ExecuteCommand(nameCmd, _ctx);
            Assert.AreEqual("Hero", _game.Player.Name);

            var descCmd = new PlayerSetDescriptionCommandData { Description = "A savior." };
            ActionExecutor.ExecuteCommand(descCmd, _ctx);
            Assert.AreEqual("A savior.", _game.Player.Description);

            var genderCmd = new PlayerSetGenderCommandData { Gender = "Female" };
            ActionExecutor.ExecuteCommand(genderCmd, _ctx);
            Assert.AreEqual("Female", _game.Player.Gender);
        }

        [Test]
        public void Execute_CustomChoicesCommands_AppendsAndClears()
        {
            var addChoice = new AddCustomChoiceCommandData { PromptName = "Gate", ChoiceText = "Open", VariableName = "gate_open" };
            ActionExecutor.ExecuteCommand(addChoice, _ctx);
            Assert.AreEqual(1, _game.CustomChoices.Count);
            Assert.AreEqual("Gate", _game.CustomChoices[0].PromptName);
            Assert.AreEqual("Open", _game.CustomChoices[0].ChoiceText);

            var clearChoices = new ClearCustomChoiceCommandData { PromptName = "Gate" };
            ActionExecutor.ExecuteCommand(clearChoices, _ctx);
            Assert.IsEmpty(_game.CustomChoices);
        }
    }
}
