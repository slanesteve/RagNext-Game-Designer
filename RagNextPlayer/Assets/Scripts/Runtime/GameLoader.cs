#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RagNextPlayer.Runtime.Models;
using UnityEngine;

namespace RagNextPlayer.Runtime
{
    /// <summary>
    /// Loads a game.json file from StreamingAssets into a GameData object.
    /// Mirrors GameLoader.cs from the MAUI player, adapted for Unity's file API.
    /// </summary>
    public static class GameLoader
    {
        private static readonly JsonSerializerSettings _settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Models.ActionStepConverter(), new Models.ActionStepListConverter() }
        };

        /// <summary>
        /// Loads game.json from Application.streamingAssetsPath.
        /// On Android this must be done via UnityWebRequest; all other platforms
        /// can read the file directly.
        /// </summary>
        public static async Task<GameData?> LoadFromStreamingAssetsAsync(string fileName = "game.json")
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: StreamingAssets lives inside the APK — must use WebRequest
            return await LoadViaWebRequestAsync(path);
#else
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[GameLoader] game.json not found at: {path}");
                return null;
            }
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameLoader] Failed to read {path}: {ex.Message}");
                return null;
            }
#endif
        }

        /// <summary>
        /// Loads from an arbitrary absolute file path (useful for desktop dev
        /// when pointing at the Designer's live export folder).
        /// </summary>
        public static async Task<GameData?> LoadFromFileAsync(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                Debug.LogWarning($"[GameLoader] File not found: {absolutePath}");
                return null;
            }
            try
            {
                var json = await File.ReadAllTextAsync(absolutePath);
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameLoader] Failed to load {absolutePath}: {ex.Message}");
                return null;
            }
        }

        public static GameData? Deserialize(string json)
        {
            try
            {
                var game = JsonConvert.DeserializeObject<GameData>(json, _settings);
                if (game != null)
                {
                    if (game.Objects != null)
                    {
                        foreach (var obj in game.Objects)
                        {
                            if (obj != null && !obj.Properties.ContainsKey("OriginalName"))
                            {
                                obj.Properties["OriginalName"] = obj.Name;
                            }
                        }
                    }
                    if (game.Characters != null)
                    {
                        foreach (var ch in game.Characters)
                        {
                            if (ch != null && !ch.Properties.ContainsKey("OriginalName"))
                            {
                                ch.Properties["OriginalName"] = ch.Name;
                            }
                        }
                    }
                }
                return game;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameLoader] JSON parse error: {ex.Message}");
                return null;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async Task<GameData?> LoadViaWebRequestAsync(string uri)
        {
            using var req = UnityEngine.Networking.UnityWebRequest.Get(uri);
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GameLoader] WebRequest failed: {req.error}");
                return null;
            }
            return Deserialize(req.downloadHandler.text);
        }
#endif
    }
}
