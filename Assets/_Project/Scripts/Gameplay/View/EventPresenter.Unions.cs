using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Union techniques (RunSimulation.Unions): a streak of light joins the two players, a burst in both classes'
    /// colours where they meet, the screen holds for a beat and the technique's name is called out.
    /// </summary>
    public sealed partial class EventPresenter
    {
        /// <summary>Each class's colour in a union: steel, ice, green, gold, violet, fire, soul green, holy white.</summary>
        private static Color UnionColor(ClassId c) => c switch
        {
            ClassId.Warrior => new Color(1f, 0.55f, 0.35f),
            ClassId.Mage => new Color(0.55f, 0.85f, 1f),
            ClassId.Ranger => new Color(0.6f, 1f, 0.45f),
            ClassId.Guardian => new Color(1f, 0.85f, 0.45f),
            ClassId.Assassin => new Color(0.75f, 0.45f, 1f),
            ClassId.Alchemist => new Color(1f, 0.5f, 0.15f),
            ClassId.Deathcaller => new Color(0.45f, 1f, 0.6f),
            _ => new Color(1f, 0.97f, 0.8f),
        };

        /// <summary>Screenshots (-fakeunion): the look of a union between two spots, without a second player.</summary>
        public void ShowUnion(Vector2 a, Vector2 b, int pair) =>
            OnUnion(new SimEvent(SimEventType.Combo) { Int = (int)ComboKind.Union, Pos = (a + b) * 0.5f, Value = RunSimulation.UnionRadius, Id = pair, Vec = b, Size = a });

        private void OnUnion(in SimEvent e)
        {
            var (a, b) = UnionCatalog.Classes(e.Id);
            Color ca = UnionColor(a), cb = UnionColor(b);
            Color mix = Color.Lerp(ca, cb, 0.5f);
            Vector2 from = e.Size, to = e.Vec, at = e.Pos;

            // The streak between the two of them, drawn as a line of sparks from each end to the middle.
            for (int k = 0; k <= 10; k++)
            {
                float t = k / 10f;
                var spark = Spawn("fx_hit", Vector2.Lerp(from, to, t) + Vector2.up * 0.6f, 20f, 0.35f, 0.7f, 0.2f, Color.Lerp(ca, cb, t), order: 2550);
                spark.FadeOut = true;
            }
            var flash = Spawn("fx_nova", at + Vector2.up * 0.4f, 1f, 0.5f, 0.6f, e.Value * 2.2f, new Color(mix.r, mix.g, mix.b, 0.9f));
            flash.FadeOut = true;
            AddLight(flash, mix, 4f, e.Value * 3f);
            var holy = Spawn("fx_holy", at + Vector2.up * 0.6f, 14f, 0.8f, 1.2f, 2.4f, Color.Lerp(mix, Color.white, 0.3f));
            holy.FadeOut = true;
            for (int i = 0; i < 12; i++) Shard(at + Vector2.up * 0.4f, i * 30f + Random.Range(0f, 15f), i % 2 == 0 ? ca : cb);
            for (int i = 0; i < 8; i++) Ember(at + Vector2.up * 0.4f, i * 45f + Random.Range(0f, 20f));
            HitStop.Freeze(0.09f, -1, major: true);
            CameraShake.Add(0.45f);
            ShowNumber(at + Vector2.up * 3.1f, "ユニオン", Color.Lerp(mix, Color.white, 0.5f), 22);
            ShowCallout(at + Vector2.up * 2.4f, UnionCatalog.Name(e.Id), Color.Lerp(mix, Color.white, 0.35f), 44);
        }
    }
}
