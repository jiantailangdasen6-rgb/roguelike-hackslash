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
        private readonly Button[] _buttons = new Button[PerkRoller.OptionCount];
        private readonly Image[] _edges = new Image[PerkRoller.OptionCount];
        private readonly Text[] _tags = new Text[PerkRoller.OptionCount];
        private readonly Text[] _titles = new Text[PerkRoller.OptionCount];
        private readonly Text[] _bodies = new Text[PerkRoller.OptionCount];
        private readonly Image[] _icons = new Image[PerkRoller.OptionCount];
        private PerkOption[] _options;

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

            var title = UIKit.Title(dim.transform, "力を選べ", 64);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 90f), new Vector2(0f, 320f));
            _timerText = UIKit.Label(dim.transform, "", 24, TextAnchor.MiddleCenter, UIKit.MutedText);
            _timerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(900f, 40f), new Vector2(0f, 255f));

            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                var card = UIKit.Framed(dim.transform, "Card" + i, new Color(0.06f, 0.045f, 0.06f, 1f));
                var outer = card.Outer();
                outer.Place(new Vector2(0.5f, 0.5f), new Vector2(460f, 480f), new Vector2((i - 1) * 500f, -50f));
                var button = outer.gameObject.AddComponent<Button>();
                button.targetGraphic = outer.GetComponent<Image>();
                var colors = button.colors;
                colors.highlightedColor = new Color(1.6f, 1.5f, 1.2f);
                colors.selectedColor = new Color(1.8f, 1.6f, 1.2f);
                button.colors = colors;
                button.onClick.AddListener(() => Choose(index));

                _edges[i] = UIKit.Panel(card.transform, "Edge", Color.gray);
                _edges[i].rectTransform.anchorMin = new Vector2(0f, 1f);
                _edges[i].rectTransform.anchorMax = new Vector2(1f, 1f);
                _edges[i].rectTransform.pivot = new Vector2(0.5f, 1f);
                _edges[i].rectTransform.sizeDelta = new Vector2(0f, 8f);
                _tags[i] = UIKit.Label(card.transform, "", 22, TextAnchor.UpperCenter, UIKit.MutedText);
                _tags[i].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(420f, 34f), new Vector2(0f, -24f));
                _titles[i] = UIKit.Title(card.transform, "", 40);
                _icons[i] = UIKit.Rect("Icon", card.transform).Place(new Vector2(0.5f, 1f), new Vector2(112f, 112f), new Vector2(0f, -58f)).gameObject.AddComponent<Image>();
                _icons[i].raycastTarget = false;
                _titles[i].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(420f, 70f), new Vector2(0f, -172f));
                _bodies[i] = UIKit.Label(card.transform, "", 27, TextAnchor.UpperCenter);
                _bodies[i].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(400f, 200f), new Vector2(0f, -248f));
                var key = UIKit.Label(card.transform, $"[{i + 1}]", 22, TextAnchor.LowerCenter, UIKit.MutedText);
                key.rectTransform.Stretch(14f);
                _buttons[i] = button;
            }

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
            Show(PerkOption.Unpack(e.Seed));
        }

        private static Color ClassColor(ClassId? c) => c switch
        {
            ClassId.Warrior => new Color(0.75f, 0.15f, 0.15f),
            ClassId.Mage => new Color(0.35f, 0.55f, 1f),
            ClassId.Ranger => new Color(0.35f, 0.75f, 0.35f),
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
                ClassId? cls = options[i].IsHeal ? null : PerkCatalog.Get((PerkId)options[i].Perk).Class;
                _titles[i].text = title;
                _bodies[i].text = body;
                _tags[i].text = cls.HasValue ? ClassCatalog.Get(cls.Value).Name + "の技" : "共通";
                _edges[i].color = ClassColor(cls);
                _icons[i].sprite = GameIcons.Perk(options[i]);
                _icons[i].enabled = _icons[i].sprite != null;
            }
            _deadline = Time.time + RunSimulation.ChoiceTimeout;
            _panel.SetActive(true);
            IsShowing = true;
            EventSystem.current?.SetSelectedGameObject(_buttons[0].gameObject);

            if (CommandLine.Has("-botmove")) Invoke(nameof(BotChoose), CommandLine.GetFloat("-perkdelay", 0.8f));
        }

        private void BotChoose() => Choose(Random.Range(0, _count));

        private void Choose(int index)
        {
            if (!IsShowing) return;
            RunDirector.Instance?.ChoosePerkRpc(index);
            if (_options != null && index < _options.Length) Taken.Add(_options[index]);
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
            }
            if (!IsShowing) return;

            float left = _deadline - Time.time;
            _timerText.text = $"残り {Mathf.CeilToInt(Mathf.Max(0f, left))} 秒（時間切れで左を選択）";
            var director = RunDirector.Instance;
            if (left < -1f || (director != null && !director.Paused.Value && left < RunSimulation.ChoiceTimeout - 1f))
            {
                // Timed out: the host picked the first option.
                if (_options != null && _options.Length > 0) Taken.Add(_options[0]);
                _options = null;
                Hide();
            }
        }
    }
}
