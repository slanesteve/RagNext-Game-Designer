using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;
using RagsCore.Models;
using RagsCore.Actions;
using RagsCore.Services;

namespace RagNext.Tests
{
    public class ActionExecutionTests
    {
        [Fact]
        public void SetVariableCommand_ExecutesAndMutatesVariableState()
        {
            // Arrange
            var game = new Game();
            game.Variables.Add(new GameVariable { Name = "Gold", Value = "100", Type = "number" });
            var ctx = new ActionContext(game);

            var cmd = new SetVariableCommand { Name = "Gold", Value = "250" };

            // Act
            cmd.Execute(ctx);

            // Assert
            Assert.Equal("250", ctx.GetVariable("Gold")?.Value);
        }

        [Fact]
        public void MovePlayerToRoomCommand_UpdatesPlayerCurrentRoomId()
        {
            // Arrange
            var game = new Game();
            var room1 = new Room { Id = Guid.NewGuid(), Name = "Foyer" };
            var room2 = new Room { Id = Guid.NewGuid(), Name = "Hallway" };
            game.Rooms.Add(room1);
            game.Rooms.Add(room2);
            game.Player.StartingRoom = room1;
            var ctx = new ActionContext(game, currentRoom: room1);
            var cmd = new MovePlayerToRoomCommand { RoomId = room2.Id.ToString() };

            // Act
            cmd.Execute(ctx);

            // Assert
            Assert.Equal(room2.Id.ToString(), ctx.GetVariable("player.currentRoomId")?.Value);
        }

        [Fact]
        public void AddObjectToRoomCommand_MovesObjectToTargetRoom()
        {
            // Arrange
            var game = new Game();
            var room = new Room { Id = Guid.NewGuid(), Name = "Kitchen" };
            var item = new GameObject { Id = Guid.NewGuid(), Name = "Apple" };
            game.Rooms.Add(room);
            game.Objects.Add(item);

            var ctx = new ActionContext(game);
            var cmd = new AddObjectToRoomCommand { RoomId = room.Id.ToString(), ObjectId = item.Id.ToString() };

            // Act
            cmd.Execute(ctx);

            // Assert
            Assert.Contains(item.Id, room.ObjectIds);
        }

        [Fact]
        public void AppendTextCommand_AppendsTextToVariable()
        {
            // Arrange
            var game = new Game();
            game.Variables.Add(new GameVariable { Name = "LogText", Value = "Header", Type = "string" });
            var ctx = new ActionContext(game);

            var cmd = new AppendTextCommand { VariableName = "LogText", Text = " - Continued" };

            // Act
            cmd.Execute(ctx);

            // Assert
            Assert.Equal("Header - Continued", ctx.GetVariable("LogText")?.Value);
        }

        [Fact]
        public void AppendLineCommand_AppendsLineToVariable()
        {
            // Arrange
            var game = new Game();
            game.Variables.Add(new GameVariable { Name = "Journal", Value = "Line 1\n", Type = "string" });
            var ctx = new ActionContext(game);

            var cmd = new AppendLineCommand { VariableName = "Journal", Text = "Line 2" };

            // Act
            cmd.Execute(ctx);

            // Assert
            Assert.Equal("Line 1\nLine 2\n", ctx.GetVariable("Journal")?.Value);
        }

        [Fact]
        public void ArrayVariable_AddRowAndSetElement_ExecutesCorrectly()
        {
            // Arrange
            var game = new Game();
            var arrayVar = new GameVariable
            {
                Name = "InventoryTable",
                Type = "array",
                Columns = new ObservableCollection<string> { "ItemName", "Quantity" }
            };
            game.Variables.Add(arrayVar);
            var ctx = new ActionContext(game);

            var addRowCmd = new AddArrayRowCommand { ArrayVariableName = "InventoryTable", ValuesCommaSeparated = "Sword, 1" };
            var setElemCmd = new SetArrayElementCommand
            {
                ArrayVariableName = "InventoryTable",
                RowIndex = "0",
                ColumnName = "Quantity",
                Value = "5"
            };

            // Act
            addRowCmd.Execute(ctx);
            setElemCmd.Execute(ctx);

            // Assert
            Assert.Single(arrayVar.Rows);
            Assert.Equal("Sword", arrayVar.Rows[0][0]);
            Assert.Equal("5", arrayVar.Rows[0][1]);
        }

        [Fact]
        public void ArrayVariable_RemoveRow_RemovesSpecifiedRowIndex()
        {
            // Arrange
            var game = new Game();
            var arrayVar = new GameVariable
            {
                Name = "Scores",
                Type = "array",
                Columns = new ObservableCollection<string> { "Player", "Score" }
            };
            arrayVar.Rows.Add(new ObservableCollection<string> { "Alice", "100" });
            arrayVar.Rows.Add(new ObservableCollection<string> { "Bob", "200" });
            game.Variables.Add(arrayVar);

            var ctx = new ActionContext(game);
            var removeCmd = new RemoveArrayRowCommand { ArrayVariableName = "Scores", RowIndex = "0" };

            // Act
            removeCmd.Execute(ctx);

            // Assert
            Assert.Single(arrayVar.Rows);
            Assert.Equal("Bob", arrayVar.Rows[0][0]);
        }
    }
}
