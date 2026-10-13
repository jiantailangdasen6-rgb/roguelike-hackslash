using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// What a monster leaves when it falls: chips of what it was made of fly off (bone, flesh, ice, iron, embers, water)
    /// and a stain stays on the floor for a while (blood, ichor, soot, frost), so a fought room looks fought in.
    /// </summary>
    public sealed partial class EventPresenter
    {
        private enum Matter { Bone, Flesh, Rot, Water, Ice, Iron, Ember, Spirit }

        private const int MaxStains = 60;
        private const float StainTime = 40f;
        private readonly List<(SpriteRenderer Renderer, float Born, float Alpha)> _stains = new();

        private static Matter MatterOf(EnemyType t) => t switch
        {
            EnemyType.Skeleton or EnemyType.SkeletonArcher or EnemyType.ShieldSkeleton or EnemyType.BoneKing or EnemyType.Necromancer => Matter.Bone,
            EnemyType.Ghoul or EnemyType.Cultist or EnemyType.Flagellant or EnemyType.BloodBishop or EnemyType.Bat or EnemyType.Mimic => Matter.Flesh,
            EnemyType.Bloater => Matter.Rot,
            EnemyType.Drowned or EnemyType.TideWitch or EnemyType.DrownedSaint or EnemyType.CarrionCrab => Matter.Water,
            EnemyType.Icebound or EnemyType.FrostWitch or EnemyType.FrostQueen or EnemyType.FrostWolf or EnemyType.IceMirror => Matter.Ice,
            EnemyType.DreadKnight or EnemyType.IronSentinel => Matter.Iron,
            EnemyType.FireImp or EnemyType.SlagBloater or EnemyType.ForgeColossus => Matter.Ember,
            _ => Matter.Spirit,
        };

        private static (Color Chip, Color Stain, float StainSize) Look(Matter m) => m switch
        {
            Matter.Bone => (new Color(0.86f, 0.82f, 0.7f), new Color(0.45f, 0.42f, 0.36f, 0.45f), 0.35f),
            Matter.Flesh => (new Color(0.62f, 0.14f, 0.14f), new Color(0.42f, 0.04f, 0.06f, 0.8f), 0.55f),
            Matter.Rot => (new Color(0.62f, 0.66f, 0.2f), new Color(0.26f, 0.3f, 0.06f, 0.75f), 0.7f),
            Matter.Water => (new Color(0.45f, 0.9f, 0.8f), new Color(0.06f, 0.2f, 0.18f, 0.7f), 0.6f),
            Matter.Ice => (new Color(0.78f, 0.92f, 1f), new Color(0.6f, 0.78f, 0.95f, 0.35f), 0.45f),
            Matter.Iron => (new Color(0.5f, 0.52f, 0.56f), new Color(0.05f, 0.04f, 0.04f, 0.6f), 0.45f),
            Matter.Ember => (new Color(1f, 0.55f, 0.18f), new Color(0.08f, 0.04f, 0.02f, 0.7f), 0.55f),
            _ => (new Color(0.7f, 0.6f, 1f), new Color(0.2f, 0.12f, 0.3f, 0.35f), 0.4f),
        };

        /// <summary>A monster fell (not a prop, not one swept away with its room).</summary>
        private void Remains(in SimEvent e)
        {
            var def = EnemyCatalog.Get((EnemyType)e.Int);
            if (def.Prop || def.Friendly || e.Value >= 0.5f) return;
            var (chip, stain, size) = Look(MatterOf(def.Type));
            int chips = def.Boss ? 18 : 7;
            for (int i = 0; i < chips; i++) Shard(e.Pos + Vector2.up * 0.4f, Random.Range(0f, 360f), chip);
            if (def.Boss) size *= 2.5f;

            // The stain: an irregular splash (the blood decal), tinted and turned at random.
            var sheet = SpriteSheet.Load("decal_blood", quiet: true);
            if (sheet == null) return;
            var go = new GameObject("Stain");
            go.transform.position = e.Pos + Random.insideUnitCircle * 0.2f;
            go.transform.localScale = new Vector3(size * Random.Range(0.85f, 1.2f) * (Random.value < 0.5f ? -1f : 1f), size * Random.Range(0.8f, 1.1f), 1f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sheet.First;
            r.color = stain;
            r.sortingOrder = -994;
            _stains.Add((r, Time.time, stain.a));
            while (_stains.Count > MaxStains)
            {
                if (_stains[0].Renderer != null) Destroy(_stains[0].Renderer.gameObject);
                _stains.RemoveAt(0);
            }
        }

        private float _lastBossHealth = -1f;

        /// <summary>
        /// A lord below half health fights faster (RunSimulation.BossEnraged): the moment it happens is shown, with a
        /// red shock wave, a roar of light and its name for the new phase.
        /// </summary>
        private void UpdateBossRage()
        {
            var d = RunDirector.Instance;
            float hp = d != null && d.IsSpawned ? d.BossHealth.Value : -1f;
            if (_lastBossHealth > 0.5f && hp >= 0f && hp <= 0.5f)
            {
                var boss = EnemyView.Instance != null ? EnemyView.Instance.BossPosition() : null;
                if (boss.HasValue)
                {
                    Vector2 at = boss.Value;
                    var wave = Spawn("fx_nova", at + Vector2.up * 1f, 1f, 0.7f, 0.5f, 9f, new Color(1f, 0.2f, 0.15f, 0.85f));
                    wave.FadeOut = true;
                    AddLight(wave, new Color(1f, 0.2f, 0.1f), 4f, 12f);
                    for (int i = 0; i < 14; i++) Ember(at + Vector2.up * 1f, i * 25.7f);
                    HitStop.Freeze(0.1f, -1, major: true);
                    CameraShake.Add(0.8f);
                    ShowCallout(at + Vector2.up * 3.6f, "怒り", new Color(1f, 0.35f, 0.3f), 54);
                }
            }
            _lastBossHealth = hp;
        }

        /// <summary>Stains dry and fade away after a while.</summary>
        private void UpdateStains()
        {
            for (int i = _stains.Count - 1; i >= 0; i--)
            {
                var (r, born, alpha) = _stains[i];
                float age = Time.time - born;
                if (r == null || age > StainTime)
                {
                    if (r != null) Destroy(r.gameObject);
                    _stains.RemoveAt(i);
                    continue;
                }
                if (age > StainTime - 5f)
                {
                    var c = r.color;
                    c.a = alpha * (StainTime - age) / 5f;
                    r.color = c;
                }
            }
        }
    }
}
