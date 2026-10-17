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
        public static readonly string[] Names = { "マスター", "チャームメイカー", "グレイヴキーパー" };
        public static readonly string[] Sheets = { "npc_master", "npc_crone", "npc_keeper" };

        private static bool Done(ProfileData p, string achievement) => p.achievements != null && p.achievements.IsDone(achievement);
        private static StoryLine L(string id, QuestGiver who, string text, Func<ProfileData, bool> when) => new() { Id = id, Speaker = who, Text = text, When = when };

        public static readonly StoryLine[] All =
        {
            // The old master: a knight who came back from the labyrinth alone.
            L("m0", QuestGiver.Master, "よく来た。この火のまわりだけが、ラビリンスの闇に呑まれずに残った場所だ。剣を振る理由は、ここで見つければいい。", _ => true),
            L("m1", QuestGiver.Master, "私も昔は仲間と潜った。三人でな。……帰ってきたのは私だけだ。だから教える。ひとりで戦うな。", p => p.quests.Step(QuestGiver.Master) >= 1),
            L("m2", QuestGiver.Master, "スケルトンキングを倒したか。あれはかつてこの国の王だった。死んでなお、玉座を守り続けていたのだ。", p => Done(p, "a_king") || p.quests.Step(QuestGiver.Master) >= 3),
            L("m3", QuestGiver.Master, "ブラッドビショップは神々に背いた男だ。ラビリンスの奥で神々の声が聞こえたか？　あれは本物だ。神々もまた、ここに閉じ込められている。", p => Done(p, "a_bishop") || p.quests.Step(QuestGiver.Master) >= 5),
            L("m4", QuestGiver.Master, "リッチキングを討ったのか……。それでもラビリンスは閉じない。ディープの噂は本当だったようだな。私の仲間も、そこで眠っているのかもしれん。", p => Done(p, "a_lich") || p.bestVictory >= 0),
            L("m5", QuestGiver.Master, "ボスたちのメモリーを拾っているそうだな。……あれらは化け物になる前、皆この火を守る者だった。私も、そのひとりになり損ねた男だ。", p => Lore.Count(p.memories) >= 2),
            L("m6", QuestGiver.Master, "夜が明けたな。ラビリンスはまだそこにある。だが、もう誰も呼び戻されずに済む日が来るのかもしれん。", p => p.heartLit > 0),

            // The charm maker: an old woman who reads the souls in the shards.
            L("c0", QuestGiver.Crone, "ひっひっ、ソウルシャードを持ってきたかい。シャードには持ち主のメモリーが残っとる。チャームに込めれば、ちいとパークを貸してくれるよ。", _ => true),
            L("c1", QuestGiver.Crone, "チェストには気をつけな。中身より先に牙が出てくることもある。わしの若い頃はそれで指を二本なくしかけたもんさ。", p => p.quests.Step(QuestGiver.Crone) >= 1),
            L("c2", QuestGiver.Crone, "アルケミストの坊やは、わしの弟子だったんだよ。ブロートの毒バッグから薬を作ろうとして、キャンプを三回吹き飛ばしかけた。", p => p.quests.Step(QuestGiver.Crone) >= 3),
            L("c3", QuestGiver.Crone, "死霊を操る術は禁じられとった。だがね、骨になった者も、誰かの役に立ちたいと思っとるのさ。呼べば応えてくれる。", p => p.quests.Step(QuestGiver.Crone) >= 5),
            L("c5", QuestGiver.Crone, "このキャンプファイアはね、ただの火じゃない。ラビリンスの底の心臓から零れた火種さ。だからお前さんたちは、倒れてもここへ戻ってくるんだよ。", p => Lore.Count(p.memories) >= 4),
            L("c6", QuestGiver.Crone, "心臓に火が戻ったって？　ひっひっ、どうりで今朝はシャードがよく光るわけだ。", p => p.heartLit > 0),
            L("c4", QuestGiver.Crone, "神々のフェイバーを受けたって？　四柱の神はもとはラビリンスの灯りだったのさ。焔、氷、雷、それに命。全部そろえば、闇も晴れるかもねえ。", p => Done(p, "a_favor")),

            // The gravekeeper: buries what the labyrinth gives back.
            L("g0", QuestGiver.Gravekeeper, "……グレイヴキーパーだ。ラビリンスから戻らなかった者の名を、この石に刻んでいる。お前の名を刻む日が来ないことを祈る。", _ => true),
            L("g1", QuestGiver.Gravekeeper, "チャペルの床の血は、まだ乾いていない。ビショップの儀式は終わっていないのだ。", p => p.quests.Step(QuestGiver.Gravekeeper) >= 2),
            L("g2", QuestGiver.Gravekeeper, "アビススローンの先には、まだ道がある。降りた者は誰も戻ってこなかったが……お前たちなら、あるいは。", p => p.quests.Step(QuestGiver.Gravekeeper) >= 4),
            L("g3", QuestGiver.Gravekeeper, "ラビリンスを踏破したと聞いた。石に刻む名が、ひとつ減った。……ありがとう。", p => p.bestVictory >= 0),
            L("g5", QuestGiver.Gravekeeper, "六つのメモリーがそろったか。三つ目の玉座の先、真ん中の道を行け。アッシュキングが……最初の王が、待っている。", p => Lore.Count(p.memories) >= 6),
            L("g6", QuestGiver.Gravekeeper, "石に刻んだ名が、ひとつ、またひとつと薄れていく。眠れたのだな、皆。……ありがとう、灯火を継ぐ者よ。", p => p.heartLit > 0),
            L("g4", QuestGiver.Gravekeeper, "ディープから戻ったのか。底から何か聞こえなかったか？　鐘の音だ。昔、このキャンプにも同じ鐘があった。", p => Done(p, "a_deep")),
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
