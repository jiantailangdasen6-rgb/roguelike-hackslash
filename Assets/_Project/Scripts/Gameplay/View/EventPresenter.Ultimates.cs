using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Ultimates (RunSimulation.Ultimates): a flash in the class's colour, the name called out, the screen holding for
    /// a beat; the blade storm's whirling arcs and the ranger's rain of arrows play out over their time. The blows
    /// themselves come as the usual area events (slams, shadow steps, judgments, the bomb and its fire).
    /// </summary>
    public sealed partial class EventPresenter
    {
        private void OnUltimate(in SimEvent e)
        {
            var cls = (ClassId)e.Int;
            var color = UnionColor(cls); // EventPresenter.Unions.cs
            bool mine = IsLocal(e.ClientId);
            float r = Mathf.Max(2f, e.Value);
            var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.5f, 1f, 0.6f, 0.8f, r * 2.2f, new Color(color.r, color.g, color.b, 0.9f));
            flash.FadeOut = true;
            AddLight(flash, color, 5f, r * 2.5f);
            var holy = Spawn("fx_holy", e.Pos + Vector2.up * 0.8f, 12f, 1f, 1.6f, 3.4f, Color.Lerp(color, Color.white, 0.3f));
            holy.FadeOut = true;
            for (int i = 0; i < 16; i++) Shard(e.Pos + Vector2.up * 0.5f, i * 22.5f + Random.Range(0f, 10f), color);
            HitStop.Freeze(mine ? 0.14f : 0.08f, -1, major: true);
            CameraShake.Add(mine ? 0.7f : 0.35f);
            ShowCallout(e.Pos + Vector2.up * 2.6f, UltimateCatalog.Name(cls), Color.Lerp(color, Color.white, 0.4f), mine ? 56 : 40);

            ulong owner = e.ClientId;
            var who = NetPlayer.All.Find(p => p.OwnerClientId == owner);
            switch (cls)
            {
                case ClassId.Warrior when who != null:
                    StartCoroutine(BladeStorm(who.transform, color));
                    break;
                case ClassId.Ranger:
                    StartCoroutine(ArrowRain(e.Pos, RunSimulation.RainRadius));
                    break;
                case ClassId.Mage:
                    for (int i = 0; i < 24; i++) Shard(e.Pos + Random.insideUnitCircle * r * 0.6f, Random.Range(0f, 360f), new Color(0.75f, 0.95f, 1f));
                    break;
            }
        }

        /// <summary>The warrior's blade storm: arcs whirling round them while it lasts.</summary>
        private IEnumerator BladeStorm(Transform warrior, Color color)
        {
            float end = Time.time + RunSimulation.StormTime;
            float angle = 0f;
            while (Time.time < end && warrior != null)
            {
                angle += 50f;
                for (int k = 0; k < 3; k++)
                {
                    float a = (angle + k * 120f) * Mathf.Deg2Rad;
                    Spark((Vector2)warrior.position + Vector2.up * 0.5f + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * RunSimulation.StormRadius * 0.7f, 0.9f, color);
                }
                yield return new WaitForSeconds(0.07f);
            }
        }

        /// <summary>The ranger's rain of arrows: arrows dropping onto the marked ground for as long as it lasts.</summary>
        private IEnumerator ArrowRain(Vector2 at, float radius)
        {
            var ring = Spawn("fx_revive_ring", at, 0f, RunSimulation.RainTime, radius * 0.62f, radius * 0.62f, new Color(0.6f, 1f, 0.45f, 0.55f), order: -320);
            ring.FadeOut = true;
            float end = Time.time + RunSimulation.RainTime;
            while (Time.time < end)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector2 hit = at + Random.insideUnitCircle * radius;
                    var arrow = Spawn("fx_arrow", hit + new Vector2(0.6f, 3.2f), 0f, 0.22f, 1f, 1f, Color.white, order: 2450);
                    arrow.Velocity = new Vector2(-2.7f, -14.5f);
                    arrow.Go.transform.rotation = Quaternion.Euler(0f, 0f, -100f);
                    Spark(hit, 0.5f, new Color(0.85f, 1f, 0.7f));
                }
                yield return new WaitForSeconds(0.08f);
            }
        }
    }
}
