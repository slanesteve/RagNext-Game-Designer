using System;
using System.IO;
using System.Text.Json;
using Xunit;
using RagsCore.Models;
using RagsCore;
using RagNext.Designer.Avalonia.Services;

namespace RagNext.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void Game_ShouldRoundTripSerializeCorrectly()
        {
            // Arrange
            var game = new Game
            {
                Id = Guid.NewGuid(),
                Title = "Test Adventure",
                Author = "Test Author",
                Version = "1.2.3"
            };

            // Add variables
            game.Variables.Add(new GameVariable { Name = "test_var", Value = "42", Type = "int" });
            game.Variables.Add(new GameVariable { Name = "test_date", Value = "2026-06-22T12:00:00", Type = "DateTime" });

            // Add room
            var room = new Room { Id = Guid.NewGuid(), Name = "Room 1", Description = "A basic room." };
            game.Rooms.Add(room);
            game.Player.StartingRoom = room;

            // Add exit
            room.Exits["North"] = room.Id;
            room.LockedExits["North"] = true;

            // Act
            var json = JsonSerializer.Serialize(game, RagsCore.RagsJsonContext.CustomDefault.Game);
            var deserialized = JsonSerializer.Deserialize(json, RagsCore.RagsJsonContext.CustomDefault.Game);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(game.Id, deserialized.Id);
            Assert.Equal(game.Title, deserialized.Title);
            Assert.Equal(game.Author, deserialized.Author);
            Assert.Equal(game.Version, deserialized.Version);

            Assert.Equal(2, deserialized.Variables.Count);
            Assert.Equal("test_var", deserialized.Variables[0].Name);
            Assert.Equal("42", deserialized.Variables[0].Value);

            Assert.Single(deserialized.Rooms);
            Assert.Equal("Room 1", deserialized.Rooms[0].Name);
            Assert.True(deserialized.Rooms[0].Exits.ContainsKey("North"));
            Assert.Equal(room.Id, deserialized.Rooms[0].Exits["North"]);
            Assert.True(deserialized.Rooms[0].LockedExits["North"]);
        }
    }
}
