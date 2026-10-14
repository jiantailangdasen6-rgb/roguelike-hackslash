using Game.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// The colours of each land (Biome): its ambient light, its door torches, the sign of a way that leads there, and
    /// its colour grading (a tint over the whole picture, so a land feels like itself at a glance).
    /// </summary>
    public static class LandLook
    {
        private readonly struct Look
        {
            public readonly Color Ambient, Torch, Sign, Filter;
            public readonly float Intensity;
            public readonly Vector4 Lift, Gain;

            public Look(Color ambient, float intensity, Color torch, Color sign, Color filter, Vector4 lift, Vector4 gain)
            {
                Ambient = ambient; Intensity = intensity; Torch = torch; Sign = sign; Filter = filter; Lift = lift; Gain = gain;
            }
        }

        private static readonly Look[] Looks =
        {
            // crypt: warm grey, candle orange
            new(new Color(0.9f, 0.82f, 0.8f), 0.8f, new Color(1f, 0.62f, 0.32f), new Color(1f, 0.82f, 0.55f),
                Color.white, new Vector4(0.98f, 0.97f, 1.04f, 0f), new Vector4(1f, 1f, 1f, 0f)),
            // chapel: blood red
            new(new Color(0.95f, 0.72f, 0.7f), 0.72f, new Color(1f, 0.35f, 0.28f), new Color(1f, 0.45f, 0.4f),
                new Color(1f, 0.95f, 0.94f), new Vector4(1.03f, 0.96f, 0.98f, 0f), new Vector4(1.03f, 0.98f, 0.97f, 0f)),
            // abyss: cold violet
            new(new Color(0.72f, 0.7f, 0.95f), 0.7f, new Color(0.72f, 0.48f, 1f), new Color(0.75f, 0.58f, 1f),
                Color.white, new Vector4(1f, 0.96f, 1.06f, 0f), new Vector4(1f, 0.98f, 1.03f, 0f)),
            // sunken: drowned teal-green
            new(new Color(0.76f, 0.9f, 0.86f), 0.82f, new Color(0.4f, 0.95f, 0.8f), new Color(0.45f, 1f, 0.85f),
                new Color(0.94f, 1f, 0.97f), new Vector4(0.97f, 1.01f, 1.01f, 0f), new Vector4(0.97f, 1.02f, 1f, 0f)),
            // frost: pale blue
            new(new Color(0.8f, 0.88f, 1f), 0.82f, new Color(0.55f, 0.8f, 1f), new Color(0.7f, 0.88f, 1f),
                new Color(0.94f, 0.97f, 1f), new Vector4(0.97f, 1f, 1.05f, 0f), new Vector4(0.99f, 1.01f, 1.05f, 0f)),
            // forge: ember orange
            new(new Color(0.96f, 0.82f, 0.72f), 0.8f, new Color(1f, 0.55f, 0.22f), new Color(1f, 0.6f, 0.3f),
                new Color(1f, 0.95f, 0.9f), new Vector4(1.03f, 0.99f, 0.96f, 0f), new Vector4(1.04f, 0.99f, 0.95f, 0f)),
            // heart: ash-red with ember gold
            new(new Color(0.95f, 0.7f, 0.62f), 0.72f, new Color(1f, 0.55f, 0.25f), new Color(1f, 0.5f, 0.25f),
                new Color(1f, 0.92f, 0.88f), new Vector4(1.04f, 0.97f, 0.96f, 0f), new Vector4(1.05f, 0.98f, 0.94f, 0f)),
        };

        private static int Index(Biome b) => Mathf.Clamp((int)b, 0, Looks.Length - 1);

        public static Color Ambient(Biome b) => Looks[Index(b)].Ambient;
        public static float AmbientIntensity(Biome b) => Looks[Index(b)].Intensity;
        public static Color Torch(Biome b) => Looks[Index(b)].Torch;
        public static Color Sign(Biome b) => Looks[Index(b)].Sign;

        private static Volume _volume;
        private static ColorAdjustments _color;
        private static LiftGammaGain _lift;

        /// <summary>Tints the whole picture for a land (a global volume over the scene's own post-processing).</summary>
        public static void ApplyGrading(Biome b)
        {
            if (_volume == null)
            {
                var go = new GameObject("Land Grading");
                _volume = go.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 5f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _color = profile.Add<ColorAdjustments>();
                _lift = profile.Add<LiftGammaGain>();
                _volume.sharedProfile = profile;
            }
            var look = Looks[Index(b)];
            _color.colorFilter.Override(look.Filter);
            _lift.lift.Override(look.Lift);
            _lift.gain.Override(look.Gain);
        }
    }
}
