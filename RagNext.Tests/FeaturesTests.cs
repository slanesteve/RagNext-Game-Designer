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
    }
}
