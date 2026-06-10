using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RagsCore.Models;
using RagsCore.Actions;

namespace RagsCore.Services
{
    public static class GlobalActionLibraryService
    {
        private static string GetFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "RagNext");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "GlobalActionLibrary.json");
        }

        public static List<RagsCore.Models.Action> LoadLibrary()
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                var defaults = GetDefaultActions();
                SaveLibrary(defaults);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, RagsJsonContext.CustomDefault.ListAction) ?? GetDefaultActions();
            }
            catch
            {
                return GetDefaultActions();
            }
        }

        public static void SaveLibrary(List<RagsCore.Models.Action> actions)
        {
            var path = GetFilePath();
            try
            {
                var json = JsonSerializer.Serialize(actions, RagsJsonContext.CustomDefault.ListAction);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save global action library: {ex}");
            }
        }

        public static List<RagsCore.Models.Action> GetDefaultActions()
        {
            var examine = new RagsCore.Models.Action
            {
                Name = "Examine",
                Trigger = ActionTrigger.UserClicked,
                ApplyToRooms = true,
                ApplyToCharacters = true,
                ApplyToGrabableObjects = true,
                ApplyToStaticObjects = true
            };
            examine.Nodes.Add(new DisplayTextCommand { Text = "{this.Description}" });

            var take = new RagsCore.Models.Action
            {
                Name = "Take",
                Trigger = ActionTrigger.UserClicked,
                ApplyToGrabableObjects = true
            };
            take.Nodes.Add(new ObjectMoveToInventoryCommand { ObjectId = "{this.Id}" });
            take.Nodes.Add(new DisplayTextCommand { Text = "You take the {this.Name}." });

            var drop = new RagsCore.Models.Action
            {
                Name = "Drop",
                Trigger = ActionTrigger.UserClicked,
                ApplyToGrabableObjects = true
            };
            drop.Nodes.Add(new AddObjectToRoomCommand { RoomId = "{Player.CurrentRoomId}", ObjectId = "{this.Id}" });
            drop.Nodes.Add(new DisplayTextCommand { Text = "You drop the {this.Name}." });

            var wear = new RagsCore.Models.Action
            {
                Name = "Wear",
                Trigger = ActionTrigger.UserClicked,
                ApplyToGrabableObjects = true
            };
            wear.Nodes.Add(new SetItemAttributeCommand { ItemId = "{this.Id}", AttributeName = "Worn", Value = "True" });
            wear.Nodes.Add(new DisplayTextCommand { Text = "You put on the {this.Name}." });

            return new List<RagsCore.Models.Action> { examine, take, drop, wear };
        }
    }
}
