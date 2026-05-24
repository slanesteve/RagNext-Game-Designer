using System.Collections.Generic;
using UnityEngine;

namespace RagNextPlayer.Managers
{
    /// <summary>
    /// Simple audio manager that loads sound clips from Resources/Audio
    /// and plays them through pooled AudioSources.
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

        public void PlaySound(string soundId, float volume = 1f)
        {
            if (string.IsNullOrWhiteSpace(soundId)) return;

            if (!_cache.TryGetValue(soundId, out var clip))
            {
                clip = Resources.Load<AudioClip>($"Audio/{soundId}");
                if (clip is null)
                {
                    Debug.LogWarning($"[AudioManager] Clip not found: Audio/{soundId}");
                    return;
                }
                _cache[soundId] = clip;
            }

            var src = GetFreeSource();
            if (src is null) return;

            src.clip   = clip;
            src.volume = Mathf.Clamp01(volume);
            src.Play();
        }

        private AudioSource? GetFreeSource()
        {
            foreach (var s in _pool)
                if (!s.isPlaying) return s;
            return _pool.Count > 0 ? _pool[0] : null; // fallback: interrupt oldest
        }
    }
}
