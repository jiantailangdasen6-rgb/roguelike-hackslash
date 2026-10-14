using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// The fungal grotto and the cursed library on screen: spore shots and clouds, pages flying and bursting, ink
    /// erupting, and the moments of their lords' trials (the fog and its glowing caps, the parasite's mark over its
    /// hosts, the tomes shining in their order, the candles lit against the ink).
    /// </summary>
    public sealed partial class EventPresenter
    {
        private static readonly Color SporeColor = new(0.62f, 0.95f, 0.38f);
        private static readonly Color InkColor = new(0.16f, 0.12f, 0.32f);
        private readonly Dictionary<ulong, Fx> _parasiteMarks = new();

        private Fx Lands2Projectile(ProjectileKind kind, in SimEvent e, ref float angle)
        {
            Fx fx;
            if (kind == ProjectileKind.Page)
            {
                // A torn page fluttering through the air.
                fx = Spawn("fx_shard", e.Pos, 0f, e.Value + 0.05f, 1.4f, 1.4f, new Color(0.95f, 0.9f, 0.78f));
                AddLight(fx, new Color(0.7f, 0.65f, 1f), 0.6f, 1.2f);
                angle -= 45f;
                return fx;
            }
            fx = Spawn("fx_orb", e.Pos, 10f, e.Value + 0.05f, 0.95f, 0.95f, SporeColor);
            AddLight(fx, new Color(0.5f, 1f, 0.3f), 0.9f, 1.4f);
            return fx;
        }

        private void Lands2HazardFire(in SimEvent e)
        {
            if ((GimmickKind)e.Int == GimmickKind.SporeVent)
            {
                // The vent coughs a cloud that hangs for as long as it hurts.
                float r = e.Size.x;
                var cloud = Spawn("fx_circle", e.Pos, 0f, e.Value, r * 0.6f, r, new Color(SporeColor.r, SporeColor.g, SporeColor.b, 0.32f), order: -310);
                cloud.FadeOut = true;
                AddLight(cloud, new Color(0.5f, 1f, 0.3f), 0.8f, r + 1.5f);
                for (int i = 0; i < 7; i++)
                    Burst("fx_puff", e.Pos + Random.insideUnitCircle * r * 0.6f + Vector2.up * 0.3f, Random.Range(4f, 6f), r * Random.Range(0.5f, 0.8f), new Color(0.6f, 0.9f, 0.4f, 0.75f));
                return;
            }
            // A page bursts in black fire.
            InkSplash(e.Pos, e.Size.x);
        }

        private void InkSplash(Vector2 at, float r)
        {
            var nova = Spawn("fx_nova", at + Vector2.up * 0.2f, 1f, 0.5f, 0.4f, r * 1.3f, new Color(0.35f, 0.25f, 0.7f, 0.85f));
            nova.FadeOut = true;
            AddLight(nova, new Color(0.5f, 0.35f, 1f), 1.8f, r + 2f);
            var pool = Spawn("fx_circle", at, 0f, 1.2f, r * 0.8f, r, new Color(InkColor.r, InkColor.g, InkColor.b, 0.7f), order: -310);
            pool.FadeOut = true;
            for (int i = 0; i < 8; i++) Shard(at + Vector2.up * 0.3f, i * 45f + Random.Range(0f, 25f), new Color(0.25f, 0.2f, 0.45f));
            CameraShake.Add(0.2f);
        }

        private void Lands2Area(in SimEvent e)
        {
            float r = e.Value;
            if ((AreaKind)e.Int == AreaKind.InkBurst)
            {
                InkSplash(e.Pos, r);
                return;
            }
            float life = e.Size.x > 0f ? e.Size.x : 0.8f;
            var cloud = Spawn("fx_circle", e.Pos, 0f, life, r * 0.7f, r, new Color(SporeColor.r, SporeColor.g, SporeColor.b, 0.3f), order: -310);
            cloud.FadeOut = true;
            AddLight(cloud, new Color(0.5f, 1f, 0.3f), 0.7f, r + 1.2f);
            for (int i = 0; i < 6; i++)
                Burst("fx_puff", e.Pos + Random.insideUnitCircle * r * 0.6f + Vector2.up * 0.3f, Random.Range(4f, 6f), r * Random.Range(0.5f, 0.9f), new Color(0.6f, 0.9f, 0.4f, 0.7f));
        }

        /// <summary>The new lords' trial moments (false for the others).</summary>
        private bool Lands2Trial(in SimEvent e)
        {
            switch ((TrialMoment)e.Int)
            {
                case TrialMoment.FogStart:
                {
                    var nova = Spawn("fx_nova", e.Pos, 1f, 0.9f, 1f, 10f, new Color(0.6f, 0.95f, 0.4f, 0.6f));
                    nova.FadeOut = true;
                    CameraShake.Add(0.5f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "胞子の霧", SporeColor, 46);
                    return true;
                }
                case TrialMoment.CapWilt:
                    Burst("fx_puff", e.Pos + Vector2.up * 0.4f, 8f, 1.4f, new Color(0.45f, 0.55f, 0.35f, 0.8f));
                    return true;
                case TrialMoment.CapSprout:
                {
                    var glow = Spawn("fx_heal", e.Pos, 1f, 0.8f, 0.5f, RunSimulation.CapRadius, new Color(0.75f, 1f, 0.6f, 0.8f));
                    glow.FadeOut = true;
                    return true;
                }
                case TrialMoment.ParasiteMark:
                {
                    if (_parasiteMarks.TryGetValue(e.ClientId, out var old)) Expire(old);
                    var mark = Spawn("fx_mark", e.Pos + Vector2.up * 1.6f, 0f, 60f, 1.4f, 1.4f, new Color(0.6f, 1f, 0.3f), order: 3200);
                    AddLight(mark, new Color(0.5f, 1f, 0.3f), 1f, 2.2f);
                    _parasiteMarks[e.ClientId] = mark;
                    if (IsLocal(e.ClientId)) ShowCallout(e.Pos + Vector2.up * 2.6f, "寄生された！ 百合のそばへ", new Color(0.75f, 1f, 0.5f), 36);
                    return true;
                }
                case TrialMoment.ParasiteJump:
                {
                    Vector2 to = e.Vec - e.Pos;
                    var line = Spawn("fx_line", e.Pos + Vector2.up * 0.6f, 0f, 0.4f, 0f, 0f, new Color(0.6f, 1f, 0.3f, 0.8f), order: 2900);
                    line.Go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
                    line.Go.transform.localScale = new Vector3(to.magnitude / 2f, 1.2f, 1f);
                    line.FadeOut = true;
                    ShowCallout(e.Vec + Vector2.up * 2.2f, "寄生体が移った", new Color(0.75f, 1f, 0.5f), 32);
                    return true;
                }
                case TrialMoment.ParasiteCured:
                    if (_parasiteMarks.Remove(e.ClientId, out var cured)) Expire(cured);
                    ShowCallout(e.Pos + Vector2.up * 2.2f, "清められた", new Color(1f, 1f, 0.85f), 32);
                    return true;
                case TrialMoment.ParasiteBurst:
                    ClearParasiteMarks();
                    CameraShake.Add(0.7f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "寄生体の破裂", new Color(0.6f, 1f, 0.3f), 48);
                    return true;
                case TrialMoment.TomeShine:
                {
                    var shine = Spawn("fx_holy", e.Pos + Vector2.up * 0.8f, 12f, e.Value, 1f, 1.6f, new Color(0.75f, 0.6f, 1f));
                    shine.FadeOut = true;
                    AddLight(shine, new Color(0.65f, 0.5f, 1f), 3f, 4f);
                    ShowCallout(e.Pos + Vector2.up * 2.4f, e.Id.ToString(), new Color(0.85f, 0.75f, 1f), 56);
                    return true;
                }
                case TrialMoment.TomeRight:
                {
                    var ring = Spawn("fx_heal", e.Pos, 1f, 0.6f, 0.4f, 1.8f, new Color(0.8f, 0.7f, 1f, 0.9f));
                    ring.FadeOut = true;
                    ShowCallout(e.Pos + Vector2.up * 2.2f, e.Id + " 冊目", new Color(0.85f, 0.8f, 1f), 36);
                    return true;
                }
                case TrialMoment.TomeWrong:
                    CameraShake.Add(0.4f);
                    ShowCallout(e.Pos + Vector2.up * 2.2f, "順番が違う！", new Color(1f, 0.45f, 0.45f), 40);
                    return true;
                case TrialMoment.TomesFail:
                case TrialMoment.InkFlood:
                    CameraShake.Add(0.7f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "墨の洪水", new Color(0.65f, 0.55f, 1f), 48);
                    return true;
                case TrialMoment.InkDive:
                    InkSplash(e.Pos, 2.6f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "墨に潜った", new Color(0.65f, 0.55f, 1f), 46);
                    return true;
                case TrialMoment.CandleLit:
                {
                    var fire = Spawn("fx_holy", e.Pos + Vector2.up * 1.2f, 12f, 0.7f, 0.8f, 1.4f, new Color(1f, 0.8f, 0.45f));
                    fire.FadeOut = true;
                    AddLight(fire, new Color(1f, 0.75f, 0.4f), 2.5f, 4f);
                    ShowCallout(e.Pos + Vector2.up * 2.4f, "灯った", new Color(1f, 0.85f, 0.55f), 34);
                    return true;
                }
                case TrialMoment.FogClears:
                case TrialMoment.ParasiteGone:
                case TrialMoment.TomesRead:
                case TrialMoment.InkDriven:
                {
                    ClearParasiteMarks();
                    var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.8f, 1f, 0.7f, 0.5f, 8f, new Color(1f, 0.95f, 0.8f, 0.9f));
                    flash.FadeOut = true;
                    AddLight(flash, new Color(1f, 0.9f, 0.6f), 4f, 10f);
                    for (int i = 0; i < 12; i++) Shard(e.Pos + Vector2.up * 1f, i * 30f + Random.Range(0f, 20f), new Color(1f, 0.9f, 0.65f));
                    HitStop.Freeze(0.12f, -1, major: true);
                    CameraShake.Add(0.7f);
                    ShowCallout(e.Pos + Vector2.up * 3f, "隙あり！", new Color(1f, 0.85f, 0.4f), 52);
                    return true;
                }
                default:
                    return false;
            }
        }

        private void ClearParasiteMarks()
        {
            foreach (var m in _parasiteMarks.Values) Expire(m);
            _parasiteMarks.Clear();
        }

        /// <summary>Keeps each parasite's mark over its host.</summary>
        private void UpdateLands2()
        {
            if (_parasiteMarks.Count == 0) return;
            var director = RunDirector.Instance;
            bool trial = director != null && director.IsSpawned && director.Trial.Value == BossTrial.Parasite;
            List<ulong> gone = null;
            foreach (var (client, mark) in _parasiteMarks)
            {
                var player = FindPlayer(client);
                if (!trial || player == null || !_effects.Contains(mark))
                {
                    (gone ??= new List<ulong>()).Add(client);
                    continue;
                }
                mark.Go.transform.position = player.transform.position + Vector3.up * 1.8f;
            }
            if (gone == null) return;
            foreach (ulong c in gone)
            {
                Expire(_parasiteMarks[c]);
                _parasiteMarks.Remove(c);
            }
        }
    }
}
