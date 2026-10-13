using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>One thing a resident of the camp says, once its condition is met.</summary>
    public sealed class StoryLine
    {
        public string Id;
        public QuestGiver Speaker;
        public string Text;
        public Func<ProfileData, bool> When;
    }

    /// <summary>
    /// The camp's people and the world they talk about (拠点の住人の物語). Lines open up as this device's profile
    /// grows (requests, lords slain, the labyrinth cleared); each is told once, and can be heard again at their station.
    /// </summary>
    public static class CampStories
    {
        public static readonly string[] Names = { "師範", "お守り職人", "墓守" };
        public static readonly string[] Sheets = { "npc_master", "npc_crone", "npc_keeper" };

        private static bool Done(ProfileData p, string achievement) => p.achievements != null && p.achievements.IsDone(achievement);
        private static StoryLine L(string id, QuestGiver who, string text, Func<ProfileData, bool> when) => new() { Id = id, Speaker = who, Text = text, When = when };

        public static readonly StoryLine[] All =
        {
            // The old master: a knight who came back from the labyrinth alone.
            L("m0", QuestGiver.Master, "よく来た。この火のまわりだけが、迷宮の闇に呑まれずに残った場所だ。剣を振る理由は、ここで見つければいい。", _ => true),
            L("m1", QuestGiver.Master, "私も昔は仲間と潜った。三人でな。……帰ってきたのは私だけだ。だから教える。ひとりで戦うな。", p => p.quests.Step(QuestGiver.Master) >= 1),
            L("m2", QuestGiver.Master, "騎士王を倒したか。あれはかつてこの国の王だった。死んでなお、玉座を守り続けていたのだ。", p => Done(p, "a_king") || p.quests.Step(QuestGiver.Master) >= 3),
            L("m3", QuestGiver.Master, "血の司教は神々に背いた男だ。迷宮の奥で神々の声が聞こえたか？　あれは本物だ。神々もまた、ここに閉じ込められている。", p => Done(p, "a_bishop") || p.quests.Step(QuestGiver.Master) >= 5),
            L("m4", QuestGiver.Master, "冥王を討ったのか……。それでも迷宮は閉じない。深層の噂は本当だったようだな。私の仲間も、そこで眠っているのかもしれん。", p => Done(p, "a_lich") || p.bestVictory >= 0),

            // The charm maker: an old woman who reads the souls in the shards.
            L("c0", QuestGiver.Crone, "ひっひっ、魂の欠片を持ってきたかい。欠片には持ち主の記憶が残っとる。お守りに込めれば、ちいと力を貸してくれるよ。", _ => true),
            L("c1", QuestGiver.Crone, "宝箱には気をつけな。中身より先に牙が出てくることもある。わしの若い頃はそれで指を二本なくしかけたもんさ。", p => p.quests.Step(QuestGiver.Crone) >= 1),
            L("c2", QuestGiver.Crone, "錬金術師の坊やは、わしの弟子だったんだよ。腐れ膨れの毒袋から薬を作ろうとして、拠点を三回吹き飛ばしかけた。", p => p.quests.Step(QuestGiver.Crone) >= 3),
            L("c3", QuestGiver.Crone, "死霊を操る術は禁じられとった。だがね、骨になった者も、誰かの役に立ちたいと思っとるのさ。呼べば応えてくれる。", p => p.quests.Step(QuestGiver.Crone) >= 5),
            L("c4", QuestGiver.Crone, "神々の寵愛を受けたって？　四柱の神はもとは迷宮の灯りだったのさ。焔、氷、雷、それに命。全部そろえば、闇も晴れるかもねえ。", p => Done(p, "a_favor")),

            // The gravekeeper: buries what the labyrinth gives back.
            L("g0", QuestGiver.Gravekeeper, "……墓守だ。迷宮から戻らなかった者の名を、この石に刻んでいる。お前の名を刻む日が来ないことを祈る。", _ => true),
            L("g1", QuestGiver.Gravekeeper, "礼拝堂の床の血は、まだ乾いていない。司教の儀式は終わっていないのだ。", p => p.quests.Step(QuestGiver.Gravekeeper) >= 2),
            L("g2", QuestGiver.Gravekeeper, "奈落の玉座の先には、まだ道がある。降りた者は誰も戻ってこなかったが……お前たちなら、あるいは。", p => p.quests.Step(QuestGiver.Gravekeeper) >= 4),
            L("g3", QuestGiver.Gravekeeper, "迷宮を踏破したと聞いた。石に刻む名が、ひとつ減った。……ありがとう。", p => p.bestVictory >= 0),
            L("g4", QuestGiver.Gravekeeper, "深層から戻ったのか。底から何か聞こえなかったか？　鐘の音だ。昔、この拠点にも同じ鐘があった。", p => Done(p, "a_deep")),
        };

        /// <summary>Lines of one resident that are open now, in order.</summary>
        public static List<StoryLine> Open(ProfileData p, QuestGiver who)
        {
            var lines = new List<StoryLine>();
            foreach (var l in All) if (l.Speaker == who && l.When(p)) lines.Add(l);
            return lines;
        }

        /// <summary>Open lines not told yet.</summary>
        public static List<StoryLine> Unread(ProfileData p, QuestGiver who) =>
            Open(p, who).FindAll(l => !p.storyRead.Contains(l.Id));
    }
}
