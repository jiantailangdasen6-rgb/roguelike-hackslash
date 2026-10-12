using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    public enum UiCue : byte
    {
        Hover,  // the cursor or the selection lands on a button
        Click,  // a button is pressed
        Open,   // a panel opens
        Close,  // a panel closes
        Page,   // a tab or a card turns
    }

    /// <summary>
    /// Interface sounds, in every scene (title, camp, run): soft synthesized ticks and chimes under the effects
    /// volume. Lives across scene loads like the music.
    /// </summary>
    public sealed class UiSound : MonoBehaviour
    {
        private static UiSound _instance;
        private AudioSource _source;
        private readonly Dictionary<UiCue, (AudioClip Clip, float Volume)> _cues = new();
        private readonly Dictionary<UiCue, float> _last = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode) return;
            var go = new GameObject("UiSound");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UiSound>();
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _cues[UiCue.Hover] = (SfxSynth.Chime("ui_hover", new[] { 1760f }, 0f, 0.05f), 0.12f);
            _cues[UiCue.Click] = (SfxSynth.Chime("ui_click", new[] { 880f, 1320f }, 0.03f, 0.12f), 0.3f);
            _cues[UiCue.Open] = (SfxSynth.Chime("ui_open", new[] { 523f, 784f, 1047f }, 0.04f, 0.3f), 0.28f);
            _cues[UiCue.Close] = (SfxSynth.Chime("ui_close", new[] { 784f, 523f }, 0.05f, 0.22f), 0.22f);
            _cues[UiCue.Page] = (SfxSynth.Chime("ui_page", new[] { 1175f, 1568f }, 0.025f, 0.12f), 0.18f);
        }

        public static void Play(UiCue cue)
        {
            if (_instance == null || !_instance._cues.TryGetValue(cue, out var c) || c.Clip == null) return;
            // The same cue can't stack up within a few frames (selection moving fast, several cards).
            if (_instance._last.TryGetValue(cue, out float last) && Time.unscaledTime - last < 0.04f) return;
            _instance._last[cue] = Time.unscaledTime;
            _instance._source.pitch = Random.Range(0.97f, 1.03f);
            _instance._source.PlayOneShot(c.Clip, c.Volume * AudioPrefs.Sfx);
        }
    }
}
