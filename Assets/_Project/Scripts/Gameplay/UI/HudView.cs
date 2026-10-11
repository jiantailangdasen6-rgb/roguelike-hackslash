using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// In-run HUD: level/XP, own HP/shield, chosen perks and skill cooldowns, teammate frames, room objective,
    /// boss bar, banners and one-time tips. Teammate markers in the world are drawn by <see cref="AllyOverlay"/>.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public static HudView Instance { get; private set; }

        public static readonly string[] AreaNames = { "朽ちた納骨堂", "血塗られた礼拝堂", "奈落の玉座" };

        private sealed class SkillSlot
        {
            public Text Key;
            public Text Name;
            public Image Shade;
            public Text Time;
        }

        private sealed class TeamFrame
        {
            public GameObject Root;
            public Image Edge;
            public Text Name;
            public UIKit.Bar Hp;
            public UIKit.Bar Shield;
            public Text HpText;
        }

        private UIKit.Bar _xpBar;
        private Text _levelText;
        private Text _roomText;
        private Text _objectiveText;
        private Text _classText;
        private UIKit.Bar _hpBar;
        private UIKit.Bar _shieldBar;
        private Text _hpText;
        private Text _timeText;
        private Text _difficultyText;
        private UIKit.Bar _bossBar;
        private GameObject _bossRoot;
        private Text _bossName;
        private Text _centerText;
        private Text _bannerText;
        private Text _bannerSub;
        private float _bannerUntil;
        private Text _toastText;
        private Text _hintText;
        private RectTransform _perkRow;
        private int _perksShown = -1;
        private readonly SkillSlot[] _skills = new SkillSlot[4];
        private readonly TeamFrame[] _team = new TeamFrame[GameSession.MaxPlayers - 1];
        private readonly List<(string Text, float Until)> _toasts = new();
        private readonly HashSet<string> _tipsShown = new();
        private readonly StringBuilder _sb = new();
        private ClassId? _shownClass;
        private CanvasGroup _curtain;
        private Text _curtainText;

        private void Awake()
        {
            Instance = this;
            var root = UIKit.CreateCanvas("HUD", 20, transform).transform;

            _xpBar = UIKit.CreateBar(root, "XpBar", new Color(0.45f, 0.75f, 1f), new Color(0f, 0f, 0f, 0.7f));
            var xp = _xpBar.Root;
            xp.anchorMin = new Vector2(0f, 1f);
            xp.anchorMax = new Vector2(1f, 1f);
            xp.pivot = new Vector2(0.5f, 1f);
            xp.sizeDelta = new Vector2(0f, 10f);
            xp.anchoredPosition = Vector2.zero;

            // Top-left: own panel (class, level, HP, perks)
            var panel = UIKit.Framed(root, "PlayerPanel", new Color(0.04f, 0.03f, 0.05f, 0.85f));
            panel.Outer().Place(new Vector2(0f, 1f), new Vector2(470f, 132f), new Vector2(20f, -24f));
            _classText = UIKit.Title(panel.transform, "", 30, TextAnchor.UpperLeft);
            _classText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 40f), new Vector2(16f, -8f));
            _levelText = UIKit.Title(panel.transform, "Lv 1", 30, TextAnchor.UpperRight);
            _levelText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(140f, 40f), new Vector2(-16f, -8f));
            _hpBar = UIKit.CreateBar(panel.transform, "Hp", new Color(0.72f, 0.12f, 0.14f), new Color(0.12f, 0.03f, 0.04f));
            _hpBar.Root.Place(new Vector2(0f, 1f), new Vector2(438f, 26f), new Vector2(16f, -54f));
            _shieldBar = UIKit.CreateBar(_hpBar.Root, "Shield", new Color(0.85f, 0.75f, 0.4f, 0.75f), new Color(0f, 0f, 0f, 0f));
            _shieldBar.Root.Stretch();
            _shieldBar.Root.offsetMax = new Vector2(0f, -18f);
            _hpText = UIKit.Label(_hpBar.Root, "", 20, TextAnchor.MiddleCenter);
            _hpText.rectTransform.Stretch();
            _perkRow = UIKit.Rect("Perks", panel.transform).Place(new Vector2(0f, 1f), new Vector2(438f, 36f), new Vector2(16f, -88f));

            // Teammates under it
            for (int i = 0; i < _team.Length; i++) _team[i] = CreateTeamFrame(root, i);

            // Top-right: time, difficulty, room, objective
            _timeText = UIKit.Title(root, "", 30, TextAnchor.UpperRight, UIKit.MutedText);
            _timeText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(240f, 44f), new Vector2(-28f, -22f));
            _difficultyText = UIKit.Label(root, "", 22, TextAnchor.UpperRight, UIKit.MutedText);
            _difficultyText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(400f, 32f), new Vector2(-28f, -64f));
            _roomText = UIKit.Title(root, "", 26, TextAnchor.UpperRight, UIKit.MutedText);
            _roomText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(700f, 40f), new Vector2(-28f, -98f));
            _objectiveText = UIKit.Label(root, "", 28, TextAnchor.UpperRight, UIKit.AccentColor);
            _objectiveText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(800f, 40f), new Vector2(-28f, -136f));

            // Boss bar
            var boss = UIKit.Framed(root, "Boss", new Color(0.03f, 0.02f, 0.03f, 0.9f));
            boss.Outer().Place(new Vector2(0.5f, 1f), new Vector2(900f, 62f), new Vector2(0f, -28f));
            _bossRoot = boss.Outer().gameObject;
            _bossName = UIKit.Title(boss.transform, "", 24, TextAnchor.UpperCenter);
            _bossName.rectTransform.Stretch(2f);
            _bossBar = UIKit.CreateBar(boss.transform, "BossHp", new Color(0.6f, 0.08f, 0.1f), new Color(0.1f, 0.02f, 0.03f));
            _bossBar.Root.Place(new Vector2(0.5f, 0f), new Vector2(860f, 14f), new Vector2(0f, 8f));

            // Bottom: skills
            string[] keys = { "左クリック", "Q", "E", "Space" };
            for (int i = 0; i < 4; i++)
            {
                var slot = UIKit.Framed(root, "Skill" + i, new Color(0.05f, 0.04f, 0.05f, 0.9f));
                slot.Outer().Place(new Vector2(0.5f, 0f), new Vector2(200f, 86f), new Vector2((i - 1.5f) * 214f, 22f));
                var key = UIKit.Label(slot.transform, keys[i], 18, TextAnchor.UpperLeft, UIKit.MutedText);
                key.rectTransform.Stretch(8f);
                var name = UIKit.Label(slot.transform, "", 22, TextAnchor.LowerCenter);
                name.rectTransform.Stretch(8f);
                var shade = UIKit.Panel(slot.transform, "Shade", new Color(0f, 0f, 0f, 0.65f));
                shade.rectTransform.anchorMin = Vector2.zero;
                shade.rectTransform.anchorMax = new Vector2(1f, 0f);
                shade.rectTransform.offsetMin = Vector2.zero;
                shade.rectTransform.offsetMax = Vector2.zero;
                var time = UIKit.Title(slot.transform, "", 32, TextAnchor.MiddleCenter, Color.white);
                time.rectTransform.Stretch();
                _skills[i] = new SkillSlot { Key = key, Name = name, Shade = shade, Time = time };
            }

            _centerText = UIKit.Title(root, "", 38, TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 0.8f));
            _centerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1300f, 160f), new Vector2(0f, 170f));

            _bannerText = UIKit.Title(root, "", 64, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _bannerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 100f), new Vector2(0f, 300f));
            _bannerSub = UIKit.Label(root, "", 28, TextAnchor.MiddleCenter, UIKit.MutedText);
            _bannerSub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1500f, 50f), new Vector2(0f, 240f));

            _toastText = UIKit.Label(root, "", 26, TextAnchor.LowerCenter);
            _toastText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1300f, 220f), new Vector2(0f, 130f));

            _hintText = UIKit.Label(root,
                "WASD 移動　マウス 照準　左クリック 攻撃　Q・E スキル　Space 回避　Esc メニュー\n" +
                "<color=#9fd8ff>連携：凍った敵は強打で「砕ける」／印の敵は全員の攻撃が通りやすい／倒れた仲間のそばに立つと復活</color>",
                22, TextAnchor.LowerLeft, UIKit.MutedText);
            _hintText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(1100f, 80f), new Vector2(24f, 120f));

            BuildCurtain();
            gameObject.AddComponent<AllyOverlay>();
            RunDirector.EventReceived += OnEvent;
        }

        /// <summary>
        /// Covers the screen from the scene load until the local player exists: the host waits for every peer
        /// to finish loading before the run starts, and without a player the camera has nothing to follow.
        /// </summary>
        private void BuildCurtain()
        {
            var canvas = UIKit.CreateCanvas("StartCurtain", 50, transform);
            _curtain = canvas.gameObject.AddComponent<CanvasGroup>();
            _curtain.blocksRaycasts = false;
            UIKit.Panel(canvas.transform, "Background", new Color(0.035f, 0.03f, 0.04f)).rectTransform.Stretch();
            _curtainText = UIKit.Title(canvas.transform, "", 44, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _curtainText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 80f), new Vector2(0f, 20f));
            var sub = UIKit.Label(_curtainText.transform, "全員の準備ができたら始まります", 26, TextAnchor.MiddleCenter, UIKit.MutedText);
            sub.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 44f), new Vector2(0f, -64f));
        }

        private void UpdateCurtain(RunDirector director, NetPlayer local)
        {
            bool waiting = director == null || !director.IsSpawned || (local == null && director.Phase.Value == RunPhase.Playing);
            float target = waiting ? 1f : 0f;
            _curtain.alpha = waiting ? 1f : Mathf.MoveTowards(_curtain.alpha, target, Time.unscaledDeltaTime * 2.5f);
            _curtain.gameObject.SetActive(_curtain.alpha > 0.001f);
            // The words go at once; only the dark backdrop fades, so they never hang over the arena.
            _curtainText.gameObject.SetActive(waiting);
            if (waiting) _curtainText.text = "迷宮へ向かっています" + new string('・', 1 + (int)(Time.unscaledTime * 2f) % 3);
        }

        private TeamFrame CreateTeamFrame(Transform root, int index)
        {
            var frame = UIKit.Framed(root, "Ally" + index, new Color(0.04f, 0.03f, 0.05f, 0.82f));
            frame.Outer().Place(new Vector2(0f, 1f), new Vector2(360f, 58f), new Vector2(20f, -166f - index * 66f));
            var edge = UIKit.Panel(frame.transform, "Edge", Color.white);
            edge.rectTransform.anchorMin = Vector2.zero;
            edge.rectTransform.anchorMax = new Vector2(0f, 1f);
            edge.rectTransform.pivot = new Vector2(0f, 0.5f);
            edge.rectTransform.sizeDelta = new Vector2(6f, 0f);
            var name = UIKit.Label(frame.transform, "", 20, TextAnchor.UpperLeft);
            name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(330f, 26f), new Vector2(16f, -3f));
            var hp = UIKit.CreateBar(frame.transform, "Hp", new Color(0.72f, 0.12f, 0.14f), new Color(0.12f, 0.03f, 0.04f));
            hp.Root.Place(new Vector2(0f, 0f), new Vector2(330f, 18f), new Vector2(16f, 7f));
            var shield = UIKit.CreateBar(hp.Root, "Shield", new Color(0.85f, 0.75f, 0.4f, 0.75f), new Color(0f, 0f, 0f, 0f));
            shield.Root.Stretch();
            shield.Root.offsetMax = new Vector2(0f, -12f);
            var text = UIKit.Label(hp.Root, "", 16, TextAnchor.MiddleCenter);
            text.rectTransform.Stretch();
            frame.Outer().gameObject.SetActive(false);
            return new TeamFrame { Root = frame.Outer().gameObject, Edge = edge, Name = name, Hp = hp, Shield = shield, HpText = text };
        }

        private void OnDestroy()
        {
            RunDirector.EventReceived -= OnEvent;
            if (Instance == this) Instance = null;
        }

        public void Toast(string text)
        {
            _toasts.Add((text, Time.time + 4.5f));
            if (_toasts.Count > 4) _toasts.RemoveAt(0);
        }

        /// <summary>A short explanation the first time something new shows up in a run.</summary>
        private void Tip(string key, string text)
        {
            if (_tipsShown.Add(key)) Toast($"<color=#e0b45a>◆</color> {text}");
        }

        public void Banner(string text, string sub = "", float seconds = 2.8f)
        {
            _bannerText.text = text;
            _bannerSub.text = sub;
            _bannerUntil = Time.time + seconds;
        }

        public static string RoomTitle(int room)
        {
            int area = Mathf.Min(room / RunSimulation.RoomsPerArea, AreaNames.Length - 1);
            bool boss = room % RunSimulation.RoomsPerArea == RunSimulation.RoomsPerArea - 1;
            return boss ? $"{AreaNames[area]} ─ 主の間" : $"{AreaNames[area]} ─ 第{room % RunSimulation.RoomsPerArea + 1}の間";
        }

        private static EnemyDef AreaBoss(int room) =>
            EnemyCatalog.Get(EnemyCatalog.AreaBosses[Mathf.Clamp(room / RunSimulation.RoomsPerArea, 0, EnemyCatalog.AreaBosses.Length - 1)]);

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.RoomStarted:
                {
                    var objective = (e.Int % RunSimulation.RoomsPerArea) switch
                    {
                        1 => $"{RunSimulation.SurviveSeconds:0}秒間 生き延びよ",
                        3 => $"{AreaBoss(e.Int).Name}を討て",
                        _ => "すべての魔物を倒せ",
                    };
                    Banner(RoomTitle(e.Int), objective);
                    var layout = Arena.Get(e.Id);
                    foreach (var g in layout.Gimmicks)
                    {
                        if (g.Kind == GimmickKind.PowderKeg) Tip("keg", "火薬樽は攻撃すると爆発する。魔物の群れを巻き込め（味方も巻き込むので注意）");
                        if (g.Kind is GimmickKind.SpikeTrap or GimmickKind.FlameJet) Tip("trap", "罠は魔物にも効く。誘い込んで一網打尽に");
                        if (g.Kind == GimmickKind.HealingFont) Tip("font", "癒しの泉：上に立つと周りの仲間ごと回復（部屋に1回）");
                    }
                    break;
                }
                case SimEventType.RoomCleared:
                    Banner("部屋を制圧した", "全員で奥の門へ");
                    break;
                case SimEventType.BossSpawned:
                {
                    var def = EnemyCatalog.Get((EnemyType)e.Int);
                    Banner(def.Name, def.Title ?? "", 3.5f);
                    if (def.Type == EnemyType.BoneKing) Tip("pillar", "王の突進を柱にぶつけると、しばらくよろめく");
                    break;
                }
                case SimEventType.SpawnWarning:
                    switch ((EnemyType)e.Int)
                    {
                        case EnemyType.DreadKnight: Tip("armor", "亡者の騎士は鎧で攻撃を防ぐ。凍結・気絶・印で鎧を崩してから叩け"); break;
                        case EnemyType.Bloater: Tip("bloater", "腐れ膨れは近づくと破裂する。離れて倒すか、魔物の群れの中で破裂させろ"); break;
                        case EnemyType.Wraith: Tip("wraith", "怨霊は姿を消して足元に現れる。光る円から離れろ"); break;
                        case EnemyType.Necromancer: Tip("necro", "死霊術師は骸骨を呼び続ける。先に仕留めろ"); break;
                    }
                    break;
                case SimEventType.WaveStarted when e.Int > 1:
                    Toast($"第{e.Int}波 襲来！");
                    break;
                case SimEventType.PlayerDowned:
                    if (NetPlayer.Local != null && e.ClientId != NetPlayer.Local.OwnerClientId)
                        Toast($"<color=#ff7060>{NameOf(e.ClientId)}が倒れた！</color> 矢印の先へ。そばに立てば助け起こせる");
                    break;
                case SimEventType.PlayerRevived:
                    if (NetPlayer.Local != null && e.ClientId != NetPlayer.Local.OwnerClientId) Toast($"<color=#9fff9f>{NameOf(e.ClientId)}が立ち上がった</color>");
                    break;
                case SimEventType.AreaEffect when (AreaKind)e.Int == AreaKind.Shatter:
                    Toast("<color=#9fd8ff>連携：砕き！</color>");
                    break;
            }
        }

        private static string NameOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return $"{p.DisplayName}（{p.ClassDef.Name}）";
            return "仲間";
        }

        private void Update()
        {
            var director = RunDirector.Instance;
            var local = NetPlayer.Local;
            UpdateCurtain(director, local);
            if (director == null || !director.IsSpawned) return;

            _xpBar.Fill = director.XpFraction.Value;
            _levelText.text = $"Lv {director.TeamLevel.Value}";
            _timeText.text = UIKit.FormatTime(director.Elapsed.Value);
            _difficultyText.text = $"難易度：{DifficultyCatalog.Get(director.DifficultyLevel.Value).Name}";
            _roomText.text = RoomTitle(director.RoomIndex.Value) + $"   {director.RoomIndex.Value + 1}/{RunSimulation.TotalRooms}";
            _objectiveText.text = ObjectiveText(director);

            float boss = director.BossHealth.Value;
            UIKit.Toggle(_bossRoot, boss >= 0f);
            if (boss >= 0f)
            {
                _bossBar.Fill = boss;
                var def = EnemyCatalog.Get(director.BossType.Value);
                _bossName.text = boss < 0.5f ? $"{def.Name}  <size=18><color=#ff7060>激昂</color></size>" : def.Name;
            }

            if (local != null)
            {
                var def = local.ClassDef;
                if (_shownClass != def.Id)
                {
                    _shownClass = def.Id;
                    _classText.text = def.Name;
                    _skills[0].Name.text = def.BasicName;
                    _skills[1].Name.text = def.Skill1Name;
                    _skills[2].Name.text = def.Skill2Name;
                    _skills[3].Name.text = "回避";
                }
                float max = Mathf.Max(1f, local.MaxHp.Value);
                _hpBar.Fill = local.Hp.Value / max;
                _shieldBar.Fill = local.Shield.Value / max;
                _hpText.text = local.Shield.Value > 0 ? $"{local.Hp.Value:0} / {max:0}  (+{local.Shield.Value:0})" : $"{local.Hp.Value:0} / {max:0}";
                SetCooldown(_skills[1], local.Skill1Cooldown.Value, def.Skill1Cooldown);
                SetCooldown(_skills[2], local.Skill2Cooldown.Value, def.Skill2Cooldown);
                SetCooldown(_skills[3], local.DodgeCooldown.Value, ClassCatalog.DodgeCooldown);
                SetCooldown(_skills[0], 0f, 1f);
            }

            UpdatePerkRow();
            UpdateTeam(local);

            _hintText.enabled = director.RoomIndex.Value == 0 && director.Elapsed.Value < 30f;
            _centerText.text = CenterMessage(director, local);
            _bannerText.enabled = _bannerSub.enabled = Time.time < _bannerUntil;

            _toasts.RemoveAll(t => t.Until < Time.time);
            _sb.Clear();
            foreach (var t in _toasts) _sb.Append(t.Text).Append('\n');
            _toastText.text = _sb.ToString();
        }

        private void UpdateTeam(NetPlayer local)
        {
            int slot = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == local || slot >= _team.Length) continue;
                var f = _team[slot++];
                UIKit.Toggle(f.Root, true);
                f.Edge.color = p.Color;
                float max = Mathf.Max(1f, p.MaxHp.Value);
                if (p.Downed.Value)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                    f.Name.text = $"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.DisplayName}</color> {p.ClassDef.Name}　<color=#ff6050>瀕死</color>";
                    f.Hp.Fill = p.ReviveProgress.Value;
                    f.Shield.Fill = 0f;
                    f.HpText.text = p.BeingRevived ? $"復活中 {p.ReviveProgress.Value:P0}" : "助けが必要";
                    f.HpText.color = Color.Lerp(new Color(1f, 0.5f, 0.45f), Color.white, pulse * 0.5f);
                }
                else
                {
                    f.Name.text = $"<color=#{ColorUtility.ToHtmlStringRGB(p.Color)}>{p.DisplayName}</color> {p.ClassDef.Name}";
                    f.Hp.Fill = p.Hp.Value / max;
                    f.Shield.Fill = p.Shield.Value / max;
                    f.HpText.text = $"{p.Hp.Value:0} / {max:0}";
                    f.HpText.color = Color.white;
                }
            }
            for (; slot < _team.Length; slot++) UIKit.Toggle(_team[slot].Root, false);
        }

        private void UpdatePerkRow()
        {
            var taken = PerkView.Taken;
            if (taken.Count == _perksShown) return;
            _perksShown = taken.Count;
            for (int i = _perkRow.childCount - 1; i >= 0; i--) Destroy(_perkRow.GetChild(i).gameObject);

            // One icon per perk (highest level shown as a small number).
            var levels = new Dictionary<byte, int>();
            var order = new List<PerkOption>();
            foreach (var o in taken)
            {
                if (o.IsHeal) continue;
                if (!levels.ContainsKey(o.Perk)) order.Add(o);
                levels[o.Perk] = Mathf.Max(levels.TryGetValue(o.Perk, out int l) ? l : 0, o.Level);
            }
            for (int i = 0; i < order.Count && i < 13; i++)
            {
                var rect = UIKit.Rect("Perk", _perkRow).Place(new Vector2(0f, 0.5f), new Vector2(32f, 32f), new Vector2(i * 34f, 0f));
                var img = rect.gameObject.AddComponent<Image>();
                img.sprite = GameIcons.Perk(order[i]);
                img.raycastTarget = false;
                int level = levels[order[i].Perk];
                if (level > 1)
                {
                    var lv = UIKit.Label(rect, level.ToString(), 16, TextAnchor.LowerRight, UIKit.AccentColor);
                    lv.rectTransform.Stretch(-2f);
                }
            }
        }

        private static void SetCooldown(SkillSlot slot, float remaining, float max)
        {
            bool cooling = remaining > 0.05f;
            slot.Shade.rectTransform.anchorMax = new Vector2(1f, cooling ? Mathf.Clamp01(remaining / Mathf.Max(0.1f, max)) : 0f);
            slot.Time.text = cooling ? remaining.ToString(remaining < 1f ? "0.0" : "0") : "";
        }

        private static string ObjectiveText(RunDirector d)
        {
            if (d.RoomCleared.Value)
            {
                string hold = d.ExitHold.Value > 0f ? $"  …{d.ExitHold.Value / RunSimulation.ExitHoldTime:P0}" : "";
                return $"奥の門へ全員で向かえ{hold}";
            }
            return d.Objective.Value switch
            {
                RoomObjective.Survive => $"生き延びよ  残り {UIKit.FormatTime(d.Target.Value - d.Progress.Value)}",
                RoomObjective.Waves => $"魔物の群れ  第{Mathf.Max(1, d.Wave.Value)}波 / {d.WaveCount.Value}",
                _ => $"{EnemyCatalog.Get(d.BossType.Value).Name}を討て",
            };
        }

        private static string CenterMessage(RunDirector d, NetPlayer local)
        {
            if (d.Phase.Value != RunPhase.Playing) return "";
            if (local == null) return ""; // the start curtain covers this
            if (local.Downed.Value)
                return local.BeingRevived
                    ? $"仲間が助け起こしている…  {local.ReviveProgress.Value:P0}"
                    : NetPlayer.All.Count > 1 ? "倒れた… 仲間がそばに来れば立ち上がれる" : "倒れた…";
            if (d.Paused.Value && !PerkView.IsShowing) return "仲間が力を選んでいる…";
            return "";
        }
    }
}
