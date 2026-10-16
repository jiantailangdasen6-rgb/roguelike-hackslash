using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// The lords' stages (RunSimulation.Stages): the roar when a lord flies into a rage or makes its last stand,
    /// glowing fissures that open across the hall floor and stay until the fight ends, and the marked spots
    /// bursting in the land's own colours.
    /// </summary>
    public sealed partial class EventPresenter
    {
        private readonly List<(SpriteRenderer Sprite, Light2D Light, float Phase)> _fissures = new();
        private int _shownStage;
        private static Material _fissureMaterial;

        /// <summary>Bone, blood, void, water, ice, fire, ember, spores, ink.</summary>
        private static Color LandRage(Biome land) => land switch
        {
            Biome.Crypt => new Color(0.95f, 0.85f, 0.6f),
            Biome.Chapel => new Color(1f, 0.15f, 0.2f),
            Biome.Abyss => new Color(0.7f, 0.35f, 1f),
            Biome.Sunken => new Color(0.35f, 0.95f, 0.85f),
            Biome.Frost => new Color(0.6f, 0.85f, 1f),
            Biome.Forge => new Color(1f, 0.5f, 0.12f),
            Biome.Grotto => new Color(0.6f, 1f, 0.35f),
            Biome.Library => new Color(0.6f, 0.35f, 1f),
            _ => new Color(1f, 0.35f, 0.2f),
        };

        private static Biome CurrentLand => RunDirector.Instance != null ? RunDirector.Instance.Biome : Biome.Crypt;

        /// <summary>Each frame: a new stage roars; fissures glow while the fight lasts and close after it.</summary>
        private void UpdateBossRage()
        {
            var d = RunDirector.Instance;
            int stage = d != null && d.IsSpawned ? d.BossStage.Value : 0;
            if (stage > _shownStage && _shownStage >= 1 && stage >= 2) Roar(stage);
            if (stage == 0 && _fissures.Count > 0) CloseFissures();
            _shownStage = stage;

            for (int i = 0; i < _fissures.Count; i++)
            {
                var (sprite, light, phase) = _fissures[i];
                if (sprite == null) continue;
                float glow = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.3f + phase);
                var c = sprite.color;
                c.a = Mathf.MoveTowards(c.a, 0.45f * glow, Time.deltaTime * 1.5f);
                sprite.color = c;
                if (light != null) light.intensity = 0.9f * glow * c.a;
            }
        }

        private void Roar(int stage)
        {
            var boss = EnemyView.Instance != null ? EnemyView.Instance.BossPosition() : null;
            if (!boss.HasValue) return;
            Vector2 at = boss.Value;
            bool last = stage >= 3;
            var color = LandRage(CurrentLand);
            var wave = Spawn("fx_nova", at + Vector2.up * 1f, 1f, last ? 0.9f : 0.7f, 0.5f, last ? 13f : 9f, new Color(1f, 0.2f, 0.15f, 0.85f));
            wave.FadeOut = true;
            AddLight(wave, new Color(1f, 0.2f, 0.1f), last ? 5f : 4f, last ? 16f : 12f);
            for (int i = 0; i < (last ? 24 : 14); i++) Ember(at + Vector2.up * 1f, i * (last ? 15f : 25.7f));
            for (int i = 0; i < 10; i++) Shard(at + Vector2.up * 0.6f, i * 36f + Random.Range(0f, 20f), color);
            HitStop.Freeze(last ? 0.14f : 0.1f, -1, major: true);
            if (last) HitStop.Slow(0.7f, 0.45f);
            CameraShake.Add(last ? 1f : 0.8f);
            ShowCallout(at + Vector2.up * 3.6f, last ? "最後のあがき" : "怒り", new Color(1f, 0.35f, 0.3f), last ? 60 : 54);
            EnemyView.Instance.KickBoss(last ? 0.4f : 0.3f);
            OpenFissures(last ? 7 : 6, color);
        }

        /// <summary>Glowing cracks across the hall floor (more with each stage).</summary>
        private void OpenFissures(int count, Color color)
        {
            var d = RunDirector.Instance;
            var room = d != null ? d.LocalRoom : null;
            var sheet = SpriteSheet.Load("decal_crack", quiet: true);
            if (room == null || room.Kind != RoomKind.Boss || sheet == null) return;
            var frames = sheet.Sprites(sheet.Clips[0].Name);
            var area = room.Bounds;
            for (int k = 0; k < count; k++)
            {
                var go = new GameObject("Fissure");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector2(Random.Range(area.xMin + 2f, area.xMax - 2f), Random.Range(area.yMin + 2f, area.yMax - 2.5f));
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);
                go.transform.localScale = Vector3.one * Random.Range(0.9f, 1.3f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = frames[Random.Range(0, frames.Length)];
                r.sortingOrder = -940;
                r.color = new Color(Mathf.Min(1f, color.r * 1.2f), Mathf.Min(1f, color.g * 1.2f), Mathf.Min(1f, color.b * 1.2f), 0f);
                // The crack's shape drawn as light in the land's colour (the white-flash material), so it glows in a dark hall.
                if (_fissureMaterial == null) _fissureMaterial = Resources.Load<Material>("SpriteFlash");
                if (_fissureMaterial != null) r.sharedMaterial = _fissureMaterial;
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = color;
                light.pointLightOuterRadius = 2.4f;
                light.pointLightInnerRadius = 0.2f;
                light.intensity = 0f;
                _fissures.Add((r, light, Random.Range(0f, 6.3f)));
            }
        }

        private void CloseFissures()
        {
            foreach (var (sprite, _, _) in _fissures)
                if (sprite != null) Destroy(sprite.gameObject);
            _fissures.Clear();
        }

        /// <summary>A marked spot of the hall floor bursting (RunSimulation.Quake).</summary>
        private void QuakeArea(in SimEvent e)
        {
            var color = LandRage(CurrentLand);
            var fx = Spawn("fx_explosion", e.Pos + Vector2.up * 0.3f, 18f, 0.45f, e.Value * 0.7f, e.Value * 1.1f, Color.Lerp(color, Color.white, 0.25f), order: 2400);
            AddLight(fx, color, 2.2f, e.Value * 2.2f);
            for (int i = 0; i < 6; i++) Shard(e.Pos + Vector2.up * 0.2f, i * 60f + Random.Range(0f, 30f), color);
            Juice.Dust(e.Pos, 1f, 0.5f);
            CameraShake.Add(0.12f);
        }
    }
}
