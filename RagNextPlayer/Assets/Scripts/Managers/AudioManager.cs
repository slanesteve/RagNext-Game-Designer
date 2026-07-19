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
        private AudioSource _musicSource;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;

            for (int i = 0; i < _sourcePoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _pool.Add(src);
            }
        }

        private readonly Dictionary<AudioSource, string> _sourcePaths = new();
        private readonly Dictionary<AudioSource, string> _sourceIds = new();
        private readonly Dictionary<AudioSource, bool> _sourceLooping = new();
        public string? CurrentMusicId { get; private set; }
        private float _musicVolume = 1f;
        private bool _musicLoop = true;
        private float _musicStartTime = 0f;
        private float _musicEndTime = 0f;
        private Coroutine _musicEndCoroutine;

        public void PlaySound(string soundId, float volume = 1f, bool loop = false, float startTime = 0f, float endTime = 0f)
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
                PlayClip(clip, volume, loop, path, soundId, startTime, endTime);
            }
            else
            {
                StartCoroutine(LoadAndPlayAudioRoutine(path, soundId, volume, loop, startTime, endTime));
            }
        }

        private void PlayClip(AudioClip clip, float volume, bool loop, string path, string soundId, float startTime = 0f, float endTime = 0f)
        {
            var src = GetFreeSource();
            if (src is null) return;

            src.clip   = clip;
            src.volume = Mathf.Clamp01(volume);
            src.loop   = loop && (startTime <= 0f && endTime <= 0f);
            
            if (startTime > 0f && startTime < clip.length)
            {
                src.time = startTime;
            }
            else
            {
                src.time = 0f;
            }

            src.Play();
            _sourcePaths[src] = path;
            _sourceIds[src]   = soundId;
            _sourceLooping[src] = loop;

            if (startTime > 0f || (endTime > 0f && endTime < clip.length))
            {
                StartCoroutine(MonitorTrimmedPlaybackRoutine(src, clip, startTime, endTime, loop));
            }
        }

        private System.Collections.IEnumerator MonitorTrimmedPlaybackRoutine(AudioSource src, AudioClip clip, float startTime, float endTime, bool loop)
        {
            float targetEnd = (endTime > 0f && endTime < clip.length) ? endTime : clip.length;
            while (src != null && src.isPlaying && src.clip == clip)
            {
                if (src.time >= targetEnd)
                {
                    if (loop)
                    {
                        src.time = (startTime > 0f && startTime < clip.length) ? startTime : 0f;
                    }
                    else
                    {
                        src.Stop();
                        src.clip = null;
                        yield break;
                    }
                }
                yield return null;
            }
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
            StopMusic();
        }


        public void PlayMusic(string soundId, float volume = 1f, bool loop = true, float startTime = 0f, float endTime = 0f)
        {
            if (string.IsNullOrWhiteSpace(soundId)) return;
            CurrentMusicId = soundId;
            string path = soundId;
            _musicVolume = volume;
            _musicLoop = loop;
            _musicStartTime = startTime;
            _musicEndTime = endTime;
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
                PlayMusicClip(clip);
            }
            else
            {
                StartCoroutine(LoadAndPlayMusicRoutine(path));
            }
        }

        private void PlayMusicClip(AudioClip clip)
        {
            if (_musicSource == null) return;
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;
            if (_musicEndCoroutine != null) { StopCoroutine(_musicEndCoroutine); _musicEndCoroutine = null; }
            _musicSource.clip = clip;
            _musicSource.volume = _musicVolume;
            _musicSource.loop = _musicLoop;
            _musicSource.time = Mathf.Clamp(_musicStartTime, 0f, clip.length - 0.1f);
            _musicSource.Play();
            if (_musicEndTime > 0f && _musicEndTime > _musicStartTime)
            {
                _musicEndCoroutine = StartCoroutine(MusicEndRoutine(_musicStartTime, _musicEndTime));
            }
        }

        private System.Collections.IEnumerator MusicEndRoutine(float startTime, float endTime)
        {
            float duration = endTime - startTime;
            yield return new WaitForSeconds(duration);
            if (_musicSource != null && _musicSource.isPlaying)
            {
                if (_musicLoop)
                {
                    // Loop back to start point
                    _musicSource.time = Mathf.Clamp(startTime, 0f, _musicSource.clip.length - 0.1f);
                    _musicEndCoroutine = StartCoroutine(MusicEndRoutine(startTime, endTime));
                }
                else
                {
                    _musicSource.Stop();
                }
            }
        }

        private System.Collections.IEnumerator LoadAndPlayMusicRoutine(string path)
        {
            string url = FormatLocalPathForWeb(path);
            if (string.IsNullOrEmpty(url)) yield break;

            AudioType audioType = AudioType.UNKNOWN;
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".mp3") audioType = AudioType.MPEG;
            else if (ext == ".wav") audioType = AudioType.WAV;
            else if (ext == ".ogg") audioType = AudioType.OGGVORBIS;

            using var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null)
                {
                    _cache[path] = clip;
                    PlayMusicClip(clip);
                }
            }
            else
            {
                var clip = Resources.Load<AudioClip>($"Audio/{path}");
                if (clip != null)
                {
                    _cache[path] = clip;
                    PlayMusicClip(clip);
                }
            }
        }

        public void StopMusic()
        {
            CurrentMusicId = null;
            if (_musicSource != null && _musicSource.isPlaying)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
            }
        }

        public List<string> GetPlayingLoopingSoundIds()
        {
            var list = new List<string>();
            foreach (var src in _pool)
            {
                bool isLooping = false;
                _sourceLooping.TryGetValue(src, out isLooping);
                if (src.isPlaying && isLooping && _sourceIds.TryGetValue(src, out var id))
                {
                    if (!list.Contains(id))
                    {
                        list.Add(id);
                    }
                }
            }
            return list;
        }

        private System.Collections.IEnumerator LoadAndPlayAudioRoutine(string path, string soundId, float volume, bool loop, float startTime = 0f, float endTime = 0f)
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
                    PlayClip(clip, volume, loop, path, soundId, startTime, endTime);
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
                    PlayClip(clip, volume, loop, path, soundId, startTime, endTime);
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

        public float GetLoudnessOfSound(string soundId)
        {
            if (string.IsNullOrWhiteSpace(soundId)) return 0f;
            foreach (var src in _pool)
            {
                if (src != null && src.isPlaying && _sourceIds.TryGetValue(src, out var id) && string.Equals(id, soundId, StringComparison.OrdinalIgnoreCase))
                {
                    float[] samples = new float[64];
                    src.GetOutputData(samples, 0);
                    float sum = 0f;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        sum += samples[i] * samples[i];
                    }
                    return Mathf.Sqrt(sum / samples.Length);
                }
            }
            return 0f;
        }

        private AudioSource? GetFreeSource()
        {
            foreach (var s in _pool)
                if (!s.isPlaying) return s;
            return _pool.Count > 0 ? _pool[0] : null; // fallback: interrupt oldest
        }
    }
}
