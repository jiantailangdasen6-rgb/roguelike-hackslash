using System.Collections.Generic;

namespace Game.Core
{
    public enum CodexSection : byte
    {
        Monsters,
        Lords,
        Records,  // the story: inscriptions, the lords' memories, the heart (Lore)
        Gimmicks,
        Rooms,
        Gods,
        Achievements,
        Teamwork,
        Controls,
        Gear,     // phase 4: each class's other weapon and the runes (always open)
    }

    /// <summary>
    /// The codex (図鑑): what the game used to explain in toasts and HUD lines (monster traits, traps, rooms,
    /// teamwork, controls) lives here instead. Monsters, lords, gimmicks and rooms are recorded as they are met
    /// (ProfileData.codexSeen); teamwork and controls are always open.
    /// </summary>
    public static class CodexCatalog
    {
        public sealed class Entry
        {
            public string Id;
            public CodexSection Section;
            public string Name;
            /// <summary>One line under the name (a title, a role).</summary>
            public string Sub;
            /// <summary>Sprite sheet for the picture (Resources/Sheets); its "idle" clip or its first one.</summary>
            public string Sheet;
            /// <summary>Icon from icons_reward (by RoomReward) instead of a sheet, -1 for none.</summary>
            public int RewardIcon = -1;
            /// <summary>An icon that fills its tile (sheet + frame), e.g. a skill icon for teamwork; null for none.</summary>
            public string IconSheet;
            public int IconFrame;
            /// <summary>The picture fills its frame (icons): shown smaller than characters, not cropped.</summary>
            public bool FillsFrame => IconSheet != null || RewardIcon >= 0;
            /// <summary>A map mark drawn as a tile when there is no picture.</summary>
            public string Mark;
            public string[] Traits;
            public string Counter;
            public bool AlwaysKnown;
        }

        public static string SectionName(CodexSection s) => s switch
        {
            CodexSection.Monsters => "モンスター",
            CodexSection.Lords => "ラビリンスのボス",
            CodexSection.Records => "記録",
            CodexSection.Gimmicks => "仕掛け・罠",
            CodexSection.Rooms => "部屋",
            CodexSection.Gods => "神々",
            CodexSection.Achievements => "アチーブメント",
            CodexSection.Teamwork => "連携",
            CodexSection.Gear => "武器とルーン",
            _ => "操作",
        };

        public static string MonsterId(EnemyType t) => "enemy:" + t;
        public static string RoomId(RoomKind k) => "room:" + k;
        public static string GimmickId(string name) => "gimmick:" + name;
        public static string GodEntry(GodId god) => "god:" + god;
        public static string AchievementEntry(string id) => "ach:" + id;

        private static Entry Monster(EnemyType t, string sub, string counter, params string[] traits)
        {
            var def = EnemyCatalog.Get(t);
            return new Entry { Id = MonsterId(t), Section = def.Boss ? CodexSection.Lords : CodexSection.Monsters, Name = def.Name, Sub = def.Title ?? sub, Sheet = def.Sheet, Traits = traits, Counter = counter };
        }

        private static Entry Room(RoomKind k, string sub, string counter, params string[] traits) => new()
        {
            Id = RoomId(k), Section = CodexSection.Rooms, Name = RoomCatalog.Name(k), Sub = sub, Mark = RoomCatalog.Mark(k),
            RewardIcon = RoomCatalog.Reward(k) == RoomReward.None ? -1 : (int)RoomCatalog.Reward(k), Traits = traits, Counter = counter,
        };

        private static Entry Gimmick(string id, string name, string sheet, string sub, string counter, params string[] traits) => new()
        {
            Id = GimmickId(id), Section = CodexSection.Gimmicks, Name = name, Sheet = sheet, Sub = sub, Traits = traits, Counter = counter,
        };

        private static Entry Open(CodexSection section, string id, string name, string sub, string sheet, string counter, params string[] traits) => new()
        {
            Id = id, Section = section, Name = name, Sub = sub, Sheet = sheet, Traits = traits, Counter = counter, AlwaysKnown = true,
        };

        public static readonly List<Entry> All = Build();

