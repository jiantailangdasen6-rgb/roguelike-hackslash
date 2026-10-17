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
            CodexSection.Monsters => "魔物",
            CodexSection.Lords => "迷宮の主",
            CodexSection.Records => "記録",
            CodexSection.Gimmicks => "仕掛け・罠",
            CodexSection.Rooms => "部屋",
            CodexSection.Gods => "神々",
            CodexSection.Achievements => "実績",
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
                Monster(EnemyType.Skeleton, "迷宮にもっとも多い亡者", "扇の予兆が出たら横へ抜ける。群れは範囲攻撃でまとめて崩す",
                    "前方を扇状に薙ぐ（足元に扇の予兆）", "数で押し寄せる"),
                Monster(EnemyType.SkeletonArcher, "距離を取って射る", "赤い線から外れる。近づけば脆い",
                    "離れた位置を保ち、矢を放つ（線の予兆の方向へ飛ぶ）", "体力は低い"),
                Monster(EnemyType.Ghoul, "飢えた俊足の屍", "飛びかかりの線を見て回避し、着地の隙に叩く",
                    "素早く距離を詰め、一直線に飛びかかる"),
                Monster(EnemyType.Cultist, "呪いを唱える信徒", "円の予兆からすぐ出る。詠唱の間に仕留める",
                    "離れた場所の足元に呪いの円を落とす（大きなダメージ）", "血の礼拝堂では司教の儀式に駆けつける"),
                Monster(EnemyType.Bat, "闇を舞う群れ", "連射や範囲攻撃で落とす",
                    "空を飛び、障害物を越えて来る", "速いが体力は低い"),
                Monster(EnemyType.Bloater, "腐敗で膨れ上がった屍", "離れて倒すか、魔物の群れの中で破裂させる",
                    "近づくと破裂する", "破裂は周りの魔物も巻き込む"),
                Monster(EnemyType.DreadKnight, "鎧に身を包んだ亡者", "凍結・気絶・印で鎧を崩してから叩く。仲間と合わせるのが近道",
                    "鎧で攻撃を大きく防ぐ", "凍結・気絶・印の間は鎧が崩れる（「鎧崩し！」）"),
                Monster(EnemyType.Necromancer, "骸骨を呼ぶ術者", "呼ばれた骸骨より先に、術者を仕留める",
                    "骸骨兵や弓兵を呼び続ける", "距離を取りたがる"),
                Monster(EnemyType.Wraith, "姿なき怨念", "足元の光る円から離れる。姿を現した瞬間が狙い目",
                    "姿を消して、獲物の足元に現れる"),
                Monster(EnemyType.ShieldSkeleton, "大盾を構える亡者（朽ちた納骨堂）", "凍結・気絶・印で鎧を崩してから叩く",
                    "大盾で攻撃を大きく防ぐ（鎧）", "盾で前を打ち払う（扇の予兆）"),
                Monster(EnemyType.Flagellant, "血の礼拝堂の苦行僧", "鞭の線から横へ外れる。振り終わりが狙い目",
                    "長い鞭で一直線を打つ（線の予兆）", "足が速い"),
                Monster(EnemyType.AbyssEye, "奈落に浮かぶ眼", "円からすぐ出て、距離を詰めて叩く",
                    "宙に浮き、障害物を越える", "獲物の足元に呪いの円を落とす"),
                Monster(EnemyType.ShadeStalker, "影から影へ渡る狩人", "光る円から離れ、続く飛びかかりの線も避ける",
                    "姿を消して足元に現れる", "続けて一直線に飛びかかる"),
                Monster(EnemyType.Mimic, "焚き火の間の宝箱に化ける", "距離を取り、噛みつきの線と周りの円を避けてから叩く",
                    "叩くと宝箱が正体を現す（焚き火の間に時々いる）", "跳ねて追いかけ、一直線に噛みつく", "倒すと魂がたくさんと、部屋の全員に希少な装備"),
                Monster(EnemyType.TreasureImp, "仕掛けの間の逃げ足", "囲んで追い詰める。氷結や気絶で足を止める",
                    "宝を抱えて逃げ回る（20秒で逃げ切る）", "攻撃されると一瞬だけ加速する", "倒すと鍵と魂・装備"),
                Monster(EnemyType.Drowned, "水底から這い上がった亡者（水没した地下墓地）", "鈍いので回り込んで叩く。水を吐く線は横へ避ける",
                    "重い腕で前を薙ぐ（扇の予兆）", "離れると水の弾を吐く（線の予兆）"),
                Monster(EnemyType.CarrionCrab, "屍を喰らう大蟹（水没した地下墓地）", "凍結・気絶・印で甲羅を崩してから叩く",
                    "硬い甲羅で攻撃を大きく防ぐ（鎧）", "素早く横から飛びかかる（線の予兆）"),
                Monster(EnemyType.TideWitch, "水を操る魔女（水没した地下墓地）", "円からすぐ出て、距離を詰めて仕留める",
                    "獲物の足元に水の柱を立てる（円の予兆）", "3つに分かれる水の弾"),
                Monster(EnemyType.FrostWolf, "群れで狩る雪狼（凍てつく霊廟）", "2匹で来る。飛びかかりの線を避けてから叩く",
                    "2匹ずつ現れる", "素早く、一直線に飛びかかる"),
                Monster(EnemyType.Icebound, "氷に閉ざされた亡者（凍てつく霊廟）", "倒したらすぐ離れる。砕ける氷に巻き込まれない",
                    "重い一撃（扇の予兆）", "倒れると氷が砕け散り、周りを傷つける（円の予兆）"),
                Monster(EnemyType.FrostWitch, "霜を呼ぶ魔女（凍てつく霊廟）", "線の外へ出てから距離を詰める",
                    "長い一直線に氷の棘を突き出す（線の予兆）", "距離を取りたがる"),
                Monster(EnemyType.IronSentinel, "炉を守る鉄の番兵（燃え落ちた鍛冶場）", "凍結・気絶・印で鎧を崩す。振り下ろしの円から出る",
                    "鉄の鎧で攻撃を大きく防ぐ（鎧）", "足元を叩き割る（円の予兆）と、肩からの突進"),
                Monster(EnemyType.FireImp, "火の粉を撒く小鬼（燃え落ちた鍛冶場）", "連射や範囲攻撃でまとめて落とす",
                    "2匹ずつ現れ、宙を舞う", "火の玉を投げる（線の予兆）"),
                Monster(EnemyType.SlagBloater, "溶けた鉄で膨れた屍（燃え落ちた鍛冶場）", "離れて倒す。残った溶鉄の上に立たない",
                    "近づくと破裂する（魔物も巻き込む）", "破裂した跡に溶けた鉄が残り、しばらく燃える"),
                // The lands' champions (RunSimulation.Champions): each guards its land's champion's lair.
                Monster(EnemyType.BoneDigger, "骨を継ぎ直す墓掘り（朽ちた納骨堂の中ボス）", "倒れたら、骨の山が組み上がる前に叩き壊す",
                    "倒れると骨の山になり、4秒で立ち上がる（体力は半分）", "骨の山を壊せば二度と立たない。立ち上がるのは一度だけ"),
                Monster(EnemyType.TwinAcolyte, "祈りを分け合う双子（血塗られた礼拝堂の中ボス）", "片方を倒したら、すぐにもう片方へ。二人を続けて倒す",
                    "二人で現れる", "片方が倒れても、もう片方が立っていれば4.5秒後に蘇らせる"),
                Monster(EnemyType.ShadeLord, "影を従える頭領（奈落の玉座の中ボス）", "影の写しは一撃で消える。範囲攻撃でまとめて払う",
                    "ときどき影を渡って姿を消し、影の写しを2体残す", "写しも斬りかかってくるが、6秒で消える"),
                Monster(EnemyType.LeechMother, "水に棲む蛭の母（水没した地下墓地の中ボス）", "水から引きずり出してから叩く",
                    "水の中では少しずつ回復する", "水の中では受けるダメージが半分になる"),
                Monster(EnemyType.IceKnight, "氷の殻をまとう騎士（凍てつく霊廟の中ボス）", "手数の多い攻撃で殻を割り、割れた間に大技を",
                    "氷の殻が攻撃のほとんどを防ぐ", "6回叩くと殻が割れてよろめき、7秒のあいだ攻撃が通る。殻はまた張り直す"),
                Monster(EnemyType.Bombsmith, "火薬を投げる鍛冶師（燃え落ちた鍛冶場の中ボス）", "投げられた樽から離れ、鍛冶師を爆発に巻き込む",
                    "火の付いた樽を皆の足元へ投げる（円の予兆）", "樽は少しして爆ぜる。叩けばすぐ爆ぜる。爆発は鍛冶師にも大きく効く"),
                // The fungal grotto and the cursed library (RunSimulation.Lands2)
                Monster(EnemyType.Sporeling, "跳ねる毒茸（菌糸の洞窟）", "倒した跡の胞子の雲に入らない。離れて倒す",
                    "跳びかかって噛みつく（線の予兆）", "倒れた場所にしばらく胞子の雲が残る"),
                Monster(EnemyType.Mycelid, "菌糸に食われた亡者（菌糸の洞窟）", "突進の線を横へ避けてから叩く",
                    "前を薙ぐ（扇の予兆）", "離れると突進してくる（線の予兆）"),
                Monster(EnemyType.MossShaman, "胞子を操る呪術師（菌糸の洞窟）", "距離を詰めて先に倒す",
                    "胞子の弾を扇に撃つ", "足元に胞子の円を呼ぶ（円の予兆）"),
                Monster(EnemyType.GreatCap, "胞子を吹く大きな茸（菌糸の洞窟の中ボス）", "離れて叩くか、叩いたらすぐ下がる",
                    "叩かれるたびに周りへ胞子を吹き出す（円の予兆）", "離れると胞子の弾を撃つ"),
                Monster(EnemyType.LivingTome, "ひとりでに飛ぶ魔導書（呪われた書庫）", "連射や範囲攻撃でまとめて落とす",
                    "表紙を羽ばたかせて宙を舞う", "頁を2枚ずつ飛ばしてくる（線の予兆）"),
                Monster(EnemyType.InkWraith, "墨でできた亡霊（呪われた書庫）", "消えたら現れる円から離れる",
                    "姿を消して円の中に現れ、斬りつける", "宙を舞い、障害物を抜ける"),
                Monster(EnemyType.Scribe, "呪いを書きつける写字生（呪われた書庫）", "足元の円から出て、近づいて倒す",
                    "足元に呪いの円を書く（円の予兆）", "頁を扇に飛ばす"),
                Monster(EnemyType.Bookworm, "古紙を食う大きな紙魚（呪われた書庫の中ボス）", "分かれた半身もすぐに倒す",
                    "倒れると2匹の半身に分かれて戦い続ける", "離れると突進してくる（線の予兆）"),
                new()
                {
                    Id = "enemy:Elite", Section = CodexSection.Monsters, Name = "精鋭", Sub = "大きく、強く、光る個体", Sheet = "dread_knight",
                    Traits = new[] { "体力3.5倍・攻撃1.4倍", "倒すと良い装備を落としやすい", "熟練以上では鎧をまとう" },
                    Counter = "予兆をよく見て、強い技は精鋭に合わせる",
                },
                new()
                {
                    Id = "enemy:Champion", Section = CodexSection.Monsters, Name = "精鋭の主", Sub = "精鋭の主の間に待つ", Sheet = "ghoul",
                    Traits = new[] { "精鋭よりさらに3倍の体力", "護衛を連れている（入った人数で増える）", "倒すと鍵と希少な装備" },
                    Counter = "護衛を先に減らし、主には連携技を叩き込む",
                },
                new()
                {
                    Id = "enemy:LordStages", Section = CodexSection.Monsters, Name = "主の怒り", Sub = "主は戦いの途中で二度変わる", Sheet = "bone_king", AlwaysKnown = true,
                    Traits = new[] { "体力60%で「怒り」：攻撃が速くなり、主の間の床が裂けて印の場所が弾ける", "体力25%で「最後のあがき」：さらに速く、弾ける場所も増える", "試練の最中は変わらない（試練が終わってから）" },
                    Counter = "床に印が出たらすぐ離れる。印のひとつは誰かの足元を狙う",
                },
                new()
                {
                    Id = "enemy:Affixes", Section = CodexSection.Monsters, Name = "精鋭の特性", Sub = "精鋭は特性を持つ（頭上に名前）", Sheet = "dread_knight", AlwaysKnown = true,
                    Traits = new[]
                    {
                        "数：標準は1つ、難しいは2つ、悪夢は3つ",
                        "俊足：" + EliteAffixCatalog.Description(EliteAffix.Swift), "再生：" + EliteAffixCatalog.Description(EliteAffix.Regenerating),
                        "爆発：" + EliteAffixCatalog.Description(EliteAffix.Volatile), "分裂：" + EliteAffixCatalog.Description(EliteAffix.Splitting),
                        "召喚：" + EliteAffixCatalog.Description(EliteAffix.Summoner), "守護：" + EliteAffixCatalog.Description(EliteAffix.Shielded),
                    },
                    Counter = "再生と守護は手を休めずに攻め続ける。爆発は倒したら離れる。召喚は先に倒す",
                },

                // ---------------------------------------------------------------- Lords
                Monster(EnemyType.BoneKing, "", "突進を柱で受け止め、檻は仲間が壊す。加護の間は燭台を優先",
                    "薙ぎ払い・回転斬り・突進・骸骨の召喚", "突進が柱にぶつかると、しばらくよろめく",
                    "試練「骨の檻」：円の中の人を閉じ込める。攻撃で壊せる（仲間が壊すと早い）。4秒で潰れる",
                    "試練「不死の加護」：HP50%で骨の燭台3つ。全部壊すまで無敵"),
                Monster(EnemyType.BloodBishop, "", "儀式が始まったら全員で紋章へ。血の雨は止まらず動き続ける",
                    "血の弾と、足元への血の雨",
                    "試練「血の儀式」：HP70%・35%で無敵の詠唱。床の紋章3つの上に立って灯す",
                    "灯し切れないと全員に大ダメージ、司教は回復する"),
                Monster(EnemyType.Lich, "", "崩落の予兆が出たら中央へ。倒したと思っても器を探す",
                    "冥府の弾幕と、亡者の召喚",
                    "試練「奈落の崩落」：HP66%・33%で部屋の外周が奈落に。歩ける範囲が狭まる",
                    "試練「魂の器」：倒すと魂が器へ逃げる。12秒以内に器を壊さないと復活"),
                Monster(EnemyType.DrownedSaint, "", "水が満ちたら手分けして栓へ。聖女の攻撃は止まらない",
                    "扇に広がる水の弾・足元の渦・近づくと水の爆発・亡者と蟹の召喚",
                    "試練「水が満ちる」：HP60%・30%で部屋が水に沈む。毎秒少しずつ傷つき、足も遅くなる",
                    "部屋の縁の「封じの栓」をすべて壊すと水が引く。間に合わないと濁流に呑まれ、聖女は回復する"),
                Monster(EnemyType.FrostQueen, "", "影のある方が本物。写し身から先に砕く",
                    "凍える息（扇）・氷柱の雨・氷の槍・氷の歩み・雪狼と亡者の召喚",
                    "試練「氷の写し身」：HP66%・33%で同じ姿の写し身を2体呼ぶ。その間、女王は無敵",
                    "写し身には影がなく、叩くと砕ける。2体とも砕くと女王がよろめく。遅れると写し身が冷気となって弾ける"),
                Monster(EnemyType.ForgeColossus, "", "水甕を壊し、巨人の通り道に水を撒く。巨人を水へ誘い込む",
                    "大槌の振り下ろし・周りへの衝撃波・溶鉄の散弾・突進・小鬼と番兵の召喚",
                    "試練「灼熱」：HP50%で赤く焼けた体になり、攻撃がほとんど通らない。溶けた鉄が降り注ぐ",
                    "部屋の両側の水甕を壊すと水たまりができる。巨人が水を踏むと冷えてよろめく。冷めるのを待つと大爆発"),
                Monster(EnemyType.GraveReaper, "", "棺の揺れと土ぼこりをよく見る。外れの棺からはグールが出る",
                    "大鎌の薙ぎ・死者の雨・瞬く斬撃・骸骨とグールの召喚", "試練「棺の中」：HP60%・30%で4つの棺のどれかに隠れる。正しい棺を壊すとよろめく。遅れると魂を弾けさせて出てくる"),
                Monster(EnemyType.ChoirMistress, "", "光の帯の回る向きに合わせて歩き続ける",
                    "血の弾・聖なる槍（線）・血の雨・狂信者と修道士の召喚", "試練「聖光の回廊」：HP66%・33%で部屋の真ん中から3本の光の帯が回る（魔物も焼く）。耐えきるとよろめく"),
                Monster(EnemyType.VoidEye, "", "見つめられたら柱の陰へ。仲間はその間に叩く",
                    "魂の輪・呪いの円・視線の光線・奈落の眼と影の狩人の召喚", "試練「凝視」：HP70%・40%で、ひとりずつ順に見つめる。柱を挟めば防げる。4回耐えるとよろめく"),
                Monster(EnemyType.GreatCrab, "", "火薬樽を大蟹の近くで爆発させる",
                    "鋏の薙ぎ・横への突進・水の弾・蟹と亡者の召喚", "試練「殻ごもり」：HP60%・30%で殻に閉じこもり、攻撃がほとんど通らない。火薬樽が転がってくる。そばで爆ぜると殻が割れてよろめく"),
                Monster(EnemyType.GreatWolf, "", "印を付けられた人は柱の前に立ち、突進を柱にぶつけさせる",
                    "噛みつき・長い突進・氷柱の遠吠え・雪狼の召喚", "試練「狩りの印」：HP65%・30%で、ひとりに印を付けて突進する（4回）。柱に激突させるとよろめく"),
                Monster(EnemyType.Executioner, "", "刃の列の切れ目を探して抜け、鎖の杭を壊す",
                    "大斧の振り下ろし・薙ぎ払い・突進・番兵と小鬼の召喚", "試練「処刑の列」：HP50%で部屋を横切る刃の列が次々に落ちる（切れ目がある）。鎖の杭を3つとも壊すとよろめく"),
                Monster(EnemyType.MyceliumMother, "", "霧のあいだは光る茸のそばへ。茸が枯れたら次の茸へ移る",
                    "胞子の弾（扇）・胞子の雨・周りへの胞子の爆発・胞子茸と亡者の召喚",
                    "試練「胞子の霧」：HP65%・30%で部屋が胞子の霧に満ちる。光る茸のそばだけ息ができる（茸は順に枯れ、別の場所に生える）",
                    "霧が晴れるまで耐えると母はよろめく"),
                Monster(EnemyType.RotGiant, "", "寄生された人は百合のそばに立ち、ほかの人は寄生された人から離れる",
                    "拳の振り下ろし・突進・周りへの踏みつけ・胞子茸の召喚",
                    "試練「寄生」：HP60%・30%で何人かに寄生体が宿る。少しずつ傷つき、そばにいる仲間へ移る",
                    "百合のそばに立つと清められる。全員が清められると巨人がよろめく。遅れると寄生体が破裂する"),
                Monster(EnemyType.Archivist, "", "禁書が光った順番を覚えて、その順に叩く。仲間と手分けしてもよい",
                    "頁の輪・呪いの雨・墨の線・魔導書と写字生の召喚",
                    "試練「禁書の順」：HP66%・33%で禁書が4冊現れ、1冊ずつ光る。司書は守られている",
                    "光った順に叩くとよろめく。順番を間違えると墨が爆ぜ、もう一度光り直す"),
                Monster(EnemyType.InkBeast, "", "潜っている間は足元の円から逃げつつ、燭台のそばに立って火を灯す",
                    "噛みつき・長い突進・墨の雨・墨の亡霊と魔導書の召喚",
                    "試練「墨の潜行」：HP60%・30%で墨に潜り、誰かの足元から次々に噴き上がる",
                    "燭台を3つとも灯すと墨から追い出されてよろめく。遅れると墨が全員の足元で爆ぜる"),
                Monster(EnemyType.AshenKing, "", "試練が来るたびに役割を分ける。最後は心臓を探して砕く",
                    "六人の主の技を次々に使う：薙ぎ払い・血の弾・魂の輪・雨・突進・骨の檻・召喚",
                    "試練は騎士王の加護（75%）→司教の儀式（50%）→奈落の崩落（25%）の順に来る",
                    "倒しても心臓へ逃げる。時間内に灰の心臓を壊すと、灰の王は崩れ落ちる"),
                Open(CodexSection.Lords, "lord:Trial", "主の試練", "どの主にもある", "fx_holy", "試練は避けずに越える。仲間と役割を分けると早い",
                    "主は体力の節目で「試練」を始める", "越えると主が4.5秒よろめき「隙あり！」（受けるダメージ+50%）",
                    "冥府の難易度では試練が厳しくなる"),

                // ---------------------------------------------------------------- Gimmicks and traps
                Gimmick("DarkHall", "闇の間", "prop_lantern", "灯りを持って戦う部屋（各エリアに1つ）", "灯り役は仲間の真ん中に立ち、ほかの人は灯りの中で戦う",
                    "部屋は真っ暗で、灯りの中だけが見える", "灯りの外の魔物には攻撃がほとんど通らない（3割）", "灯りは触れると拾える。持ち主が倒れるか部屋を出るとその場に落ちる"),
                Gimmick("TwinPlates", "双子の押し板", "pressure_plate", "2枚を同時に踏む仕掛け", "2人が板を守り、残りの仲間が魔物を引きつける",
                    "部屋の両端の押し板を同時に踏み続けると、封印が少しずつ解ける", "どちらかが離れると、たまった力がゆっくり抜けていく", "1人のときは、離れても板がしばらく光り続ける"),
                Gimmick("Captive", "囚われた冒険者", "captive", "戦いの部屋の檻（各エリアに1つ）", "戦いの合間に檻を叩いて開ける",
                    "檻を壊すと助け出せる", "お礼に、部屋にいる全員へ装備と魂。HPも少し回復する"),
                Gimmick("PowderKeg", "火薬樽", "powder_keg", "攻撃すると爆ぜる", "魔物の群れを引き寄せてから撃つ",
                    "攻撃すると爆発する", "魔物も味方も巻き込む"),
                Gimmick("SpikeTrap", "トゲ罠", "trap_spikes", "床から突き出す刃", "予兆の間に抜け、魔物を誘い込む",
                    "一定の間隔で床からトゲが出る", "魔物にも効く"),
                Gimmick("FlameJet", "炎の噴出口", "trap_vent", "壁から吹き出す炎", "噴く方向の線を避ける。群れを焼かせる",
                    "線上に炎を吹き出す", "魔物にも効く"),
                Gimmick("HealingFont", "癒しの泉", "prop_font", "部屋に1回だけ", "苦しくなるまで取っておき、仲間と一緒に使う",
                    "上に立つと、周りの仲間ごと回復する"),
                Gimmick("TreasureChest", "宝箱", "treasure_chest", "焚き火の間などに", "戦闘のない間に開ける",
                    "叩いて壊すと、魂と装備が散らばる"),
                Gimmick("CrackedWall", "ひび割れた壁", "cracked_wall", "隠し部屋の入口", "壁際のひびを見つけたら壊してみる",
                    "隣の部屋に初めて入ると扉の前に現れる", "壊すと隠し部屋への扉が開く"),
                Gimmick("PressurePlate", "押し板", "pressure_plate", "仕掛けの間", "協力なら手分けして同時に踏む",
                    "上に立つと灯る（しばらくすると消える）", "全部灯すと鍵が手に入る"),
                Gimmick("WardCrystal", "守りの結晶", "ward_crystal", "仕掛けの間", "結晶に群がる魔物を最優先で倒す",
                    "魔物が結晶を壊しに来る", "30秒守り切れば鍵が手に入る"),
                Gimmick("Bell", "集合の鐘", "decor_bell", "焚き火の間（協力時）", "主の扉の前で待つ前に鳴らす",
                    "下に立つと鳴り、戦闘中でない仲間を焚き火へ呼ぶ", "15秒に1回"),
                Gimmick("Urn", "壺", "urn", "部屋の壁際に", "戦いの合間に叩いて回る",
                    "一撃で割れる", "ときどき魂がこぼれ出る"),
                Gimmick("Water", "浅い水", "decal_pool", "水没した地下墓地", "水の外で戦い、水を渡る魔物を待ち伏せる",
                    "水の中では歩みが遅くなる", "魔物も同じく遅くなる（空を飛ぶものは除く）"),
                Gimmick("Ice", "氷の床", "decal_ice", "凍てつく霊廟", "早めに止まる。滑りを使って一気に間合いを詰めるのも手",
                    "氷の上では足が滑り、すぐには止まれない"),
                Gimmick("Icicle", "落ちる氷柱", "trap_icicle", "凍てつく霊廟", "床の霜の輪にひびが入ったら離れる。魔物を誘い込む",
                    "天井から一定の間隔で氷柱が落ちる", "魔物にも効く"),
                Gimmick("LavaCrack", "溶岩の亀裂", "trap_lava", "燃え落ちた鍛冶場", "亀裂が光ったら線の外へ。群れを焼かせる",
                    "床の亀裂から一定の間隔で炎が噴き上がる", "魔物にも効く"),

                Gimmick("SporeVent", "胞子の噴き出し口", "trap_spore", "菌糸の洞窟", "膨らんだら離れる。魔物を雲の中へ誘い込む",
                    "一定の間隔で胞子の雲を吹き、しばらく残る", "魔物にも効く"),
                Gimmick("Glyph", "呪文の頁", "trap_glyph", "呪われた書庫", "踏んだらすぐ離れる。追ってくる魔物を巻き込む",
                    "誰かが踏むと文字が燃え、少しして爆ぜる", "一度爆ぜると少しのあいだ眠る。魔物にも効く"),

                // ---------------------------------------------------------------- Rooms
                Room(RoomKind.Start, "エリアの始まり", "まずは近くの扉から探索を始める", "魔物はいない", "壁の切れ目から通路を歩いて、隣の部屋へ進む"),
                Room(RoomKind.Combat, "魔物の群れ", "波が終わるまで部屋の中で戦う。仲間を呼ぶのも手",
                    "誰かが入ると魔物が波で押し寄せる（2〜3波）", "途中で出られるが、誰もいない間は次の波が止まる", "制圧すると体力が少し戻る"),
                Room(RoomKind.Elite, "精鋭の群れ", "強い技は精鋭に合わせる", "精鋭を含む3つの波", "良い装備が出やすい"),
                Room(RoomKind.MiniBoss, "精鋭の主が待つ", "護衛を減らしてから主へ", "精鋭の主と護衛", "倒すと鍵（宝物庫が開く）"),
                Room(RoomKind.Puzzle, "4つの仕掛けのどれか", "仕掛けの種類を見て役割を分ける",
                    "押し板・守りの結晶・逃げる宝の小鬼・罠の通路", "解くと鍵が手に入る"),
                Room(RoomKind.Shrine, "力を授かる", "迷ったら寄り道する価値がある", "祭壇の上に立つと新しい力を1つ選べる（1人1回）"),
                Room(RoomKind.Whetstone, "力を研ぐ", "伸ばしたい力が決まってから使う", "祭壇の上に立つと持っている力を1つ強化できる"),
                Room(RoomKind.Spring, "命を満たす", "体力が減ってから使うと得", "最大HP+10%と全回復"),
                Room(RoomKind.CurseAltar, "力と引き換えに", "呪いの重さと相談して使う", "力を2回選べる", "代償に呪いが1つつく（ランの間ずっと、自分だけ）"),
                Room(RoomKind.Merchant, "魂の欠片で買う", "欲しい物がなければ何も買わずに帰ってよい", "1人1つ買える", "代金は全員の取り分から払う"),
                Room(RoomKind.Treasure, "鍵のかかった部屋", "鍵は精鋭の主の間・仕掛けの間で手に入る", "鍵を持って扉の前に立つと開く", "希少以上の装備が手に入る"),
                Room(RoomKind.Secret, "壁の向こうの部屋", "ひび割れた壁を探す", "古い地図（エリアの全部屋が見える）と装備"),
                new Entry
                {
                    Id = "room:Omen", Section = CodexSection.Rooms, Name = "迷宮の異変", Sub = "ランごとに1つ", Mark = "異",
                    Traits = new[] { "血の月：精鋭が増え、魂も増える", "豊穣の迷宮：装備が落ちやすく、魔物は手強い", "狂乱：魔物が素早く、もろい", "群れの季節：群れが大きく、一体一体はもろい", "怨嗟：倒れた魔物が魂を弾けさせる。魂は増える", "祝福の風：始めに力をもう1つ。迷宮の主は手強い" },
                    Counter = "異変は右上に出ている。得をする方へ戦い方を寄せる",
                },
                new Entry
                {
                    Id = "room:Deep", Section = CodexSection.Rooms, Name = "深層", Sub = "3つ目の主の先", Mark = "深",
                    Traits = new[] { "3つ目の主を倒すと「帰還」と「深層へ」の2つの道が開く", "深層は階を重ねるほど魔物が強く、最後にまた迷宮の主が待つ", "3つ目の主を倒した時点で踏破。深層で力尽きても踏破として数える" },
                    Counter = "余力があるうちに深く潜り、危なくなる前に帰還する",
                },
                new()
                {
                    Id = "room:Waves", Section = CodexSection.Rooms, Name = "波の種類", Sub = "戦いの間の2波目から", Mark = "波", RewardIcon = -1, AlwaysKnown = true,
                    Traits = new[]
                    {
                        $"{WaveCatalog.Name(WaveKind.Ambush)}：{WaveCatalog.Hint(WaveKind.Ambush)}",
                        $"{WaveCatalog.Name(WaveKind.Swarm)}：{WaveCatalog.Hint(WaveKind.Swarm)}",
                        $"{WaveCatalog.Name(WaveKind.Encircle)}：{WaveCatalog.Hint(WaveKind.Encircle)}",
                        $"{WaveCatalog.Name(WaveKind.Totem)}：{WaveCatalog.Hint(WaveKind.Totem)}（柱が残っている間は部屋が終わらない）",
                    },
                    Counter = "波の始めに出る名前を見て、動き方を変える",
                },
                new()
                {
                    Id = "room:Contract", Section = CodexSection.Rooms, Name = "契約", Sub = "拠点の迷宮の碑で", Mark = "契", RewardIcon = -1, AlwaysKnown = true,
                    Traits = new[]
                    {
                        "難易度の上に、魔物の強化や枷を自分で重ねる（部屋を作った人が結ぶ）",
                        "条項ごとの点数の合計が段位。段位1つにつき魂の欠片 +6%",
                        "段位5・10・20で踏破すると称号（実績）。最高記録は碑に残る。今週の迷宮では無効",
                    },
                    Counter = "勝てるようになったら1つずつ足す",
                },
                Room(RoomKind.Encounter, "道で出会う何か", "仲間と相談して選ぶ。急ぐなら損の少ない方を",
                    "2つの円のどちらかに、部屋にいる全員で立つと選んだことになる（1人なら1人で）。1つの部屋で1回",
                    $"{EncounterCatalog.Name(EncounterKind.LostSoul)}：{EncounterCatalog.Choice(EncounterKind.LostSoul, 0)}（{EncounterCatalog.Effect(EncounterKind.LostSoul, 0)}）／{EncounterCatalog.Choice(EncounterKind.LostSoul, 1)}（{EncounterCatalog.Effect(EncounterKind.LostSoul, 1)}）",
                    $"{EncounterCatalog.Name(EncounterKind.Statue)}：{EncounterCatalog.Choice(EncounterKind.Statue, 0)}（{EncounterCatalog.Effect(EncounterKind.Statue, 0)}）／{EncounterCatalog.Choice(EncounterKind.Statue, 1)}（{EncounterCatalog.Effect(EncounterKind.Statue, 1)}）",
                    $"{EncounterCatalog.Name(EncounterKind.Gambler)}：{EncounterCatalog.Choice(EncounterKind.Gambler, 0)}（{EncounterCatalog.Effect(EncounterKind.Gambler, 0)}）／{EncounterCatalog.Choice(EncounterKind.Gambler, 1)}（{EncounterCatalog.Effect(EncounterKind.Gambler, 1)}）",
                    $"{EncounterCatalog.Name(EncounterKind.BloodFont)}：{EncounterCatalog.Choice(EncounterKind.BloodFont, 0)}（{EncounterCatalog.Effect(EncounterKind.BloodFont, 0)}）／{EncounterCatalog.Choice(EncounterKind.BloodFont, 1)}（{EncounterCatalog.Effect(EncounterKind.BloodFont, 1)}）"),
                Room(RoomKind.Spirit, "さまよう霊の祠", "加護が欲しい神があと1つで寵愛になるときに", "最大HPの1割を捧げると（ランの間ずっと）、神の加護を1つ授かる", "1人1回。エリア2以降に時々ある"),
                Room(RoomKind.Campfire, "主の手前のひと休み", "全員がそろってから主の扉へ",
                    "焚き火のそばで体力が回復し、倒れた仲間も起き上がる", "集合の鐘で仲間を呼べる", "主の扉は生きている全員がそろうと開く"),
                Room(RoomKind.Boss, "エリアの主", "試練を越えて隙を作る", "主が戦っている間は扉が閉まる", "倒すと奥への道が開く（全員で立つと次のエリアへ）"),

                // ---------------------------------------------------------------- Weapons and runes (always open, phase 4)
                Open(CodexSection.Gear, "gear:AltWeapon", "異形の武器", "職業ごとのもう一つの武器", "icons_item", "拾ったら一度持ち替えてみる。袋の説明で通常攻撃の変わり方が分かる",
                    "どの職業にも、いつもと違う武器がある（大斧・魔導書・弩・大槍・投げ短剣・爆薬瓶・大鎌・大剣）",
                    "持つと通常攻撃の形が変わり、見た目も変わる", "宝箱や魔物の落とし物から、ときどき見つかる"),
                Open(CodexSection.Gear, "gear:Rune", "ルーン", "技に宿る石", "icons_item", "技1・技2の枠にはめる。珍しいルーンほど強く効く",
                    "技1のルーン・技2のルーンの枠にはめると、その技を使うたびに力を貸す",
                    "炎（燃える床）・氷（凍らせる）・雷（近くの敵へ）・命（回復）・響（すぐもう一度）・疾（再使用が早い）・影（回避と会心）・魂（魂が飛び散る）",
                    "突進や貫く矢なら通り道に沿って効く（炎の道など）。冒険中は貸し出されない"),
                Open(CodexSection.Gear, "gear:Smith", "鍛冶", "拠点の倉庫で", "camp_anvil", "気に入った装備は鍛えて長く使う",
                    "鍛える：魂の欠片で装備を +1 ずつ鍛える（+5まで）。1回ごとに性能 +8%",
                    "振り直す：魂の欠片で、効果のうち1つを別の効果に引き直す",
                    "珍しい装備ほど欠片が多くかかる。貸し出し品は鍛えられない"),
                Open(CodexSection.Gear, "gear:Sets", "組装備", "鎧・首飾り・指輪の三点", "icons_item", "同じ組の装備を2点・3点とそろえる",
                    "希少の鎧・首飾り・指輪の一部が組装備（緑の名前）。2点で1つ目、3点で2つ目の効果",
                    $"{GearSetCatalog.All[0].Name}：2点 {GearSetCatalog.All[0].TwoText}／3点 {GearSetCatalog.All[0].ThreeText}",
                    $"{GearSetCatalog.All[1].Name}：2点 {GearSetCatalog.All[1].TwoText}／3点 {GearSetCatalog.All[1].ThreeText}",
                    $"{GearSetCatalog.All[2].Name}：2点 {GearSetCatalog.All[2].TwoText}／3点 {GearSetCatalog.All[2].ThreeText}",
                    $"{GearSetCatalog.All[3].Name}：2点 {GearSetCatalog.All[3].TwoText}／3点 {GearSetCatalog.All[3].ThreeText}",
                    $"{GearSetCatalog.All[4].Name}：2点 {GearSetCatalog.All[4].TwoText}／3点 {GearSetCatalog.All[4].ThreeText}",
                    $"{GearSetCatalog.All[5].Name}：2点 {GearSetCatalog.All[5].TwoText}／3点 {GearSetCatalog.All[5].ThreeText}"),

                // ---------------------------------------------------------------- Teamwork (always open)
                Open(CodexSection.Teamwork, "team:Shatter", "砕き", "凍結×強打", "fx_ice", "魔法使いの氷結陣のあと、戦士や狩人が強打を合わせる",
                    "凍った敵に戦士の斬撃・狩人の貫通の一矢などを当てると「砕ける」（大ダメージ）"),
                Open(CodexSection.Teamwork, "team:Mark", "狩人の印", "印×全員の攻撃", "fx_mark", "印を付けたら全員でその敵を狙う",
                    "印の付いた敵は、全員の攻撃が通りやすくなる", "暗殺者の影渡りは印の敵に必ず会心"),
                Open(CodexSection.Teamwork, "team:WarCry", "鬨の声", "仲間の強化", "fx_warcry", "群れに飛び込む直前に使う",
                    "周りの敵を吹き飛ばして気絶させ、近くの仲間の攻撃と守りを上げる"),
                Open(CodexSection.Teamwork, "team:Armor", "鎧崩し", "凍結・気絶・印", "dread_knight", "鎧の敵が出たら、崩す役と叩く役に分かれる",
                    "鎧の敵（亡者の騎士・熟練以上の精鋭）は凍結・気絶・印の間だけ鎧が崩れる"),
                Open(CodexSection.Teamwork, "team:Revive", "助け起こす", "倒れた仲間", "fx_revive_ring", "地図で赤く点滅する仲間のもとへ急ぐ",
                    "倒れた仲間のそばに立つと起き上がらせる", "倒れている間は魔物に狙われない", "全員が倒れると全滅"),
                Open(CodexSection.Teamwork, "team:Ghost", "幽霊", "倒れている間", "fx_soul", "亡骸を仲間に見せつつ、魔物の群れに張り付いて足止めする",
                    "倒れると亡骸を残し、幽霊になってその部屋の中を漂える（障害物はすり抜ける）", "幽霊のそばの魔物は動きがとても遅くなる",
                    "攻撃で魂の火花を飛ばせる。魂も拾える", "起き上がるのは亡骸の場所（仲間が亡骸のそばに立つ）"),
                Open(CodexSection.Teamwork, "team:Ping", "合図", "F キー・マウス中ボタン", "ping_come", "離れて探索するときこそ使う",
                    "敵に向けると「危ない！」、祭壇や扉には「ここ！」、ほかは「来て！」", "M の地図で部屋をクリックすると「集合！」"),
                Open(CodexSection.Teamwork, "team:Break", "体勢崩し", "精鋭・中ボス・主", "fx_nova", "強打・砕き・連携・必殺技を集めて一気に崩す",
                    "精鋭・精鋭の主・主は体力とは別に「体勢」を持つ（主は体力の下の金色の線）",
                    "攻撃で体勢が削れる。強打は2.5倍、砕きは3倍、連携（仲間が止めた敵）は1.5倍、必殺技は2倍。しばらく攻撃しないと戻っていく",
                    $"削り切ると「崩し」：数秒よろめいて動けず、受けるダメージ +{RunSimulation.BreakBonus * 100f:0}%。主は崩すたびに少し固くなる"),
                Open(CodexSection.Teamwork, "team:Union", "合体技", "2人で同時に技", "fx_nova", "声をかけ合って、同じ瞬間に技を使う",
                    "近く（6マス以内）にいる2人が0.8秒以内に技（どちらの技でも）を使うと、2人の間に合体技が出る",
                    "職業の組み合わせごとに名前がある（同じ職業どうしでも出る）",
                    "両方の職業の力（凍結・炎上・気絶・印・呪い）が乗り、2人とも守りの盾を得る。聖騎士がいると光の中の仲間が回復する",
                    "1人につき10秒に1回まで"),
            };
            foreach (var c in ComboCatalog.All)
                list.Add(new Entry
                {
                    Id = "combo:" + c.Kind, Section = CodexSection.Teamwork, Name = c.Name, AlwaysKnown = true,
                    Sub = $"{ClassCatalog.Get(c.A).Name}×{ClassCatalog.Get(c.B).Name}の組み合わせ技", Sheet = "fx_nova",
                    Traits = new[] { c.How, "別々の人が操作する2つの職業がそろうと使える" },
                    Counter = "2人で息を合わせて狙う",
                });

            // ---------------------------------------------------------------- Gods (recorded when their blessing is first offered)
            foreach (var god in GodCatalog.All)
            {
                var traits = new List<string>();
                foreach (var b in GodCatalog.Blessings(god.Id)) traits.Add($"{b.Name}：{b.Text}");
                traits.Add($"{god.FavorName}（同じ神の加護を{GodCatalog.FavorPieces}つ）：{god.FavorText}");
                list.Add(new Entry
                {
                    Id = GodEntry(god.Id), Section = CodexSection.Gods, Name = god.Name, Sub = god.FavorName,
                    IconSheet = "icons_perk", IconFrame = (int)GodCatalog.Blessings(god.Id)[0].Id, Traits = traits.ToArray(),
                    Counter = "中ボスや主を倒すと、全員が神の加護をひとつ授かる。同じ神をそろえると寵愛が目覚める",
                });
            }

            // ---------------------------------------------------------------- Achievements (recorded when achieved)
            foreach (var a in AchievementCatalog.All)
                list.Add(new Entry
                {
                    Id = AchievementEntry(a.Id), Section = CodexSection.Achievements, Name = a.Name, Sub = $"称号「{AchievementCatalog.TitleOf(a)}」",
                    RewardIcon = (int)RoomReward.Boon, Traits = new[] { a.Text }, AlwaysKnown = true,
                    Counter = "称号は拠点の鏡で名前の横に付けられる（仲間にも見える）",
                });

            // ---------------------------------------------------------------- Controls (always open)
            list.Add(Open(CodexSection.Controls, "control:Keyboard", "キーボードとマウス", "PC", "warrior", "照準はマウスの方向。回避中は無敵",
                "WASD：移動　マウス：照準", "左クリック：通常攻撃（押しっぱなし）", "Q・右クリック：スキル1　E：スキル2", "Space・Shift：回避",
                "I・Tab：袋（装備）　M：地図　F：合図　B：図鑑", "Esc：メニュー"));
            list.Add(Open(CodexSection.Controls, "control:Dodge", "回避と見切り", "回避は2回分", "fx_revive_ring", "連打せず、予兆を見て当たる直前に避ける",
                $"回避は{ClassCatalog.DodgeCharges}回分たまる（回避アイコンの●）。1回分は{ClassCatalog.DodgeRechargeTime:0.#}秒で戻る",
                "無敵は回避で動いている一瞬だけ",
                "その一瞬に攻撃を受け流すと「見切り」：時間が一瞬ゆっくりになり、回避が1回分すぐ戻る"));
            list.Add(Open(CodexSection.Controls, "control:Charge", "溜め攻撃", "通常攻撃を押し続けて離す", "fx_nova", "精鋭や主の体勢を崩したいときに",
                "押し続けている間も通常攻撃は出る。足元の輪が金色になったら（約1.2秒）離すと重い一撃",
                "さらに押し続けて輪が赤くなったら（約2.4秒）離すと渾身の一撃",
                "近接の職業は周りを大きく薙ぎ払い、それ以外は貫く衝撃波を放つ。体勢をよく削る"));
            list.Add(Open(CodexSection.Controls, "control:Ultimate", "必殺技と闘志", "R キー・Y ボタン", "fx_holy", "強い敵や群れが来たときのためにとっておく",
                "敵に攻撃を当てる・攻撃を受ける・見切り・合体技で闘志がたまる（回避の右のアイコン）。満タンで必殺技が使える",
                $"戦士「{UltimateCatalog.Name(ClassId.Warrior)}」・魔法使い「{UltimateCatalog.Name(ClassId.Mage)}」・狩人「{UltimateCatalog.Name(ClassId.Ranger)}」・守護騎士「{UltimateCatalog.Name(ClassId.Guardian)}」",
                $"暗殺者「{UltimateCatalog.Name(ClassId.Assassin)}」・錬金術師「{UltimateCatalog.Name(ClassId.Alchemist)}」・死霊術師「{UltimateCatalog.Name(ClassId.Deathcaller)}」・聖騎士「{UltimateCatalog.Name(ClassId.Paladin)}」",
                "それぞれの働きは鏡（職業選び）で確かめられる"));
            list.Add(Open(CodexSection.Controls, "control:Gamepad", "ゲームパッド", "パッド", "ranger", "照準は右スティック（倒していなければ移動方向）",
                "左スティック：移動　右スティック：照準", "RT：通常攻撃　RB：スキル1　LB：スキル2", "A：回避", "Select：地図　十字キー上：合図"));
            list.Add(Open(CodexSection.Controls, "control:Run", "迷宮の進み方", "1回およそ15分", "fx_portal", "寄り道（祠・泉・精鋭）をするほど強くなる",
                "3つのエリアを抜ける。各エリアは部屋が扉でつながっている", "焚き火の間の奥に主の間。主を倒すと次のエリアへ",
                "力の選択と装備の付け替えは、戦闘中でない部屋で", "倒れても、拠点へ戻れば装備と魂の欠片は残る"));
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
