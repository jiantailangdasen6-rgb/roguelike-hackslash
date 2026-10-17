using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The master's sphere board (FF10-like, one round board per class). From the class at the centre three roads run
    /// outwards; two rings join them so a road can be left for its neighbour. Along each road: small spheres (a little
    /// of one stat), the class's named spheres (MetaCatalog: growth, new perks, a perk to start with), a sealed sphere
    /// that a key opens halfway out, and at the end the road's art. A sphere opens once one of its neighbours is open
    /// (the first ones touch the centre). Spheres are bought with spheres (ProfileData.spheres), seals take a key too.
    /// </summary>
    public static class SphereBoard
    {
        public const string Core = "core";
        public const int RoadLength = 10;
        public const float Unit = 1f;

        // Where each named step sits on its road (MetaCatalog Tier -> distance from the centre).
        private static readonly int[] TierRadius = { 0, 2, 4, 8, 10 };
        private const int GateRadius = 5;
        private const int InnerRing = 3, OuterRing = 7;

        private struct Small
        {
            public string Name, Text;
            public StatModifier Mod;
            public Small(string name, string text, StatType s, ModifierKind k, float v) { Name = name; Text = text; Mod = new StatModifier(s, k, v); }
        }

        // The small spheres of each road: offence, defence, technique (two kinds alternating along the road).
        private static readonly Small[][] RoadSmall =
        {
            new[] { new Small("パワー", "与えるダメージ +2%", StatType.Might, ModifierKind.PercentAdd, 0.02f),
                    new Small("クリティカル", "クリティカル率 +1.5%", StatType.CritChance, ModifierKind.Flat, 0.015f) },
            new[] { new Small("バイタリティ", "最大HP +3%", StatType.MaxHealth, ModifierKind.PercentAdd, 0.03f),
                    new Small("アーマー", "防御 +0.5", StatType.Armor, ModifierKind.Flat, 0.5f) },
            new[] { new Small("クイック", "スキルのクールダウン −2%", StatType.Cooldown, ModifierKind.PercentAdd, -0.02f),
                    new Small("ラピッド", "通常攻撃の速度 +3%", StatType.AttackSpeed, ModifierKind.PercentAdd, 0.03f) },
        };

        private static readonly Small[] InnerSmall =
        {
            new("ステップ", "移動速度 +2%", StatType.MoveSpeed, ModifierKind.PercentAdd, 0.02f),
            new("マグネット", "ソウルを拾う範囲 +8%", StatType.PickupRange, ModifierKind.PercentAdd, 0.08f),
        };

        private static readonly Small[] OuterSmall =
        {
            new("ワイド", "スキルと攻撃の範囲 +3%", StatType.Area, ModifierKind.PercentAdd, 0.03f),
            new("グレートパワー", "与えるダメージ +4%", StatType.Might, ModifierKind.PercentAdd, 0.04f),
            new("リジェネ", "毎秒HP +0.15 回復", StatType.Regen, ModifierKind.Flat, 0.15f),
        };

        // Sphere costs.
        public const int SmallCost = 1, FarSmallCost = 2, RingCost = 2, GateCost = 2, GrantCost = 6, ArtCost = 10;
        private static readonly int[] TierCost = { 0, 3, 4, 6, 10 };

        public static Vector2 RoadDir(int road) => Rotate(Vector2.up, -120f * road);

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Lays out every class's board around its named spheres (kept by id, so old saves still hold them).</summary>
        public static MasteryNode[] Build(MasteryNode[] named)
        {
            var all = new List<MasteryNode>();
            var classes = new List<ClassId>();
            foreach (var n in named) if (!classes.Contains(n.Class)) classes.Add(n.Class);
            foreach (var cls in classes) all.AddRange(BuildClass(cls, named));
            return all.ToArray();
        }

        private static string Prefix(ClassId c) => "sb" + (int)c;

        private static List<MasteryNode> BuildClass(ClassId cls, MasteryNode[] named)
        {
            var nodes = new List<MasteryNode>();
            var road = new MasteryNode[MetaCatalog.BranchCount, RoadLength + 1];
            string p = Prefix(cls);

            for (int b = 0; b < MetaCatalog.BranchCount; b++)
            {
                Vector2 dir = RoadDir(b);
                int small = 0;
                for (int r = 1; r <= RoadLength; r++)
                {
                    MasteryNode node = null;
                    int tier = System.Array.IndexOf(TierRadius, r);
                    if (tier > 0)
                        foreach (var n in named)
                            if (n.Class == cls && n.Branch == b && n.Tier == tier) node = n;
                    if (node != null)
                    {
                        node.Cost = TierCost[tier];
                        node.Major = tier >= 3 || node.Kind is MasteryKind.UnlockPerk or MasteryKind.StartPerk;
                    }
                    else if (r == GateRadius)
                        node = new MasteryNode
                        {
                            Id = $"{p}_g{b}", Class = cls, Branch = b, Kind = MasteryKind.Gate, Cost = GateCost, Keys = 1, Major = true,
                            Name = "シール", Text = "鍵で開く封印。この先のルートへ進める（鍵はボスを倒すと手に入る）",
                        };
                    else
                    {
                        var s = RoadSmall[b][small++ % 2];
                        node = Stat($"{p}_r{b}_{r}", cls, b, s, r < GateRadius ? SmallCost : FarSmallCost);
                    }
                    node.Pos = dir * (r * Unit);
                    node.Links = new List<string>();
                    road[b, r] = node;
                    nodes.Add(node);
                }
                // Along the road, and the first sphere touches the centre.
                road[b, 1].Links.Add(Core);
                for (int r = 2; r <= RoadLength; r++) Join(road[b, r - 1], road[b, r]);
            }

            // The rings: arcs between neighbouring roads.
            for (int b = 0; b < MetaCatalog.BranchCount; b++)
            {
                int next = (b + 1) % MetaCatalog.BranchCount;
                Ring(nodes, cls, p, b, road[b, InnerRing], road[next, InnerRing], InnerRing, 2, InnerSmall, "i");
                Ring(nodes, cls, p, b, road[b, OuterRing], road[next, OuterRing], OuterRing, 3, OuterSmall, "o");
            }
            return nodes;
        }

        private static void Ring(List<MasteryNode> nodes, ClassId cls, string p, int b, MasteryNode from, MasteryNode to, int radius, int count, Small[] kinds, string tag)
        {
            var prev = from;
            for (int k = 1; k <= count; k++)
            {
                var node = Stat($"{p}_{tag}{b}_{k}", cls, b, kinds[(k - 1) % kinds.Length], RingCost);
                node.Pos = Rotate(RoadDir(b), -120f * k / (count + 1)) * (radius * Unit);
                node.Links = new List<string>();
                if (count == 3 && k == 2) { node.Major = true; node.Cost = RingCost + 1; }
                nodes.Add(node);
                Join(prev, node);
                prev = node;
            }
            Join(prev, to);
        }

        private static MasteryNode Stat(string id, ClassId cls, int branch, Small s, int cost) => new()
        {
            Id = id, Class = cls, Branch = branch, Kind = MasteryKind.Stat, Modifier = s.Mod, Cost = cost, Name = s.Name, Text = s.Text,
        };

        private static void Join(MasteryNode a, MasteryNode b)
        {
            if (!a.Links.Contains(b.Id)) a.Links.Add(b.Id);
            if (!b.Links.Contains(a.Id)) b.Links.Add(a.Id);
        }

        /// <summary>Spheres earned for a run: a little for the rooms, more for each boss, a lot for the end.</summary>
        public static int SpheresForRun(int roomsCleared, int bossesKilled, bool victory) =>
            2 + roomsCleared / 2 + bossesKilled * 3 + (victory ? 5 : 0);

        /// <summary>Keys earned for a run: one for each boss that fell.</summary>
        public static int KeysForRun(int bossesKilled) => bossesKilled;
    }
}