        private static List<Entry> Build()
        {
            var list = new List<Entry>
            {
                // ---------------------------------------------------------------- Monsters
                Monster(EnemyType.Skeleton, "ラビリンスにもっとも多い亡者", "扇の予兆が出たら横へ抜ける。群れは範囲攻撃でまとめて崩す",
                    "前方を扇状に薙ぐ（足元に扇の予兆）", "数で押し寄せる"),
                Monster(EnemyType.SkeletonArcher, "距離を取って射る", "赤い線から外れる。近づけば脆い",
                    "離れた位置を保ち、矢を放つ（線の予兆の方向へ飛ぶ）", "体力は低い"),
                Monster(EnemyType.Ghoul, "飢えたスウィフトの屍", "飛びかかりの線を見てドッジし、着地の隙に叩く",
                    "素早く距離を詰め、一直線に飛びかかる"),
                Monster(EnemyType.Cultist, "カースを唱える信徒", "円の予兆からすぐ出る。詠唱の間に仕留める",
                    "離れた場所の足元にカースの円を落とす（大きなダメージ）", "血のチャペルではビショップの儀式に駆けつける"),
                Monster(EnemyType.Bat, "闇を舞う群れ", "連射や範囲攻撃で落とす",
                    "空を飛び、障害物を越えて来る", "速いが体力は低い"),
                Monster(EnemyType.Bloater, "腐敗で膨れ上がった屍", "離れて倒すか、モンスターの群れの中で破裂させる",
                    "近づくと破裂する", "破裂は周りのモンスターも巻き込む"),
                Monster(EnemyType.DreadKnight, "鎧に身を包んだ亡者", "フリーズ・スタン・マークで鎧をブレイクてから叩く。仲間と合わせるのが近道",
                    "鎧で攻撃を大きく防ぐ", "フリーズ・スタン・マークの間は鎧が崩れる（「鎧ブレイク！」）"),
                Monster(EnemyType.Necromancer, "スケルトンを呼ぶ術者", "呼ばれたスケルトンより先に、術者を仕留める",
                    "スケルトン兵や弓兵を呼び続ける", "距離を取りたがる"),
                Monster(EnemyType.Wraith, "姿なきグラッジ", "足元の光る円から離れる。姿を現した瞬間が狙い目",
                    "姿を消して、獲物の足元に現れる"),
                Monster(EnemyType.ShieldSkeleton, "大盾を構える亡者（ロッテンクリプト）", "フリーズ・スタン・マークで鎧をブレイクてから叩く",
                    "大盾で攻撃を大きく防ぐ（鎧）", "盾で前を打ち払う（扇の予兆）"),
                Monster(EnemyType.Flagellant, "血のチャペルの苦行僧", "鞭の線から横へ外れる。振り終わりが狙い目",
                    "長い鞭で一直線を打つ（線の予兆）", "足が速い"),
                Monster(EnemyType.AbyssEye, "アビスに浮かぶ眼", "円からすぐ出て、距離を詰めて叩く",
                    "宙に浮き、障害物を越える", "獲物の足元にカースの円を落とす"),
                Monster(EnemyType.ShadeStalker, "影から影へ渡る狩人", "光る円から離れ、続く飛びかかりの線も避ける",
                    "姿を消して足元に現れる", "続けて一直線に飛びかかる"),
                Monster(EnemyType.Mimic, "ボンファイアのチェストに化ける", "距離を取り、噛みつきの線と周りの円を避けてから叩く",
                    "叩くとチェストが正体を現す（ボンファイアに時々いる）", "跳ねて追いかけ、一直線に噛みつく", "倒すとソウルがたくさんと、部屋の全員にレアな装備"),
                Monster(EnemyType.TreasureImp, "パズルルームの逃げ足", "囲んで追い詰める。氷結やスタンで足を止める",
                    "宝を抱えて逃げ回る（20秒で逃げ切る）", "攻撃されると一瞬だけ加速する", "倒すと鍵とソウル・装備"),
                Monster(EnemyType.Drowned, "水底から這い上がった亡者（サンケンカタコンベ）", "鈍いので回り込んで叩く。水を吐く線は横へ避ける",
                    "重い腕で前を薙ぐ（扇の予兆）", "離れると水の弾を吐く（線の予兆）"),
                Monster(EnemyType.CarrionCrab, "屍を喰らう大蟹（サンケンカタコンベ）", "フリーズ・スタン・マークで甲羅をブレイクてから叩く",
                    "硬い甲羅で攻撃を大きく防ぐ（鎧）", "素早く横から飛びかかる（線の予兆）"),
                Monster(EnemyType.TideWitch, "水を操る魔女（サンケンカタコンベ）", "円からすぐ出て、距離を詰めて仕留める",
                    "獲物の足元に水の柱を立てる（円の予兆）", "3つに分かれる水の弾"),
                Monster(EnemyType.FrostWolf, "群れで狩るスノーウルフ（フローズンモーソレアム）", "2匹で来る。飛びかかりの線を避けてから叩く",
                    "2匹ずつ現れる", "素早く、一直線に飛びかかる"),
                Monster(EnemyType.Icebound, "氷に閉ざされた亡者（フローズンモーソレアム）", "倒したらすぐ離れる。砕ける氷に巻き込まれない",
                    "重い一撃（扇の予兆）", "倒れると氷が砕け散り、周りを傷つける（円の予兆）"),
                Monster(EnemyType.FrostWitch, "霜を呼ぶ魔女（フローズンモーソレアム）", "線の外へ出てから距離を詰める",
                    "長い一直線に氷の棘を突き出す（線の予兆）", "距離を取りたがる"),
                Monster(EnemyType.IronSentinel, "炉を守るアイアンセンチネル（バーントフォージ）", "フリーズ・スタン・マークで鎧を崩す。振り下ろしの円から出る",
                    "鉄の鎧で攻撃を大きく防ぐ（鎧）", "足元を叩き割る（円の予兆）と、肩からの突進"),
                Monster(EnemyType.FireImp, "火の粉を撒く小鬼（バーントフォージ）", "連射や範囲攻撃でまとめて落とす",
                    "2匹ずつ現れ、宙を舞う", "火の玉を投げる（線の予兆）"),
                Monster(EnemyType.SlagBloater, "溶けた鉄で膨れた屍（バーントフォージ）", "離れて倒す。残った溶鉄の上に立たない",
                    "近づくと破裂する（モンスターも巻き込む）", "破裂した跡に溶けた鉄が残り、しばらく燃える"),
                // The lands' champions (RunSimulation.Champions): each guards its land's champion's lair.
                Monster(EnemyType.BoneDigger, "骨を継ぎ直す墓掘り（ロッテンクリプトの中ボス）", "倒れたら、ボーンパイルが組み上がる前に叩き壊す",
                    "倒れるとボーンパイルになり、4秒で立ち上がる（体力は半分）", "ボーンパイルを壊せば二度と立たない。立ち上がるのは一度だけ"),
                Monster(EnemyType.TwinAcolyte, "祈りを分け合う双子（ブラッドチャペルの中ボス）", "片方を倒したら、すぐにもう片方へ。二人を続けて倒す",
                    "二人で現れる", "片方が倒れても、もう片方が立っていれば4.5秒後に蘇らせる"),
                Monster(EnemyType.ShadeLord, "影を従える頭領（アビススローンの中ボス）", "シャドウクローンは一撃で消える。範囲攻撃でまとめて払う",
                    "ときどき影を渡って姿を消し、シャドウクローンを2体残す", "写しも斬りかかってくるが、6秒で消える"),
                Monster(EnemyType.LeechMother, "水に棲むリーチマザー（サンケンカタコンベの中ボス）", "水から引きずり出してから叩く",
                    "水の中では少しずつ回復する", "水の中では受けるダメージが半分になる"),
                Monster(EnemyType.IceKnight, "氷の殻をまとう騎士（フローズンモーソレアムの中ボス）", "手数の多い攻撃で殻を割り、割れた間に大技を",
                    "氷の殻が攻撃のほとんどを防ぐ", "6回叩くと殻が割れてよろめき、7秒のあいだ攻撃が通る。殻はまた張り直す"),
                Monster(EnemyType.Bombsmith, "火薬を投げるブラックスミス師（バーントフォージの中ボス）", "投げられた樽から離れ、ブラックスミス師を爆発に巻き込む",
                    "ライトケグを皆の足元へ投げる（円の予兆）", "樽は少しして爆ぜる。叩けばすぐ爆ぜる。爆発はブラックスミス師にも大きく効く"),
                // The fungal grotto and the cursed library (RunSimulation.Lands2)
                Monster(EnemyType.Sporeling, "跳ねる毒茸（ファンガルグロット）", "倒した跡の胞子の雲に入らない。離れて倒す",
                    "跳びかかって噛みつく（線の予兆）", "倒れた場所にしばらく胞子の雲が残る"),
                Monster(EnemyType.Mycelid, "菌糸に食われた亡者（ファンガルグロット）", "突進の線を横へ避けてから叩く",
                    "前を薙ぐ（扇の予兆）", "離れると突進してくる（線の予兆）"),
                Monster(EnemyType.MossShaman, "胞子を操る呪術師（ファンガルグロット）", "距離を詰めて先に倒す",
                    "胞子の弾を扇に撃つ", "足元に胞子の円を呼ぶ（円の予兆）"),
                Monster(EnemyType.GreatCap, "胞子を吹く大きな茸（ファンガルグロットの中ボス）", "離れて叩くか、叩いたらすぐ下がる",
                    "叩かれるたびに周りへ胞子を吹き出す（円の予兆）", "離れると胞子の弾を撃つ"),
                Monster(EnemyType.LivingTome, "ひとりでに飛ぶグリモア（カースドライブラリ）", "連射や範囲攻撃でまとめて落とす",
                    "表紙を羽ばたかせて宙を舞う", "頁を2枚ずつ飛ばしてくる（線の予兆）"),
                Monster(EnemyType.InkWraith, "墨でできた亡霊（カースドライブラリ）", "消えたら現れる円から離れる",
                    "姿を消して円の中に現れ、斬りつける", "宙を舞い、障害物を抜ける"),
                Monster(EnemyType.Scribe, "カースを書きつける写字生（カースドライブラリ）", "足元の円から出て、近づいて倒す",
                    "足元にカースの円を書く（円の予兆）", "頁を扇に飛ばす"),
                Monster(EnemyType.Bookworm, "古紙を食う大きな紙魚（カースドライブラリの中ボス）", "分かれた半身もすぐに倒す",
                    "倒れると2匹の半身に分かれて戦い続ける", "離れると突進してくる（線の予兆）"),
                new()
                {
                    Id = "enemy:Elite", Section = CodexSection.Monsters, Name = "エリート", Sub = "大きく、強く、光る個体", Sheet = "dread_knight",
                    Traits = new[] { "体力3.5倍・攻撃1.4倍", "倒すと良い装備を落としやすい", "ベテラン以上では鎧をまとう" },
                    Counter = "予兆をよく見て、強いスキルはエリートに合わせる",
                },
                new()
                {
                    Id = "enemy:Champion", Section = CodexSection.Monsters, Name = "チャンピオン", Sub = "チャンピオンルームに待つ", Sheet = "ghoul",
                    Traits = new[] { "エリートよりさらに3倍の体力", "護衛を連れている（入った人数で増える）", "倒すと鍵とレアな装備" },
                    Counter = "護衛を先に減らし、ボスにはコンボを叩き込む",
                },
                new()
                {
                    Id = "enemy:LordStages", Section = CodexSection.Monsters, Name = "ボスレイジ", Sub = "ボスは戦いの途中で二度変わる", Sheet = "bone_king", AlwaysKnown = true,
                    Traits = new[] { "体力60%で「怒り」：攻撃が速くなり、ボスルームの床が裂けてマークの場所が弾ける", "体力25%で「最後のあがき」：さらに速く、弾ける場所も増える", "試練の最中は変わらない（試練が終わってから）" },
                    Counter = "床にマークが出たらすぐ離れる。マークのひとつは誰かの足元を狙う",
                },
                new()
                {
                    Id = "enemy:Warband", Section = CodexSection.Monsters, Name = "モンスターのウォーバンド", Sub = "役割のある組（頭上に役）", Sheet = "shield_skeleton", AlwaysKnown = true,
                    Traits = new[]
                    {
                        "エリアの2部屋目から、ウェーブに3体のウォーバンドが混じることがある",
                        $"ガード：守る相手（射手・ヒーラー）と皆の間に立つ。ガードのそばのモンスターは受けるダメージ -{RunSimulation.GuardCover * 100f:0}%",
                        $"ヒーラー：後ろに下がり、{RunSimulation.HealEvery:0}秒ごとに周りのモンスターを最大体力の{RunSimulation.HealShare * 100f:0}%回復する",
                    },
                    Counter = "回り込んでヒーラーを先に倒す。ガードから引き離せばダメージが通る",
                },
                new()
                {
                    Id = "enemy:Affixes", Section = CodexSection.Monsters, Name = "エリートの特性", Sub = "エリートは特性を持つ（頭上に名前）", Sheet = "dread_knight", AlwaysKnown = true,
                    Traits = new[]
                    {
                        "数：ノーマルは1つ、難しいは2つ、悪夢は3つ",
                        "スウィフト：" + EliteAffixCatalog.Description(EliteAffix.Swift), "リジェネ：" + EliteAffixCatalog.Description(EliteAffix.Regenerating),
                        "ヴォラタイル：" + EliteAffixCatalog.Description(EliteAffix.Volatile), "スプリット：" + EliteAffixCatalog.Description(EliteAffix.Splitting),
                        "サモナー：" + EliteAffixCatalog.Description(EliteAffix.Summoner), "シールド：" + EliteAffixCatalog.Description(EliteAffix.Shielded),
                    },
                    Counter = "再生と守護は手を休めずに攻め続ける。爆発は倒したら離れる。召喚は先に倒す",
                },

                // ---------------------------------------------------------------- Lords
                Monster(EnemyType.BoneKing, "", "突進を柱で受け止め、檻は仲間が壊す。ブレッシングの間はキャンドルスタンドを優先",
                    "リーパースイング・回転斬り・突進・スケルトンの召喚", "突進が柱にぶつかると、しばらくよろめく",
                    "試練「ボーンケージ」：円の中の人を閉じ込める。攻撃で壊せる（仲間が壊すと早い）。4秒で潰れる",
                    "試練「不死のブレッシング」：HP50%でボーンキャンドル3つ。全部壊すまで無敵"),
                Monster(EnemyType.BloodBishop, "", "儀式が始まったら全員で紋章へ。血の雨は止まらず動き続ける",
                    "血の弾と、足元への血の雨",
                    "試練「血の儀式」：HP70%・35%で無敵の詠唱。床の紋章3つの上に立って灯す",
                    "灯し切れないと全員に大ダメージ、ビショップは回復する"),
                Monster(EnemyType.Lich, "", "崩落の予兆が出たら中央へ。倒したと思っても器を探す",
                    "ナイトメアの弾幕と、亡者の召喚",
                    "試練「アビスの崩落」：HP66%・33%で部屋の外周がアビスに。歩ける範囲が狭まる",
                    "試練「ソウルヴェッセル」：倒すとソウルが器へ逃げる。12秒以内に器を壊さないと復活"),
                Monster(EnemyType.DrownedSaint, "", "水が満ちたら手分けして栓へ。セイントの攻撃は止まらない",
                    "扇に広がる水の弾・足元の渦・近づくと水の爆発・亡者と蟹の召喚",
                    "試練「水が満ちる」：HP60%・30%で部屋が水に沈む。毎秒少しずつ傷つき、足も遅くなる",
                    "部屋の縁の「シールプラグ」をすべて壊すと水が引く。間に合わないと濁流に呑まれ、セイントは回復する"),
                Monster(EnemyType.FrostQueen, "", "影のある方が本物。写し身から先に砕く",
                    "凍える息（扇）・氷柱の雨・氷の槍・氷の歩み・スノーウルフと亡者の召喚",
                    "試練「アイスミラー」：HP66%・33%で同じ姿の写し身を2体呼ぶ。その間、クイーンは無敵",
                    "写し身には影がなく、叩くと砕ける。2体とも砕くとクイーンがよろめく。遅れると写し身が冷気となって弾ける"),
                Monster(EnemyType.ForgeColossus, "", "ウォータージャーを壊し、ジャイアントの通り道に水を撒く。ジャイアントを水へ誘い込む",
                    "大槌の振り下ろし・周りへのショックウェーブ・溶鉄の散弾・突進・小鬼と番兵の召喚",
                    "試練「灼熱」：HP50%で赤く焼けた体になり、攻撃がほとんど通らない。溶けた鉄が降り注ぐ",
                    "部屋の両側のウォータージャーを壊すと水たまりができる。ジャイアントが水を踏むと冷えてよろめく。冷めるのを待つと大爆発"),
                Monster(EnemyType.GraveReaper, "", "棺の揺れと土ぼこりをよく見る。外れの棺からはグールが出る",
                    "デスサイズの薙ぎ・死者の雨・瞬く斬撃・スケルトンとグールの召喚", "試練「棺の中」：HP60%・30%で4つの棺のどれかに隠れる。正しい棺を壊すとよろめく。遅れるとソウルを弾けさせて出てくる"),
                Monster(EnemyType.ChoirMistress, "", "光の帯の回る向きに合わせて歩き続ける",
                    "血の弾・聖なる槍（線）・血の雨・カルティストと修道士の召喚", "試練「聖光の回廊」：HP66%・33%で部屋の真ん中から3本の光の帯が回る（モンスターも焼く）。耐えきるとよろめく"),
                Monster(EnemyType.VoidEye, "", "見つめられたら柱の陰へ。仲間はその間に叩く",
                    "ソウルの輪・カースの円・視線の光線・アビスアイと影のレンジャーの召喚", "試練「凝視」：HP70%・40%で、ひとりずつ順に見つめる。柱を挟めば防げる。4回耐えるとよろめく"),
                Monster(EnemyType.GreatCrab, "", "パウダーケグを大蟹の近くで爆発させる",
                    "鋏の薙ぎ・横への突進・水の弾・蟹と亡者の召喚", "試練「殻ごもり」：HP60%・30%で殻に閉じこもり、攻撃がほとんど通らない。パウダーケグが転がってくる。そばで爆ぜると殻が割れてよろめく"),
                Monster(EnemyType.GreatWolf, "", "マークを付けられた人は柱の前に立ち、突進を柱にぶつけさせる",
                    "噛みつき・長い突進・氷柱の遠吠え・スノーウルフの召喚", "試練「狩りのマーク」：HP65%・30%で、ひとりにマークを付けて突進する（4回）。柱に激突させるとよろめく"),
                Monster(EnemyType.Executioner, "", "刃の列の切れ目を探して抜け、チェインステイクを壊す",
                    "グレートアクスの振り下ろし・リーパースイング・突進・番兵と小鬼の召喚", "試練「処刑の列」：HP50%で部屋を横切る刃の列が次々に落ちる（切れ目がある）。チェインステイクを3つとも壊すとよろめく"),
                Monster(EnemyType.MyceliumMother, "", "霧のあいだはグロウシュルームのそばへ。茸が枯れたら次の茸へ移る",
                    "胞子の弾（扇）・胞子の雨・周りへの胞子の爆発・スポアシュルームと亡者の召喚",
                    "試練「胞子の霧」：HP65%・30%で部屋が胞子の霧に満ちる。グロウシュルームのそばだけ息ができる（茸は順に枯れ、別の場所に生える）",
                    "霧が晴れるまで耐えると母はよろめく"),
                Monster(EnemyType.RotGiant, "", "寄生された人は百合のそばに立ち、ほかの人は寄生された人から離れる",
                    "拳の振り下ろし・突進・周りへの踏みつけ・スポアシュルームの召喚",
                    "試練「寄生」：HP60%・30%で何人かに寄生体が宿る。少しずつ傷つき、そばにいる仲間へ移る",
                    "百合のそばに立つと清められる。全員が清められるとジャイアントがよろめく。遅れると寄生体が破裂する"),
                Monster(EnemyType.Archivist, "", "フォービドゥンブックが光った順番を覚えて、その順に叩く。仲間と手分けしてもよい",
                    "頁の輪・カースの雨・墨の線・グリモアと写字生の召喚",
                    "試練「フォービドゥンブックの順」：HP66%・33%でフォービドゥンブックが4冊現れ、1冊ずつ光る。ライブラリアンは守られている",
                    "光った順に叩くとよろめく。順番を間違えると墨が爆ぜ、もう一度光り直す"),
                Monster(EnemyType.InkBeast, "", "潜っている間は足元の円から逃げつつ、キャンドルスタンドのそばに立って火を灯す",
                    "噛みつき・長い突進・墨の雨・インクファントムとグリモアの召喚",
                    "試練「墨の潜行」：HP60%・30%で墨に潜り、誰かの足元から次々に噴き上がる",
                    "キャンドルスタンドを3つとも灯すと墨から追い出されてよろめく。遅れると墨が全員の足元で爆ぜる"),
                Monster(EnemyType.AshenKing, "", "試練が来るたびに役割を分ける。最後は心臓を探して砕く",
                    "六人のボスのスキルを次々に使う：リーパースイング・血の弾・ソウルの輪・雨・突進・ボーンケージ・召喚",
                    "試練はスケルトンキングのブレッシング（75%）→ビショップの儀式（50%）→アビスの崩落（25%）の順に来る",
                    "倒しても心臓へ逃げる。時間内に灰の心臓を壊すと、アッシュキングは崩れ落ちる"),
                Open(CodexSection.Lords, "lord:Trial", "ボスの試練", "どのボスにもある", "fx_holy", "試練は避けずに越える。仲間と役割を分けると早い",
                    "ボスは体力の節目で「試練」を始める", "越えるとボスが4.5秒よろめき「隙あり！」（受けるダメージ+50%）",
                    "ナイトメアの難易度では試練が厳しくなる"),

                // ---------------------------------------------------------------- Gimmicks and traps
                Gimmick("DarkHall", "ダークホール", "prop_lantern", "灯りを持って戦う部屋（各エリアに1つ）", "灯り役は仲間の真ん中に立ち、ほかの人は灯りの中で戦う",
                    "部屋は真っ暗で、灯りの中だけが見える", "灯りの外のモンスターには攻撃がほとんど通らない（3割）", "灯りは触れると拾える。持ち主が倒れるか部屋を出るとその場に落ちる"),
                Gimmick("TwinPlates", "双子のプレッシャープレート", "pressure_plate", "2枚を同時に踏む仕掛け", "2人が板を守り、残りの仲間がモンスターを引きつける",
                    "部屋の両端のプレッシャープレートを同時に踏み続けると、封印が少しずつ解ける", "どちらかが離れると、たまったパークがゆっくり抜けていく", "1人のときは、離れても板がしばらく光り続ける"),
                Gimmick("Captive", "囚われた冒険者", "captive", "戦いの部屋の檻（各エリアに1つ）", "戦いの合間に檻を叩いて開ける",
                    "檻を壊すと助け出せる", "お礼に、部屋にいる全員へ装備とソウル。HPも少し回復する"),
                Gimmick("PowderKeg", "パウダーケグ", "powder_keg", "攻撃すると爆ぜる", "モンスターの群れを引き寄せてから撃つ",
                    "攻撃すると爆発する", "モンスターも味方も巻き込む"),
                Gimmick("SpikeTrap", "トゲ罠", "trap_spikes", "床から突き出す刃", "予兆の間に抜け、モンスターを誘い込む",
                    "一定の間隔で床からトゲが出る", "モンスターにも効く"),
                Gimmick("FlameJet", "炎の噴出口", "trap_vent", "壁から吹き出す炎", "噴く方向の線を避ける。群れを焼かせる",
                    "線上に炎を吹き出す", "モンスターにも効く"),
                Gimmick("HealingFont", "癒しの泉", "prop_font", "部屋に1回だけ", "苦しくなるまで取っておき、仲間と一緒に使う",
                    "上に立つと、周りの仲間ごと回復する"),
                Gimmick("TreasureChest", "チェスト", "treasure_chest", "ボンファイアなどに", "戦闘のない間に開ける",
                    "叩いて壊すと、ソウルと装備が散らばる"),
                Gimmick("CrackedWall", "クラックドウォール", "cracked_wall", "シークレットルームのエントランス", "壁際のひびを見つけたら壊してみる",
                    "隣の部屋に初めて入ると扉の前に現れる", "壊すとシークレットルームへの扉が開く"),
                Gimmick("PressurePlate", "プレッシャープレート", "pressure_plate", "パズルルーム", "協力なら手分けして同時に踏む",
                    "上に立つと灯る（しばらくすると消える）", "全部灯すと鍵が手に入る"),
                Gimmick("WardCrystal", "ワードクリスタル", "ward_crystal", "パズルルーム", "結晶に群がるモンスターを最優先で倒す",
                    "モンスターが結晶を壊しに来る", "30秒守り切れば鍵が手に入る"),
                Gimmick("Bell", "集合の鐘", "decor_bell", "ボンファイア（協力時）", "ボスの扉の前で待つ前に鳴らす",
                    "下に立つと鳴り、戦闘中でない仲間をキャンプファイアへ呼ぶ", "15秒に1回"),
                Gimmick("Urn", "ポット", "urn", "部屋の壁際に", "戦いの合間に叩いて回る",
                    "一撃で割れる", "ときどきソウルがこぼれ出る"),
                Gimmick("Water", "浅い水", "decal_pool", "サンケンカタコンベ", "水の外で戦い、水を渡るモンスターを待ち伏せる",
                    "水の中では歩みが遅くなる", "モンスターも同じく遅くなる（空を飛ぶものは除く）"),
                Gimmick("Ice", "氷の床", "decal_ice", "フローズンモーソレアム", "早めに止まる。滑りを使って一気に間合いを詰めるのも手",
                    "氷の上では足が滑り、すぐには止まれない"),
                Gimmick("Icicle", "落ちる氷柱", "trap_icicle", "フローズンモーソレアム", "床の霜の輪にひびが入ったら離れる。モンスターを誘い込む",
                    "天井から一定の間隔で氷柱が落ちる", "モンスターにも効く"),
                Gimmick("LavaCrack", "溶岩の亀裂", "trap_lava", "バーントフォージ", "亀裂が光ったら線の外へ。群れを焼かせる",
                    "床の亀裂から一定の間隔で炎が噴き上がる", "モンスターにも効く"),

                Gimmick("SporeVent", "胞子の噴き出し口", "trap_spore", "ファンガルグロット", "膨らんだら離れる。モンスターを雲の中へ誘い込む",
                    "一定の間隔で胞子の雲を吹き、しばらく残る", "モンスターにも効く"),
                Gimmick("Glyph", "呪文の頁", "trap_glyph", "カースドライブラリ", "踏んだらすぐ離れる。追ってくるモンスターを巻き込む",
                    "誰かが踏むと文字が燃え、少しして爆ぜる", "一度爆ぜると少しのあいだ眠る。モンスターにも効く"),

                // ---------------------------------------------------------------- Rooms
                Room(RoomKind.Start, "エリアの始まり", "まずは近くの扉から探索を始める", "モンスターはいない", "壁の切れ目から通路を歩いて、隣の部屋へ進む"),
                Room(RoomKind.Combat, "モンスターの群れ", "ウェーブが終わるまで部屋の中で戦う。仲間を呼ぶのも手",
                    "誰かが入るとモンスターがウェーブで押し寄せる（2〜3ウェーブ）", "途中で出られるが、誰もいない間は次のウェーブが止まる", "制圧すると体力が少し戻る"),
                Room(RoomKind.Elite, "エリートの群れ", "強いスキルはエリートに合わせる", "エリートを含む3つのウェーブ", "良い装備が出やすい"),
                Room(RoomKind.MiniBoss, "チャンピオンが待つ", "護衛を減らしてからボスへ", "チャンピオンと護衛", "倒すと鍵（トレジャリーが開く）"),
                Room(RoomKind.Puzzle, "4つの仕掛けのどれか", "仕掛けの種類を見て役割を分ける",
                    "プレッシャープレート・ワードクリスタル・逃げる宝の小鬼・罠の通路", "解くと鍵が手に入る"),
                Room(RoomKind.Shrine, "パークを授かる", "迷ったら寄り道する価値がある", "祭壇の上に立つと新しいパークを1つ選べる（1人1回）"),
                Room(RoomKind.Whetstone, "パークを研ぐ", "伸ばしたいパークが決まってから使う", "祭壇の上に立つと持っているパークを1つ強化できる"),
                Room(RoomKind.Spring, "命を満たす", "体力が減ってから使うと得", "最大HP+10%と全回復"),
                Room(RoomKind.CurseAltar, "パークと引き換えに", "カースの重さと相談して使う", "パークを2回選べる", "代償にカースが1つつく（ランの間ずっと、自分だけ）"),
                Room(RoomKind.Merchant, "ソウルシャードで買う", "欲しい物がなければ何も買わずに帰ってよい", "1人1つ買える", "代金は全員の取り分から払う"),
                Room(RoomKind.Treasure, "鍵のかかった部屋", "鍵はチャンピオンルーム・パズルルームで手に入る", "鍵を持って扉の前に立つと開く", "レア以上の装備が手に入る"),
                Room(RoomKind.Secret, "壁の向こうの部屋", "クラックドウォールを探す", "オールドマップ（エリアの全部屋が見える）と装備"),
                new Entry
                {
                    Id = "room:Omen", Section = CodexSection.Rooms, Name = "ラビリンスのオーメン", Sub = "ランごとに1つ", Mark = "異",
                    Traits = new[] { "ブラッドムーン：エリートが増え、ソウルも増える", "ラビリンスオブプレンティ：装備が落ちやすく、モンスターは手強い", "フレンジー：モンスターが素早く、もろい", "スウォームシーズン：群れが大きく、一体一体はもろい", "ヘイトレッド：倒れたモンスターがソウルを弾けさせる。ソウルは増える", "ブレスドウィンド：始めにパークをもう1つ。ラビリンスのボスは手強い" },
                    Counter = "オーメンは右上に出ている。得をする方へ戦い方を寄せる",
                },
                new Entry
                {
                    Id = "room:Deep", Section = CodexSection.Rooms, Name = "ディープ", Sub = "3つ目のボスの先", Mark = "深",
                    Traits = new[] { "3つ目のボスを倒すと「帰還」と「ディープへ」の2つの道が開く", "ディープは階を重ねるほどモンスターが強く、最後にまたラビリンスのボスが待つ", "3つ目のボスを倒した時点で踏破。ディープで力尽きても踏破として数える" },
                    Counter = "余力があるうちに深く潜り、危なくなる前に帰還する",
                },
                new()
                {
                    Id = "room:Waves", Section = CodexSection.Rooms, Name = "ウェーブの種類", Sub = "バトルルームの2ウェーブ目から", Mark = "波", RewardIcon = -1, AlwaysKnown = true,
                    Traits = new[]
                    {
                        $"{WaveCatalog.Name(WaveKind.Ambush)}：{WaveCatalog.Hint(WaveKind.Ambush)}",
                        $"{WaveCatalog.Name(WaveKind.Swarm)}：{WaveCatalog.Hint(WaveKind.Swarm)}",
                        $"{WaveCatalog.Name(WaveKind.Encircle)}：{WaveCatalog.Hint(WaveKind.Encircle)}",
                        $"{WaveCatalog.Name(WaveKind.Totem)}：{WaveCatalog.Hint(WaveKind.Totem)}（柱が残っている間は部屋が終わらない）",
                    },
                    Counter = "ウェーブの始めに出る名前を見て、動き方を変える",
                },
                new()
                {
                    Id = "room:Contract", Section = CodexSection.Rooms, Name = "コントラクト", Sub = "キャンプのモノリスで", Mark = "契", RewardIcon = -1, AlwaysKnown = true,
                    Traits = new[]
                    {
                        "難易度の上に、モンスターの強化や枷を自分で重ねる（部屋を作った人が結ぶ）",
                        "条項ごとの点数の合計がランク。ランク1つにつきソウルシャード +6%",
                        "ランク5・10・20で踏破するとタイトル（アチーブメント）。最高記録は碑に残る。今週のラビリンスでは無効",
                    },
                    Counter = "勝てるようになったら1つずつ足す",
                },
                Room(RoomKind.Encounter, "道で出会う何か", "仲間と相談して選ぶ。急ぐなら損の少ない方を",
                    "2つの円のどちらかに、部屋にいる全員で立つと選んだことになる（1人なら1人で）。1つの部屋で1回",
                    $"{EncounterCatalog.Name(EncounterKind.LostSoul)}：{EncounterCatalog.Choice(EncounterKind.LostSoul, 0)}（{EncounterCatalog.Effect(EncounterKind.LostSoul, 0)}）／{EncounterCatalog.Choice(EncounterKind.LostSoul, 1)}（{EncounterCatalog.Effect(EncounterKind.LostSoul, 1)}）",
                    $"{EncounterCatalog.Name(EncounterKind.Statue)}：{EncounterCatalog.Choice(EncounterKind.Statue, 0)}（{EncounterCatalog.Effect(EncounterKind.Statue, 0)}）／{EncounterCatalog.Choice(EncounterKind.Statue, 1)}（{EncounterCatalog.Effect(EncounterKind.Statue, 1)}）",
                    $"{EncounterCatalog.Name(EncounterKind.Gambler)}：{EncounterCatalog.Choice(EncounterKind.Gambler, 0)}（{EncounterCatalog.Effect(EncounterKind.Gambler, 0)}）／{EncounterCatalog.Choice(EncounterKind.Gambler, 1)}（{EncounterCatalog.Effect(EncounterKind.Gambler, 1)}）",
                    $"{EncounterCatalog.Name(EncounterKind.BloodFont)}：{EncounterCatalog.Choice(EncounterKind.BloodFont, 0)}（{EncounterCatalog.Effect(EncounterKind.BloodFont, 0)}）／{EncounterCatalog.Choice(EncounterKind.BloodFont, 1)}（{EncounterCatalog.Effect(EncounterKind.BloodFont, 1)}）"),
                Room(RoomKind.Spirit, "さまようスピリットシュライン", "ブレッシングが欲しい神があと1つでフェイバーになるときに", "最大HPの1割を捧げると（ランの間ずっと）、神のブレッシングを1つ授かる", "1人1回。エリア2以降に時々ある"),
                Room(RoomKind.Campfire, "ボスの手前のひと休み", "全員がそろってからボスの扉へ",
                    "キャンプファイアのそばで体力が回復し、倒れた仲間も起き上がる", "集合の鐘で仲間を呼べる", "ボスの扉は生きている全員がそろうと開く"),
                Room(RoomKind.Boss, "エリアのボス", "試練を越えて隙を作る", "ボスが戦っている間は扉が閉まる", "倒すと奥への道が開く（全員で立つと次のエリアへ）"),

                // ---------------------------------------------------------------- Weapons and runes (always open, phase 4)
                Open(CodexSection.Gear, "gear:AltWeapon", "エキゾチックウェポン", "職業ごとのもう一つの武器", "icons_item", "拾ったら一度持ち替えてみる。バッグの説明で通常攻撃の変わり方が分かる",
                    "どの職業にも、いつもと違う武器がある（グレートアクス・グリモア・クロスボウ・グレートランス・スローイングダガー・ボムフラスク・デスサイズ・グレートソード）",
                    "持つと通常攻撃の形が変わり、見た目も変わる", "チェストやモンスターの落とし物から、ときどき見つかる"),
                Open(CodexSection.Gear, "gear:Rune", "ルーン", "スキルに宿る石", "icons_item", "スキル1・スキル2の枠にはめる。珍しいルーンほど強く効く",
                    "スキル1ルーン・スキル2ルーンの枠にはめると、そのスキルを使うたびにパークを貸す",
                    "炎（燃える床）・氷（凍らせる）・雷（近くの敵へ）・命（回復）・響（すぐもう一度）・疾（再使用が早い）・影（ドッジとクリティカル）・ソウル（ソウルが飛び散る）",
                    "突進や貫く矢なら通り道に沿って効く（炎の道など）。冒険中は貸し出されない"),
                Open(CodexSection.Gear, "gear:Tree", "スキルツリー", "キャンプのマスターで", "icons_perk", "アーツは振り直せば何度でも選び直せる。試して合うものを探す",
                    "職業ごとに3本の枝、各4段。ソウルシャードで上の段から順に開く（効果は毎回のランで有効）",
                    "3段目：その職業のパークを1つ持った状態でランが始まる",
                    "4段目はアーツ：スキル1かスキル2が変わる（炎の道・雷・フリーズ・回復など）。アーツは職業ごとに1つだけ。スキルの枠に紫のマークが付く",
                    "振り直す：その職業の習得をすべて白紙に戻し、使ったシャードは全部戻る"),
                Open(CodexSection.Gear, "gear:Smith", "ブラックスミス", "キャンプのヴォールトで", "camp_anvil", "気に入った装備は鍛えて長く使う",
                    "鍛える：ソウルシャードで装備を +1 ずつ鍛える（+5まで）。1回ごとに性能 +8%",
                    "振り直す：ソウルシャードで、効果のうち1つを別の効果に引き直す",
                    "珍しい装備ほどシャードが多くかかる。貸し出し品は鍛えられない"),
                Open(CodexSection.Gear, "gear:Sets", "セット装備", "鎧・アミュレット・リングの三点", "icons_item", "同じ組の装備を2点・3点とそろえる",
                    "レアの鎧・アミュレット・リングの一部がセット装備（緑の名前）。2点で1つ目、3点で2つ目の効果",
                    $"{GearSetCatalog.All[0].Name}：2点 {GearSetCatalog.All[0].TwoText}／3点 {GearSetCatalog.All[0].ThreeText}",
                    $"{GearSetCatalog.All[1].Name}：2点 {GearSetCatalog.All[1].TwoText}／3点 {GearSetCatalog.All[1].ThreeText}",
                    $"{GearSetCatalog.All[2].Name}：2点 {GearSetCatalog.All[2].TwoText}／3点 {GearSetCatalog.All[2].ThreeText}",
                    $"{GearSetCatalog.All[3].Name}：2点 {GearSetCatalog.All[3].TwoText}／3点 {GearSetCatalog.All[3].ThreeText}",
                    $"{GearSetCatalog.All[4].Name}：2点 {GearSetCatalog.All[4].TwoText}／3点 {GearSetCatalog.All[4].ThreeText}",
                    $"{GearSetCatalog.All[5].Name}：2点 {GearSetCatalog.All[5].TwoText}／3点 {GearSetCatalog.All[5].ThreeText}"),

                // ---------------------------------------------------------------- Teamwork (always open)
                Open(CodexSection.Teamwork, "team:Shatter", "砕き", "フリーズ×強打", "fx_ice", "メイジのフロストノヴァのあと、ウォリアーやレンジャーが強打を合わせる",
                    "凍った敵にウォリアーの斬撃・レンジャーのピアシングショットなどを当てると「砕ける」（大ダメージ）"),
                Open(CodexSection.Teamwork, "team:Mark", "ハンターズマーク", "マーク×全員の攻撃", "fx_mark", "マークを付けたら全員でその敵を狙う",
                    "マークの付いた敵は、全員の攻撃が通りやすくなる", "アサシンのシャドウステップはマークの敵に必ずクリティカル"),
                Open(CodexSection.Teamwork, "team:WarCry", "ウォークライ", "仲間の強化", "fx_warcry", "群れに飛び込む直前に使う",
                    "周りの敵を吹き飛ばしてスタンさせ、近くの仲間の攻撃と守りを上げる"),
                Open(CodexSection.Teamwork, "team:Armor", "鎧ブレイク", "フリーズ・スタン・マーク", "dread_knight", "鎧の敵が出たら、崩す役と叩く役に分かれる",
                    "鎧の敵（デスナイト・ベテラン以上のエリート）はフリーズ・スタン・マークの間だけ鎧が崩れる"),
                Open(CodexSection.Teamwork, "team:Revive", "助け起こす", "倒れた仲間", "fx_revive_ring", "マップで赤く点滅する仲間のもとへ急ぐ",
                    "倒れた仲間のそばに立つと起き上がらせる", "倒れている間はモンスターに狙われない", "全員が倒れると全滅"),
                Open(CodexSection.Teamwork, "team:Ghost", "幽霊", "倒れている間", "fx_soul", "亡骸を仲間に見せつつ、モンスターの群れに張り付いて足止めする",
                    "倒れると亡骸を残し、幽霊になってその部屋の中を漂える（障害物はすり抜ける）", "幽霊のそばのモンスターは動きがとても遅くなる",
                    "攻撃でソウルの火花を飛ばせる。ソウルも拾える", "起き上がるのは亡骸の場所（仲間が亡骸のそばに立つ）"),
                Open(CodexSection.Teamwork, "team:Ping", "ピン", "F キー・マウス中ボタン", "ping_come", "離れて探索するときこそ使う",
                    "敵に向けると「危ない！」、祭壇や扉には「ここ！」、ほかは「来て！」", "M のマップで部屋をクリックすると「集合！」"),
                Open(CodexSection.Teamwork, "team:Break", "ブレイク", "エリート・中ボス・ボス", "fx_nova", "強打・砕き・連携・アルティメットを集めて一気に崩す",
                    "エリート・チャンピオン・ボスは体力とは別に「体勢」を持つ（ボスは体力の下の金色の線）",
                    "攻撃で体勢が削れる。強打は2.5倍、砕きは3倍、連携（仲間が止めた敵）は1.5倍、アルティメットは2倍。しばらく攻撃しないと戻っていく",
                    $"削り切ると「ブレイク」：数秒よろめいて動けず、受けるダメージ +{RunSimulation.BreakBonus * 100f:0}%。ボスは崩すたびに少し固くなる"),
                Open(CodexSection.Teamwork, "team:Union", "ユニオン", "2人で同時にスキル", "fx_nova", "声をかけ合って、同じ瞬間にスキルを使う",
                    "近く（6マス以内）にいる2人が0.8秒以内にスキル（どちらのスキルでも）を使うと、2人の間にユニオンが出る",
                    "職業の組み合わせごとに名前がある（同じ職業どうしでも出る）",
                    "両方の職業のパーク（フリーズ・炎上・スタン・マーク・カース）が乗り、2人とも守りの盾を得る。パラディンがいると光の中の仲間が回復する",
                    "1人につき10秒に1回まで"),
            };
            foreach (var c in ComboCatalog.All)
                list.Add(new Entry
                {
                    Id = "combo:" + c.Kind, Section = CodexSection.Teamwork, Name = c.Name, AlwaysKnown = true,
                    Sub = $"{ClassCatalog.Get(c.A).Name}×{ClassCatalog.Get(c.B).Name}の組み合わせスキル", Sheet = "fx_nova",
                    Traits = new[] { c.How, "別々の人が操作する2つの職業がそろうと使える" },
                    Counter = "2人で息を合わせて狙う",
                });

            // ---------------------------------------------------------------- Gods (recorded when their blessing is first offered)
            foreach (var god in GodCatalog.All)
            {
                var traits = new List<string>();
                foreach (var b in GodCatalog.Blessings(god.Id)) traits.Add($"{b.Name}：{b.Text}");
                traits.Add($"{god.FavorName}（同じ神のブレッシングを{GodCatalog.FavorPieces}つ）：{god.FavorText}");
                list.Add(new Entry
                {
                    Id = GodEntry(god.Id), Section = CodexSection.Gods, Name = god.Name, Sub = god.FavorName,
                    IconSheet = "icons_perk", IconFrame = (int)GodCatalog.Blessings(god.Id)[0].Id, Traits = traits.ToArray(),
                    Counter = "中ボスやボスを倒すと、全員が神のブレッシングをひとつ授かる。同じ神をそろえるとフェイバーが目覚める",
                });
            }

            // ---------------------------------------------------------------- Achievements (recorded when achieved)
            foreach (var a in AchievementCatalog.All)
                list.Add(new Entry
                {
                    Id = AchievementEntry(a.Id), Section = CodexSection.Achievements, Name = a.Name, Sub = $"タイトル「{AchievementCatalog.TitleOf(a)}」",
                    RewardIcon = (int)RoomReward.Boon, Traits = new[] { a.Text }, AlwaysKnown = true,
                    Counter = "タイトルはキャンプのミラーで名前の横に付けられる（仲間にも見える）",
                });

            // ---------------------------------------------------------------- Controls (always open)
            list.Add(Open(CodexSection.Controls, "control:Keyboard", "キーボードとマウス", "PC", "warrior", "照準はマウスの方向。ドッジ中は無敵",
                "WASD：移動　マウス：照準", "左クリック：通常攻撃（押しっぱなし）", "Q・右クリック：スキル1　E：スキル2", "Space・Shift：ドッジ",
                "I・Tab：バッグ（装備）　M：マップ　F：ピン　B：コーデックス", "Esc：メニュー"));
            list.Add(Open(CodexSection.Controls, "control:Dodge", "ドッジと見切り", "ドッジは2回分", "fx_revive_ring", "連打せず、予兆を見て当たる直前に避ける",
                $"ドッジは{ClassCatalog.DodgeCharges}回分たまる（ドッジアイコンの●）。1回分は{ClassCatalog.DodgeRechargeTime:0.#}秒で戻る",
                "無敵はドッジで動いている一瞬だけ",
                "その一瞬に攻撃を受け流すと「見切り」：時間が一瞬ゆっくりになり、ドッジが1回分すぐ戻る"));
            list.Add(Open(CodexSection.Controls, "control:Picks", "戦闘中のパーク選び", "1・2・3 キー・十字キー", "icons_perk", "急がなくてよい。落ち着いてから選んでもカードは残る",
                "戦闘中にレベルが上がると、スキルの上に小さなカードが3枚出る。動きながら 1・2・3（パッドは十字キーの左・下・右）で選ぶ",
                "選ばずに戦い続けてもよい。戦いが終わると大きなカードで開く（戦闘中は時間切れにならない）",
                "ひとりのときは Enter で大きく開くと、選ぶまでゲームが止まる"));
            list.Add(Open(CodexSection.Controls, "control:Charge", "溜め攻撃", "通常攻撃を押し続けて離す", "fx_nova", "エリートやボスの体勢をブレイクたいときに",
                "押し続けている間も通常攻撃は出る。足元の輪が金色になったら（約1.2秒）離すと重い一撃",
                "さらに押し続けて輪が赤くなったら（約2.4秒）離すと渾身の一撃",
                "近接の職業は周りを大きくリーパースイング、それ以外は貫くショックウェーブを放つ。体勢をよく削る"));
            list.Add(Open(CodexSection.Controls, "control:Ultimate", "アルティメットとテンション", "R キー・Y ボタン", "fx_holy", "強い敵や群れが来たときのためにとっておく",
                "敵に攻撃を当てる・攻撃を受ける・見切り・ユニオンでテンションがたまる（ドッジの右のアイコン）。満タンでアルティメットが使える",
                $"ウォリアー「{UltimateCatalog.Name(ClassId.Warrior)}」・メイジ「{UltimateCatalog.Name(ClassId.Mage)}」・レンジャー「{UltimateCatalog.Name(ClassId.Ranger)}」・ガーディアン「{UltimateCatalog.Name(ClassId.Guardian)}」",
                $"アサシン「{UltimateCatalog.Name(ClassId.Assassin)}」・アルケミスト「{UltimateCatalog.Name(ClassId.Alchemist)}」・ネクロマンサー「{UltimateCatalog.Name(ClassId.Deathcaller)}」・パラディン「{UltimateCatalog.Name(ClassId.Paladin)}」",
                "それぞれの働きは鏡（職業選び）で確かめられる"));
            list.Add(Open(CodexSection.Controls, "control:Gamepad", "ゲームパッド", "パッド", "ranger", "照準は右スティック（倒していなければ移動方向）",
                "左スティック：移動　右スティック：照準", "RT：通常攻撃　RB：スキル1　LB：スキル2", "A：ドッジ", "Select：マップ　十字キー上：ピン"));
            list.Add(Open(CodexSection.Controls, "control:Run", "ラビリンスの進み方", "1回およそ15分", "fx_portal", "寄り道（祠・泉・エリート）をするほど強くなる",
                "3つのエリアを抜ける。各エリアは部屋が扉でつながっている", "ボンファイアの奥にボスルーム。ボスを倒すと次のエリアへ",
                "パークの選択と装備の付け替えは、戦闘中でない部屋で", "倒れても、キャンプへ戻れば装備とソウルシャードは残る"));
            UseSkillIcons(list);
            // The story (Lore): each text as lines, recorded as it is found.
            foreach (var t in Lore.All)
                list.Add(new Entry { Id = t.Id, Section = CodexSection.Records, Name = t.Name, Sub = t.Sub, Mark = t.Mark, Traits = t.Lines });
            return list;
        }

