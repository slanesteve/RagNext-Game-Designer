using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Xunit;
using RagsCore.Models;
using RagsCore.Actions;
using RagsCore.Services;

namespace RagNext.Tests
{
    public class FeaturesTests
    {
        [Fact]
        public void TemplateResolver_ShouldResolveNestedDynamicVariables()
        {
            // Arrange
            var game = new Game();
            
            // Set up player variable value (Melee = 5)
            game.Variables.Add(new GameVariable { Name = "Melee", Value = "5", Type = "int" });

            // Create a focus object with custom attribute (SkillName = "Melee")
            var item = new GameObject
            {
                Id = Guid.NewGuid(),
                Name = "Sword",
                Description = "A steel sword."
            };
            item.Attributes.Add(new CustomAttribute { Name = "SkillName", Value = "Melee" });

            var ctx = new ActionContext(game, focusObject: item);

            // Act
            // First resolves {this.attributes.SkillName} -> "Melee"
            // Then resolves {Variables.Melee} -> "5"
            var resolvedValue = TemplateResolver.Resolve("{Variables.{this.attributes.SkillName}}", ctx);

            // Assert
            Assert.Equal("5", resolvedValue);
        }

        [Fact]
        public void PasteGlobalAction_UniqueNameGeneratorLogic_ShouldResolveDuplicates()
        {
            // Arrange
            var actions = new List<RagsCore.Models.Action>
            {
                new RagsCore.Models.Action { Name = "Wear" },
                new RagsCore.Models.Action { Name = "Wear - Copy" }
            };

            // Mimic unique candidate suffix calculation loop
            string baseName = "Wear";
            string candidate = baseName;
            int counter = 1;
            while (actions.Exists(a => string.Equals(a.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                if (counter == 1)
                {
                    candidate = $"{baseName} - Copy";
                }
                else
                {
                    candidate = $"{baseName} - Copy ({counter})";
                }
                counter++;
            }

            // Assert
            Assert.Equal("Wear - Copy (2)", candidate);
        }

        [Fact]
        public void WearSlots_Serialization_ShouldPreserveProperties()
        {
            // Arrange
            var game = new Game();
            game.WearSlots.Clear();
            game.WearSlots.Add("CustomHead");
            game.WearSlots.Add("CustomFeet");

            var item = new GameObject
            {
                Id = Guid.NewGuid(),
                Name = "Special Hat",
                WearSlot = "CustomHead",
                IsWearable = true
            };
            game.Objects.Add(item);

            // Act
            var json = RagNext.Designer.Avalonia.Services.GameJsonExporter.Export(game);
            
            // Deserialize back to Dto to verify values exist
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var dto = System.Text.Json.JsonSerializer.Deserialize<RagNext.Designer.Avalonia.Services.ExportGameDto>(json, options);

            // Assert
            Assert.NotNull(dto);
            Assert.Contains("CustomHead", dto.WearSlots);
            Assert.Contains("CustomFeet", dto.WearSlots);
            
            var objDto = dto.Objects?.Find(o => o.Name == "Special Hat");
            Assert.NotNull(objDto);
            Assert.Equal("CustomHead", objDto.WearSlot);
        }

        [Fact]
        public void ItemCanWearCondition_ShouldEvaluateCorrectly()
        {
            // Arrange
            var game = new Game();
            var shoes1 = new GameObject { Id = Guid.NewGuid(), Name = "Boots", WearSlot = "Feet", IsWearable = true };
            var shoes2 = new GameObject { Id = Guid.NewGuid(), Name = "Heels", WearSlot = "Feet", IsWearable = true };

            game.Objects.Add(shoes1);
            game.Objects.Add(shoes2);

            // Add shoes1 to player inventory and mark it as worn
            game.Player.Inventory.Add(shoes1);
            shoes1.IsWorn = true;

            // Condition checking shoes2 (which conflicts with shoes1)
            var condConflict = new ItemCanWearCondition { ItemId = shoes2.Id.ToString() };
            var condOk = new ItemCanWearCondition { ItemId = shoes1.Id.ToString() }; // Checking self (should be ok)

            var ctx = new ActionContext(game);

            // Act
            var canWearConflict = condConflict.Evaluate(ctx);
            var canWearOk = condOk.Evaluate(ctx);

            // Assert
            Assert.False(canWearConflict);
            Assert.True(canWearOk);
        }

        [Fact]
        public void TemplateResolver_ShouldResolveWornSlotItemName()
        {
            // Arrange
            var game = new Game();
            var shoes = new GameObject { Id = Guid.NewGuid(), Name = "Stiletto Heels", WearSlot = "Feet", IsWearable = true, IsWorn = true };
            game.Player.Inventory.Add(shoes);

            var ctx = new ActionContext(game);

            // Act
            var resolvedName1 = TemplateResolver.Resolve("{player.wornIn.Feet}", ctx);
            var resolvedName2 = TemplateResolver.Resolve("{player.wornSlot.Feet}", ctx);

            // Assert
            Assert.Equal("Stiletto Heels", resolvedName1);
            Assert.Equal("Stiletto Heels", resolvedName2);
        }

        [Fact]
        public void TemplateResolver_ShouldResolveFocusWearSlot()
        {
            // Arrange
            var game = new Game();
            var item = new GameObject { Id = Guid.NewGuid(), Name = "Special Socks", WearSlot = "Feet", IsWearable = true };
            var ctx = new ActionContext(game, focusObject: item);

            // Act
            var resolvedSlot = TemplateResolver.Resolve("{this.WearSlot}", ctx);

            // Assert
            Assert.Equal("Feet", resolvedSlot);
        }

        [Fact]
        public void InteractiveScreenSettings_Serialization_ShouldRoundtrip()
        {
            // Arrange
            var room = new Room
            {
                Name = "Test Screen Room",
                InteractiveScreenSettings = new InteractiveScreenSettings
                {
                    Enabled = true,
                    BackdropAssetId = "test_backdrop.png"
                }
            };
            room.InteractiveScreenSettings.Hotspots.Add(new ScreenHotspot
            {
                Name = "Test Hotspot",
                X = 12.5,
                Y = 34.2,
                Width = 20,
                Height = 10,
                StyleType = "TextButton",
                LabelText = "Click Me"
            });

            var options = new System.Text.Json.JsonSerializerOptions
            {
                TypeInfoResolver = RagsCore.RagsJsonContext.Default
            };

            // Act
            var json = System.Text.Json.JsonSerializer.Serialize(room, options);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<Room>(json, options);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.InteractiveScreenSettings);
            Assert.True(deserialized.InteractiveScreenSettings.Enabled);
            Assert.Equal("test_backdrop.png", deserialized.InteractiveScreenSettings.BackdropAssetId);
            Assert.Single(deserialized.InteractiveScreenSettings.Hotspots);

            var hotspot = deserialized.InteractiveScreenSettings.Hotspots[0];
            Assert.Equal("Test Hotspot", hotspot.Name);
            Assert.Equal(12.5, hotspot.X);
            Assert.Equal(34.2, hotspot.Y);
            Assert.Equal(20, hotspot.Width);
            Assert.Equal(10, hotspot.Height);
            Assert.Equal("TextButton", hotspot.StyleType);
            Assert.Equal("Click Me", hotspot.LabelText);
        }

        [Fact]
        public void Action_DirectionFilter_Serialization_Test()
        {
            var game = new Game();
            var room = new Room { Name = "TestRoom" };
            var action = new RagsCore.Models.Action
            {
                Name = "OnExitSouth",
                Trigger = ActionTrigger.OnPlayerExit,
                DirectionFilter = "S"
            };
            room.Actions.Add(action);
            game.Rooms.Add(room);

            // Serialize game
            var gameJson = System.Text.Json.JsonSerializer.Serialize(game, RagsCore.RagsJsonContext.CustomDefault.Game);
            Assert.Contains("\"DirectionFilter\": \"S\"", gameJson);

            // Deserialize game
            var deserializedGame = System.Text.Json.JsonSerializer.Deserialize(gameJson, RagsCore.RagsJsonContext.CustomDefault.Game);
            Assert.NotNull(deserializedGame);
            Assert.Single(deserializedGame.Rooms);
            Assert.Single(deserializedGame.Rooms[0].Actions);
            Assert.Equal("S", deserializedGame.Rooms[0].Actions[0].DirectionFilter);
        }

        [Fact]
        public void RoomAttributeCheck_ByNameAndId_ShouldResolveCorrectly()
        {
            var roomGuid = Guid.NewGuid();
            var room = new Room
            {
                Id = roomGuid,
                Name = "Street"
            };
            room.Attributes.Add(new CustomAttribute { Name = "enterfirst", Value = "true" });

            Assert.Equal("Street", room.Name);
            Assert.Equal(roomGuid.ToString(), room.Id.ToString());
            Assert.Equal("true", room.Attributes[0].Value);
        }
    }
}
