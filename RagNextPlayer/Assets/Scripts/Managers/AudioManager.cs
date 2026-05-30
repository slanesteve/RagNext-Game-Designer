using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace RagNextPlayer.Managers
{
    /// <summary>
    /// Audio manager that dynamically loads sound clips from StreamingAssets/Assets
    /// via UnityWebRequestMultimedia, falling back to static Resources/Audio.
    /// Plays them through pooled AudioSources.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private int _sourcePoolSize = 6;

        private readonly List<AudioSource> _pool = new();
        private readonly Dictionary<string, AudioClip> _cache = new();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            for (int i = 0; i < _sourcePoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _pool.Add(src);
            }
        }

        private readonly Dictionary<AudioSource, string> _sourcePaths = new();

        public void PlaySound(string soundId, float volume = 1f, bool loop = false)
        {
            if (string.IsNullOrWhiteSpace(soundId)) return;

            // 1. Resolve path from Game.MediaAssets if possible
            string path = soundId;
            var game = GameManager.Instance?.ActiveGame;
            if (game != null)
            {
                var asset = game.MediaAssets.Find(a => 
                    string.Equals(a.Id, soundId, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(a.OriginalFileName, soundId, StringComparison.OrdinalIgnoreCase));
                if (asset != null)
                {
                    path = asset.RelativePath;
                }
            }

            if (_cache.TryGetValue(path, out var clip))
            {
                PlayClip(clip, volume, loop, path);
            }
            else
            {
                StartCoroutine(LoadAndPlayAudioRoutine(path, volume, loop));
            }
        }

        private void PlayClip(AudioClip clip, float volume, bool loop, string path)
        {
            var src = GetFreeSource();
            if (src is null) return;

            src.clip   = clip;
            src.volume = Mathf.Clamp01(volume);
            src.loop   = loop;
            src.Play();
            _sourcePaths[src] = path;
        }

        public void StopSound(string soundId)
        {
            if (string.IsNullOrWhiteSpace(soundId)) return;

            string path = soundId;
            var game = GameManager.Instance?.ActiveGame;
            if (game != null)
            {
                var asset = game.MediaAssets.Find(a => 
                    string.Equals(a.Id, soundId, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(a.OriginalFileName, soundId, StringComparison.OrdinalIgnoreCase));
                if (asset != null)
                {
                    path = asset.RelativePath;
                }
            }

            foreach (var src in _pool)
            {
                if (src.isPlaying && _sourcePaths.TryGetValue(src, out var p) && string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
                {
                    src.Stop();
                    src.clip = null;
                    src.loop = false;
                }
            }
        }

        public void StopAllLoopingSounds()
        {
            foreach (var src in _pool)
            {
                if (src.isPlaying && src.loop)
                {
                    src.Stop();
                    src.clip = null;
                    src.loop = false;
                }
            }
        }

        public void StopAllSounds()
        {
            foreach (var src in _pool)
            {
                src.Stop();
                src.clip = null;
                src.loop = false;
            }
        }

        private System.Collections.IEnumerator LoadAndPlayAudioRoutine(string path, float volume, bool loop)
        {
            string url = FormatLocalPathForWeb(path);
            if (string.IsNullOrEmpty(url)) yield break;

            AudioType audioType = AudioType.UNKNOWN;
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".mp3") audioType = AudioType.MPEG;
            else if (ext == ".wav") audioType = AudioType.WAV;
            else if (ext == ".ogg") audioType = AudioType.OGGVORBIS;

            Debug.Log($"[AudioManager] Loading dynamic audio clip from URL: '{url}'");
            using var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null)
                {
                    _cache[path] = clip;
                    PlayClip(clip, volume, loop, path);
                    Debug.Log($"[AudioManager] Successfully loaded and played dynamic audio clip: '{path}'");
                }
            }
            else
            {
                // Fallback to static Resources
                var clip = Resources.Load<AudioClip>($"Audio/{path}");
                if (clip != null)
                {
                    _cache[path] = clip;
                    PlayClip(clip, volume, loop, path);
                }
                else
                {
                    Debug.LogError($"[AudioManager] Failed to load audio clip from URL '{url}': {req.error}");
                }
            }
        }

        private string FormatLocalPathForWeb(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            if (path.StartsWith("file://") || path.StartsWith("http://") || path.StartsWith("https://"))
                return path;

            string fullPath = path;
            if (!Path.IsPathRooted(path))
            {
                fullPath = Path.Combine(Application.streamingAssetsPath, path);
            }
            else
            {
                var fileName = Path.GetFileName(path);
                var streamingLocalPath = Path.Combine(Application.streamingAssetsPath, "Assets", fileName);
                if (File.Exists(streamingLocalPath))
                {
                    fullPath = streamingLocalPath;
                }
            }

            fullPath = fullPath.Replace("\\", "/");
            if (!fullPath.StartsWith("/"))
                return "file:///" + fullPath;
            else
                return "file://" + fullPath;
        }

        private AudioSource? GetFreeSource()
        {
            foreach (var s in _pool)
                if (!s.isPlaying) return s;
            return _pool.Count > 0 ? _pool[0] : null; // fallback: interrupt oldest
        }
    }
}
