using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Encounter rooms (RunSimulation.Encounters): the moment the team's choice is made.</summary>
    public sealed partial class EventPresenter
    {
        private void OnEncounter(in SimEvent e)
        {
            var kind = (EncounterKind)Mathf.RoundToInt(e.Value);
            bool dark = e.Int == 1 && kind != EncounterKind.Gambler || kind == EncounterKind.Gambler && Mathf.RoundToInt(e.Size.x) == 2;
            var color = dark ? new Color(0.85f, 0.25f, 0.3f) : new Color(1f, 0.85f, 0.5f);
            var flash = Spawn("fx_nova", e.Pos + Vector2.up * 0.6f, 1f, 0.55f, 0.6f, 6f, new Color(color.r, color.g, color.b, 0.85f));
            flash.FadeOut = true;
            AddLight(flash, color, 3f, 8f);
            var holy = Spawn(dark ? "fx_soul" : "fx_holy", e.Pos + Vector2.up * 0.8f, 12f, 1f, 1.2f, 2.6f, color);
            holy.FadeOut = true;
            for (int i = 0; i < 10; i++) Shard(e.Pos + Vector2.up * 0.6f, i * 36f + Random.Range(0f, 18f), color);
            CameraShake.Add(kind == EncounterKind.Statue && e.Int == 1 ? 0.6f : 0.25f);
            string word = kind == EncounterKind.Gambler && e.Int == 0 ? (Mathf.RoundToInt(e.Size.x) == 1 ? "勝ち！" : "負け…") : EncounterCatalog.Choice(kind, e.Int);
            ShowCallout(e.Pos + Vector2.up * 3f, word, Color.Lerp(color, Color.white, 0.3f), 46);
        }
    }
}
