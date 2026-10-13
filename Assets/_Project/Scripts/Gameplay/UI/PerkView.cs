using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>Level-up perk choice (3 cards) for the local player. Mouse, 1/2/3 keys or gamepad.</summary>
    public sealed class PerkView : MonoBehaviour
    {
        public static bool IsShowing { get; private set; }

        private GameObject _panel;
        private Text _timerText;
        private Text _heading;
        private readonly Button[] _buttons = new Button[PerkRoller.OptionCount];
        private readonly Image[] _edges = new Image[PerkRoller.OptionCount];
        private readonly Text[] _tags = new Text[PerkRoller.OptionCount];
        private readonly Text[] _titles = new Text[PerkRoller.OptionCount];
        private readonly Text[] _bodies = new Text[PerkRoller.OptionCount];
        private readonly Image[] _icons = new Image[PerkRoller.OptionCount];
        private readonly RectTransform[] _cards = new RectTransform[PerkRoller.OptionCount];
        private readonly Image[] _halos = new Image[PerkRoller.OptionCount];
        private float _shownAt;
        private PerkOption[] _options;
        private Button _skip;
        private bool _shop;
        private readonly bool[] _affordable = new bool[PerkRoller.OptionCount];

        /// <summary>Perks this player has picked in the current run (for the HUD).</summary>
        public static readonly List<PerkOption> Taken = new();
        private float _deadline;
        private int _count;

        private void Awake()
        {
            Taken.Clear(); // a new Game scene is a new run
            var root = UIKit.CreateCanvas("Perks", 40, transform).transform;
            var dim = UIKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.7f));
            dim.rectTransform.Stretch();
            _panel = dim.gameObject;

            var title = _heading = UIKit.Title(dim.transform, "力を選べ", 64);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 90f), new Vector2(0f, 385f));
            _timerText = UIKit.Label(dim.transform, "", 24, TextAnchor.MiddleCenter, UIKit.MutedText);
            _timerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(900f, 40f), new Vector2(0f, 322f));

            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                // A tall card: a coloured banner with whose skill it is, the icon on a glowing medallion, the name, what it does,
                // and its key on a cap at the bottom.
                var card = UIKit.Framed(dim.transform, "Card" + i, new Color(0.065f, 0.05f, 0.06f, 1f));
                var outer = card.Outer();
                outer.Place(new Vector2(0.5f, 0.5f), new Vector2(440f, 560f), new Vector2((i - 1) * 480f, -40f));
                _cards[i] = outer;
                var button = outer.gameObject.AddComponent<Button>();
                button.targetGraphic = outer.GetComponent<Image>();
                var colors = button.colors;
                colors.highlightedColor = new Color(1.6f, 1.5f, 1.2f);
                colors.selectedColor = new Color(1.8f, 1.6f, 1.2f);
                button.colors = colors;
                button.onClick.AddListener(() => Choose(index));

                _edges[i] = UIKit.Panel(card.transform, "Banner", Color.gray);
                _edges[i].sprite = UIKit.GlossSprite;
                _edges[i].rectTransform.anchorMin = new Vector2(0f, 1f);
                _edges[i].rectTransform.anchorMax = new Vector2(1f, 1f);
                _edges[i].rectTransform.pivot = new Vector2(0.5f, 1f);
                _edges[i].rectTransform.sizeDelta = new Vector2(0f, 46f);
                _tags[i] = UIKit.Title(_edges[i].transform, "", 24, TextAnchor.MiddleCenter, Color.white);
                _tags[i].rectTransform.Stretch();
                var halo = UIKit.Panel(card.transform, "Halo", new Color(1f, 0.8f, 0.45f, 0.35f));
                halo.sprite = UIKit.GlowSprite;
                halo.raycastTarget = false;
                halo.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(260f, 260f), new Vector2(0f, -30f));
                _halos[i] = halo;
                var medal = UIKit.Panel(card.transform, "Medal", new Color(0.86f, 0.68f, 0.32f));
                medal.sprite = UIKit.ThinRingSprite;
                medal.raycastTarget = false;
                medal.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(164f, 164f), new Vector2(0f, -78f));
                _icons[i] = UIKit.Rect("Icon", card.transform).Place(new Vector2(0.5f, 1f), new Vector2(132f, 132f), new Vector2(0f, -94f)).gameObject.AddComponent<Image>();
                _icons[i].raycastTarget = false;
                _titles[i] = UIKit.Title(card.transform, "", 40);
                _titles[i].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(410f, 64f), new Vector2(0f, -254f));
                var rule = UIKit.Panel(card.transform, "Rule", UIKit.GoldTrim);
                rule.raycastTarget = false;
                rule.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(240f, 2f), new Vector2(0f, -320f));
                _bodies[i] = UIKit.Label(card.transform, "", 26, TextAnchor.UpperCenter);
                _bodies[i].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(390f, 170f), new Vector2(0f, -334f));
                UIKit.KeyCap(outer, (i + 1).ToString(), 22, out _).Place(new Vector2(0.5f, 0f), new Vector2(46f, 36f), new Vector2(0f, -18f));
                _buttons[i] = button;
            }

            // The merchant: walk away without buying.
            _skip = UIKit.Button(dim.transform, "買わない [4]", 28, () => Choose(RunSimulation.ShopSkip));
            _skip.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(360f, 64f), new Vector2(0f, -340f));

            _panel.SetActive(false);
            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy()
        {
            RunDirector.EventReceived -= OnEvent;
            IsShowing = false;
        }

        private void OnEvent(SimEvent e)
        {
            var local = NetPlayer.Local;
            if (e.Type != SimEventType.PerkOffer || local == null || e.ClientId != local.OwnerClientId) return;
            _shop = (ChoiceKind)e.Int == ChoiceKind.Shop;
            _heading.text = (ChoiceKind)e.Int switch
            {
                ChoiceKind.Start => "旅立ちの力を選べ",
                ChoiceKind.Boon => "<color=#ffcc5c>祝福</color>  力を選べ",
                ChoiceKind.Upgrade => "<color=#8cd9ff>研磨</color>  強化する力を選べ",
                ChoiceKind.Shop => $"<color=#f2d173>闇商人</color>  1つ買える  <size=30><color=#c9a8ff>手持ちの魂の欠片 {Purse()}</color></size>",
                ChoiceKind.Blessing => "<color=#ffe3a0>神々の加護</color>  ひとつ授かる",
                _ => "力を選べ",
            };
            int more = Mathf.RoundToInt(e.Value);
            if (more > 0) _heading.text += $"  <size=28><color=#bfb6aa>（あと {more} 回）</color></size>";
            Show(PerkOption.Unpack(e.Seed));
        }

        private static Color ClassColor(ClassId? c) => c switch
        {
            ClassId.Warrior => new Color(0.75f, 0.15f, 0.15f),
            ClassId.Mage => new Color(0.35f, 0.55f, 1f),
            ClassId.Ranger => new Color(0.35f, 0.75f, 0.35f),
            ClassId.Guardian => new Color(0.85f, 0.75f, 0.4f),
            ClassId.Assassin => new Color(0.55f, 0.35f, 0.85f),
            ClassId.Alchemist => new Color(0.85f, 0.5f, 0.2f),
            _ => new Color(0.7f, 0.6f, 0.4f),
        };

        private void Show(PerkOption[] options)
        {
            _count = options.Length;
            _options = options;
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool used = i < options.Length;
                _buttons[i].gameObject.SetActive(used);
                if (!used) continue;
                var (title, body) = options[i].Describe();
                ClassId? cls = options[i].IsPerk ? PerkCatalog.Get((PerkId)options[i].Perk).Class : null;
                _affordable[i] = true;
                if (_shop)
                {
                    // Priced wares; what the team can't pay for is greyed out.
                    int price = RewardCatalog.Price(options[i], Area());
                    _affordable[i] = price <= Purse();
                    body += _affordable[i] ? $"\n\n<color=#c9a8ff>魂の欠片 {price}</color>" : $"\n\n<color=#8a7f80>魂の欠片 {price}（足りない）</color>";
                }
                _buttons[i].interactable = _affordable[i];
                GodId? god = options[i].IsPerk ? PerkCatalog.Get((PerkId)options[i].Perk).God : null;
                Color banner = ClassColor(cls);
                _tags[i].text = cls.HasValue ? ClassCatalog.Get(cls.Value).Name + "の技" : "共通";
                if (god.HasValue)
                {
                    // A god's blessing: the god's colour and name, and how close its favour is.
                    var g = GodCatalog.Get(god.Value);
                    Codex.Record(CodexCatalog.GodEntry(god.Value));
                    banner = g.Color * new Color(0.75f, 0.75f, 0.75f, 1f);
                    _tags[i].text = g.Name + "の加護";
                    int have = GodPieces(god.Value);
                    string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(g.Color, Color.white, 0.3f));
                    string state = have + 1 >= GodCatalog.FavorPieces ? "これで寵愛が目覚める！" : $"同じ神をあと{GodCatalog.FavorPieces - have - 1}つで";
                    body += $"\n\n<size=22><color=#{hex}>{g.FavorName}（{state}）\n{g.FavorText}</color></size>";
                }
                _titles[i].text = title;
                _bodies[i].text = body;
                _edges[i].color = banner;
                _halos[i].color = Color.Lerp(banner, new Color(1f, 0.85f, 0.5f), god.HasValue ? 0.2f : 0.5f) * new Color(1f, 1f, 1f, god.HasValue ? 0.6f : 0.4f);
                _icons[i].sprite = GameIcons.Perk(options[i]);
                _icons[i].enabled = _icons[i].sprite != null;
            }
            _skip.gameObject.SetActive(_shop);
            _shownAt = Time.unscaledTime;
            UiSound.Play(UiCue.Open);
            _deadline = Time.time + RunSimulation.ChoiceTimeout;
            _panel.SetActive(true);
            IsShowing = true;
            EventSystem.current?.SetSelectedGameObject(_buttons[0].gameObject);

            if (CommandLine.Has("-botmove")) Invoke(nameof(BotChoose), CommandLine.GetFloat("-perkdelay", 0.8f));
        }

        private void BotChoose()
        {
            int pick = Random.Range(0, _shop ? _count + 1 : _count);
            Choose(pick < _count && _affordable[pick] ? pick : _shop ? RunSimulation.ShopSkip : 0);
        }

        /// <summary>What the team would take home now (the merchant is paid from it).</summary>
        private static int Purse()
        {
            var d = RunDirector.Instance;
            return d == null ? 0 : RewardCatalog.Purse(d.RoomsCleared.Value, d.BossesKilled.Value, d.Kills.Value, d.DifficultyLevel.Value, d.BonusShards.Value);
        }

        /// <summary>Blessings of a god this player has taken this run.</summary>
        public static int GodPieces(GodId god)
        {
            var seen = new HashSet<byte>();
            foreach (var o in Taken)
                if (o.IsPerk && PerkCatalog.Get((PerkId)o.Perk).God == god) seen.Add(o.Perk);
            return seen.Count;
        }

        private static int Area() => RunDirector.Instance != null ? RunDirector.Instance.Area.Value : 0;

        private void Choose(int index)
        {
            if (!IsShowing) return;
            if (index < _count && !_affordable[index]) return; // can't pay for that one
            RunDirector.Instance?.ChoosePerkRpc(index);
            if (_options != null && index < _options.Length && _options[index].IsPerk) Taken.Add(_options[index]);
            _options = null;
            Hide();
        }

        private void Hide()
        {
            _panel.SetActive(false);
            IsShowing = false;
        }

        private void Update()
        {
            if (!IsShowing) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Choose(0);
                else if (kb.digit2Key.wasPressedThisFrame && _count > 1) Choose(1);
                else if (kb.digit3Key.wasPressedThisFrame && _count > 2) Choose(2);
                else if (kb.digit4Key.wasPressedThisFrame && _shop) Choose(RunSimulation.ShopSkip);
            }
            if (!IsShowing) return;

            // Cards turn over one after another, then the one under the cursor or selection lifts a little.
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < _count; i++)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _shownAt - i * 0.09f) / 0.22f);
                float flip = 1f - (1f - t) * (1f - t);
                bool lifted = selected == _cards[i].gameObject;
                float s = lifted ? 1.05f : 1f;
                _cards[i].localScale = new Vector3(flip * s, s, 1f);
                _cards[i].anchoredPosition = new Vector2((i - (_count - 1) * 0.5f) * 480f, -40f + (lifted ? 16f : 0f) - (1f - flip) * 40f);
            }

            float left = _deadline - Time.time;
            _timerText.text = $"残り {Mathf.CeilToInt(Mathf.Max(0f, left))} 秒（{(_shop ? "時間切れで何も買わない" : "時間切れで左を選択")}）";
            var director = RunDirector.Instance;
            if (left < -1f || (director != null && !director.Paused.Value && left < RunSimulation.ChoiceTimeout - 1f))
            {
                // Timed out: the host picked the first option.
                if (!_shop && _options != null && _options.Length > 0 && _options[0].IsPerk) Taken.Add(_options[0]);
                _options = null;
                Hide();
            }
        }
    }
}
