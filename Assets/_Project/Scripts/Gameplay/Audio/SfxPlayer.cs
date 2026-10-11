using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Plays sounds for simulation events, with distance falloff from the local player, slight pitch variation
    /// and per-sound rate limits. Clips are rendered offline into Resources/Audio (Editor/Audio); if one is
    /// missing the runtime synth stands in. Also loops a quiet crypt ambience under the music.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        private const int Voices = 20;
        private const float HearingRange = 16f;

        public static SfxPlayer Instance { get; private set; }

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
            Instance = this;
            Add("swing", 0.4f, 0.04f, SfxSynth.Swing);
            Add("hit", 0.5f, 0.03f, SfxSynth.Hit);
            Add("heavyhit", 0.6f, 0.05f, SfxSynth.HeavyHit);
            Add("bolt", 0.32f, 0.05f, SfxSynth.Bolt);
            Add("arrow", 0.38f, 0.04f, SfxSynth.Arrow);
            Add("shatter", 0.8f, 0.08f, SfxSynth.Shatter);
            Add("death", 0.45f, 0.06f, SfxSynth.Death);
            Add("hurt", 0.6f, 0.1f, SfxSynth.Hurt);
            Add("slam", 0.75f, 0.15f, SfxSynth.Slam);
            Add("telegraph", 0.25f, 0.12f, SfxSynth.Telegraph);
            Add("warcry", 0.55f, 0.3f, SfxSynth.WarCry);
            Add("nova", 0.5f, 0.2f, SfxSynth.Nova);
            Add("heal", 0.45f, 0.3f, () => SfxSynth.Chime("heal", new[] { 523f, 659f, 784f, 1046f }, 0.06f, 0.8f));
            Add("levelup", 0.5f, 0.5f, () => SfxSynth.Chime("levelup", new[] { 392f, 523f, 659f, 784f }, 0.09f, 1f));
            Add("clear", 0.5f, 1f, () => SfxSynth.Chime("clear", new[] { 262f, 330f, 392f, 523f, 659f }, 0.12f, 1.4f));
            Add("soul", 0.12f, 0.05f, SfxSynth.Soul);
            Add("explosion", 0.85f, 0.1f, SfxSynth.Slam);
            Add("spikes", 0.4f, 0.1f, SfxSynth.HeavyHit);
            Add("flame", 0.45f, 0.3f, SfxSynth.Nova);
            Add("clank", 0.35f, 0.06f, SfxSynth.Hit);
            Add("blink", 0.45f, 0.15f, SfxSynth.Swing);
            Add("orb", 0.3f, 0.08f, SfxSynth.Bolt);
            Add("screech", 0.22f, 0.25f, SfxSynth.Bolt);
            Add("roar", 0.8f, 1f, SfxSynth.WarCry);
            Add("downed", 0.6f, 0.3f, SfxSynth.Slam);
            Add("revive", 0.55f, 0.3f, () => SfxSynth.Chime("revive", new[] { 392f, 523f, 659f }, 0.08f, 0.9f));
            Add("fuse", 0.4f, 0.2f, SfxSynth.Telegraph);
            Add("click", 0.3f, 0.03f, SfxSynth.Hit);

            for (int i = 0; i < Voices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
            }

            _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.clip = SfxSynth.Ambient();
            _ambient.loop = true;
            _ambient.volume = 0.1f * AudioPrefs.Sfx;
            _ambient.Play();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Add(string name, float volume, float minInterval, Func<AudioClip> fallback)
        {
            var clip = Resources.Load<AudioClip>("Audio/sfx_" + name) ?? fallback();
            _sounds[name] = new Sound { Clip = clip, Volume = volume, MinInterval = minInterval };
        }

        private void OnEnable() => RunDirector.EventReceived += OnEvent;
        private void OnDisable() => RunDirector.EventReceived -= OnEvent;

        public void Play(string name, Vector2? at = null, float pitch = 1f, float volume = 1f)
        {
            if (!_sounds.TryGetValue(name, out var s) || s.Clip == null) return;
            if (Time.unscaledTime - s.LastPlayed < s.MinInterval) return;

            float falloff = 1f;
            var local = NetPlayer.Local;
            if (at.HasValue && local != null)
                falloff = Mathf.Clamp01(1f - ((Vector2)local.transform.position - at.Value).magnitude / HearingRange);
            if (falloff <= 0.02f) return;

            s.LastPlayed = Time.unscaledTime;
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            voice.pitch = pitch * UnityEngine.Random.Range(0.94f, 1.06f);
            voice.PlayOneShot(s.Clip, s.Volume * volume * falloff * AudioPrefs.Sfx);
        }

        private static ClassId? ClassOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return p.Class.Value;
            return null;
        }

        private static bool IsLocal(ulong clientId) => NetPlayer.Local != null && NetPlayer.Local.OwnerClientId == clientId;

        private void Update() => _ambient.volume = 0.1f * AudioPrefs.Sfx;

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
                    else if (cls == ClassId.Alchemist) Play("swing", e.Pos, anim == AttackAnim.Skill1 ? 1.2f : 1.5f, 0.6f); // a flask whooshing off
                    else if (cls == ClassId.Assassin) Play(anim == AttackAnim.Skill1 ? "blink" : "swing", e.Pos, anim == AttackAnim.Skill1 ? 1f : 1.35f, 0.8f);
                    else if (cls == ClassId.Guardian) Play("swing", e.Pos, anim == AttackAnim.Skill1 ? 0.6f : 0.75f, 1.2f);
                    else Play("swing", e.Pos, anim == AttackAnim.Skill1 ? 0.75f : anim == AttackAnim.BasicB ? 1.1f : 1f, anim == AttackAnim.Skill1 ? 1.3f : 1f);
                    break;
                }
                case SimEventType.EnemyHit:
                {
                    var flags = (HitFlags)e.Int;
                    if ((flags & HitFlags.DamageOverTime) != 0) break;
                    if ((flags & HitFlags.Shatter) != 0) Play("shatter", e.Pos);
                    else if ((flags & HitFlags.Blocked) != 0) Play("clank", e.Pos);
                    else Play((flags & HitFlags.Heavy) != 0 ? "heavyhit" : "hit", e.Pos, (flags & HitFlags.Crit) != 0 ? 1.25f : 1f);
                    break;
                }
                case SimEventType.EnemyDied:
                {
                    var type = (EnemyType)e.Int;
                    if (type == EnemyType.PowderKeg) break; // the explosion follows
                    if (type == EnemyType.TreasureChest && e.Value < 0.5f)
                    {
                        Play("levelup", e.Pos, 1.3f, 0.7f);
                        Play("clank", e.Pos, 0.8f);
                        break;
                    }
                    bool boss = EnemyCatalog.Get(type).Boss;
                    Play(boss ? "roar" : "death", e.Pos, boss ? 0.7f : type is EnemyType.Bat ? 1.4f : 1f);
                    break;
                }
                case SimEventType.EnemyTelegraph: Play("telegraph", e.Pos, e.Int == (int)AttackShape.Line ? 1.2f : 1f); break;
                case SimEventType.EnemyStrike:
                    switch ((AttackKind)e.Int)
                    {
                        case AttackKind.Projectile: Play("orb", e.Pos, 1f, 0.8f); break;
                        case AttackKind.Melee: Play("swing", e.Pos, 0.6f); break;
                        case AttackKind.Blink: Play("blink", e.Pos); break;
                        case AttackKind.Lunge: Play("screech", e.Pos, 1f, 0.6f); break;
                    }
                    break;
                case SimEventType.AreaEffect:
                    switch ((AreaKind)e.Int)
                    {
                        case AreaKind.FrostNova: Play("nova", e.Pos, 1.2f); break;
                        case AreaKind.Heal: Play("heal", e.Pos); break;
                        case AreaKind.WarCry: Play("warcry", e.Pos); break;
                        case AreaKind.Mark: Play("bolt", e.Pos, 0.5f); break;
                        case AreaKind.Slam: Play("slam", e.Pos); break;
                        case AreaKind.Explosion: Play("explosion", e.Pos); break;
                        case AreaKind.HolyLight: Play("heal", e.Pos, 0.8f, 1.3f); break;
                        case AreaKind.Sanctuary: Play("heal", e.Pos, 0.7f, 1.2f); break;
                        case AreaKind.Smoke: Play("explosion", e.Pos, 1.7f, 0.45f); break;
                        case AreaKind.ShadowStep: Play("blink", e.Pos, 0.8f); break;
                        case AreaKind.FirePool: Play("flame", e.Pos); break;
                        case AreaKind.Potion: Play("heal", e.Pos, 1.2f); break;
                        case AreaKind.FlaskBurst: Play("shatter", e.Pos, 1.5f, 0.45f); break;
                    }
                    break;
                case SimEventType.HazardWarn:
                    if ((GimmickKind)e.Int == GimmickKind.PowderKeg) Play("fuse", e.Pos);
                    else Play("telegraph", e.Pos, 0.7f, 0.5f);
                    break;
                case SimEventType.HazardFire:
                    if ((GimmickKind)e.Int == GimmickKind.SpikeTrap) Play("spikes", e.Pos);
                    else if ((GimmickKind)e.Int == GimmickKind.FlameJet) Play("flame", e.Pos);
                    break;
                case SimEventType.PlayerHurt:
                    if (IsLocal(e.ClientId)) Play("hurt");
                    else Play("hit", e.Pos, 0.7f);
                    break;
                case SimEventType.PlayerDowned: Play("downed", e.Pos); break;
                case SimEventType.PlayerRevived: Play("revive", e.Pos); break;
                case SimEventType.SoulCollected:
                    if (IsLocal(e.ClientId)) Play("soul", null, 1f + UnityEngine.Random.value * 0.3f);
                    break;
                case SimEventType.TeamLevelUp: Play("levelup"); break;
                case SimEventType.LegendaryProc:
                    if (e.Int == (int)LegendaryId.None) Play("bolt", e.Pos, 1.6f, 0.6f);
                    else Play("blink", e.Pos, 0.7f);
                    break;
                case SimEventType.LootDropped:
                    if (IsLocal(e.ClientId) && e.Int >= (int)Rarity.Rare) Play("blink", e.Pos, 1.3f);
                    break;
                case SimEventType.LootPicked:
                    if (IsLocal(e.ClientId) && e.Int >= 0) Play("soul", null, 1.25f);
                    break;
                case SimEventType.PerkOffer:
                    if (IsLocal(e.ClientId)) Play("soul", null, 0.8f);
                    break;
                case SimEventType.RoomCleared: Play("clear"); break;
                case SimEventType.RewardGranted:
                    if ((RoomReward)e.Int == RoomReward.Vigor) Play("heal", null, 1f, 0.9f);
                    else if ((RoomReward)e.Int == RoomReward.Shards) Play("soul", null, 0.75f);
                    break;
                case SimEventType.BossSpawned: Play("roar"); break;
                case SimEventType.SpawnWarning: Play("telegraph", e.Pos, 0.6f, 0.6f); break;
            }
        }
    }
}
