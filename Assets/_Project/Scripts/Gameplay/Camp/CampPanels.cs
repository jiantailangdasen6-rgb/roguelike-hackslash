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
            _dim.SetActive(false);
        }

        public void Show(CampStation station)
        {
            Open = station;
            _status.text = "";
            _dim.SetActive(true);
            Rebuild();
            EventSystem.current?.SetSelectedGameObject(_close.gameObject);
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
            _shards.text = $"魂の欠片 {Profile.shards}";
            switch (Open.Value)
            {
                case CampStation.Mirror: BuildMirror(); break;
                case CampStation.Master: BuildMastery(); break;
                case CampStation.Charms: BuildCharms(); break;
                case CampStation.Tablet: BuildDifficulty(); break;
                case CampStation.Vault: BuildVault(); break;
                case CampStation.Board: BuildQuests(); break;
            }
        }

        // ------------------------------------------------------------------ Notice board: requests

        /// <summary>One request per person in the camp: what, how far, what for, and a button to turn it in.</summary>
        private void BuildQuests()
        {
            _title.text = "依頼の掲示板　<size=28><color=#bfb6aa>仲間と一緒でも進む・達成したらここで報酬</color></size>";
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

                string reward = $"<color=#9fd8ff>魂の欠片 {q.Shards}</color>" + (q.Unlock != null && !Profile.IsUnlocked(q.Unlock) ? $"\n<color=#e0b45a>{q.UnlockName}</color>" : "");
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
            Save($"「{q.Name}」の報酬：魂の欠片 {q.Shards}{extra}");
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

        /// <summary>Stand-in until the gear vault screen (VaultView) is merged in.</summary>
        private void BuildVault()
        {
            _title.text = "倉庫　<size=28><color=#bfb6aa>持ち帰った装備</color></size>";
            var note = UIKit.Label(_body, "装備の倉庫は準備中です", 30, TextAnchor.MiddleCenter, UIKit.MutedText);
            note.rectTransform.Stretch();
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
            _title.text = "古い鏡　<size=28><color=#bfb6aa>クラスと見た目</color></size>";
            var current = ClassCatalog.Get(Profile.selectedClass);

            for (int i = 0; i < ClassCatalog.All.Length; i++)
            {
                var def = ClassCatalog.All[i];
                bool selected = def.Id == current.Id;
                var card = UIKit.Framed(_body, def.Name, selected ? new Color(0.17f, 0.11f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.07f, 1f),
                    selected ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                card.Outer().Place(new Vector2(0f, 1f), new Vector2(360f, 96f), new Vector2(0f, -i * 104f));
                var preview = Preview(card.transform, SkinCatalog.Sheet(def.Id, Profile.SkinFor(def.Id)), "idle", 96f, new Vector2(0f, 0.5f), new Vector2(0f, 4f));
                preview.raycastTarget = false;
                var name = UIKit.Title(card.transform, def.Name, 32, TextAnchor.UpperLeft, Color.white);
                name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(250f, 44f), new Vector2(100f, -8f));
                var role = UIKit.Label(card.transform, def.Role, 20, TextAnchor.UpperLeft, UIKit.MutedText);
                role.rectTransform.Place(new Vector2(0f, 1f), new Vector2(250f, 40f), new Vector2(100f, -52f));
                var id = def.Id;
                AddButton(card.Outer(), () =>
                {
                    Profile.selectedClass = id;
                    Save();
                });
            }

            // Detail of the chosen class.
            var detail = UIKit.Rect("Detail", _body).Place(new Vector2(1f, 1f), new Vector2(740f, 620f), Vector2.zero);
            Preview(detail, SkinCatalog.Sheet(current.Id, Profile.SkinFor(current.Id)), "attack", 200f, new Vector2(0f, 1f), new Vector2(0f, 10f));
            var title = UIKit.Title(detail, current.Name, 54, TextAnchor.UpperLeft, Color.white);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(500f, 70f), new Vector2(220f, -6f));

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
                $"<color=#e0b45a>◆{current.Skill2Name}</color>（E）\n<size=21><color=#d8d0c4>{current.Skill2Text}</color></size>",
                24, TextAnchor.UpperLeft);
            body.rectTransform.Place(new Vector2(0f, 1f), new Vector2(720f, 380f), new Vector2(10f, -220f));
        }

        private void ChooseSkin(SkinDef skin)
        {
            string message = null;
            if (!Profile.IsUnlocked(skin.Id))
            {
                if (!Profile.TryBuy(skin.Id, skin.Cost))
                {
                    _status.text = $"魂の欠片が足りない（「{skin.Name}」は {skin.Cost}）";
                    return;
                }
                message = $"「{skin.Name}」を手に入れた";
            }
            Profile.SetSkin(skin.Class, skin.Index);
            Save(message);
        }

        // ------------------------------------------------------------------ Master: mastery

        private void BuildMastery()
        {
            var def = ClassCatalog.Get(Profile.selectedClass);
            _title.text = $"師範　<size=28><color=#bfb6aa>{def.Name}の熟練（解放した効果は毎回有効）</color></size>";
            int row = 0;
            foreach (var node in MetaCatalog.NodesFor(def.Id))
            {
                bool owned = Profile.IsUnlocked(node.Id);
                var item = UIKit.Framed(_body, node.Id, owned ? new Color(0.14f, 0.09f, 0.05f, 1f) : new Color(0.06f, 0.05f, 0.06f, 1f),
                    owned ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                item.Outer().Place(new Vector2(0.5f, 1f), new Vector2(1100f, 90f), new Vector2(0f, -row * 100f));
                string kind = node.Kind == MasteryKind.UnlockPerk ? "新しい技" : node.Kind == MasteryKind.StartPerk ? "開始時の加護" : "成長";
                var label = UIKit.Label(item.transform, $"<b>{node.Name}</b>  <size=20><color=#9fd8ff>{kind}</color></size>\n<size=22><color=#bfb6aa>{node.Text}</color></size>", 28, TextAnchor.MiddleLeft);
                label.rectTransform.Stretch(14f);
                label.rectTransform.offsetMax = new Vector2(-230f, -4f);
                var n = node;
                var button = UIKit.Button(item.transform, owned ? "習得済み" : $"習得 {node.Cost}", 24, () =>
                {
                    if (Profile.TryBuy(n.Id, n.Cost)) Save($"「{n.Name}」を習得した");
                    else _status.text = "魂の欠片が足りない";
                }, owned ? new Color(0.35f, 0.25f, 0.1f) : new Color(0.1f, 0.15f, 0.25f));
                button.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(200f, 60f), new Vector2(-12f, 0f));
                button.interactable = !owned && Profile.shards >= node.Cost;
                row++;
            }
        }

        // ------------------------------------------------------------------ Charm maker: trinkets

        private void BuildCharms()
        {
            int equipped = Profile.equippedTrinkets.Count(Profile.IsUnlocked);
            _title.text = $"お守り職人　<size=28><color=#bfb6aa>持っていけるのは {MetaCatalog.TrinketSlots} つ（いま {equipped}）</color></size>";
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
                        if (Profile.TryBuy(id, cost)) Save("お守りを作ってもらった。押すと持っていける");
                        else _status.text = "魂の欠片が足りない";
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
            _title.text = "迷宮の碑　<size=28><color=#bfb6aa>難易度</color></size>";
            var nm = Unity.Netcode.NetworkManager.Singleton;
            bool host = nm != null && nm.IsServer;
            var current = CampDirector.Instance != null ? CampDirector.Instance.DifficultyLevel.Value : Profile.difficulty;
            for (int i = 0; i < DifficultyCatalog.All.Length; i++)
            {
                var def = DifficultyCatalog.All[i];
                bool open = Profile.IsDifficultyOpen(def.Id);
                bool chosen = def.Id == current;
                var card = UIKit.Framed(_body, def.Name, chosen ? new Color(0.25f, 0.06f, 0.06f, 1f) : new Color(0.06f, 0.05f, 0.07f, 1f),
                    chosen ? UIKit.AccentColor : UIKit.GoldTrim * 0.6f);
                card.Outer().Place(new Vector2(0f, 1f), new Vector2(272f, 300f), new Vector2(i * 284f, -20f));
                var name = UIKit.Title(card.transform, def.Name, 44, TextAnchor.MiddleCenter, open ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(260f, 70f), new Vector2(0f, -20f));
                var text = UIKit.Label(card.transform, open ? def.Text : "熟練を踏破すると挑める", 21, TextAnchor.UpperCenter, open ? UIKit.MutedText : new Color(1f, 1f, 1f, 0.35f));
                text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(240f, 180f), new Vector2(0f, -100f));
                var d = def.Id;
                var button = AddButton(card.Outer(), () =>
                {
                    if (!Profile.IsDifficultyOpen(d))
                    {
                        _status.text = "「冥府」は熟練を踏破すると挑める";
                        return;
                    }
                    Profile.difficulty = d;
                    Save();
                });
                button.interactable = host;
            }
            var note = UIKit.Label(_body, host
                    ? "仲間と遊ぶときも、あなた（部屋を作った人）の選んだ難易度になります"
                    : $"難易度は部屋を作った人が選びます（いまは「{DifficultyCatalog.Get(current).Name}」）",
                24, TextAnchor.MiddleCenter, UIKit.MutedText);
            note.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(1100f, 50f), new Vector2(0f, -360f));
        }

        // ------------------------------------------------------------------ Helpers

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
