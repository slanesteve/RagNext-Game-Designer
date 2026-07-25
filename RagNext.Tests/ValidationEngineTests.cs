using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;
using RagsCore.Models;
using RagsCore.Actions;
using RagNext.Designer.Avalonia.Services;
using RagNext.Designer.Avalonia.Models;

namespace RagNext.Tests
{
    public class ValidationEngineTests
    {
        [Fact]
        public void Validate_NullGame_ReturnsError()
        {
            var results = ValidationEngine.Validate(null!);
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.Severity == "Error" && r.Message.Contains("null"));
        }

        [Fact]
        public void Validate_ValidGame_ReturnsNoErrors()
        {
            var game = new Game
            {
                Title = "Test Game",
                Author = "Steve",
                Rooms = new ObservableCollection<Room>
                {
                    new Room { Id = Guid.NewGuid(), Name = "Start Room" }
                }
            };

            var results = ValidationEngine.Validate(game);
            Assert.Empty(results);
        }

        [Fact]
        public void Validate_OrphanedRoomExit_ReturnsError()
        {
            var roomId = Guid.NewGuid();
            var invalidRoomId = Guid.NewGuid();

            var game = new Game
            {
                Title = "Test Game",
                Author = "Steve",
                Rooms = new ObservableCollection<Room>
                {
                    new Room
                    {
                        Id = roomId,
                        Name = "Start Room",
                        Exits = new Dictionary<string, Guid> { { "north", invalidRoomId } }
                    }
                }
            };

            var results = ValidationEngine.Validate(game);
            Assert.Contains(results, r => r.Severity == "Error" && r.Category == "Room" && r.Message.Contains("invalid/deleted Room ID"));
        }

        [Fact]
        public void Validate_OrphanedActionStepReference_ReturnsError()
        {
            var roomId = Guid.NewGuid();
            var invalidObjectId = Guid.NewGuid();

            var room = new Room
            {
                Id = roomId,
                Name = "Hallway"
            };

            var action = new RagsCore.Models.Action { Name = "Inspect" };
            action.Nodes.Add(new RoomHasObjectCondition { RoomId = roomId.ToString(), ObjectId = invalidObjectId.ToString() });
            room.Actions.Add(action);

            var game = new Game
            {
                Title = "Test Game",
                Author = "Steve",
                Rooms = new ObservableCollection<Room> { room }
            };

            var results = ValidationEngine.Validate(game);
            Assert.Contains(results, r => r.Severity == "Error" && r.Message.Contains("non-existent Object ID"));
        }

        [Fact]
        public void Validate_BrokenMathExpression_ReturnsError()
        {
            var game = new Game
            {
                Title = "Test Game",
                Author = "Steve",
                Rooms = new ObservableCollection<Room> { new Room { Id = Guid.NewGuid(), Name = "Room" } },
                Variables = new ObservableCollection<GameVariable>
                {
                    new GameVariable { Id = Guid.NewGuid(), Name = "Health", Type = "number", Value = "10 + (5 *" }
                }
            };

            var results = ValidationEngine.Validate(game);
            Assert.Contains(results, r => r.Severity == "Error" && r.Category == "Variable" && r.Message.Contains("invalid"));
        }

        [Fact]
        public void Validate_CircularFunctionCall_ReturnsError()
        {
            var func1Id = Guid.NewGuid();
            var func2Id = Guid.NewGuid();

            var func1 = new GlobalFunction { Id = func1Id, Name = "FuncA" };
            var func1Action = (RagsCore.Models.Action)func1;
            func1Action.Nodes.Add(new CallFunctionCommand { FunctionId = func2Id.ToString() });

            var func2 = new GlobalFunction { Id = func2Id, Name = "FuncB" };
            var func2Action = (RagsCore.Models.Action)func2;
            func2Action.Nodes.Add(new CallFunctionCommand { FunctionId = func1Id.ToString() });

            var game = new Game
            {
                Title = "Test Game",
                Author = "Steve",
                Rooms = new ObservableCollection<Room> { new Room { Id = Guid.NewGuid(), Name = "Room" } },
                Functions = new ObservableCollection<GlobalFunction> { func1, func2 }
            };

            var results = ValidationEngine.Validate(game);
            Assert.Contains(results, r => r.Severity == "Error" && r.Category == "Function" && r.Message.Contains("Circular Function Call"));
        }

        [Fact]
        public void Validate_UnconfiguredForEachLoop_ReturnsWarning()
        {
            var room = new Room { Id = Guid.NewGuid(), Name = "Room" };
            var action = new RagsCore.Models.Action { Name = "test" };
            action.Nodes.Add(new ForEachLoopCommand { ArrayVariableName = "-- Select --" });
            room.Actions.Add(action);

            var game = new Game
            {
                Title = "Test Game",
                Author = "Steve",
                Rooms = new ObservableCollection<Room> { room }
            };

            var results = ValidationEngine.Validate(game);
            Assert.Contains(results, r => r.Severity == "Warning" && r.Message.Contains("For Each Loop"));
        }
    }
}
