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

                // ---------------------------------------------------------------- Rooms
                Room(RoomKind.Start, "エリアの始まり", "まずは近くの扉から探索を始める", "魔物はいない", "扉の切れ目で壁に向かって歩くと隣の部屋へ"),
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
                Room(RoomKind.Spirit, "さまよう霊の祠", "加護が欲しい神があと1つで寵愛になるときに", "最大HPの1割を捧げると（ランの間ずっと）、神の加護を1つ授かる", "1人1回。エリア2以降に時々ある"),
                Room(RoomKind.Campfire, "主の手前のひと休み", "全員がそろってから主の扉へ",
                    "焚き火のそばで体力が回復し、倒れた仲間も起き上がる", "集合の鐘で仲間を呼べる", "主の扉は生きている全員がそろうと開く"),
                Room(RoomKind.Boss, "エリアの主", "試練を越えて隙を作る", "主が戦っている間は扉が閉まる", "倒すと奥への道が開く（全員で立つと次のエリアへ）"),

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
            _ => MonsterId(t),
        };
    }
}
