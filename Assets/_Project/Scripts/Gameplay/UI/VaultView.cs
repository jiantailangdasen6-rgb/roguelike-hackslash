using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// The camp stash (倉庫): what you wear when setting out (four slots) and everything brought home. Equip, take off,
    /// salvage (one, or every common piece), sort, and buy more room with soul shards. Saved in this device's profile;
    /// the worn set travels to the host in the loadout. Open with <see cref="Open"/> (camp chest / lobby button).
    /// </summary>
    public sealed class VaultView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        /// <summary>Frame the vault was closed with Esc (other Esc handlers ignore that press).</summary>
        public static int ClosedFrame { get; private set; } = -1;

        private const float WornCell = 112f;
        private const float StashCell = 66f;
        private const int Columns = 10;

        private static VaultView _instance;

        private GameObject _panel;
        private Text _header;
        private Text _detailTitle;
        private Text _detailBody;
        private Text _compareBody;
        private Button _equipButton;
        private Text _equipLabel;
        private Button _salvageButton;
        private Text _salvageLabel;
        private Text _expandLabel;
        private Button _expandButton;
        private Text _status;
        private readonly List<(Image Frame, Image Icon)> _worn = new();
        private readonly List<(GameObject Root, Image Frame, Image Icon)> _stash = new();
        private int _selected = -1; // 0..3 worn, 4.. stash

        public static void Open()
        {
            if (_instance == null)
            {
                var go = new GameObject("Vault");
                _instance = go.AddComponent<VaultView>();
            }
            _instance.Show(true);
        }

        public static void Close() => _instance?.Show(false);

        private static GearVaultData Data
        {
            get
            {
                var profile = ProfileStore.Current;
                profile.gear = GearVault.Normalize(profile.gear);
                return profile.gear;
            }
        }

        private void Awake()
        {
            _instance = this;
            var root = UIKit.CreateCanvas("Vault", 30, transform).transform;
            var dim = UIKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.75f));
            dim.rectTransform.Stretch();
            _panel = dim.gameObject;

            var frame = UIKit.Framed(dim.transform, "VaultFrame", new Color(0.05f, 0.04f, 0.055f, 0.98f));
            frame.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(1720f, 900f), Vector2.zero);
            var title = UIKit.Title(frame.transform, "倉庫", 52, TextAnchor.UpperLeft);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 70f), new Vector2(36f, -18f));
            _header = UIKit.Label(frame.transform, "", 24, TextAnchor.UpperRight, UIKit.MutedText);
            _header.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1100f, 40f), new Vector2(-36f, -34f));

            var wornLabel = UIKit.Label(frame.transform, "出発時の装備", 26, TextAnchor.UpperLeft, UIKit.AccentColor);
            wornLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 36f), new Vector2(40f, -100f));
            for (int s = 0; s < SimPlayer.SlotCount; s++)
            {
                int index = s;
                var cell = Cell(frame.transform, new Vector2(40f + (s % 2) * (WornCell + 26f), -146f - (s / 2) * (WornCell + 46f)), WornCell, () => Select(index));
                var caption = UIKit.Label(cell.Frame.transform, ItemText.SlotName((EquipSlot)s), 20, TextAnchor.UpperCenter, UIKit.MutedText);
                caption.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(WornCell + 20f, 30f), new Vector2(0f, -32f));
                _worn.Add((cell.Frame, cell.Icon));
            }
            var note = UIKit.Label(frame.transform, "冒険で手に入れた装備は\n全部ここに持ち帰る。\n深い部屋の装備ほど強いが、\n浅い部屋では力が抑えられる。\n空いた枠には冒険中だけ\n貸し出し品が入る。", 20, TextAnchor.UpperLeft, UIKit.MutedText);
            note.rectTransform.Place(new Vector2(0f, 1f), new Vector2(290f, 220f), new Vector2(40f, -480f));

            var stashLabel = UIKit.Label(frame.transform, "倉庫", 26, TextAnchor.UpperLeft, UIKit.AccentColor);
            stashLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 36f), new Vector2(350f, -100f));
            for (int i = 0; i < GearVault.MaxCapacity; i++)
            {
                int index = SimPlayer.SlotCount + i;
                var cell = Cell(frame.transform, new Vector2(350f + (i % Columns) * (StashCell + 6f), -146f - (i / Columns) * (StashCell + 6f)), StashCell, () => Select(index));
                _stash.Add((cell.Frame.gameObject, cell.Frame, cell.Icon));
            }

            var detail = UIKit.Framed(frame.transform, "Detail", new Color(0.07f, 0.055f, 0.07f, 1f), UIKit.GoldTrim * 0.7f);
            detail.Outer().Place(new Vector2(1f, 1f), new Vector2(570f, 600f), new Vector2(-30f, -100f));
            _detailTitle = UIKit.Title(detail.transform, "", 32, TextAnchor.UpperLeft, Color.white);
            _detailTitle.rectTransform.Place(new Vector2(0f, 1f), new Vector2(530f, 48f), new Vector2(20f, -16f));
            _detailBody = UIKit.Label(detail.transform, "", 23, TextAnchor.UpperLeft);
            _detailBody.rectTransform.Place(new Vector2(0f, 1f), new Vector2(530f, 220f), new Vector2(20f, -70f));
            _compareBody = UIKit.Label(detail.transform, "", 20, TextAnchor.UpperLeft, UIKit.MutedText);
            _compareBody.rectTransform.Place(new Vector2(0f, 1f), new Vector2(530f, 180f), new Vector2(20f, -300f));
            _equipButton = UIKit.Button(detail.transform, "装備する", 28, EquipOrRemove, UIKit.BloodColor);
            _equipButton.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(255f, 62f), new Vector2(20f, 20f));
            _equipLabel = _equipButton.GetComponentInChildren<Text>();
            _salvageButton = UIKit.Button(detail.transform, "分解", 22, Salvage);
            _salvageButton.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(255f, 62f), new Vector2(-20f, 20f));
            _salvageLabel = _salvageButton.GetComponentInChildren<Text>();

            // Bottom row: housekeeping.
            var commons = UIKit.Button(frame.transform, "「並」を全部分解", 22, SalvageCommons);
            commons.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(240f, 56f), new Vector2(350f, 24f));
            var sort = UIKit.Button(frame.transform, "並べ替え", 22, () => { GearVault.Sort(Data); Save(); });
            sort.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(170f, 56f), new Vector2(604f, 24f));
            _expandButton = UIKit.Button(frame.transform, "", 22, Expand);
            _expandButton.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(380f, 56f), new Vector2(788f, 24f));
            _expandLabel = _expandButton.GetComponentInChildren<Text>();
            var close = UIKit.Button(frame.transform, "閉じる（Esc）", 24, () => Show(false));
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(240f, 56f), new Vector2(-30f, 24f));
            _status = UIKit.Label(frame.transform, "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.5f));
            _status.rectTransform.Place(new Vector2(0f, 0f), new Vector2(700f, 36f), new Vector2(40f, 90f));

            _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            IsOpen = false;
        }

        private static (Image Frame, Image Icon) Cell(Transform parent, Vector2 at, float size, UnityEngine.Events.UnityAction onClick)
        {
            var inner = UIKit.Framed(parent, "Cell", new Color(0.09f, 0.07f, 0.09f, 1f), new Color(0.3f, 0.26f, 0.22f));
            var outer = inner.Outer();
            outer.Place(new Vector2(0f, 1f), new Vector2(size, size), at);
            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = outer.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            var icon = UIKit.Rect("Icon", inner.transform).Stretch(size * 0.09f).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return (outer.GetComponent<Image>(), icon);
        }

        private void Show(bool open)
        {
            IsOpen = open;
            _panel.SetActive(open);
            if (!open) return;
            _status.text = "";
            if (At(_selected) == null) _selected = FirstFilled();
            Refresh();
        }

        private void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Show(false);
                ClosedFrame = Time.frameCount;
            }
        }

        // ------------------------------------------------------------------ Data

        /// <summary>Weapons are shown in the shape of the class picked in the camp.</summary>
        private static ItemInstance ForDisplay(ItemInstance item)
        {
            if (item != null && item.Slot == EquipSlot.Weapon) item.classId = ProfileStore.Current.selectedClass;
            return item;
        }

        private ItemInstance At(int index)
        {
            var data = Data;
            if (index < 0) return null;
            if (index < SimPlayer.SlotCount) return ForDisplay(GearVault.Equipped(data, (EquipSlot)index));
            int i = index - SimPlayer.SlotCount;
            return i < data.items.Count ? ForDisplay(GearVault.Unpack(data.items[i])) : null;
        }

        private int FirstFilled()
        {
            var data = Data;
            if (data.items.Count > 0) return SimPlayer.SlotCount;
            for (int s = 0; s < SimPlayer.SlotCount; s++) if (!string.IsNullOrEmpty(data.equipped[s])) return s;
            return -1;
        }

        private void Select(int index)
        {
            _selected = index;
            Refresh();
        }

        private void Save()
        {
            ProfileStore.Save(); // also re-sends the loadout in the camp
            Refresh();
        }

        // ------------------------------------------------------------------ Drawing

        private void Refresh()
        {
            var data = Data;
            if (At(_selected) == null) _selected = FirstFilled();
            _header.text = $"倉庫 {data.items.Count}/{data.capacity}　　<color=#9fd8ff>魂の欠片 {ProfileStore.Current.shards}</color>";

            for (int s = 0; s < _worn.Count; s++) Paint(_worn[s].Frame, _worn[s].Icon, At(s), s == _selected);
            for (int i = 0; i < _stash.Count; i++)
            {
                bool exists = i < data.capacity;
                _stash[i].Root.SetActive(exists);
                if (exists) Paint(_stash[i].Frame, _stash[i].Icon, At(SimPlayer.SlotCount + i), SimPlayer.SlotCount + i == _selected);
            }

            var selected = At(_selected);
            bool inStash = _selected >= SimPlayer.SlotCount;
            if (selected == null)
            {
                _detailTitle.text = "";
                _detailBody.text = "まだ装備がない。\n冒険で魔物が落とす装備や「宝物」の門の装備は、すべてここに持ち帰られる。";
                _compareBody.text = "";
            }
            else
            {
                _detailTitle.text = ItemText.ColoredTitle(selected);
                _detailBody.text = $"<color=#bfb6aa>{ItemText.SlotName(selected.Slot)}・Lv{selected.itemLevel}</color>\n" + string.Join("\n", ItemText.PropertyLines(selected));
                var worn = inStash ? ForDisplay(GearVault.Equipped(data, selected.Slot)) : null;
                _compareBody.text = !inStash ? "出発時に装備している"
                    : worn != null ? $"いま装備中：{ItemText.ColoredTitle(worn)}\n" + string.Join("\n", ItemText.PropertyLines(worn))
                    : "この枠はいま空いている（冒険中は貸し出し品が入る）";
            }

            _equipButton.gameObject.SetActive(selected != null);
            _equipLabel.text = inStash ? "装備する" : "外す";
            _equipButton.interactable = inStash || data.items.Count < data.capacity;
            _salvageButton.gameObject.SetActive(selected != null && inStash);
            if (selected != null) _salvageLabel.text = $"分解（魂の欠片 +{RunSimulation.SalvageValue(selected.rarity)}）";

            bool full = data.capacity >= GearVault.MaxCapacity;
            _expandLabel.text = full ? "倉庫はこれ以上広げられない" : $"倉庫を広げる +{GearVault.CapacityStep}（魂の欠片 {GearVault.ExpandCost(data.capacity)}）";
            _expandButton.interactable = !full && ProfileStore.Current.shards >= GearVault.ExpandCost(data.capacity);
        }

        private static void Paint(Image frame, Image icon, ItemInstance item, bool selected)
        {
            icon.sprite = GameIcons.Item(item);
            icon.enabled = icon.sprite != null;
            var rim = item != null ? GameIcons.RarityColor(item.rarity) : new Color(0.3f, 0.26f, 0.22f);
            frame.color = selected ? Color.Lerp(rim, Color.white, 0.5f) : rim * (item != null ? 0.85f : 1f);
        }

        // ------------------------------------------------------------------ Actions

        private void EquipOrRemove()
        {
            var data = Data;
            if (_selected >= SimPlayer.SlotCount) GearVault.Equip(data, _selected - SimPlayer.SlotCount);
            else if (_selected >= 0 && !GearVault.Unequip(data, (EquipSlot)_selected)) _status.text = "倉庫がいっぱいで外せない";
            Save();
        }

        private void Salvage()
        {
            if (_selected < SimPlayer.SlotCount) return;
            int shards = GearVault.Salvage(Data, _selected - SimPlayer.SlotCount);
            ProfileStore.Current.shards += shards;
            _status.text = $"分解した：魂の欠片 +{shards}";
            Save();
        }

        private void SalvageCommons()
        {
            int shards = GearVault.SalvageCommons(Data);
            ProfileStore.Current.shards += shards;
            _status.text = shards > 0 ? $"「並」を分解した：魂の欠片 +{shards}" : "分解する「並」はない";
            Save();
        }

        private void Expand()
        {
            var data = Data;
            int cost = GearVault.ExpandCost(data.capacity);
            var profile = ProfileStore.Current;
            if (data.capacity >= GearVault.MaxCapacity || profile.shards < cost) return;
            profile.shards -= cost;
            data.capacity = Mathf.Min(GearVault.MaxCapacity, data.capacity + GearVault.CapacityStep);
            _status.text = $"倉庫が {data.capacity} 枠になった";
            Save();
        }
    }
}
