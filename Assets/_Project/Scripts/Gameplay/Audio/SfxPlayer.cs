using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Plays the procedural sounds for simulation events, with distance falloff from the local player,
    /// slight pitch variation and per-sound rate limits. Also loops the crypt ambience.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        private const int Voices = 16;
        private const float HearingRange = 16f;

        private sealed class Sound
        {
            public AudioClip Clip;
            public float Volume;
            public float MinInterval;
            public float LastPlayed = -10f;
        }

        private readonly Dictionary<string, Sound> _sounds = new();
        private readonly AudioSource[] _voices = new AudioSource[Voices];
        private int _nextVoice;
        private AudioSource _ambient;

        private void Awake()
        {
            Add("swing", SfxSynth.Swing(), 0.35f, 0.04f);
            Add("hit", SfxSynth.Hit(), 0.45f, 0.03f);
            Add("heavyhit", SfxSynth.HeavyHit(), 0.6f, 0.05f);
            Add("bolt", SfxSynth.Bolt(), 0.3f, 0.05f);
            Add("arrow", SfxSynth.Arrow(), 0.35f, 0.04f);
            Add("shatter", SfxSynth.Shatter(), 0.8f, 0.08f);
            Add("death", SfxSynth.Death(), 0.45f, 0.06f);
            Add("hurt", SfxSynth.Hurt(), 0.6f, 0.1f);
            Add("slam", SfxSynth.Slam(), 0.8f, 0.15f);
            Add("telegraph", SfxSynth.Telegraph(), 0.22f, 0.12f);
            Add("warcry", SfxSynth.WarCry(), 0.5f, 0.3f);
            Add("nova", SfxSynth.Nova(), 0.5f, 0.2f);
            Add("heal", SfxSynth.Chime("heal", new[] { 523f, 659f, 784f, 1046f }, 0.06f, 0.8f), 0.4f, 0.3f);
            Add("levelup", SfxSynth.Chime("levelup", new[] { 392f, 523f, 659f, 784f }, 0.09f, 1f), 0.5f, 0.5f);
            Add("clear", SfxSynth.Chime("clear", new[] { 262f, 330f, 392f, 523f, 659f }, 0.12f, 1.4f), 0.5f, 1f);
            Add("soul", SfxSynth.Soul(), 0.12f, 0.05f);

            for (int i = 0; i < Voices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
            }

            _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.clip = SfxSynth.Ambient();
            _ambient.loop = true;
            _ambient.volume = 0.22f;
            _ambient.Play();
        }

        private void Add(string name, AudioClip clip, float volume, float minInterval) =>
            _sounds[name] = new Sound { Clip = clip, Volume = volume, MinInterval = minInterval };

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        public void Play(string name, Vector2? at = null, float pitch = 1f, float volume = 1f)
        {
            if (!_sounds.TryGetValue(name, out var s)) return;
            if (Time.unscaledTime - s.LastPlayed < s.MinInterval) return;

            float falloff = 1f;
            var local = NetPlayer.Local;
            if (at.HasValue && local != null)
                falloff = Mathf.Clamp01(1f - ((Vector2)local.transform.position - at.Value).magnitude / HearingRange);
            if (falloff <= 0.02f) return;

            s.LastPlayed = Time.unscaledTime;
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            voice.pitch = pitch * Random.Range(0.93f, 1.07f);
            voice.PlayOneShot(s.Clip, s.Volume * volume * falloff);
        }

        private static ClassId? ClassOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return p.Class.Value;
            return null;
        }

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.PlayerAttack:
                {
                    var anim = (AttackAnim)e.Int;
                    var cls = ClassOf(e.ClientId);
                    if (anim == AttackAnim.Dodge) Play("swing", e.Pos, 0.7f, 0.6f);
                    else if (anim == AttackAnim.Skill2) break; // the area effect plays its own sound
                    else if (cls == ClassId.Mage) Play(anim == AttackAnim.Skill1 ? "nova" : "bolt", e.Pos, anim == AttackAnim.Skill1 ? 0.8f : 1f);
                    else if (cls == ClassId.Ranger) Play("arrow", e.Pos, anim == AttackAnim.Skill1 ? 0.75f : 1f, anim == AttackAnim.Skill1 ? 1.4f : 1f);
                    else Play("swing", e.Pos, anim == AttackAnim.Skill1 ? 0.75f : anim == AttackAnim.BasicB ? 1.1f : 1f, anim == AttackAnim.Skill1 ? 1.3f : 1f);
                    break;
                }
                case SimEventType.EnemyHit:
                {
                    var flags = (HitFlags)e.Int;
                    if ((flags & HitFlags.DamageOverTime) != 0) break;
                    if ((flags & HitFlags.Shatter) != 0) Play("shatter", e.Pos);
                    else Play((flags & HitFlags.Heavy) != 0 ? "heavyhit" : "hit", e.Pos, (flags & HitFlags.Crit) != 0 ? 1.25f : 1f);
                    break;
                }
                case SimEventType.EnemyDied: Play("death", e.Pos, e.Int == (int)EnemyType.BoneKing ? 0.5f : 1f); break;
                case SimEventType.EnemyTelegraph: Play("telegraph", e.Pos, e.Int == (int)AttackShape.Line ? 1.2f : 1f); break;
                case SimEventType.EnemyStrike:
                    if ((AttackKind)e.Int == AttackKind.Projectile) Play("arrow", e.Pos, 0.6f, 0.7f);
                    else if ((AttackKind)e.Int == AttackKind.Melee) Play("swing", e.Pos, 0.6f);
                    break;
                case SimEventType.AreaEffect:
                    switch ((AreaKind)e.Int)
                    {
                        case AreaKind.FrostNova: Play("nova", e.Pos, 1.2f); break;
                        case AreaKind.Heal: Play("heal", e.Pos); break;
                        case AreaKind.WarCry: Play("warcry", e.Pos); break;
                        case AreaKind.Mark: Play("bolt", e.Pos, 0.5f); break;
                        case AreaKind.Slam: Play("slam", e.Pos); break;
                    }
                    break;
                case SimEventType.PlayerHurt:
                    if (NetPlayer.Local != null && e.ClientId == NetPlayer.Local.OwnerClientId) Play("hurt");
                    else Play("hit", e.Pos, 0.7f);
                    break;
                case SimEventType.PlayerDowned: Play("slam", e.Pos, 1.4f, 0.6f); break;
                case SimEventType.PlayerRevived: Play("heal", e.Pos, 1.2f); break;
                case SimEventType.SoulCollected:
                    if (NetPlayer.Local != null && e.ClientId == NetPlayer.Local.OwnerClientId) Play("soul", null, 1f + Random.value * 0.3f);
                    break;
                case SimEventType.PerkOffer:
                    if (NetPlayer.Local != null && e.ClientId == NetPlayer.Local.OwnerClientId) Play("levelup");
                    break;
                case SimEventType.RoomCleared: Play("clear"); break;
                case SimEventType.BossSpawned: Play("slam", null, 0.6f, 1.2f); break;
                case SimEventType.SpawnWarning: Play("telegraph", e.Pos, 0.6f, 0.6f); break;
            }
        }
    }
}
