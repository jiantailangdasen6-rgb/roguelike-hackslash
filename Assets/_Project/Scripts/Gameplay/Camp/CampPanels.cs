using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The preparation panels opened at the camp's stations: mirror (class and look), master (mastery),
    /// charm maker (trinkets) and the labyrinth tablet (difficulty, the host's choice). One at a time, modal.
    /// Everything here changes this device's profile; <see cref="CampDirector"/> passes the result to the host.
    /// </summary>
    public sealed class CampPanels
    {
        private const float Width = 1240f, Height = 820f;

        private readonly GameObject _dim;
        private readonly Text _title;
        private readonly Text _shards;
        private readonly RectTransform _body;
        private readonly Text _status;
        private readonly Button _close;

        public CampStation? Open { get; private set; }

        private static ProfileData Profile => ProfileStore.Current;

        public CampPanels(Transform root, UnityEngine.Events.UnityAction close)
        {
            var dim = UIKit.Panel(root, "PanelDim", new Color(0f, 0f, 0f, 0.62f));
            dim.rectTransform.Stretch();
            _dim = dim.gameObject;

            var panel = UIKit.Framed(dim.transform, "Panel");
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
            _title = UIKit.Title(panel.transform, "", 50, TextAnchor.MiddleLeft);
            _title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(800f, 70f), new Vector2(40f, -20f));
            _shards = UIKit.Label(panel.transform, "", 28, TextAnchor.MiddleRight, new Color(0.6f, 0.85f, 1f));
            _shards.rectTransform.Place(new Vector2(1f, 1f), new Vector2(400f, 70f), new Vector2(-40f, -20f));
            _body = UIKit.Rect("Body", panel.transform).Place(new Vector2(0.5f, 1f), new Vector2(Width - 80f, Height - 220f), new Vector2(0f, -104f));
            _status = UIKit.Label(panel.transform, "", 24, TextAnchor.MiddleLeft, new Color(1f, 0.65f, 0.55f));
            _status.rectTransform.Place(new Vector2(0f, 0f), new Vector2(800f, 60f), new Vector2(40f, 26f));
            _close = UIKit.Button(panel.transform, "閉じる（Esc）", 28, close);
            _close.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(260f, 64f), new Vector2(-30f, 24f));

            // The resident's words (CampStories): a box with their portrait over the bottom of the panel.
            _listen = UIKit.Button(panel.transform, "話を聞く", 24, () => Talk(all: true));
            _listen.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(200f, 64f), new Vector2(-300f, 24f));
            var speech = UIKit.Framed(panel.transform, "Speech", new Color(0.05f, 0.04f, 0.05f, 0.98f));
            _speech = speech.Outer();
            _speech.Place(new Vector2(0.5f, 0f), new Vector2(Width - 60f, 190f), new Vector2(0f, 100f));
            var speechButton = _speech.gameObject.AddComponent<Button>();
            speechButton.targetGraphic = _speech.GetComponent<Image>();
            speechButton.onClick.AddListener(NextLine);
            var frame = UIKit.Panel(speech.transform, "PortraitBack", new Color(0.1f, 0.08f, 0.09f, 1f));
            frame.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(150f, 150f), new Vector2(24f, 0f));
            frame.raycastTarget = false;
            _portrait = UIKit.Rect("Portrait", frame.transform).Stretch().gameObject.AddComponent<Image>();
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            _speaker = UIKit.Title(speech.transform, "", 30, TextAnchor.UpperLeft, UIKit.AccentColor);
            _speaker.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 40f), new Vector2(196f, -14f));
            _line = UIKit.Label(speech.transform, "", 25, TextAnchor.UpperLeft, new Color(0.95f, 0.92f, 0.86f));
            _line.rectTransform.Place(new Vector2(0f, 1f), new Vector2(Width - 300f, 110f), new Vector2(196f, -58f));
            _more = UIKit.Label(speech.transform, "", 18, TextAnchor.LowerRight, UIKit.MutedText);
            _more.rectTransform.Place(new Vector2(1f, 0f), new Vector2(360f, 30f), new Vector2(-20f, 10f));
            _speech.gameObject.SetActive(false);
            _dim.SetActive(false);
        }

        private readonly Button _listen;
        private readonly RectTransform _speech;
        private readonly Image _portrait;
        private readonly Text _speaker, _line, _more;
        private readonly List<StoryLine> _talk = new();

        private static QuestGiver? Resident(CampStation s) => s switch
        {
            CampStation.Master => QuestGiver.Master,
            CampStation.Charms => QuestGiver.Crone,
            CampStation.Board => QuestGiver.Gravekeeper,
            _ => null,
        };

        public void Show(CampStation station)
        {
            Open = station;
            _contractPage = station == CampStation.Tablet && CommandLine.Has("-contractpage"); // screenshots
            _status.text = "";
            _dim.SetActive(true);
            Rebuild();
            _listen.gameObject.SetActive(Resident(station).HasValue);
            Talk(all: false);
            EventSystem.current?.SetSelectedGameObject(_close.gameObject);
        }

        /// <summary>The resident at a station has something they have not told yet.</summary>
        public static bool HasNews(CampStation s) => Resident(s) is QuestGiver who && CampStories.Unread(ProfileStore.Current, who).Count > 0;

        /// <summary>The resident tells what they have not told yet (or, asked, everything they have to say).</summary>
        private void Talk(bool all)
        {
            _talk.Clear();
            if (Open == null || Resident(Open.Value) is not QuestGiver who) return;
            _talk.AddRange(all ? CampStories.Open(Profile, who) : CampStories.Unread(Profile, who));
            if (_talk.Count == 0)
            {
                _speech.gameObject.SetActive(false);
                return;
            }
            var sheet = SpriteSheet.Load(CampStories.Sheets[(int)who], quiet: true);
            _portrait.sprite = sheet != null && sheet.TryGetClip("idle", out _) ? sheet.Sprites("idle")[0] : sheet?.First;
            _speaker.text = CampStories.Names[(int)who];
            ShowLine();
        }

        private void ShowLine()
        {
            var l = _talk[0];
            _line.text = $"「{l.Text}」";
            _more.text = _talk.Count > 1 ? $"クリックで次へ（あと{_talk.Count - 1}）" : "クリックで閉じる";
            _speech.gameObject.SetActive(true);
            _speech.SetAsLastSibling();
            UiSound.Play(UiCue.Page);
        }

        private void NextLine()
        {
            if (_talk.Count == 0) return;
            if (!Profile.storyRead.Contains(_talk[0].Id))
            {
                Profile.storyRead.Add(_talk[0].Id);
                ProfileStore.Save();
            }
            _talk.RemoveAt(0);
            if (_talk.Count > 0) ShowLine();
            else _speech.gameObject.SetActive(false);
        }

        public void Hide()
        {
            Open = null;
            _dim.SetActive(false);
        }

        public void Rebuild()
        {
            if (Open == null) return;
            for (int i = _body.childCount - 1; i >= 0; i--) Object.Destroy(_body.GetChild(i).gameObject);
            _shards.text = $"ソウルシャード {Profile.shards}";
            switch (Open.Value)
            {
                case CampStation.Mirror: BuildMirror(); break;
                case CampStation.Master: BuildMastery(); break;
                case CampStation.Charms: BuildCharms(); break;
                case CampStation.Tablet: if (_contractPage) BuildContract(); else BuildDifficulty(); break;
                case CampStation.Board: BuildQuests(); break;
                case CampStation.Hearth: BuildHearth(); break;
            }
        }

        // ------------------------------------------------------------------ Hearth: growing the camp

        /// <summary>The camp's facilities: what each gives now, what the next level gives, and what it costs.</summary>
        private void BuildHearth()
        {
            _title.text = "キャンプファイア　<size=28><color=#bfb6aa>ソウルシャードでキャンプを育てる</color></size>";
            string[] icons = { "camp_fire", "camp_anvil", "camp_shelf", "camp_shrine", "camp_urn" };
            for (int i = 0; i < CampUpgrades.All.Length; i++)
            {
                var def = CampUpgrades.All[i];
                int level = Profile.CampLevel(def.Id);
                bool max = level >= def.MaxLevel;
                int cost = max ? 0 : def.Costs[level];
                bool affordable = !max && Profile.shards >= cost;
                var row = UIKit.Framed(_body, def.Name, level > 0 ? new Color(0.12f, 0.08f, 0.04f, 1f) : new Color(0.06f, 0.05f, 0.06f, 1f),
                    level > 0 ? UIKit.AccentColor * 0.85f : UIKit.GoldTrim * 0.55f);
                row.Outer().Place(new Vector2(0.5f, 1f), new Vector2(1140f, 104f), new Vector2(0f, -i * 114f));

                Preview(row.transform, icons[i], "default", 92f, new Vector2(0f, 0.5f), new Vector2(60f, 0f));
                var name = UIKit.Title(row.transform, def.Name, 34, TextAnchor.MiddleLeft, level > 0 ? Color.white : new Color(0.85f, 0.8f, 0.72f));
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(200f, 44f), new Vector2(140f, -10f));
                // Level pips
                for (int l = 0; l < def.MaxLevel; l++)
                {
                    var pip = UIKit.Panel(row.transform, "Pip", l < level ? UIKit.AccentColor : new Color(0.25f, 0.22f, 0.2f, 1f));
                    pip.rectTransform.Place(new Vector2(0f, 1f), new Vector2(18f, 18f), new Vector2(144f + l * 24f, -66f));
                }
                string now = level > 0 ? def.Text(level) : "<color=#8a7f80>まだない</color>";
                string next = max ? "<color=#e0b45a>完成</color>" : $"次：{def.Text(level + 1)}";
                var text = UIKit.Label(row.transform, $"{now}\n<size=20><color=#bfb6aa>{next}</color></size>", 24, TextAnchor.MiddleLeft);
                text.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(560f, 90f), new Vector2(360f, 0f));

                if (max) continue;
                var id = def.Id;
                var button = UIKit.Button(row.transform, $"{(level == 0 ? "建てる" : "強くする")}  <color=#9fd8ff>{cost}</color>", 24, () =>
                {
                    if (Profile.TryUpgradeCamp(id)) Save($"{CampUpgrades.Get(id).Name}を{(Profile.CampLevel(id) == 1 ? "建てた" : "強くした")}");
                    else Save("ソウルシャードが足りない");
                }, affordable ? UIKit.BloodColor : (Color?)null);
                button.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(230f, 62f), new Vector2(-24f, 0f));
                button.interactable = affordable;
            }
        }

        // ------------------------------------------------------------------ Notice board: requests

        /// <summary>One request per person in the camp: what, how far, what for, and a button to turn it in.</summary>
        private void BuildQuests()
        {
            _title.text = "クエストボード　<size=28><color=#bfb6aa>仲間と一緒でも進む・達成したらここで報酬</color></size>";
            var log = Profile.quests;
            int row = 0;
            foreach (var q in QuestCatalog.Active(log))
            {
                int progress = log.Progress(q);
                bool done = progress >= q.Target;
                var item = UIKit.Framed(_body, q.Id, done ? new Color(0.16f, 0.12f, 0.05f, 1f) : new Color(0.06f, 0.05f, 0.06f, 1f),
                    done ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                item.Outer().Place(new Vector2(0.5f, 1f), new Vector2(1140f, 170f), new Vector2(0f, -row * 190f));

                var giver = UIKit.Label(item.transform, $"{QuestCatalog.GiverNames[(int)q.Giver]}より", 20, TextAnchor.UpperLeft, UIKit.MutedText);
                giver.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 28f), new Vector2(20f, -12f));
                var name = UIKit.Title(item.transform, q.Name, 36, TextAnchor.UpperLeft, Color.white);
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(560f, 46f), new Vector2(20f, -40f));
                var text = UIKit.Label(item.transform, q.Text, 24, TextAnchor.UpperLeft, new Color(0.85f, 0.8f, 0.72f));
                text.rectTransform.Place(new Vector2(0f, 1f), new Vector2(560f, 34f), new Vector2(20f, -90f));

                var bar = UIKit.CreateBar(item.transform, "Progress", done ? UIKit.AccentColor : new Color(0.55f, 0.75f, 0.95f), new Color(0.03f, 0.03f, 0.05f, 1f));
                bar.Root.Place(new Vector2(0f, 0f), new Vector2(420f, 14f), new Vector2(20f, 20f));
                bar.Fill = progress / (float)q.Target;
                var count = UIKit.Label(item.transform, done ? "達成！" : $"{progress} / {q.Target}", 22, TextAnchor.MiddleLeft, done ? UIKit.AccentColor : Color.white);
                count.rectTransform.Place(new Vector2(0f, 0f), new Vector2(160f, 30f), new Vector2(452f, 12f));

                string reward = $"<color=#9fd8ff>ソウルシャード {q.Shards}</color>" + (q.Unlock != null && !Profile.IsUnlocked(q.Unlock) ? $"\n<color=#e0b45a>{q.UnlockName}</color>" : "");
                var rewardText = UIKit.Label(item.transform, $"<size=20><color=#bfb6aa>報酬</color></size>\n{reward}", 24, TextAnchor.UpperLeft);
                rewardText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 120f), new Vector2(620f, -16f));

                var quest = q;
                var claim = UIKit.Button(item.transform, done ? "受け取る" : "進行中", 28, () => Claim(quest), done ? UIKit.BloodColor : new Color(0.12f, 0.1f, 0.1f));
                claim.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(190f, 70f), new Vector2(-20f, 0f));
                claim.interactable = done;
                row++;
            }
        }

        private void Claim(QuestDef q)
        {
            var log = Profile.quests;
            if (!log.IsDone(q)) return;
            Profile.shards += q.Shards;
            string extra = "";
            if (q.Unlock != null && !Profile.IsUnlocked(q.Unlock))
            {
                Profile.unlocked.Add(q.Unlock);
                extra = $"、{q.UnlockName}";
            }
            log.Claim(q);
            SfxPlayer.Instance?.Play("levelup");
            Save($"「{q.Name}」の報酬：ソウルシャード {q.Shards}{extra}");
        }

        /// <summary>Requests waiting to be turned in (the board shows a mark).</summary>
        public static int Claimable
        {
            get
            {
                int n = 0;
                var log = Profile.quests;
                foreach (var q in QuestCatalog.Active(log)) if (log.IsDone(q)) n++;
                return n;
            }
        }


        private void Save(string message = null)
        {
            if (message != null) _status.text = message;
            ProfileStore.Save(); // CampDirector hears this and updates the host
            Rebuild();
        }

        // ------------------------------------------------------------------ Mirror: class and look

        private void BuildMirror()
        {
            _title.text = "ミラー　<size=28><color=#bfb6aa>クラスと見た目</color></size>";
            var current = ClassCatalog.Get(Profile.selectedClass);

            if (!Profile.IsClassOpen(current.Id)) current = ClassCatalog.Get(ClassId.Warrior);
            for (int i = 0; i < ClassCatalog.All.Length; i++)
            {
                var def = ClassCatalog.All[i];
                bool open = Profile.IsClassOpen(def.Id);
                bool selected = def.Id == current.Id;
                var card = UIKit.Framed(_body, def.Name, selected ? new Color(0.17f, 0.11f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.07f, 1f),
                    selected ? UIKit.AccentColor : UIKit.GoldTrim * (open ? 0.6f : 0.3f));
                card.Outer().Place(new Vector2(0f, 1f), new Vector2(360f, 70f), new Vector2(0f, -i * 74f));
                var preview = Preview(card.transform, SkinCatalog.Sheet(def.Id, Profile.SkinFor(def.Id)), "idle", 76f, new Vector2(0f, 0.5f), new Vector2(4f, 4f));
                preview.raycastTarget = false;
                if (!open) preview.color = new Color(0.1f, 0.09f, 0.12f, 0.9f); // a silhouette until unlocked
                var name = UIKit.Title(card.transform, def.Name, 26, TextAnchor.UpperLeft, open ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(250f, 34f), new Vector2(90f, -4f));
                var unlockQuest = UnlockingQuest(def.Id);
                string roleText = open ? def.Role : unlockQuest != null ? $"「{unlockQuest.Name}」で解放" : "まだ解放されていない";
                var role = UIKit.Label(card.transform, roleText, 17, TextAnchor.UpperLeft, open ? UIKit.MutedText : new Color(0.9f, 0.75f, 0.45f, 0.8f));
                role.rectTransform.Place(new Vector2(0f, 1f), new Vector2(262f, 30f), new Vector2(90f, -38f));
                var id = def.Id;
                AddButton(card.Outer(), () =>
                {
                    if (!Profile.IsClassOpen(id))
                    {
                        var q = UnlockingQuest(id);
                        _status.text = q != null
                            ? $"「{ClassCatalog.Get(id).Name}」は{QuestCatalog.GiverNames[(int)q.Giver]}のクエスト「{q.Name}」（{q.Text}）で解放される"
                            : "まだ解放されていない";
                        return;
                    }
                    Profile.selectedClass = id;
                    Save();
                });
            }

            // Detail of the chosen class.
            var detail = UIKit.Rect("Detail", _body).Place(new Vector2(1f, 1f), new Vector2(740f, 620f), Vector2.zero);
            Preview(detail, SkinCatalog.Sheet(current.Id, Profile.SkinFor(current.Id)), "attack", 200f, new Vector2(0f, 1f), new Vector2(0f, 10f));
            var title = UIKit.Title(detail, current.Name, 54, TextAnchor.UpperLeft, Color.white);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(500f, 70f), new Vector2(220f, -6f));

            // The title worn next to the name: cycles through the ones earned from achievements.
            var titles = new List<string>(Profile.achievements.Titles());
            string worn = titles.Contains(Profile.title) ? Profile.title : "";
            var titleButton = UIKit.Button(detail, titles.Count == 0 ? "タイトル：まだない" : $"タイトル：{(worn == "" ? "なし" : $"「{worn}」")}", 20, () =>
            {
                if (titles.Count == 0)
                {
                    _status.text = "タイトルはアチーブメントを達成すると手に入る（コーデックスの「アチーブメント」）";
                    return;
                }
                int next = titles.IndexOf(worn) + 1; // none -> first -> ... -> last -> none
                Profile.title = next >= titles.Count ? "" : titles[next];
                Save();
            });
            titleButton.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(250f, 44f), new Vector2(-6f, -14f));

            int selectedSkin = Profile.SkinFor(current.Id);
            foreach (var skin in SkinCatalog.For(current.Id))
            {
                bool owned = Profile.IsUnlocked(skin.Id);
                var frame = UIKit.Framed(detail, skin.Id, new Color(0.08f, 0.06f, 0.08f, 1f), skin.Index == selectedSkin ? UIKit.AccentColor : UIKit.GoldTrim * 0.5f);
                frame.Outer().Place(new Vector2(0f, 1f), new Vector2(76f, 76f), new Vector2(222f + skin.Index * 84f, -80f));
                var img = Preview(frame.transform, SkinCatalog.Sheet(current.Id, skin.Index), "idle", 0f, Vector2.zero, Vector2.zero);
                img.rectTransform.Stretch(-6f);
                if (!owned)
                {
                    img.color = new Color(0.35f, 0.35f, 0.4f, 0.9f);
                    UIKit.Label(frame.transform, $"{skin.Cost}", 20, TextAnchor.LowerCenter, new Color(0.6f, 0.85f, 1f)).rectTransform.Stretch(2f);
                }
                var s = skin;
                AddButton(frame.Outer(), () => ChooseSkin(s));
            }
            var skinName = UIKit.Label(detail, $"見た目：{SkinCatalog.Get(current.Id, selectedSkin).Name}", 20, TextAnchor.UpperLeft, UIKit.MutedText);
            skinName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 28f), new Vector2(224f, -162f));

            var body = UIKit.Label(detail,
                $"<color=#bfb6aa>{current.Role}</color>\n" +
                $"HP {current.Health:0}（+{current.HealthPerLevel:0}/Lv）　移動 {current.MoveSpeed:0.0}　防御 {current.Armor:0}\n\n" +
                $"<color=#e0b45a>◆{current.BasicName}</color>（左クリック）\n" +
                $"<color=#e0b45a>◆{current.Skill1Name}</color>（Q / 右クリック）\n<size=21><color=#d8d0c4>{current.Skill1Text}</color></size>\n" +
                $"<color=#e0b45a>◆{current.Skill2Name}</color>（E）\n<size=21><color=#d8d0c4>{current.Skill2Text}</color></size>\n" +
                $"<color=#ffd27a>◆アルティメット「{UltimateCatalog.Name(current.Id)}」</color>（R・テンションが満タンで）\n<size=21><color=#d8d0c4>{UltimateCatalog.Text(current.Id)}</color></size>",
                24, TextAnchor.UpperLeft);
            body.rectTransform.Place(new Vector2(0f, 1f), new Vector2(720f, 460f), new Vector2(10f, -220f));
        }

        private void ChooseSkin(SkinDef skin)
        {
            string message = null;
            if (!Profile.IsUnlocked(skin.Id))
            {
                if (!Profile.TryBuy(skin.Id, skin.Cost))
                {
                    _status.text = $"ソウルシャードが足りない（「{skin.Name}」は {skin.Cost}）";
                    return;
                }
                message = $"「{skin.Name}」を手に入れた";
            }
            Profile.SetSkin(skin.Class, skin.Index);
            Save(message);
        }

        // ------------------------------------------------------------------ Master: the class's sphere board

        /// <summary>The class's sphere board (SphereBoardView): spheres opened with spheres, seals with keys, arts worn one at a time.</summary>
        private void BuildMastery()
        {
            var def = ClassCatalog.Get(Profile.IsClassOpen(Profile.selectedClass) ? Profile.selectedClass : ClassId.Warrior);
            _title.text = $"マスター　<size=28><color=#bfb6aa>{def.Name}のスフィア盤</color></size>";
            _shards.text = $"<color=#9fd8ff>スフィア {Profile.spheres}</color>　<color=#e6c766>鍵 {Profile.keys}</color>";
            SphereBoardView.Build(_body, Profile, def, Save, message => _status.text = message);
        }

        // ------------------------------------------------------------------ Charm maker: trinkets

        private void BuildCharms()
        {
            int equipped = Profile.equippedTrinkets.Count(Profile.IsUnlocked);
            _title.text = $"チャームメイカー　<size=28><color=#bfb6aa>持っていけるのは {MetaCatalog.TrinketSlots} つ（いま {equipped}）</color></size>";
            const float cardW = 364f, cardH = 100f, gapX = 10f, gapY = 10f;
            int slot = 0;
            foreach (var t in MetaCatalog.Trinkets)
            {
                bool unlocked = Profile.IsUnlocked(t.Id);
                bool on = unlocked && Profile.equippedTrinkets.Contains(t.Id);
                bool affordable = unlocked || Profile.shards >= t.Cost;
                var item = UIKit.Framed(_body, t.Id, on ? new Color(0.2f, 0.13f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.06f, 1f), on ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                var outer = item.Outer();
                outer.Place(new Vector2(0f, 1f), new Vector2(cardW, cardH), new Vector2((slot % 3) * (cardW + gapX), -(slot / 3) * (cardH + gapY)));

                var icon = UIKit.Rect("Icon", item.transform).Place(new Vector2(0f, 1f), new Vector2(52f, 52f), new Vector2(8f, -8f)).gameObject.AddComponent<Image>();
                icon.sprite = GameIcons.Trinket(t.Id);
                icon.enabled = icon.sprite != null;
                icon.raycastTarget = false;
                if (!unlocked) icon.color = new Color(0.4f, 0.4f, 0.45f, 0.8f);

                var name = UIKit.Label(item.transform, $"<b>{t.Name}</b>", 22, TextAnchor.MiddleLeft, unlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(cardW - 70f, 30f), new Vector2(68f, -6f));
                string state = on ? "<color=#e6b85a>持っていく</color>" : unlocked ? "<color=#8f877d>置いていく</color>"
                    : affordable ? $"<color=#9fd8ff>作る {t.Cost}</color>" : $"<color=#6f7f90>作る {t.Cost}</color>";
                var tag = UIKit.Label(item.transform, state, 18, TextAnchor.MiddleLeft);
                tag.rectTransform.Place(new Vector2(0f, 1f), new Vector2(cardW - 70f, 24f), new Vector2(68f, -36f));
                var text = UIKit.Label(item.transform, $"<color=#bfb6aa>{t.Text}</color>", 17, TextAnchor.UpperLeft);
                text.rectTransform.Place(new Vector2(0f, 1f), new Vector2(cardW - 16f, 40f), new Vector2(10f, -62f));

                var id = t.Id;
                var cost = t.Cost;
                var button = AddButton(outer, () =>
                {
                    if (!Profile.IsUnlocked(id))
                    {
                        if (Profile.TryBuy(id, cost)) Save("チャームを作ってもらった。押すと持っていける");
                        else _status.text = "ソウルシャードが足りない";
                        return;
                    }
                    Profile.ToggleTrinket(id);
                    Save();
                });
                button.interactable = affordable;
                slot++;
            }
        }

        // ------------------------------------------------------------------ Tablet: difficulty

        private void BuildDifficulty()
        {
            _title.text = "モノリス　<size=28><color=#bfb6aa>難易度</color></size>";
            var nm = Unity.Netcode.NetworkManager.Singleton;
            bool host = nm != null && nm.IsServer;
            var current = host || CampDirector.Instance == null ? CampDirector.ChosenDifficulty() : CampDirector.Instance.DifficultyLevel.Value;
            int signed = host || CampDirector.Instance == null ? RunDirector.ChosenContract() : CampDirector.Instance.Contract.Value;
            var toContract = UIKit.Button(_body, $"コントラクト →（ランク {ContractCatalog.Rank(signed, DifficultyCatalog.Get(current))}）", 22, () =>
            {
                _contractPage = true;
                Rebuild();
            }, UIKit.BloodColor);
            toContract.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(240f, 48f), new Vector2(0f, 62f));
            for (int i = 0; i < DifficultyCatalog.All.Length; i++)
            {
                var def = DifficultyCatalog.All[i];
                bool open = Profile.IsDifficultyOpen(def.Id);
                bool chosen = def.Id == current;
                var card = UIKit.Framed(_body, def.Name, chosen ? new Color(0.25f, 0.06f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.07f, 1f),
                    chosen ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                card.Outer().Place(new Vector2(0f, 1f), new Vector2(272f, 220f), new Vector2(i * 284f, -6f));
                var name = UIKit.Title(card.transform, def.Name, 44, TextAnchor.MiddleCenter, open ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(260f, 64f), new Vector2(0f, -14f));
                var text = UIKit.Label(card.transform, open ? def.Text : "ベテランを踏破すると挑める", 20, TextAnchor.UpperCenter, open ? UIKit.MutedText : new Color(1f, 1f, 1f, 0.35f));
                text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(244f, 130f), new Vector2(0f, -84f));
                var d = def.Id;
                var button = AddButton(card.Outer(), () =>
                {
                    if (!Profile.IsDifficultyOpen(d))
                    {
                        _status.text = "「ナイトメア」はベテランを踏破すると挑める";
                        return;
                    }
                    Profile.difficulty = d;
                    Save();
                });
                button.interactable = host;
            }

            // What the chosen difficulty changes: the monsters' strengths and the team's handicaps, so the
            // party can pick gear and classes for it.
            var chosenDef = DifficultyCatalog.Get(current);
            var detail = UIKit.Framed(_body, "Detail", new Color(0.045f, 0.035f, 0.05f, 1f), UIKit.GoldTrim * 0.7f);
            detail.Outer().Place(new Vector2(0f, 1f), new Vector2(1124f, 246f), new Vector2(0f, -244f));
            var head = UIKit.Title(detail.transform, $"「{chosenDef.Name}」で変わること", 28, TextAnchor.UpperLeft);
            head.rectTransform.Place(new Vector2(0f, 1f), new Vector2(600f, 40f), new Vector2(24f, -14f));
            var scale = UIKit.Label(detail.transform,
                $"モンスターの体力 ×{chosenDef.EnemyHealth:0.##}　攻撃 ×{chosenDef.EnemyDamage:0.##}　数 ×{chosenDef.EnemyCount:0.##}　ソウルシャード ×{chosenDef.Shards:0.##}",
                20, TextAnchor.UpperRight, UIKit.MutedText);
            scale.rectTransform.Place(new Vector2(1f, 1f), new Vector2(560f, 34f), new Vector2(-24f, -20f));

            int monsterRow = 0, playerRow = 0;
            foreach (var mod in DifficultyCatalog.ModsOf(chosenDef))
            {
                bool monster = DifficultyCatalog.IsMonsterMod(mod);
                bool help = DifficultyCatalog.IsHelp(mod);
                string color = help ? "#8fe39a" : monster ? "#ff8a7a" : "#d6a8ff";
                int row = monster ? monsterRow++ : playerRow++;
                var line = UIKit.Label(detail.transform,
                    $"<color={color}>{DifficultyCatalog.ModName(mod)}</color>　<size=19><color=#cfc6bb>{DifficultyCatalog.ModText(mod)}</color></size>",
                    22, TextAnchor.UpperLeft);
                line.rectTransform.Place(new Vector2(0f, 1f), new Vector2(540f, 34f), new Vector2(monster ? 24f : 572f, -104f - row * 36f));
            }
            if (monsterRow + playerRow == 0)
            {
                var none = UIKit.Label(detail.transform, "特別な強化も枷もない、本来のラビリンス", 22, TextAnchor.UpperLeft, UIKit.MutedText);
                none.rectTransform.Place(new Vector2(0f, 1f), new Vector2(800f, 34f), new Vector2(24f, -104f));
            }
            else
            {
                var monsterHead = UIKit.Label(detail.transform, monsterRow > 0 ? "モンスターの強化" : "", 20, TextAnchor.UpperLeft, new Color(1f, 0.55f, 0.48f));
                monsterHead.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 30f), new Vector2(24f, -66f));
                var playerHead = UIKit.Label(detail.transform, playerRow > 0 ? (chosenDef.Has(DifficultyMod.Mercy) ? "助け" : "あなたたちへの枷") : "", 20, TextAnchor.UpperLeft,
                    chosenDef.Has(DifficultyMod.Mercy) ? new Color(0.56f, 0.89f, 0.6f) : new Color(0.84f, 0.66f, 1f));
                playerHead.rectTransform.Place(new Vector2(0f, 1f), new Vector2(400f, 30f), new Vector2(572f, -66f));
            }

            // This week's labyrinth: the same maps for everyone this week, on the standard difficulty, and a best to beat.
            bool weekly = host || CampDirector.Instance == null ? Profile.weekly : CampDirector.Instance.WeeklyRun.Value;
            var strip = UIKit.Framed(_body, "Weekly", weekly ? new Color(0.05f, 0.1f, 0.16f, 1f) : new Color(0.045f, 0.035f, 0.05f, 1f),
                weekly ? new Color(0.6f, 0.85f, 1f) : UIKit.GoldTrim * 0.7f);
            strip.Outer().Place(new Vector2(0f, 1f), new Vector2(1124f, 96f), new Vector2(0f, -502f));
            var weekTitle = UIKit.Title(strip.transform, $"今週のラビリンス　<size=22><color=#9fd8ff>{Weekly.Label(Weekly.Current)}</color></size>", 30, TextAnchor.MiddleLeft);
            weekTitle.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(520f, 50f), new Vector2(24f, 14f));
            int week = Weekly.Current;
            string best = Profile.weeklyWeek == week && Profile.weeklyBestRoom >= 0
                ? $"今週の記録：{(Profile.weeklyCleared ? $"踏破 {UIKit.FormatTime(Profile.weeklyBestTime)}" : $"{HudView.RoomTitle(Profile.weeklyBestRoom)}まで（{UIKit.FormatTime(Profile.weeklyBestTime)}）")}"
                : "今週の記録：まだない";
            var sub = UIKit.Label(strip.transform, $"今週は誰が挑んでも同じマップ（難易度はノーマル）　{best}", 20, TextAnchor.MiddleLeft, UIKit.MutedText);
            sub.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(800f, 34f), new Vector2(24f, -24f));
            var toggle = UIKit.Button(strip.transform, weekly ? "挑む（オン）" : "挑まない（オフ）", 24, () =>
            {
                Profile.weekly = !Profile.weekly;
                Save();
            }, weekly ? new Color(0.2f, 0.45f, 0.7f) : (Color?)null);
            toggle.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(240f, 60f), new Vector2(-24f, 0f));
            toggle.interactable = host;
            if (!host)
            {
                var hostNote = UIKit.Label(strip.transform, "部屋を作った人が選ぶ", 18, TextAnchor.MiddleRight, UIKit.MutedText);
                hostNote.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(240f, 30f), new Vector2(-24f, -40f));
            }

            var note = UIKit.Label(detail.transform, host
                    ? "仲間と遊ぶときも、あなた（部屋を作った人）の選んだ難易度になります"
                    : $"難易度は部屋を作った人が選びます（いまは「{DifficultyCatalog.Get(current).Name}」）",
                20, TextAnchor.MiddleCenter, UIKit.MutedText);
            note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1080f, 30f), new Vector2(0f, 10f));
        }

        // ------------------------------------------------------------------ Tablet: contracts

        private bool _contractPage;

        /// <summary>
        /// Contracts (ContractCatalog): clauses signed on top of the difficulty. Each level is worth points; their sum is
        /// the rank, which makes the labyrinth harder and the soul shards richer. The host signs for everyone.
        /// </summary>
        private void BuildContract()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            bool host = nm == null || nm.IsServer;
            int contract = host || CampDirector.Instance == null ? RunDirector.ChosenContract() : CampDirector.Instance.Contract.Value;
            var difficulty = DifficultyCatalog.Get(host || CampDirector.Instance == null ? CampDirector.ChosenDifficulty() : CampDirector.Instance.DifficultyLevel.Value);
            int rank = ContractCatalog.Rank(contract, difficulty);
            _title.text = $"モノリス　<size=28><color=#bfb6aa>コントラクト　ランク {rank}</color></size>";

            var back = UIKit.Button(_body, "← 難易度へ", 22, () =>
            {
                _contractPage = false;
                Rebuild();
            });
            back.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(200f, 48f), new Vector2(0f, 4f));
            string best = Profile.bestContract >= 0 ? $"最高記録：ランク {Profile.bestContract} で踏破" : "最高記録：まだない";
            var head = UIKit.Label(_body,
                $"ランク {rank} / {ContractCatalog.MaxRank}　ソウルシャード ×{1f + ContractCatalog.ShardsPerRank * rank:0.00}　（難易度「{difficulty.Name}」の上に重ねる）　{best}",
                21, TextAnchor.MiddleLeft, UIKit.MutedText);
            head.rectTransform.Place(new Vector2(0f, 1f), new Vector2(900f, 40f), new Vector2(0f, 0f));

            for (int i = 0; i < ContractCatalog.All.Length; i++)
            {
                var clause = ContractCatalog.All[i];
                int level = ContractCatalog.Level(contract, i);
                bool given = clause.Mod != DifficultyMod.None && difficulty.Has(clause.Mod);
                var row = UIKit.Framed(_body, clause.Name, level > 0 ? new Color(0.2f, 0.06f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.07f, 1f),
                    level > 0 ? UIKit.AccentColor : UIKit.GoldTrim * 0.5f);
                row.Outer().Place(new Vector2(0f, 1f), new Vector2(364f, 106f), new Vector2(i % 3 * 380f, -50f - i / 3 * 114f));
                string pips = new string('■', level) + new string('□', clause.MaxLevel - level);
                var name = UIKit.Title(row.transform, $"{clause.Name}　<size=20><color=#e0b45a>{pips}</color></size>", 26, TextAnchor.MiddleLeft);
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(240f, 34f), new Vector2(12f, -6f));
                var points = UIKit.Label(row.transform, given ? "0点" : $"1段 {clause.PointsPerLevel}点", 17, TextAnchor.MiddleRight, new Color(0.88f, 0.7f, 0.35f));
                points.rectTransform.Place(new Vector2(1f, 1f), new Vector2(100f, 30f), new Vector2(-12f, -8f));
                var text = UIKit.Label(row.transform,
                    given ? $"{clause.Text}（難易度で既に有効）" : clause.Text,
                    17, TextAnchor.UpperLeft, UIKit.MutedText);
                text.rectTransform.Place(new Vector2(0f, 0f), new Vector2(222f, 44f), new Vector2(12f, 6f));
                int index = i;
                var minus = UIKit.Button(row.transform, "−", 28, () => SignClause(index, -1));
                minus.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(52f, 44f), new Vector2(-70f, 8f));
                minus.interactable = host && level > 0;
                var plus = UIKit.Button(row.transform, "＋", 28, () => SignClause(index, +1));
                plus.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(52f, 44f), new Vector2(-12f, 8f));
                plus.interactable = host && level < clause.MaxLevel;
            }

            var note = UIKit.Label(_body, host
                    ? "ランクが高いほどソウルシャードが増える。ランク5・10・20で踏破するとタイトル（アチーブメント）。今週のラビリンスではコントラクトは無効"
                    : "コントラクトは部屋を作った人が結ぶ",
                20, TextAnchor.MiddleCenter, UIKit.MutedText);
            note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1080f, 30f), new Vector2(0f, -4f));
        }

        private void SignClause(int clause, int delta)
        {
            int level = ContractCatalog.Level(Profile.contract, clause) + delta;
            Profile.contract = ContractCatalog.WithLevel(Profile.contract, clause, level);
            Save();
        }

        // ------------------------------------------------------------------ Helpers

        private static QuestDef UnlockingQuest(ClassId id)
        {
            string unlock = ClassCatalog.UnlockId(id);
            foreach (var chain in QuestCatalog.Chains)
                foreach (var q in chain)
                    if (q.Unlock == unlock) return q;
            return null;
        }

        private static Image Preview(Transform parent, string sheet, string clip, float size, Vector2 anchor, Vector2 offset)
        {
            var rect = UIKit.Rect("Preview", parent).Place(anchor, new Vector2(size, size), offset);
            var img = rect.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.gameObject.AddComponent<UISpriteAnimator>().Play(sheet, clip);
            return img;
        }

        private static Button AddButton(RectTransform outer, UnityEngine.Events.UnityAction onClick)
        {
            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = outer.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.6f, 1.5f, 1.2f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return button;
        }
    }
}
