using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Effects of the new lands: water, ice and fire shots; icicles and lava cracks; slag and steam; and the moments of
    /// the new lords' trials (the flood, the reflections, red-hot iron and its quenching).
    /// </summary>
    public sealed partial class EventPresenter
    {
        private Fx LandProjectile(ProjectileKind kind, in SimEvent e, ref float angle)
        {
            Fx fx;
            switch (kind)
            {
                case ProjectileKind.WaterOrb:
                    fx = Spawn("fx_orb", e.Pos, 12f, e.Value + 0.05f, 0.95f, 0.95f, new Color(0.45f, 1f, 0.88f));
                    AddLight(fx, new Color(0.3f, 0.95f, 0.8f), 1f, 1.5f);
                    break;
                case ProjectileKind.IceShard:
                    fx = Spawn("fx_shard", e.Pos, 0f, e.Value + 0.05f, 1.5f, 1.5f, new Color(0.75f, 0.92f, 1f));
                    AddLight(fx, new Color(0.55f, 0.85f, 1f), 1f, 1.5f);
                    angle -= 45f; // the shard sheet points up-right
                    break;
                default:
                    fx = Spawn("fx_orb", e.Pos, 14f, e.Value + 0.05f, 0.85f, 0.85f, new Color(1f, 0.6f, 0.25f));
                    AddLight(fx, new Color(1f, 0.5f, 0.15f), 1.2f, 1.6f);
                    break;
            }
            return fx;
        }

        private void LandHazardWarn(in SimEvent e)
        {
            switch ((GimmickKind)e.Int)
            {
                case GimmickKind.Icicle:
                    AddTelegraph(new SimEvent { Id = HazardKey(e.Id), Int = (int)AttackShape.Circle, Pos = e.Pos, Size = e.Size, Value = e.Value });
                    break;
                case GimmickKind.LavaCrack:
                    AddTelegraph(new SimEvent { Id = HazardKey(e.Id), Int = (int)AttackShape.Line, Pos = e.Pos, Vec = e.Vec, Size = e.Size, Value = e.Value });
                    break;
            }
        }

        private void LandHazardFire(in SimEvent e)
        {
            switch ((GimmickKind)e.Int)
            {
                case GimmickKind.Icicle:
                {
                    // An icicle drops out of the dark and bursts on the floor.
                    var drop = Spawn("fx_shard", e.Pos + Vector2.up * 4f, 0f, 0.14f, 2.2f, 2.2f, new Color(0.85f, 0.95f, 1f));
                    drop.Velocity = Vector2.down * 28f;
                    drop.Go.transform.rotation = Quaternion.Euler(0f, 0f, -135f);
                    for (int i = 0; i < 8; i++) Shard(e.Pos + Vector2.up * 0.2f, i * 45f + Random.Range(0f, 25f), new Color(0.8f, 0.94f, 1f));
                    var ring = Spawn("fx_circle", e.Pos, 1f, 0.35f, e.Size.x * 0.5f, e.Size.x, new Color(0.7f, 0.9f, 1f, 0.8f), order: -300);
                    ring.FadeOut = true;
                    CameraShake.Add(0.15f);
                    break;
                }
                case GimmickKind.LavaCrack:
                {
                    // Fire bursts up out of the crack along its length.
                    Vector2 dir = e.Vec.sqrMagnitude > 0.0001f ? e.Vec.normalized : Vector2.right;
                    int n = Mathf.CeilToInt(e.Size.x / 0.9f);
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 at = e.Pos + dir * (0.5f + i * 0.9f);
                        var flame = Spawn("prop_campfire", at, Random.Range(10f, 13f), e.Value, 0.55f, 0.75f, Color.white, order: EnemyView.SortOrder(at.y));
                        flame.FadeOut = true;
                    }
                    var glow = Spawn("fx_circle", e.Pos + dir * e.Size.x * 0.5f, 0f, e.Value, 0.5f, 0.5f, new Color(1f, 0.5f, 0.15f, 0f), order: -300);
                    AddLight(glow, new Color(1f, 0.5f, 0.15f), 2.4f, e.Size.x * 0.6f);
                    break;
                }
            }
        }

        private void LandArea(in SimEvent e)
        {
            float r = e.Value;
            switch ((AreaKind)e.Int)
            {
                case AreaKind.SlagPool:
                {
                    // Molten slag: a dark crust with a hot glow and a few flames, for as long as it burns.
                    float life = Mathf.Max(0.5f, e.Size.x);
                    var crust = Spawn("fx_circle", e.Pos, 0f, life, r, r, new Color(0.35f, 0.12f, 0.05f, 0.55f), order: -320);
                    crust.FadeOut = true;
                    var heat = Spawn("fx_circle", e.Pos, 0f, life, r * 0.6f, r * 0.5f, new Color(1f, 0.45f, 0.1f, 0.45f), order: -319);
                    heat.FadeOut = true;
                    AddLight(heat, new Color(1f, 0.45f, 0.12f), 1.8f, r + 2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f + Random.Range(-0.4f, 0.4f);
                        Vector2 at = e.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * r * Random.Range(0.2f, 0.7f);
                        var flame = Spawn("prop_campfire", at, Random.Range(9f, 12f), life, 0.4f, 0.25f, Color.white, order: EnemyView.SortOrder(at.y));
                        flame.FadeOut = true;
                    }
                    break;
                }
                case AreaKind.Steam:
                {
                    for (int i = 0; i < 12; i++)
                    {
                        Vector2 at = e.Pos + Random.insideUnitCircle * r * 0.8f;
                        Burst("fx_puff", at + Vector2.up * 0.4f, Random.Range(6f, 9f), r * Random.Range(0.5f, 0.9f), new Color(0.92f, 0.95f, 1f, 0.9f));
                    }
                    var ring = Spawn("fx_nova", e.Pos + Vector2.up * 0.5f, 1f, 0.6f, 0.5f, r * 1.4f, new Color(0.9f, 0.95f, 1f, 0.8f));
                    ring.FadeOut = true;
                    CameraShake.Add(0.6f);
                    break;
                }
            }
        }

        private void LandTrial(in SimEvent e)
        {
            switch ((TrialMoment)e.Int)
            {
                case TrialMoment.FloodRise:
                {
                    var nova = Spawn("fx_nova", e.Pos, 1f, 0.8f, 1f, 9f, new Color(0.4f, 1f, 0.85f, 0.7f));
                    nova.FadeOut = true;
                    AddLight(nova, new Color(0.3f, 0.95f, 0.8f), 3f, 12f);
                    CameraShake.Add(0.5f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "水が満ちる", new Color(0.5f, 1f, 0.88f), 46);
                    break;
                }
                case TrialMoment.FloodDrained:
                case TrialMoment.MirrorsShattered:
                case TrialMoment.Quenched:
                {
                    var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.8f, 1f, 0.7f, 0.5f, 8f, new Color(1f, 0.95f, 0.8f, 0.9f));
                    flash.FadeOut = true;
                    AddLight(flash, new Color(1f, 0.9f, 0.6f), 4f, 10f);
                    for (int i = 0; i < 12; i++) Shard(e.Pos + Vector2.up * 1f, i * 30f + Random.Range(0f, 20f), new Color(1f, 0.9f, 0.65f));
                    HitStop.Freeze(0.12f, -1, major: true);
                    CameraShake.Add(0.7f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "隙あり！", new Color(1f, 0.85f, 0.4f), 52);
                    break;
                }
                case TrialMoment.FloodTorrent:
                {
                    var wave = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.8f, 1f, 14f, new Color(0.3f, 0.9f, 0.8f, 0.8f));
                    wave.FadeOut = true;
                    AddLight(wave, new Color(0.25f, 0.85f, 0.75f), 4f, 14f);
                    CameraShake.Add(0.9f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "濁流", new Color(0.5f, 1f, 0.88f), 48);
                    break;
                }
                case TrialMoment.MirrorsRise:
                    CameraShake.Add(0.4f);
                    ShowCallout(e.Pos + Vector2.up * 3.2f, "氷の写し身", new Color(0.7f, 0.9f, 1f), 46);
                    break;
                case TrialMoment.MirrorBroken:
                    for (int i = 0; i < 10; i++) Shard(e.Pos + Vector2.up * 0.8f, i * 36f + Random.Range(0f, 20f), new Color(0.8f, 0.95f, 1f));
                    break;
                case TrialMoment.MirrorsBurst:
                    CameraShake.Add(0.6f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "凍てつく嵐", new Color(0.7f, 0.9f, 1f), 48);
                    break;
                case TrialMoment.OverheatStart:
                {
                    var nova = Spawn("fx_nova", e.Pos + Vector2.up * 0.8f, 1f, 0.6f, 0.5f, 7f, new Color(1f, 0.55f, 0.2f, 0.85f));
                    nova.FadeOut = true;
                    AddLight(nova, new Color(1f, 0.45f, 0.1f), 3.5f, 9f);
                    for (int i = 0; i < 14; i++) Ember(e.Pos + Vector2.up * 0.8f, i * 25f + Random.Range(0f, 15f));
                    CameraShake.Add(0.6f);
                    ShowCallout(e.Pos + Vector2.up * 3.2f, "灼熱", new Color(1f, 0.6f, 0.25f), 50);
                    break;
                }
                case TrialMoment.CisternBroken:
                    for (int i = 0; i < 10; i++) Shard(e.Pos + Vector2.up * 0.5f, i * 36f + Random.Range(0f, 20f), new Color(0.6f, 0.85f, 1f));
                    Burst("fx_puff", e.Pos + Vector2.up * 0.3f, 10f, 1.6f, new Color(0.7f, 0.85f, 1f, 0.8f));
                    break;
                default:
                    Lords2Trial(e); // EventPresenter.Lords2.cs
                    break;
                case TrialMoment.CooledDown:
                    ShowCallout(e.Pos + Vector2.up * 3f, "炉の怒り", new Color(1f, 0.5f, 0.2f), 48);
                    break;
            }
        }
    }
}
