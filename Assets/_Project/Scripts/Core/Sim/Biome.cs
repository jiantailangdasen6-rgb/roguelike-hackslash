using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// A land of the labyrinth: its look, its monsters, its champion and its lord. Lands are not tied to a depth:
    /// any of them can be the first, second or third layer (monsters grow stronger with the depth, not the land),
    /// and after each lord the team picks the next land from two ways down.
    /// </summary>
    public enum Biome : byte
    {
        Crypt,  // 朽ちた納骨堂: grey stone, bones, candles
        Chapel, // 血塗られた礼拝堂: marble, crimson carpet, stained glass
        Abyss,  // 奈落の玉座: obsidian, violet cracks, chains
        Sunken, // 水没した地下墓地: wet green stone, shallow water, drowned dead
        Frost,  // 凍てつく霊廟: ice-crusted marble, snow, a frozen court
        Forge,  // 燃え落ちた鍛冶場: charred brick and iron, molten cracks
        Heart,  // 迷宮の心臓: the bottom of it all (only through the way the six lords' memories open)
    }

    public sealed class BiomeDef
    {
        public Biome Id;
        public string Name;
        /// <summary>One line for the way-down signs (what waits there).</summary>
        public string Hint;
        public EnemyType Lord;
        /// <summary>A second lord who may wait at the end instead (each run picks one; null for none).</summary>
        public EnemyType? AltLord;
        public EnemyType[] Lords => AltLord.HasValue ? new[] { Lord, AltLord.Value } : new[] { Lord };
        public EnemyType Champion;
        /// <summary>Who comes in its waves, and from which layer on (0 = from the first).</summary>
        public (EnemyType Type, int Weight, int MinLayer)[] Pool;
        /// <summary>Fight-room layouts that suit it (Arena ids; repeats weigh more).</summary>
        public int[] FightLayouts;
        public string Music;
        /// <summary>Never picked for a route (the heart is reached by its own way).</summary>
        public bool Special;
    }

    public static class BiomeCatalog
    {
        public static readonly BiomeDef[] All =
        {
            new()
            {
                Id = Biome.Crypt, Name = "朽ちた納骨堂", Hint = "骸骨の群れと、二人の主のどちらか", Lord = EnemyType.BoneKing, AltLord = EnemyType.GraveReaper, Champion = EnemyType.DreadKnight, Music = "music_crypt",
                Pool = new[]
                {
                    (EnemyType.Skeleton, 38, 0), (EnemyType.SkeletonArcher, 20, 0), (EnemyType.Ghoul, 18, 0), (EnemyType.Bat, 9, 0),
                    (EnemyType.ShieldSkeleton, 10, 0), (EnemyType.DreadKnight, 7, 1), (EnemyType.Wraith, 8, 2),
                },
                FightLayouts = new[] { 1, 2, 3, 4, 5, 6, 7 },
            },
            new()
            {
                Id = Biome.Chapel, Name = "血塗られた礼拝堂", Hint = "狂信者の儀式と、二人の主のどちらか", Lord = EnemyType.BloodBishop, AltLord = EnemyType.ChoirMistress, Champion = EnemyType.Ghoul, Music = "music_chapel",
                Pool = new[]
                {
                    (EnemyType.Skeleton, 20, 0), (EnemyType.SkeletonArcher, 15, 0), (EnemyType.Ghoul, 14, 0), (EnemyType.Cultist, 14, 0),
                    (EnemyType.Bat, 7, 0), (EnemyType.Bloater, 14, 0), (EnemyType.Flagellant, 14, 0), (EnemyType.DreadKnight, 11, 1),
                },
                FightLayouts = new[] { 1, 2, 4, 5, 6, 7, 8, 8 },
            },
            new()
            {
                Id = Biome.Abyss, Name = "奈落の玉座", Hint = "影と呪い、二人の主のどちらか", Lord = EnemyType.Lich, AltLord = EnemyType.VoidEye, Champion = EnemyType.Necromancer, Music = "music_abyss",
                Pool = new[]
                {
                    (EnemyType.Skeleton, 10, 0), (EnemyType.SkeletonArcher, 12, 0), (EnemyType.Ghoul, 12, 0), (EnemyType.Cultist, 11, 0),
                    (EnemyType.Bat, 6, 0), (EnemyType.Bloater, 11, 0), (EnemyType.Wraith, 13, 0), (EnemyType.AbyssEye, 11, 0),
                    (EnemyType.ShadeStalker, 11, 0), (EnemyType.DreadKnight, 12, 1), (EnemyType.Necromancer, 9, 1),
                },
                FightLayouts = new[] { 2, 4, 5, 6, 7, 9, 9, 3 },
            },
            new()
            {
                Id = Biome.Sunken, Name = "水没した地下墓地", Hint = "溺れた亡者と、二人の主のどちらか", Lord = EnemyType.DrownedSaint, AltLord = EnemyType.GreatCrab, Champion = EnemyType.Drowned, Music = "music_sunken",
                Pool = new[]
                {
                    (EnemyType.Drowned, 28, 0), (EnemyType.CarrionCrab, 16, 0), (EnemyType.TideWitch, 12, 0), (EnemyType.Skeleton, 14, 0),
                    (EnemyType.Ghoul, 10, 0), (EnemyType.Bat, 6, 0), (EnemyType.Bloater, 8, 1), (EnemyType.Wraith, 8, 1),
                },
                FightLayouts = new[] { 15, 16, 17, 15, 3, 7 },
            },
            new()
            {
                Id = Biome.Frost, Name = "凍てつく霊廟", Hint = "雪狼の群れと、二人の主のどちらか", Lord = EnemyType.FrostQueen, AltLord = EnemyType.GreatWolf, Champion = EnemyType.Icebound, Music = "music_frost",
                Pool = new[]
                {
                    (EnemyType.FrostWolf, 18, 0), (EnemyType.Icebound, 18, 0), (EnemyType.FrostWitch, 12, 0), (EnemyType.Skeleton, 14, 0),
                    (EnemyType.SkeletonArcher, 14, 0), (EnemyType.ShieldSkeleton, 8, 0), (EnemyType.Wraith, 8, 1), (EnemyType.DreadKnight, 6, 2),
                },
                FightLayouts = new[] { 18, 19, 20, 18, 2, 4 },
            },
            new()
            {
                Id = Biome.Forge, Name = "燃え落ちた鍛冶場", Hint = "鉄の番兵と、二人の主のどちらか", Lord = EnemyType.ForgeColossus, AltLord = EnemyType.Executioner, Champion = EnemyType.IronSentinel, Music = "music_forge",
                Pool = new[]
                {
                    (EnemyType.IronSentinel, 12, 0), (EnemyType.FireImp, 16, 0), (EnemyType.SlagBloater, 14, 0), (EnemyType.Skeleton, 16, 0),
                    (EnemyType.Cultist, 10, 0), (EnemyType.Ghoul, 10, 0), (EnemyType.DreadKnight, 8, 1),
                },
                FightLayouts = new[] { 21, 22, 23, 21, 5, 6 },
            },
            new()
            {
                // The heart of the labyrinth: every land's dead walk here, and the Ashen King waits at the bottom.
                Id = Biome.Heart, Name = "迷宮の心臓", Hint = "灰の王が待つ", Lord = EnemyType.AshenKing, Champion = EnemyType.DreadKnight, Music = "music_heart", Special = true,
                Pool = new[]
                {
                    (EnemyType.Drowned, 10, 0), (EnemyType.FrostWolf, 10, 0), (EnemyType.IronSentinel, 8, 0), (EnemyType.ShadeStalker, 10, 0),
                    (EnemyType.Wraith, 10, 0), (EnemyType.Cultist, 8, 0), (EnemyType.DreadKnight, 8, 0), (EnemyType.Icebound, 8, 0),
                    (EnemyType.FireImp, 8, 0), (EnemyType.Necromancer, 6, 0), (EnemyType.Skeleton, 10, 0),
                },
                FightLayouts = new[] { 2, 9, 15, 18, 21, 4 },
            },
        };

        /// <summary>Lands a route can lead through (all but the heart).</summary>
        public static readonly BiomeDef[] Routable = System.Array.FindAll(All, b => !b.Special);

        public static int Count => All.Length;

        public static BiomeDef Get(Biome b) => All[(int)b < All.Length ? (int)b : 0];

        /// <summary>The fixed order of the first version (tests, and the classic route): crypt, chapel, abyss, again and again.</summary>
        public static Biome Classic(int area) => (Biome)(area % 3);

        /// <summary>The land whose lord this is (the crypt for anything else).</summary>
        public static Biome OfLord(EnemyType lord)
        {
            foreach (var b in All) if (b.Lord == lord || b.AltLord == lord) return b.Id;
            return Biome.Crypt;
        }

        /// <summary>Monsters that can come in a land's waves at this layer, with their weights.</summary>
        public static List<(EnemyType Type, int Weight)> PoolAt(Biome biome, int layer)
        {
            var list = new List<(EnemyType, int)>();
            foreach (var (type, weight, min) in Get(biome).Pool)
                if (layer >= min) list.Add((type, weight));
            return list;
        }
    }
}
