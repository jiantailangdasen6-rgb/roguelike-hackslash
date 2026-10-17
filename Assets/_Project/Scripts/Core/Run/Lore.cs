using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// The story (物語). Deep below lies the heart of a god that once lit the City of the Flame. Its fire is turning
    /// to ash and the labyrinth is its nightmare; the six lords are the city's guardians who went down to save it and
    /// were made monsters by it. The camp's fire is the heart's last ember: it calls the fallen back. Each land keeps an
    /// inscription (read on entering it) and each lord a memory (won by slaying it, kept in the profile): with all six
    /// memories the way to the heart opens past the third lord, where the Ashen King waits. Shown in the codex (記録).
    /// </summary>
    public static class Lore
    {
        /// <summary>One memory per land with a lord (bit = 1 &lt;&lt; Biome).</summary>
        public const int AllMemories = (1 << 6) - 1;

        public static int MemoryBit(Biome land) => (int)land < 6 ? 1 << (int)land : 0;

        public static int Count(int memories)
        {
            int n = 0;
            for (int i = 0; i < 6; i++) if ((memories & (1 << i)) != 0) n++;
            return n;
        }

        public static string InscriptionId(Biome land) => "lore:land:" + land;
        public static string MemoryId(Biome land) => "lore:lord:" + land;
        /// <summary>The memory a lord leaves: each land's first lord its land's, a second lord its own (both count for the land).</summary>
        public static string MemoryIdOf(EnemyType lord) =>
            BiomeCatalog.Get(BiomeCatalog.OfLord(lord)).Lord == lord ? MemoryId(BiomeCatalog.OfLord(lord)) : "lore:lord2:" + lord;
        public const string HeartId = "lore:heart";
        public const string EndingId = "lore:ending";

        public sealed class Text
        {
            public string Id, Name, Sub, Mark;
            public string[] Lines;
        }

        private static Text T(string id, string name, string sub, string mark, params string[] lines) =>
            new() { Id = id, Name = name, Sub = sub, Mark = mark, Lines = lines };

        public static readonly Text[] All =
        {
            T(InscriptionId(Biome.Crypt), "クリプトのエピタフ", "ロッテンクリプト", "碑",
                "ここに眠るは灯火の都の民。", "王は誓った。心臓の火が尽きる日まで、我らは骨となってもこの門を守ると。"),
            T(InscriptionId(Biome.Chapel), "チャペルのエピタフ", "ブラッドチャペル", "碑",
                "心臓は神の胸から取り出され、この地の底に納められた。", "祈りで火を保てるとビショップは言った。だが祈りは、やがて血を求めはじめた。"),
            T(InscriptionId(Biome.Abyss), "アビスのエピタフ", "アビススローン", "碑",
                "宮廷の魔導師は、灰になりゆく火をソウルでつなぎとめようとした。", "玉座の下の闇は、彼がこじ開けた穴である。"),
            T(InscriptionId(Biome.Sunken), "水底のエピタフ", "サンケンカタコンベ", "碑",
                "港の巫女は、地下に満ちる水で心臓の熱を鎮めようとした。", "水は火を守らず、街の死者を呼び寄せた。"),
            T(InscriptionId(Biome.Frost), "モーソレアムのエピタフ", "フローズンモーソレアム", "碑",
                "クイーンは時を凍らせ、火の消える日を遠ざけた。", "凍った時の中で、クイーンだけが歳を取り続けた。"),
            T(InscriptionId(Biome.Forge), "フォージのエピタフ", "バーントフォージ", "碑",
                "王のブラックスミス師は、心臓を収める炉を打った。", "炉は火を閉じこめ、ブラックスミス師もろとも燃え続けている。"),

            T(MemoryId(Biome.Crypt), "スケルトンキングのメモリー", "スケルトンキング", "憶",
                "最後に降りたのは私だった。民の骨を連れて。", "……門はまだ閉じているか？　ならばいい。灯火を、頼む。"),
            T(MemoryId(Biome.Chapel), "ビショップのメモリー", "ブラッドビショップ", "憶",
                "神よ、お許しを。火を守るために血を注いだ。", "注ぐほどに火は暗くなり、私は赤く染まっていった。"),
            T(MemoryId(Biome.Abyss), "リッチキングのメモリー", "リッチキング", "憶",
                "ソウルをつなげば火は消えぬと思った。だが私がつないだのは、灰と悪夢だった。", "玉座の下で、最初の王が呼んでいる。"),
            T(MemoryId(Biome.Sunken), "セイントのメモリー", "ドラウンドセイント", "憶",
                "水は冷たく、静かで、やさしかった。眠れば痛まないと思ったの。", "……ああ、灯りが見える。あなたたちの灯りが。"),
            T(MemoryId(Biome.Frost), "クイーンのメモリー", "フロストクイーン", "憶",
                "凍らせれば、終わらないと思った。終わらないことは、生きることではなかった。", "氷の外は、まだ春か？"),
            T(MemoryId(Biome.Forge), "ジャイアントのメモリー", "フォージコロッサス", "憶",
                "炉は割れぬ。俺が打った炉だ。", "だが火は炉にではなく、運ぶ者の手に宿るものだった。……持っていけ。"),

            T("lore:lord2:" + EnemyType.GraveReaper, "デスサイズのメモリー", "グレイヴリーパー", "憶",
                "私は死者を数える役目だった。数えきれなくなった日、鎌を取った。", "……もう、数えなくていいのか。"),
            T("lore:lord2:" + EnemyType.ChoirMistress, "聖歌隊長のメモリー", "フォールンカンター", "憶",
                "歌えば神に届くと信じていた。届いたのは、ラビリンスの底の灰の声だけ。", "最後にもう一度、昼の歌を聴かせて。"),
            T("lore:lord2:" + EnemyType.VoidEye, "見張りのメモリー", "ホロウアイ", "憶",
                "私は宮廷の見張りだった。心臓の火を見つめ続けるうちに、目だけが残った。", "見るのは、もうファティーグた。"),
            T("lore:lord2:" + EnemyType.GreatCrab, "港の守りのメモリー", "マイアクラブ", "憶",
                "港の守り神と呼ばれた。巫女が沈んだ日から、その墓を抱いている。", "殻を割ったのはお前たちか。……巫女に、よろしく。"),
            T("lore:lord2:" + EnemyType.GreatWolf, "猟犬のメモリー", "ホワイトウルフ", "憶",
                "クイーンの猟犬だった。凍った時の中で、主人の帰りを待ち続けた。", "……もう、待たなくていいのだな。"),
            T("lore:lord2:" + EnemyType.Executioner, "エクスキューショナーのメモリー", "チェインエクスキューショナー", "憶",
                "王の命で、心臓に近づく者を斬ってきた。最後に斬るはずだったのは、王自身だった。", "鎖を解いてくれて、礼を言う。"),

            // The two lands found later (the grotto and the library): their stories sit beside the six, not in the heart's way.
            T(InscriptionId(Biome.Grotto), "グロットのエピタフ", "ファンガルグロット", "碑",
                "都の薬師たちは、灰を養分に育つ茸を見つけた。", "茸は火の熱をゆっくり吸い、代わりに眠りの胞子を吐いた。"),
            T(InscriptionId(Biome.Library), "ライブラリのエピタフ", "カースドライブラリ", "碑",
                "都のすべての知恵は、このライブラリに集められた。", "火を救う方法を探すうち、書き手たちは読むことをやめられなくなった。"),
            T(MemoryId(Biome.Grotto), "母のメモリー", "マイセリアマザー", "憶",
                "眠れば痛みは消える。だから皆を眠らせてあげたかったの。", "……起こしてくれたのね。朝の匂いがする。"),
            T(MemoryId(Biome.Library), "ライブラリアンのメモリー", "グランドライブラリアン", "憶",
                "答えはどこかの頁にあるはずだった。最後の一冊を読み終えるまでは。", "読みかけの本は、君たちに預けよう。"),
            T("lore:lord2:" + EnemyType.RotGiant, "庭師のメモリー", "ロットジャイアント", "憶",
                "都の庭を守っていた。茸が根を張ったのは、私の体のほうだった。", "土に還るのは、怖くない。"),
            T("lore:lord2:" + EnemyType.InkBeast, "墨壺のメモリー", "インクビースト", "憶",
                "私は書き手たちの墨壺だった。書き損じた悪夢が溜まって、獣になった。", "灯りの下でなら、もう一度ただの墨に戻れる。"),

            T(HeartId, "ラビリンスハート", "六つのメモリーの先", "心",
                "脈打つ灰の大広間。神の心臓はここで眠り、悪夢を見ている。",
                "悪夢の名はアッシュキング。都の最初の王にして、最初に灯火を守ると誓い、最初に火を独り占めしようとした者。",
                "キャンプのキャンプファイアは、この心臓から零れた最後の火種だ。倒れた者を呼び戻していたのは、あの火だった。"),
            T(EndingId, "灯火を継ぐ者", "心臓に火が戻った", "灯",
                "アッシュキングは崩れ落ち、心臓に火が戻った。",
                "六人の守り手のソウルは、小さな灯りとなって昇っていった。",
                "キャンプのキャンプファイアは、もう最後の火種ではない。けれど灯火は、運ぶ者がいて初めて灯り続ける。"),
        };

        /// <summary>The words of the ending, line by line (EndingView).</summary>
        public static readonly string[] Ending =
        {
            "アッシュキングは崩れ落ち、心臓に火が戻った。",
            "六人の守り手のソウルは、小さな灯りとなって昇っていった。",
            "ラビリンスの闇はゆっくりと引き、地の底に朝の光が差した。",
            "キャンプのキャンプファイアは、もう最後の火種ではない。",
            "けれど灯火は、運ぶ者がいて初めて灯り続ける。",
            "――あなたたちが、灯火を継ぐ者だ。",
        };

        public static Text Get(string id)
        {
            foreach (var t in All) if (t.Id == id) return t;
            return null;
        }
    }
}
