using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using RagsCore;
using RagsCore.Models;

namespace Scratch
{
    class Program
    {
        static void Main()
        {
            try
            {
                string savesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", "saves");
                string path = Path.Combine(savesDir, "My New Adventure.json");
                if (!File.Exists(path))
                {
                    Console.WriteLine($"Save file not found at: {path}");
                    return;
                }

                Console.WriteLine($"Loading save file: {path}");
                string json = File.ReadAllText(path);
                var game = JsonSerializer.Deserialize(json, RagsJsonContext.CustomDefault.Game);
                Console.WriteLine("Loaded successfully!");

                if (game == null)
                {
                    Console.WriteLine("Game is null after deserialization!");
                    return;
                }

                Console.WriteLine($"Rooms count: {game.Rooms.Count}");
                if (game.Rooms.Count == 0)
                {
                    Console.WriteLine("No rooms in the game!");
                    return;
                }

                var targetRoom = game.Rooms.FirstOrDefault(r => r.Name.Contains("Bridge")) ?? game.Rooms[0];
                Console.WriteLine($"Setting StartingRoom to: {targetRoom.Name} (ID: {targetRoom.Id})");
                game.Player.StartingRoom = targetRoom;

                Console.WriteLine("Serializing...");
                string outputJson = JsonSerializer.Serialize(game, RagsJsonContext.CustomDefault.Game);
                
                Console.WriteLine("Checking if StartingRoom is in the serialized output...");
                if (outputJson.Contains("\"StartingRoom\""))
                {
                    Console.WriteLine("StartingRoom IS present in the JSON!");
                    // Find the StartingRoom chunk in JSON
                    int idx = outputJson.IndexOf("\"StartingRoom\"");
                    int length = Math.Min(outputJson.Length - idx, 300);
                    Console.WriteLine("Snippet:");
                    Console.WriteLine(outputJson.Substring(idx, length));
                }
                else
                {
                    Console.WriteLine("StartingRoom is MISSING in the JSON!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nEXCEPTION:");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
