using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The bag (I / Tab): four equipped slots, twelve bag cells, the selected item compared with what is worn, and
    /// equip / take off / give / salvage. Gear changes are for between fights; mid-fight the bag can only be looked at.
    /// Contents come from the host as GearSlot events for this player. Also shows a small "袋" badge on the HUD.
    /// </summary>
    public sealed class BagView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        /// <summary>Frame the bag was closed with Esc (the pause menu ignores that press).</summary>
        public static int ClosedFrame { get; private set; } = -1;

        private const float Cell = 112f;

        private readonly ItemInstance[] _equipped = new ItemInstance[SimPlayer.SlotCount];
        private readonly ItemInstance[] _bag = new ItemInstance[SimPlayer.BagSize];
        private readonly List<(Image Frame, Image Icon, Text Label)> _cells = new();
        private readonly List<Button> _giveButtons = new();

        private GameObject _panel;
        private Text _modeText;
        private Text _detailTitle;
        private Text _detailBody;
        private Text _compareBody;
        private Button _equipButton;
        private Text _equipLabel;
        private Button _salvageButton;
        private Text _salvageLabel;
        private Text _badge;
        private int _selected = -1; // 0..3 equipped, 4.. bag
        private int _unseen;

        private void Awake()
        {
            var root = UIKit.CreateCanvas("Bag", 38, transform).transform;
            BuildBadge(root);

            var dim = UIKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.65f));
            dim.rectTransform.Stretch();
            _panel = dim.gameObject;

            var frame = UIKit.Framed(dim.transform, "BagFrame", new Color(0.05f, 0.04f, 0.055f, 0.97f));
            frame.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1560f, 780f), Vector2.zero);
            var title = UIKit.Title(frame.transform, "袋", 52, TextAnchor.UpperLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 70f), new Vector2(36f, -20f));
            _modeText = UIKit.Label(frame.transform, "", 24, TextAnchor.UpperRight, UIKit.MutedText);
            _modeText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1000f, 40f), new Vector2(-36f, -34f));

            // Equipped: 2 x 2 on the left.
            var equipLabel = UIKit.Label(frame.transform, "装備中", 26, TextAnchor.UpperLeft, UIKit.AccentColor);
            equipLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 36f), new Vector2(40f, -100f));
            for (int s = 0; s < SimPlayer.SlotCount; s++)
                AddCell(frame.transform, s, new Vector2(40f + (s % 2) * (Cell + 26f), -146f - (s / 2) * (Cell + 46f)), ItemText.SlotName((EquipSlot)s));

            // Bag: 4 x 3 in the middle.
            var bagLabel = UIKit.Label(frame.transform, $"袋（{SimPlayer.BagSize}）", 26, TextAnchor.UpperLeft, UIKit.AccentColor);
            bagLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 36f), new Vector2(360f, -100f));
            for (int b = 0; b < SimPlayer.BagSize; b++)
                AddCell(frame.transform, SimPlayer.SlotCount + b, new Vector2(360f + (b % 4) * (Cell + 14f), -146f - (b / 4) * (Cell + 14f)), null);

            // Detail on the right.
            var detail = UIKit.Framed(frame.transform, "Detail", new Color(0.07f, 0.055f, 0.07f, 1f), UIKit.GoldTrim * 0.7f);
            detail.Outer().Place(new Vector2(1f, 1f), new Vector2(620f, 640f), new Vector2(-30f, -100f));
            _detailTitle = UIKit.Title(detail.transform, "", 34, TextAnchor.UpperLeft, Color.white);
            _detailTitle.rectTransform.Place(new Vector2(0f, 1f), new Vector2(580f, 50f), new Vector2(20f, -16f));
            _detailBody = UIKit.Label(detail.transform, "", 24, TextAnchor.UpperLeft);
            _detailBody.rectTransform.Place(new Vector2(0f, 1f), new Vector2(580f, 220f), new Vector2(20f, -74f));
            _compareBody = UIKit.Label(detail.transform, "", 21, TextAnchor.UpperLeft, UIKit.MutedText);
            _compareBody.rectTransform.Place(new Vector2(0f, 1f), new Vector2(580f, 170f), new Vector2(20f, -300f));

            _equipButton = UIKit.Button(detail.transform, "装備する", 28, EquipOrRemove, UIKit.BloodColor);
            _equipButton.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(280f, 64f), new Vector2(20f, 86f));
            _equipLabel = _equipButton.GetComponentInChildren<Text>();
            _salvageButton = UIKit.Button(detail.transform, "分解", 24, Salvage);
            _salvageButton.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(280f, 64f), new Vector2(-20f, 86f));
            _salvageLabel = _salvageButton.GetComponentInChildren<Text>();
            for (int i = 0; i < GameSession.MaxPlayers - 1; i++)
            {
                int index = i;
                var give = UIKit.Button(detail.transform, "渡す", 22, () => Give(index));
                give.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(280f, 54f), new Vector2(20f + i * 300f, 18f));
                _giveButtons.Add(give);
            }

            var hint = UIKit.Label(frame.transform, "クリックで選ぶ　I / Tab / Esc で閉じる", 22, TextAnchor.LowerLeft, UIKit.MutedText);
            hint.rectTransform.Place(new Vector2(0f, 0f), new Vector2(900f, 36f), new Vector2(40f, 22f));

            _panel.SetActive(false);
            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy()
        {
            RunDirector.EventReceived -= OnEvent;
            IsOpen = false;
        }

        private void BuildBadge(Transform root)
        {
            var badge = UIKit.Framed(root, "BagBadge", new Color(0.05f, 0.04f, 0.05f, 0.9f));
            badge.Outer().Place(new Vector2(1f, 0f), new Vector2(210f, 50f), new Vector2(-24f, 128f));
            _badge = UIKit.Label(badge.transform, "", 22, TextAnchor.MiddleCenter);
            _badge.rectTransform.Stretch(4f);
        }

        private void AddCell(Transform parent, int index, Vector2 at, string caption)
        {
            var cell = UIKit.Framed(parent, "Cell" + index, new Color(0.09f, 0.07f, 0.09f, 1f), new Color(0.3f, 0.26f, 0.22f));
            var outer = cell.Outer();
            outer.Place(new Vector2(0f, 1f), new Vector2(Cell, Cell), at);
            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = outer.GetComponent<Image>();
            button.onClick.AddListener(() => Select(index));
            var icon = UIKit.Rect("Icon", cell.transform).Stretch(10f).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Text label = null;
            if (caption != null)
            {
                label = UIKit.Label(outer, caption, 20, TextAnchor.UpperCenter, UIKit.MutedText);
                label.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(Cell + 20f, 30f), new Vector2(0f, -32f));
            }
            _cells.Add((outer.GetComponent<Image>(), icon, label));
        }

        // ------------------------------------------------------------------ Data from the host

        private static bool IsMe(ulong clientId) => NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == clientId;

        private void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.GearSlot when IsMe(e.ClientId):
                {
                    var item = ItemCodec.Unpack(e.Seed);
                    if (e.Int < SimPlayer.SlotCount) _equipped[e.Int] = item;
                    else if (e.Int - SimPlayer.SlotCount < _bag.Length) _bag[e.Int - SimPlayer.SlotCount] = item;
                    if (IsOpen) Refresh();
                    break;
                }
                case SimEventType.LootPicked when IsMe(e.ClientId) && e.Int >= 0:
                    _unseen++;
                    HudView.Instance?.Toast($"<color={ItemText.RarityColor((Rarity)e.Int)}>{ItemText.RarityName((Rarity)e.Int)}の装備</color>を拾った（I で袋を開く）");
                    break;
                case SimEventType.LootPicked when IsMe(e.ClientId):
                    HudView.Instance?.Toast("<color=#ff9a80>袋がいっぱいで、残った装備を拾えなかった</color>");
                    break;
                case SimEventType.ItemGiven when IsMe((ulong)e.Id):
                    _unseen++;
                    HudView.Instance?.Toast($"<color=#9fd8ff>{NameOf(e.ClientId)}から装備を受け取った</color>");
                    break;
                case SimEventType.ItemSalvaged when IsMe(e.ClientId):
                    HudView.Instance?.Toast($"分解した：魂の欠片 +{e.Value:0}");
                    break;
            }
        }

        private static string NameOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return p.DisplayName;
            return "仲間";
        }

        // ------------------------------------------------------------------ Open / close

        private static bool BetweenFights
        {
            get
            {
                var d = RunDirector.Instance;
                return d != null && (d.RoomCleared.Value || d.Paused.Value);
            }
        }

        private void Update()
        {
            var director = RunDirector.Instance;
            bool running = director != null && director.IsSpawned && director.Started.Value && NetPlayer.Local != null;
            int count = _bag.Count(i => i != null);
            _badge.transform.parent.parent.gameObject.SetActive(running && !IsOpen); // the framed outer panel
            _badge.text = _unseen > 0 && Mathf.Repeat(Time.time, 1f) < 0.6f
                ? $"<color=#e0b45a>新しい装備！</color> [I]"
                : $"袋 {count}/{SimPlayer.BagSize}  <color=#bfb6aa>[I]</color>";

            if (!running) return;
            if (IsOpen && PerkView.IsShowing) Toggle(false); // a pick takes over the screen
            AutomationTick();

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.iKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) Toggle(!IsOpen);
            else if (IsOpen && kb.escapeKey.wasPressedThisFrame)
            {
                Toggle(false);
                ClosedFrame = Time.frameCount;
            }
            if (IsOpen) RefreshButtons();
        }

        private float _nextBotAction;
        private float _autoCloseAt;
        private int _autoOpenedRoom = -1;

        /// <summary>
        /// Automated runs: bots (-botmove) put on whatever they pick up between fights, and -openbag opens the bag
        /// between fights so screenshots show it.
        /// </summary>
        private void AutomationTick()
        {
            if (IsOpen && _autoCloseAt > 0f && Time.time > _autoCloseAt)
            {
                _autoCloseAt = 0f;
                Toggle(false);
            }
            if (!BetweenFights || Time.time < _nextBotAction || PerkView.IsShowing) return;
            _nextBotAction = Time.time + 1.2f;
            int room = RunDirector.Instance.RoomIndex.Value;
            if (CommandLine.Has("-openbag") && !IsOpen && _autoOpenedRoom != room)
            {
                _autoOpenedRoom = room;
                _autoCloseAt = Time.time + 4f;
                Toggle(true);
            }
            if (!CommandLine.Has("-botmove")) return;
            // Bots take an empty slot or a rarer piece (never swapping back and forth).
            for (int b = 0; b < _bag.Length; b++)
            {
                var item = _bag[b];
                if (item == null) continue;
                var worn = _equipped[(int)item.Slot];
                if (worn != null && worn.rarity >= item.rarity) continue;
                RunDirector.Instance?.EquipRpc(b);
                break;
            }
        }

        private void Toggle(bool open)
        {
            if (open && (PerkView.IsShowing || PauseMenuView.IsOpen)) return;
            IsOpen = open;
            _panel.SetActive(open);
            if (!open) return;
            _unseen = 0;
            if (_selected < 0) _selected = FirstFilled();
            Refresh();
        }

        private int FirstFilled()
        {
            for (int b = 0; b < _bag.Length; b++) if (_bag[b] != null) return SimPlayer.SlotCount + b;
            for (int s = 0; s < _equipped.Length; s++) if (_equipped[s] != null) return s;
            return -1;
        }

        private ItemInstance At(int index) => index < 0 ? null : index < SimPlayer.SlotCount ? _equipped[index] : _bag[index - SimPlayer.SlotCount];

        private void Select(int index)
        {
            _selected = index;
            Refresh();
        }

        // ------------------------------------------------------------------ Drawing

        private void Refresh()
        {
            if (At(_selected) == null) _selected = FirstFilled();
            for (int i = 0; i < _cells.Count; i++)
            {
                var item = At(i);
                var (frame, icon, _) = _cells[i];
                icon.sprite = GameIcons.Item(item);
                icon.enabled = icon.sprite != null;
                var rim = item != null ? GameIcons.RarityColor(item.rarity) : new Color(0.3f, 0.26f, 0.22f);
                frame.color = i == _selected ? Color.Lerp(rim, Color.white, 0.5f) : rim * (item != null ? 0.85f : 1f);
            }

            var selected = At(_selected);
            if (selected == null)
            {
                _detailTitle.text = "";
                _detailBody.text = "装備はまだない。\n魔物を倒すとたまに落とす（精鋭・主は必ず）。\n「宝物」の門の先では、全員が良い装備を得られる。";
                _compareBody.text = "";
            }
            else
            {
                _detailTitle.text = ItemText.ColoredTitle(selected);
                _detailBody.text = $"<color=#bfb6aa>{ItemText.SlotName(selected.Slot)}・Lv{selected.itemLevel}</color>\n" +
                                   string.Join("\n", ItemText.PropertyLines(selected));
                var worn = _equipped[(int)selected.Slot];
                _compareBody.text = _selected >= SimPlayer.SlotCount && worn != null
                    ? $"いま装備中：{ItemText.ColoredTitle(worn)}\n" + string.Join("\n", ItemText.PropertyLines(worn))
                    : _selected >= SimPlayer.SlotCount ? "この枠はいま空いている" : "";
            }
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            var selected = At(_selected);
            bool between = BetweenFights;
            bool inBag = _selected >= SimPlayer.SlotCount;
            _modeText.text = between ? "準備中：装備を付け替えられる" : "<color=#ff9a80>戦闘中は見るだけ</color>（部屋を制圧すると付け替えられる）";

            _equipButton.gameObject.SetActive(selected != null);
            _equipLabel.text = inBag ? "装備する" : "外す";
            _equipButton.interactable = between;

            _salvageButton.gameObject.SetActive(selected != null && inBag);
            if (selected != null) _salvageLabel.text = $"分解（魂の欠片 +{RunSimulation.SalvageValue(selected.rarity)}）";
            _salvageButton.interactable = between;

            var mates = NetPlayer.All.Where(p => p != NetPlayer.Local).OrderBy(p => p.Slot.Value).ToList();
            for (int i = 0; i < _giveButtons.Count; i++)
            {
                bool show = selected != null && inBag && i < mates.Count;
                _giveButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                _giveButtons[i].GetComponentInChildren<Text>().text = $"{mates[i].DisplayName}（{mates[i].ClassDef.Name}）に渡す";
                _giveButtons[i].interactable = between;
            }
        }

        // ------------------------------------------------------------------ Actions (the host checks them again)

        private void EquipOrRemove()
        {
            var director = RunDirector.Instance;
            if (director == null || At(_selected) == null) return;
            if (_selected >= SimPlayer.SlotCount) director.EquipRpc(_selected - SimPlayer.SlotCount);
            else director.UnequipRpc(_selected);
        }

        private void Salvage()
        {
            if (_selected >= SimPlayer.SlotCount && At(_selected) != null)
                RunDirector.Instance?.SalvageRpc(_selected - SimPlayer.SlotCount);
        }

        private void Give(int mateIndex)
        {
            var mates = NetPlayer.All.Where(p => p != NetPlayer.Local).OrderBy(p => p.Slot.Value).ToList();
            if (mateIndex < mates.Count && _selected >= SimPlayer.SlotCount && At(_selected) != null)
                RunDirector.Instance?.GiveRpc(_selected - SimPlayer.SlotCount, mates[mateIndex].OwnerClientId);
        }
    }
}
