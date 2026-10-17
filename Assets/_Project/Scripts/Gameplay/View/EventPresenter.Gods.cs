using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Gods' blessings at work: impacts of lightning (the bolt itself is drawn by LegendaryFxView), bursts of flame and frost, a blessing granted, a favour awakening.</summary>
    public sealed partial class EventPresenter
    {
        /// <summary>Room surprises (RunSimulation.Events): the mimic wakes and falls, the captive goes free.</summary>
        private void OnRoomEvent(in SimEvent e)
        {
            switch ((RoomEventKind)e.Int)
            {
                case RoomEventKind.MimicRevealed:
                {
                    CameraShake.Add(0.45f);
                    HitStop.Freeze(0.07f, -1, major: true);
                    Burst("fx_puff", e.Pos + Vector2.up * 0.4f, 16f, 1.8f, new Color(0.6f, 0.5f, 0.45f));
                    var ring = Spawn("fx_nova", e.Pos + Vector2.up * 0.4f, 1f, 0.35f, 0.4f, 2.6f, new Color(1f, 0.35f, 0.25f, 0.8f));
                    ring.FadeOut = true;
                    AddLight(ring, new Color(1f, 0.3f, 0.2f), 2.5f, 4f);
                    ShowCallout(e.Pos + Vector2.up * 2.4f, "ミミック！", new Color(1f, 0.4f, 0.3f), 52);
                    break;
                }
                case RoomEventKind.MimicSlain:
                {
                    var glint = Spawn("fx_holy", e.Pos + Vector2.up * 0.8f, 12f, 1f, 1.4f, 2.6f, new Color(1f, 0.85f, 0.45f));
                    glint.FadeOut = true;
                    AddLight(glint, new Color(1f, 0.8f, 0.4f), 3f, 6f);
                    for (int i = 0; i < 12; i++) Shard(e.Pos + Vector2.up * 0.6f, i * 30f + Random.Range(0f, 20f), new Color(1f, 0.85f, 0.4f));
                    ShowCallout(e.Pos + Vector2.up * 2.2f, "お宝！", new Color(1f, 0.85f, 0.45f), 46);
                    break;
                }
                case RoomEventKind.CaptiveFreed:
                {
                    var column = Spawn("fx_holy", e.Pos + Vector2.up * 1f, 10f, 1.2f, 1.4f, 2.6f, new Color(1f, 0.95f, 0.8f));
                    column.FadeOut = true;
                    AddLight(column, new Color(1f, 0.9f, 0.6f), 2.5f, 6f);
                    var heal = Spawn("fx_heal", e.Pos, 1f, 0.8f, 0.5f, 3.5f, Color.white);
                    heal.FadeOut = true;
                    Burst("fx_puff", e.Pos + Vector2.up * 0.5f, 18f, 1.4f, new Color(0.75f, 0.72f, 0.7f));
                    ShowCallout(e.Pos + Vector2.up * 2.6f, "助かった！ ありがとう", new Color(0.95f, 0.9f, 0.75f), 40);
                    break;
                }
            }
        }

        private void OnGodSign(in SimEvent e)
        {
            var god = GodCatalog.Get((GodId)Mathf.Clamp(e.Id, 0, GodCatalog.All.Length - 1));
            Color c = god.Color;
            switch ((GodSignKind)e.Int)
            {
                case GodSignKind.Lightning:
                {
                    float r = e.Value;
                    var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.2f, 1f, 0.22f, 0.3f, r > 0f ? r * 1.1f : 1.1f, new Color(1f, 0.97f, 0.75f, 0.9f));
                    flash.FadeOut = true;
                    AddLight(flash, new Color(1f, 0.9f, 0.5f), r > 0f ? 3.2f : 2f, r > 0f ? r + 4f : 3f);
                    for (int i = 0; i < (r > 0f ? 8 : 4); i++) Shard(e.Pos + Vector2.up * 0.2f, i * (r > 0f ? 45f : 90f) + Random.Range(0f, 30f), new Color(1f, 0.92f, 0.5f));
                    if (r > 0f)
                    {
                        var scorch = Spawn("fx_circle", e.Pos, 1f, 0.7f, r * 0.7f, r, new Color(1f, 0.85f, 0.35f, 0.55f), order: -300);
                        scorch.FadeOut = true;
                        CameraShake.Add(0.35f);
                    }
                    else CameraShake.Add(0.08f);
                    break;
                }
                case GodSignKind.Fire:
                {
                    float r = e.Value;
                    var s = SpriteSheet.Load("fx_explosion");
                    int frames = s != null && s.Clips.Count > 0 ? s.Clips[0].Frames : 8;
                    var boom = Spawn("fx_explosion", e.Pos + Vector2.up * 0.3f, 20f, frames / 20f, r * 0.55f, r * 0.65f, new Color(1f, 0.75f, 0.55f));
                    boom.Renderer.sortingOrder = 2700;
                    AddLight(boom, new Color(1f, 0.45f, 0.15f), 2.4f, r + 2.5f);
                    var ring = Spawn("fx_circle", e.Pos, 1f, 0.4f, r * 0.5f, r, new Color(1f, 0.55f, 0.2f, 0.32f), order: -300);
                    ring.FadeOut = true;
                    for (int i = 0; i < 6; i++) Ember(e.Pos, i * 60f + Random.Range(0f, 30f));
                    CameraShake.Add(0.15f);
                    break;
                }
                case GodSignKind.Frost:
                {
                    float r = e.Value;
                    var fx = Spawn("fx_nova", e.Pos + Vector2.up * 0.2f, 1f, 0.4f, r * 0.3f, r, new Color(0.75f, 0.92f, 1f, 0.9f));
                    fx.FadeOut = true;
                    AddLight(fx, new Color(0.5f, 0.8f, 1f), 1.6f, r + 1.5f);
                    for (int i = 0; i < (r > 1f ? 8 : 4); i++) Shard(e.Pos + Vector2.up * 0.3f, i * (r > 1f ? 45f : 90f) + 20f, new Color(0.75f, 0.92f, 1f));
                    break;
                }
                case GodSignKind.Taken:
                {
                    // A column of the god's light comes down on the chosen one, and the blessing's name rises.
                    var column = Spawn("fx_holy", e.Pos + Vector2.up * 1f, 10f, 1.3f, 1.6f, 2.8f, Color.Lerp(c, Color.white, 0.35f));
                    column.FadeOut = true;
                    AddLight(column, c, 3f, 6f);
                    var ring = Spawn("fx_revive_ring", e.Pos, 1f, 0.9f, 0.6f, 2.6f, new Color(c.r, c.g, c.b, 0.85f), order: -310);
                    ring.FadeOut = true;
                    for (int i = 0; i < 10; i++) Shard(e.Pos + Vector2.up * 0.8f, i * 36f + Random.Range(0f, 20f), Color.Lerp(c, Color.white, 0.4f));
                    int perk = Mathf.RoundToInt(e.Value);
                    if (perk >= 0 && perk < PerkCatalog.Count)
                        ShowCallout(e.Pos + Vector2.up * 2.4f, PerkCatalog.Get((PerkId)perk).Name, Color.Lerp(c, Color.white, 0.25f), 40);
                    break;
                }
                case GodSignKind.Favor:
                {
                    HitStop.Freeze(0.08f, -1, major: true);
                    CameraShake.Add(0.4f);
                    for (int k = 0; k < 3; k++)
                    {
                        var ring = Spawn("fx_revive_ring", e.Pos, 1f, 0.8f + k * 0.25f, 0.5f + k * 0.4f, 4f + k * 1.2f, new Color(c.r, c.g, c.b, 0.8f - k * 0.2f), order: -310);
                        ring.FadeOut = true;
                    }
                    var nova = Spawn("fx_nova", e.Pos + Vector2.up * 0.6f, 1f, 0.6f, 0.6f, 5f, new Color(c.r, c.g, c.b, 0.75f));
                    nova.FadeOut = true;
                    AddLight(nova, c, 4.5f, 9f);
                    for (int i = 0; i < 16; i++) Shard(e.Pos + Vector2.up * 0.8f, i * 22.5f + Random.Range(0f, 15f), Color.Lerp(c, Color.white, 0.5f));
                    ShowCallout(e.Pos + Vector2.up * 3.4f, god.FavorName, Color.Lerp(c, Color.white, 0.2f), 54);
                    break;
                }
                case GodSignKind.Veil:
                {
                    var ring = Spawn("fx_revive_ring", e.Pos, 1f, 0.6f, 1.6f, 1.1f, new Color(1f, 0.95f, 0.7f, 0.85f), order: -310);
                    ring.FadeOut = true;
                    var glow = Spawn("fx_heal", e.Pos + Vector2.up * 0.3f, 1f, 0.6f, 0.5f, 1.4f, new Color(1f, 1f, 0.85f, 0.8f));
                    glow.FadeOut = true;
                    AddLight(glow, new Color(1f, 0.95f, 0.7f), 1.4f, 2.5f);
                    break;
                }
                case GodSignKind.Rise:
                {
                    var column = Spawn("fx_holy", e.Pos + Vector2.up * 1f, 10f, 1.4f, 1.6f, 3f, Color.Lerp(c, Color.white, 0.4f));
                    column.FadeOut = true;
                    AddLight(column, c, 3.5f, 7f);
                    var ring = Spawn("fx_heal", e.Pos, 1f, 0.8f, 0.5f, 3f, Color.white);
                    ring.FadeOut = true;
                    ShowCallout(e.Pos + Vector2.up * 2.6f, "リジェネ！", Color.Lerp(c, Color.white, 0.3f), 46);
                    break;
                }
            }
        }
    }
}
