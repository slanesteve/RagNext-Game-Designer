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
            // Arrange
            _game.Variables.Add(new GameVariableData { Name = "score", Value = "0", Type = "int" });
            var cmd = new SetVariableCommandData { Name = "score", Value = "10" };

            // Act
            ActionExecutor.ExecuteCommand(cmd, _ctx);

            // Assert
            Assert.AreEqual("10", _ctx.GetVariable("score")?.Value);
        }

        [Test]
        public void Execute_IncrementVariableCommand_IncreasesValue()
        {
            // Arrange
            _game.Variables.Add(new GameVariableData { Name = "counter", Value = "5", Type = "int" });
            var cmd = new VariableIncrementCommandData { Name = "counter", Value = "3" };

            // Act
            ActionExecutor.ExecuteCommand(cmd, _ctx);

            // Assert
            Assert.AreEqual("8", _ctx.GetVariable("counter")?.Value);
        }

        [Test]
        public void Execute_DecrementVariableCommand_DecreasesValue()
        {
            // Arrange
            _game.Variables.Add(new GameVariableData { Name = "counter", Value = "10", Type = "int" });
            var cmd = new VariableDecrementCommandData { Name = "counter", Value = "2" };

            // Act
            ActionExecutor.ExecuteCommand(cmd, _ctx);

            // Assert
            Assert.AreEqual("8", _ctx.GetVariable("counter")?.Value);
        }

        [Test]
        public void Execute_MovePlayerCommand_ChangesRoom()
        {
            // Arrange
            var room = new RoomData { Id = "room_42", Name = "Milestone 42" };
            _game.Rooms.Add(room);
            var cmd = new MovePlayerToRoomCommandData { RoomId = "room_42" };

            // Act
            ActionExecutor.ExecuteCommand(cmd, _ctx);

            // Assert
            Assert.AreEqual(room, _ctx.CurrentRoom);
        }

        [Test]
        public void Execute_LockUnlockExitCommands_TogglesLockedState()
        {
            // Arrange
            var room = new RoomData { Id = "room_a", Name = "Room A" };
            room.LockedExits["North"] = false;
            _game.Rooms.Add(room);
            _ctx.CurrentRoom = room;

            // Act & Assert (Lock)
            var lockCmd = new LockRoomExitCommandData { RoomId = "room_a", Direction = "North" };
            ActionExecutor.ExecuteCommand(lockCmd, _ctx);
            Assert.IsTrue(room.LockedExits["North"]);

            // Act & Assert (Unlock)
            var unlockCmd = new UnlockRoomExitCommandData { RoomId = "room_a", Direction = "North" };
            ActionExecutor.ExecuteCommand(unlockCmd, _ctx);
            Assert.IsFalse(room.LockedExits["North"]);
        }
    }
}
