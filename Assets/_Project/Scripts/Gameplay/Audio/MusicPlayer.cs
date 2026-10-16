using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>Volume settings shared by music and sound effects (saved per device).</summary>
    public static class AudioPrefs
    {
        private static float? _music, _sfx;

        public static float Music
        {
            get => _music ??= PlayerPrefs.GetFloat("vol_music", 0.3f);
            set { _music = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_music", _music.Value); }
        }

        public static float Sfx
        {
            get => _sfx ??= PlayerPrefs.GetFloat("vol_sfx", 0.4f);
            set { _sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat("vol_sfx", _sfx.Value); }
        }

        /// <summary>Automated runs (bots, -autoquit, -mute; browser: ?botmove / ?mute) play silently.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void MuteAutomatedRuns()
        {
            if (CommandLine.Has("-mute") || CommandLine.Has("-autoquit") || CommandLine.Has("-botmove"))
                AudioListener.volume = 0f;
        }
    }

    /// <summary>
    /// Background music that survives scene loads: the camp theme in the lobby, each area's theme in a run,
    /// the boss theme while a boss lives, and short stingers at the end. Tracks crossfade.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        private const float FadeSeconds = 1.6f;

        private AudioSource _a, _b;
        private AudioSource _current;
        private string _track;
        private string _stinger;
        private float _stingerUntil;
        private AudioLowPassFilter _lowPass;
        private float _intensity = 1f;

        /// <summary>1 in a fight (your room fighting, or a lord alive), 0 in a quiet room; the camp and title count as 1.</summary>
        private static float WantedIntensity()
        {
            if (SceneManager.GetActiveScene().name != GameSession.GameScene) return 1f;
            var d = RunDirector.Instance;
            if (d == null || !d.IsSpawned || !d.IsRunning) return 1f;
            return d.LocalFighting || d.BossHealth.Value >= 0f ? 1f : 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            go.AddComponent<MusicPlayer>();
        }

        private void Awake()
        {
            _a = gameObject.AddComponent<AudioSource>();
            _b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _a, _b })
            {
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0f;
            }
            // Browsers have no audio filters: there the music only gets quieter between fights.
            if (Application.platform != RuntimePlatform.WebGLPlayer) _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _current = _a;
            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy() => RunDirector.EventReceived -= OnEvent;

        private void OnEvent(SimEvent e)
        {
            if (e.Type != SimEventType.RunEnded) return;
            Stinger(e.Int == 1 ? "music_victory" : "music_defeat");
        }

        private void Stinger(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip == null) return;
            _stinger = name;
            _stingerUntil = Time.unscaledTime + clip.length;
            var other = _current == _a ? _b : _a;
            other.clip = clip;
            other.loop = false;
            other.volume = AudioPrefs.Music;
            other.Play();
            _current.volume = 0f;
            _current.Stop();
            _current = other;
            _track = name;
        }

        private string WantedTrack()
        {
            if (SceneManager.GetActiveScene().name != GameSession.GameScene) return "music_camp";
            var d = RunDirector.Instance;
            if (d == null || !d.IsSpawned) return _track ?? "music_camp";
            if (d.Phase.Value != RunPhase.Playing) return _track;
            if (d.BossHealth.Value >= 0f) return "music_boss";
            return BiomeCatalog.Get(d.Biome).Music;
        }

        private void Update()
        {
            if (_stinger != null)
            {
                if (Time.unscaledTime < _stingerUntil + 1.5f && SceneManager.GetActiveScene().name == GameSession.GameScene) return;
                _stinger = null;
            }

            string wanted = WantedTrack();
            if (wanted != null && wanted != _track)
            {
                var clip = Resources.Load<AudioClip>("Audio/" + wanted);
                _track = wanted;
                if (clip != null)
                {
                    var next = _current == _a ? _b : _a;
                    next.clip = clip;
                    next.loop = true;
                    next.volume = 0f;
                    next.Play();
                    _current = next;
                }
            }

            // Between fights the music sinks back and goes muffled; it opens up when a fight starts.
            _intensity = Mathf.MoveTowards(_intensity, WantedIntensity(), Time.unscaledDeltaTime / 1.4f);
            if (_lowPass != null) _lowPass.cutoffFrequency = Mathf.Lerp(2200f, 22000f, _intensity * _intensity);
            float target = AudioPrefs.Music * Mathf.Lerp(0.7f, 1f, _intensity);
            // A lord's last stand: the music climbs a touch.
            var director = RunDirector.Instance;
            float pitch = director != null && director.IsSpawned && director.BossStage.Value >= 3 ? 1.045f : 1f;
            foreach (var s in new[] { _a, _b }) s.pitch = Mathf.MoveTowards(s.pitch, pitch, Time.unscaledDeltaTime * 0.03f);
            float step = Time.unscaledDeltaTime / FadeSeconds;
            foreach (var s in new[] { _a, _b })
            {
                float goal = s == _current ? target : 0f;
                s.volume = Mathf.MoveTowards(s.volume, goal, step * Mathf.Max(0.2f, target));
                if (s != _current && s.volume <= 0.001f && s.clip != null)
                {
                    // A faded-out track is let go: browsers keep every loaded track decoded in memory (tens of MB each).
                    var old = s.clip;
                    s.Stop();
                    s.clip = null;
                    if (old != _current.clip) Resources.UnloadAsset(old);
                }
            }
        }
    }
}
