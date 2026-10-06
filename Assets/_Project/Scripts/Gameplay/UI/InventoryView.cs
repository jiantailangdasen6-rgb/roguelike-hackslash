using System.Linq;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>Bag screen: equipped items per slot and every owned item with an equip button. Works in the lobby and in-run.</summary>
    public sealed class InventoryView : MonoBehaviour
    {
        public static InventoryView Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._panel.activeSelf;

        private GameObject _panel;
        private Text _equippedText;
        private RectTransform _listContent;
        private PlayerInputReader _boundInput;
        private bool _dirty = true;

        private void Awake()
        {
            Instance = this;
            var root = UIKit.CreateCanvas("Inventory", 50, transform).transform;
            var panel = UIKit.Panel(root, "Panel", UIKit.PanelColor);
            panel.rectTransform.Stretch(60f);
            _panel = panel.gameObject;

            var title = UIKit.Label(panel.transform, "バッグ（装備は全ラン共通・この端末に保存）", 40, TextAnchor.UpperLeft, UIKit.AccentColor);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(1300f, 60f), new Vector2(40f, -30f));

            var close = UIKit.Button(panel.transform, "閉じる", 30, Toggle);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(180f, 70f), new Vector2(-30f, -24f));

            var equippedHeader = UIKit.Label(panel.transform, "装備中", 32, TextAnchor.UpperLeft);
            equippedHeader.rectTransform.Place(new Vector2(0f, 1f), new Vector2(560f, 50f), new Vector2(40f, -110f));
            _equippedText = UIKit.Label(panel.transform, "", 24, TextAnchor.UpperLeft);
            _equippedText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(560f, 760f), new Vector2(40f, -160f));

            // Scrollable list of all items.
            var scrollRect = UIKit.Panel(panel.transform, "List", new Color(0f, 0f, 0f, 0.3f));
            var listRect = scrollRect.rectTransform;
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(640f, 30f);
            listRect.offsetMax = new Vector2(-30f, -110f);
            scrollRect.gameObject.AddComponent<RectMask2D>();
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;

            _listContent = UIKit.Rect("Content", scrollRect.transform);
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.offsetMin = Vector2.zero;
            _listContent.offsetMax = Vector2.zero;
            var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _listContent;
            scroll.viewport = listRect;

            _panel.SetActive(CommandLine.Has("-openbag")); // -openbag: for screenshots
            StashStore.Current.Changed += MarkDirty;
        }

        private void OnDestroy()
        {
            StashStore.Current.Changed -= MarkDirty;
            if (_boundInput != null) _boundInput.InventoryPressed -= Toggle;
            if (Instance == this) Instance = null;
        }

        private void MarkDirty() => _dirty = true;

        public void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
            if (_panel.activeSelf) _dirty = true;
        }

        private void Update()
        {
            // Bind the in-run Inventory key once the local player exists.
            var input = NetPlayer.Local != null ? NetPlayer.Local.Input : null;
            if (input != _boundInput)
            {
                if (_boundInput != null) _boundInput.InventoryPressed -= Toggle;
                _boundInput = input;
                if (_boundInput != null) _boundInput.InventoryPressed += Toggle;
            }

            if (_panel.activeSelf && _dirty) Rebuild();
        }

        private void Rebuild()
        {
            _dirty = false;
            var stash = StashStore.Current;

            var sb = new System.Text.StringBuilder();
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                var item = stash.GetEquipped(slot);
                sb.Append($"<b>{ItemText.SlotName(slot)}</b>：");
                if (item == null)
                {
                    sb.Append("<color=#888888>なし</color>\n\n");
                    continue;
                }
                sb.Append(ItemText.ColoredTitle(item)).Append('\n');
                foreach (var line in ItemText.PropertyLines(item)) sb.Append("    ").Append(line).Append('\n');
                sb.Append('\n');
            }
            _equippedText.text = sb.ToString();

            for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);

            var items = stash.Items.Reverse().ToList();
            if (items.Count == 0)
            {
                AddRow("まだ装備を持っていません。迷宮で敵を倒して手に入れよう！", null);
                return;
            }
            foreach (var item in items) AddRow(null, item);
        }

        private void AddRow(string message, ItemInstance item)
        {
            var row = UIKit.Panel(_listContent, "Row", new Color(1f, 1f, 1f, 0.06f));
            var rowLayout = row.gameObject.AddComponent<LayoutElement>();

            string text = message ?? $"{ItemText.ColoredTitle(item)}  <color=#AAAAAA>{ItemText.SlotName(item.Slot)}</color>\n" +
                          string.Join("   ", ItemText.PropertyLines(item));
            var label = UIKit.Label(row.transform, text, 24, TextAnchor.MiddleLeft);
            label.rectTransform.Stretch(14f);
            label.rectTransform.offsetMax = new Vector2(-220f, -10f);
            rowLayout.preferredHeight = 96f;

            if (item == null) return;
            bool equipped = StashStore.Current.IsEquipped(item.uid);
            var button = UIKit.Button(row.transform, equipped ? "装備中" : "装備する", 26, () =>
            {
                StashStore.Current.Equip(item.uid);
                NetPlayer.Local?.SubmitEquipment();
            }, equipped ? new Color(0.25f, 0.45f, 0.3f) : UIKit.ButtonColor);
            button.interactable = !equipped;
            button.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(190f, 64f), new Vector2(-14f, 0f));
        }
    }
}
