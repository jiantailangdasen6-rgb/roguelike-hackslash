using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>The colours of each land (Biome): its ambient light, its door torches and the sign of a way that leads there.</summary>
    public static class LandLook
    {
        private static readonly (Color Ambient, float Intensity, Color Torch, Color Sign)[] Looks =
        {
            (new Color(0.9f, 0.82f, 0.8f), 0.8f, new Color(1f, 0.62f, 0.32f), new Color(1f, 0.82f, 0.55f)),  // crypt: warm grey, candle orange
            (new Color(0.95f, 0.72f, 0.7f), 0.72f, new Color(1f, 0.35f, 0.28f), new Color(1f, 0.45f, 0.4f)),  // chapel: blood red
            (new Color(0.72f, 0.7f, 0.95f), 0.7f, new Color(0.72f, 0.48f, 1f), new Color(0.75f, 0.58f, 1f)),  // abyss: cold violet
        };

        private static int Index(Biome b) => Mathf.Clamp((int)b, 0, Looks.Length - 1);

        public static Color Ambient(Biome b) => Looks[Index(b)].Ambient;
        public static float AmbientIntensity(Biome b) => Looks[Index(b)].Intensity;
        public static Color Torch(Biome b) => Looks[Index(b)].Torch;
        public static Color Sign(Biome b) => Looks[Index(b)].Sign;
    }
}