        /// <summary>Skill icons (icons_skill frames: class * 3 + slot) for the teamwork pages.</summary>
        private static void UseSkillIcons(List<Entry> list)
        {
            void Icon(string id, string sheet, int frame)
            {
                var e = list.Find(x => x.Id == id);
                if (e == null) return;
                e.IconSheet = sheet;
                e.IconFrame = frame;
            }
            Icon("team:Shatter", "icons_skill", (int)ClassId.Mage * 3 + 1);
            Icon("team:Mark", "icons_skill", (int)ClassId.Ranger * 3 + 2);
            Icon("team:WarCry", "icons_skill", (int)ClassId.Warrior * 3 + 2);
            Icon("team:Revive", "icons_skill", (int)ClassId.Mage * 3 + 2);
            Icon("combo:" + ComboKind.SteamBurst, "icons_skill", (int)ClassId.Alchemist * 3 + 1);
            Icon("combo:" + ComboKind.ShadowExecution, "icons_skill", (int)ClassId.Assassin * 3 + 1);
            Icon("combo:" + ComboKind.HolyWarCry, "icons_skill", (int)ClassId.Guardian * 3 + 2);
            Icon("combo:" + ComboKind.FlameCharge, "icons_skill", (int)ClassId.Guardian * 3 + 1);
            Icon("team:Ping", "ping_come", 0);
            Icon("lord:Trial", "icons_reward", RewardCatalog.TrialIconIndex);
            Icon("control:Keyboard", "icons_skill", (int)ClassId.Warrior * 3);
            Icon("control:Gamepad", "icons_skill", 18);
            Icon("control:Run", "icons_reward", RewardCatalog.IconIndex(RoomReward.Treasure));
        }

