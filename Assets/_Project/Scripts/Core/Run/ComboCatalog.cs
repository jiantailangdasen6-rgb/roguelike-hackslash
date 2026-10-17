using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Names of the combination techniques, the two classes each needs and how to set it off (HUD hints).</summary>
    public static class ComboCatalog
    {
        public readonly struct ComboDef
        {
            public readonly ComboKind Kind;
            public readonly ClassId A, B;
            public readonly string Name, How;

            public ComboDef(ComboKind kind, ClassId a, ClassId b, string name, string how)
            {
                Kind = kind;
                A = a;
                B = b;
                Name = name;
                How = how;
            }
        }

        public static readonly ComboDef[] All =
        {
            new(ComboKind.SteamBurst, ClassId.Mage, ClassId.Alchemist, "スチームバースト", "燃える床にフロストノヴァを重ねる"),
            new(ComboKind.ShadowExecution, ClassId.Assassin, ClassId.Ranger, "シャドウエクセキュート", "マークの付いた敵へシャドウステップ"),
            new(ComboKind.HolyWarCry, ClassId.Warrior, ClassId.Guardian, "サンクチュアリクライ", "サンクチュアリの中でウォークライ"),
            new(ComboKind.FlameCharge, ClassId.Guardian, ClassId.Alchemist, "フレイムチャージ", "燃える床を通ってシールドバッシュ"),
        };

        public static string Name(ComboKind kind)
        {
            foreach (var c in All) if (c.Kind == kind) return c.Name;
            return kind == ComboKind.Union ? "ユニオン" : ""; // any two players (UnionCatalog names each pair)
        }

        /// <summary>The techniques this party can perform (both classes present, played by different people).</summary>
        public static List<ComboDef> Available(IReadOnlyList<ClassId> party)
        {
            var result = new List<ComboDef>();
            foreach (var c in All)
            {
                bool a = false, b = false;
                foreach (var cls in party)
                {
                    if (cls == c.A) a = true;
                    if (cls == c.B) b = true;
                }
                if (a && b) result.Add(c);
            }
            return result;
        }
    }
}
