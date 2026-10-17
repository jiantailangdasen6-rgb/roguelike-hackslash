using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>A cosmetic colour scheme for a class. Index 0 is the default look.</summary>
    public sealed class SkinDef
    {
        public string Id;
        public ClassId Class;
        public int Index;
        public string Name;
        public int Cost; // soul shards; 0 = owned from the start
    }

    public static class SkinCatalog
    {
        public static readonly SkinDef[] All =
        {
            new() { Id = "warrior_0", Class = ClassId.Warrior, Index = 0, Name = "スチールソードマン", Cost = 0 },
            new() { Id = "warrior_1", Class = ClassId.Warrior, Index = 1, Name = "ブラックナイト", Cost = 60 },
            new() { Id = "warrior_2", Class = ClassId.Warrior, Index = 2, Name = "ホーリーナイト", Cost = 90 },
            new() { Id = "warrior_3", Class = ClassId.Warrior, Index = 3, Name = "ブラッドバーサーカー", Cost = 120 },

            new() { Id = "mage_0", Class = ClassId.Mage, Index = 0, Name = "トワイライトメイジ", Cost = 0 },
            new() { Id = "mage_1", Class = ClassId.Mage, Index = 1, Name = "フロストセージ", Cost = 60 },
            new() { Id = "mage_2", Class = ClassId.Mage, Index = 2, Name = "クリムゾンウィッチ", Cost = 90 },
            new() { Id = "mage_3", Class = ClassId.Mage, Index = 3, Name = "グリーンハーミット", Cost = 120 },

            new() { Id = "ranger_0", Class = ClassId.Ranger, Index = 0, Name = "フォレストハンター", Cost = 0 },
            new() { Id = "ranger_1", Class = ClassId.Ranger, Index = 1, Name = "シャドウトラッカー", Cost = 60 },
            new() { Id = "ranger_2", Class = ClassId.Ranger, Index = 2, Name = "オータムワンダラー", Cost = 90 },
            new() { Id = "ranger_3", Class = ClassId.Ranger, Index = 3, Name = "スノーアーチャー", Cost = 120 },

            new() { Id = "guardian_0", Class = ClassId.Guardian, Index = 0, Name = "シルバーガーディアン", Cost = 0 },
            new() { Id = "guardian_1", Class = ClassId.Guardian, Index = 1, Name = "アイアンゲートキーパー", Cost = 60 },
            new() { Id = "guardian_2", Class = ClassId.Guardian, Index = 2, Name = "ゴールデンテンプラー", Cost = 90 },
            new() { Id = "guardian_3", Class = ClassId.Guardian, Index = 3, Name = "モスヴェテラン", Cost = 120 },

            new() { Id = "assassin_0", Class = ClassId.Assassin, Index = 0, Name = "ナイトブレード", Cost = 0 },
            new() { Id = "assassin_1", Class = ClassId.Assassin, Index = 1, Name = "クリムゾンシャドウ", Cost = 60 },
            new() { Id = "assassin_2", Class = ClassId.Assassin, Index = 2, Name = "アッシュゴースト", Cost = 90 },
            new() { Id = "assassin_3", Class = ClassId.Assassin, Index = 3, Name = "ヴァイパー", Cost = 120 },

            new() { Id = "alchemist_0", Class = ClassId.Alchemist, Index = 0, Name = "スーティーアルケミスト", Cost = 0 },
            new() { Id = "alchemist_1", Class = ClassId.Alchemist, Index = 1, Name = "ジェードアポセカリー", Cost = 60 },
            new() { Id = "alchemist_2", Class = ClassId.Alchemist, Index = 2, Name = "パープルスモークブリュワー", Cost = 90 },
            new() { Id = "alchemist_3", Class = ClassId.Alchemist, Index = 3, Name = "ゴールデンセージ", Cost = 120 },

            new() { Id = "deathcaller_0", Class = ClassId.Deathcaller, Index = 0, Name = "グレイヴネクロマンサー", Cost = 0 },
            new() { Id = "deathcaller_1", Class = ClassId.Deathcaller, Index = 1, Name = "ブラッドシャーマン", Cost = 60 },
            new() { Id = "deathcaller_2", Class = ClassId.Deathcaller, Index = 2, Name = "アッシュプリーステス", Cost = 90 },
            new() { Id = "deathcaller_3", Class = ClassId.Deathcaller, Index = 3, Name = "アビスメンター", Cost = 120 },

            new() { Id = "paladin_0", Class = ClassId.Paladin, Index = 0, Name = "スカイパラディン", Cost = 0 },
            new() { Id = "paladin_1", Class = ClassId.Paladin, Index = 1, Name = "スカーレットオースキーパー", Cost = 60 },
            new() { Id = "paladin_2", Class = ClassId.Paladin, Index = 2, Name = "オブシディアンインクイジター", Cost = 90 },
            new() { Id = "paladin_3", Class = ClassId.Paladin, Index = 3, Name = "ホワイトリリーナイト", Cost = 120 },
        };

        public const int PerClass = 4;

        public static IEnumerable<SkinDef> For(ClassId c) => All.Where(s => s.Class == c).OrderBy(s => s.Index);

        public static SkinDef Get(ClassId c, int index) =>
            All.FirstOrDefault(s => s.Class == c && s.Index == index) ?? All.First(s => s.Class == c && s.Index == 0);

        public static SkinDef GetById(string id) => All.FirstOrDefault(s => s.Id == id);

        /// <summary>Sprite sheet for a class in a skin ("warrior", "warrior_s1", ...).</summary>
        public static string Sheet(ClassId c, int index, int weapon = 0) =>
            (index <= 0 || index >= PerClass ? ClassCatalog.Get(c).Sheet : $"{ClassCatalog.Get(c).Sheet}_s{index}") + (weapon == 1 ? "_w1" : "");

        /// <summary>The kind of weapon a loadout carries (0 usual, 1 the class's other weapon).</summary>
        public static int WeaponOf(Loadout loadout)
        {
            if (loadout?.gear == null) return 0;
            foreach (var hex in loadout.gear)
            {
                var item = GearVault.Unpack(hex);
                if (item != null && item.Slot == EquipSlot.Weapon) return item.baseId == "weapon_alt" ? 1 : 0;
            }
            return 0;
        }
    }
}