        public static Entry Get(string id) => All.Find(e => e.Id == id);

        public static List<Entry> In(CodexSection s) => All.FindAll(e => e.Section == s);

        /// <summary>The codex entry for a gimmick of a room layout (null for ones it does not cover).</summary>
        public static string GimmickEntry(GimmickKind kind) => kind switch
        {
            GimmickKind.PowderKeg => GimmickId("PowderKeg"),
            GimmickKind.SpikeTrap => GimmickId("SpikeTrap"),
            GimmickKind.FlameJet => GimmickId("FlameJet"),
            GimmickKind.HealingFont => GimmickId("HealingFont"),
            GimmickKind.TreasureChest => GimmickId("TreasureChest"),
            GimmickKind.Water => GimmickId("Water"),
            GimmickKind.Ice => GimmickId("Ice"),
            GimmickKind.Icicle => GimmickId("Icicle"),
            GimmickKind.LavaCrack => GimmickId("LavaCrack"),
            GimmickKind.SporeVent => GimmickId("SporeVent"),
            GimmickKind.Glyph => GimmickId("Glyph"),
            _ => null,
        };

        /// <summary>The codex entry a monster or object belongs to (props of trials count with their lord).</summary>
        public static string EnemyEntry(EnemyType t) => t switch
        {
            EnemyType.PowderKeg => GimmickId("PowderKeg"),
            EnemyType.TreasureChest => GimmickId("TreasureChest"),
            EnemyType.CrackedWall => GimmickId("CrackedWall"),
            EnemyType.Captive => GimmickId("Captive"),
            EnemyType.PressurePlate => GimmickId("PressurePlate"),
            EnemyType.WardCrystal => GimmickId("WardCrystal"),
            EnemyType.Urn => GimmickId("Urn"),
            EnemyType.BoneBrazier or EnemyType.BoneCage => MonsterId(EnemyType.BoneKing),
            EnemyType.BloodSigil => MonsterId(EnemyType.BloodBishop),
            EnemyType.SoulVessel => MonsterId(EnemyType.Lich),
            EnemyType.FloodSeal => MonsterId(EnemyType.DrownedSaint),
            EnemyType.IceMirror => MonsterId(EnemyType.FrostQueen),
            EnemyType.QuenchCistern => MonsterId(EnemyType.ForgeColossus),
            EnemyType.Coffin => MonsterId(EnemyType.GraveReaper),
            EnemyType.ChainPost => MonsterId(EnemyType.Executioner),
            EnemyType.BonePile => MonsterId(EnemyType.BoneDigger),
            EnemyType.ShadeDecoy => MonsterId(EnemyType.ShadeLord),
            EnemyType.LitKeg => MonsterId(EnemyType.Bombsmith),
            EnemyType.Glowcap => MonsterId(EnemyType.MyceliumMother),
            EnemyType.Lily => MonsterId(EnemyType.RotGiant),
            EnemyType.ForbiddenTome => MonsterId(EnemyType.Archivist),
            EnemyType.Candelabra => MonsterId(EnemyType.InkBeast),
            EnemyType.Wormling => MonsterId(EnemyType.Bookworm),
            _ => MonsterId(t),
        };
    }
}
