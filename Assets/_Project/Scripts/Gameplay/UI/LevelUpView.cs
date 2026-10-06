using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>The 3-choice level-up panel for the local player.</summary>
    public sealed class LevelUpView : MonoBehaviour
    {
        public static bool IsShowing { get; private set; }

        private GameObject _panel;
        private Text _timerText;
        private readonly Button[] _buttons = new Button[UpgradeRoller.OptionCount];
        private readonly Text[] _titles = new Text[UpgradeRoller.OptionCount];
        private readonly Text[] _bodies = new Text[UpgradeRoller.OptionCount];
        private float _deadline;

        private void Awake()
        {
            var root = UIKit.CreateCanvas("LevelUp", 40, transform).transform;
            var dim = UIKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.55f));
            dim.rectTransform.Stretch();
            _panel = dim.gameObject;

            var title = UIKit.Label(dim.transform, "レベルアップ！ 強化を1つ選んでください", 46, TextAnchor.MiddleCenter, UIKit.AccentColor);
            title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 80f), new Vector2(0f, 300f));
            _timerText = UIKit.Label(dim.transform, "", 28, TextAnchor.MiddleCenter);
            _timerText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(800f, 40f), new Vector2(0f, 240f));

            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                var button = UIKit.Button(dim.transform, "", 1, () => Choose(index));
                Destroy(button.GetComponentInChildren<Text>().gameObject);
                button.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(480f, 300f), new Vector2((i - 1) * 520f, -20f));
                _titles[i] = UIKit.Label(button.transform, "", 36, TextAnchor.UpperCenter, UIKit.AccentColor);
                _titles[i].rectTransform.Stretch(24f);
                _bodies[i] = UIKit.Label(button.transform, "", 28, TextAnchor.MiddleCenter);
                _bodies[i].rectTransform.Stretch(24f);
                _bodies[i].rectTransform.offsetMax = new Vector2(-24f, -90f);
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
            if (e.Type != SimEventType.LevelUpOffer || local == null || e.ClientId != local.OwnerClientId) return;
            Show(UpgradeOption.UnpackAll(e.Seed));
        }

        private void Show(UpgradeOption[] options)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool used = i < options.Length;
                _buttons[i].gameObject.SetActive(used);
                if (!used) continue;
                var (title, body) = UpgradeRoller.Describe(options[i]);
                _titles[i].text = title;
                _bodies[i].text = body;
            }
            _deadline = Time.time + RunSimulation.ChoiceTimeout;
            _panel.SetActive(true);
            IsShowing = true;
            EventSystem.current?.SetSelectedGameObject(_buttons[0].gameObject);

            if (CommandLine.Has("-botmove")) Choose(0);
        }

        private void Choose(int index)
        {
            RunDirector.Instance?.ChooseUpgradeRpc(index);
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
            float left = _deadline - Time.time;
            _timerText.text = $"残り {Mathf.CeilToInt(Mathf.Max(0f, left))} 秒（時間切れで左を自動選択）";
            // The host auto-picks on timeout; close once the game resumes.
            var director = RunDirector.Instance;
            if (left < -1f || (director != null && !director.Paused.Value && left < RunSimulation.ChoiceTimeout - 1f)) Hide();
        }
    }
}
