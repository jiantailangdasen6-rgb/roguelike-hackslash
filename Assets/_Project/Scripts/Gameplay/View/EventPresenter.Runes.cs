using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Phase 4 on screen: the lance's thrust, a rune's lightning leaping to its targets, and the flare of a rune
    /// answering its skill (a ring in the rune's colour with its sign).
    /// </summary>
    public sealed partial class EventPresenter
    {
        /// <summary>Rune colours by RuneId (matches IconArt.RuneColors).</summary>
        private static readonly Color[] RuneTint =
        {
            Color.white, new(1f, 0.48f, 0.16f), new(0.56f, 0.9f, 1f), new(1f, 0.88f, 0.4f), new(0.5f, 1f, 0.6f),
            new(0.78f, 0.66f, 1f), new(0.96f, 0.94f, 0.88f), new(0.6f, 0.48f, 1f), new(0.6f, 1f, 0.85f),
        };

        private void RuneArea(in SimEvent e)
        {
            switch ((AreaKind)e.Int)
            {
                case AreaKind.Thrust:
                {
                    // A long white streak along the lance's line.
                    Vector2 dir = e.Vec.sqrMagnitude > 0.0001f ? e.Vec.normalized : Vector2.right;
                    var streak = Spawn("fx_line", e.Pos, 0f, 0.16f, 0f, 0f, new Color(0.95f, 0.97f, 1f, 0.85f), order: EnemyView.SortOrder(e.Pos.y) + 20);
                    streak.Go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    streak.Go.transform.localScale = new Vector3(e.Value / 2f, 1.6f, 1f);
                    streak.FadeOut = true;
                    break;
                }
                case AreaKind.Lightning:
                {
                    // A jagged bolt from where the skill landed to the monster it strikes.
                    Vector2 from = e.Pos + Vector2.up * 0.6f, to = e.Vec + Vector2.up * 0.5f;
                    Vector2 prev = from;
                    const int steps = 4;
                    for (int k = 1; k <= steps; k++)
                    {
                        Vector2 next = Vector2.Lerp(from, to, k / (float)steps);
                        if (k < steps) next += Random.insideUnitCircle * 0.35f;
                        Vector2 seg = next - prev;
                        var line = Spawn("fx_line", prev, 0f, 0.2f, 0f, 0f, new Color(1f, 0.95f, 0.6f, 0.95f), order: 3000);
                        line.Go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(seg.y, seg.x) * Mathf.Rad2Deg);
                        line.Go.transform.localScale = new Vector3(seg.magnitude / 2f, 0.9f, 1f);
                        line.FadeOut = true;
                        prev = next;
                    }
                    var flash = Spawn("fx_hit", to, 24f, 0.2f, 0.8f, 1.2f, new Color(1f, 0.95f, 0.6f));
                    AddLight(flash, new Color(1f, 0.9f, 0.5f), 2f, 2.5f);
                    break;
                }
                case AreaKind.RuneFlare:
                {
                    int rune = Mathf.Clamp(Mathf.RoundToInt(e.Size.x), 0, RuneTint.Length - 1);
                    var tint = RuneTint[rune];
                    var ring = Spawn("fx_nova", e.Pos + Vector2.up * 0.3f, 1f, 0.45f, 0.4f, 1.6f, new Color(tint.r, tint.g, tint.b, 0.75f));
                    ring.FadeOut = true;
                    AddLight(ring, tint, 1.2f, 2.5f);
                    var def = ItemCatalog.GetRune((RuneId)rune);
                    if (def != null && IsLocal(e.ClientId)) ShowCallout(e.Pos + Vector2.up * 1.8f, def.Mark, tint, 30);
                    break;
                }
            }
        }
    }
}
