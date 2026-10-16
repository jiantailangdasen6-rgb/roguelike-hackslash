using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Elite affixes (RunSimulation.Affixes): their names float over the elite ("俊足・分裂"), the ward breaks in
    /// cyan shards and comes back with a ring, a summoner's call flares violet. The ward bubble itself and the
    /// swift afterimages are drawn by EnemyView.
    /// </summary>
    public sealed partial class EventPresenter
    {
        private sealed class AffixLabel
        {
            public Text Text;
            public float Born;
            public bool Seen;
        }

        private readonly Dictionary<int, AffixLabel> _affixLabels = new();
        private static readonly Color AffixColor = new(1f, 0.78f, 0.42f);
        private static readonly Color WardColor = new(0.55f, 0.9f, 1f);

        private void OnEliteAffixes(in SimEvent e)
        {
            string text = EliteAffixCatalog.Label((EliteAffix)e.Int);
            if (text.Length == 0) return;
            if (!_affixLabels.TryGetValue(e.Id, out var label))
            {
                var t = UIKit.Label(_numberLayer, text, 15, TextAnchor.MiddleCenter, AffixColor);
                t.font = UIKit.TitleFont;
                t.rectTransform.sizeDelta = new Vector2(200f, 30f);
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                label = new AffixLabel { Text = t };
                _affixLabels[e.Id] = label;
            }
            label.Text.text = text;
            label.Born = Time.time;
            label.Seen = false;
        }

        private void OnEliteShield(in SimEvent e)
        {
            switch (e.Int)
            {
                case 0: // the ward breaks
                    for (int i = 0; i < 8; i++) Shard(e.Pos + Vector2.up * 0.5f, i * 45f + Random.Range(0f, 20f), WardColor);
                    Spark(e.Pos + Vector2.up * 0.5f, 1.4f, WardColor);
                    break;
                case 1: // it comes back
                {
                    var ring = Spawn("fx_revive_ring", e.Pos + Vector2.up * 0.2f, 1f, 0.45f, 0.6f, 1.6f, new Color(WardColor.r, WardColor.g, WardColor.b, 0.8f), order: -300);
                    ring.FadeOut = true;
                    break;
                }
                case 2: // a summoner's call
                {
                    var call = Spawn("fx_holy", e.Pos + Vector2.up * 0.6f, 12f, 0.6f, 0.8f, 1.6f, new Color(0.75f, 0.45f, 1f));
                    call.FadeOut = true;
                    AddLight(call, new Color(0.7f, 0.4f, 1f), 1.6f, 3f);
                    break;
                }
            }
        }

        /// <summary>Each frame: the names follow their elites; gone with them.</summary>
        private void UpdateAffixLabels()
        {
            if (_affixLabels.Count == 0 || _camera == null) return;
            List<int> gone = null;
            foreach (var (id, label) in _affixLabels)
            {
                Vector2 top = default;
                bool shown = EnemyView.Instance != null && EnemyView.Instance.TryGetTop(id, out top);
                if (!shown)
                {
                    label.Text.enabled = false;
                    // Spawning elites rise a moment later; one that was seen and is gone has died.
                    if (label.Seen || Time.time - label.Born > 6f) (gone ??= new List<int>()).Add(id);
                    continue;
                }
                label.Seen = true;
                label.Text.enabled = true;
                label.Text.transform.position = _camera.WorldToScreenPoint(top);
            }
            if (gone == null) return;
            foreach (int id in gone)
            {
                Destroy(_affixLabels[id].Text.gameObject);
                _affixLabels.Remove(id);
            }
        }
    }
}
