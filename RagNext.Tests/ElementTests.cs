using System;
using System.Collections.ObjectModel;
using Xunit;
using RagsCore.Models;

namespace RagNext.Tests
{
    public class ElementTests
    {
        [Fact]
        public void Room_ExitsAndAttributes_ShouldModifyAndPersist()
        {
            // Arrange
            var room = new Room();
            var targetRoomId = Guid.NewGuid();

            // Act: exits
            room.Exits["North"] = targetRoomId;
            room.LockedExits["North"] = true;

            // Act: attributes
            CustomAttribute.SetAttribute("LightLevel", "Dark", room.Attributes);

            // Assert
            Assert.Equal(targetRoomId, room.Exits["North"]);
            Assert.True(room.LockedExits["North"]);
            Assert.Equal("Dark", CustomAttribute.GetAttribute("LightLevel", room.Attributes));
        }

        [Fact]
        public void Character_InventoryAndAttributes_ShouldModifyAndPersist()
        {
            // Arrange
            var character = new Character();
            var itemId = Guid.NewGuid();

            // Act: inventory
            character.ContainedObjectIds.Add(itemId);

            // Act: attributes
            CustomAttribute.SetAttribute("HP", "100", character.Attributes);

            // Assert
            Assert.Contains(itemId, character.ContainedObjectIds);
            Assert.Equal("100", CustomAttribute.GetAttribute("HP", character.Attributes));
        }
    }
}
