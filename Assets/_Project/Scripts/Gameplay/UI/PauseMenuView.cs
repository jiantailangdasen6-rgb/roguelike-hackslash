using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// Esc / Start menu during a run: resume or abandon the run. The game keeps running (it is online);
    /// if the host leaves, everyone returns to the camp.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private GameObject _panel;
        private Text _note;
        private Button _resume;

        private void Awake()
        {
            var root = UIKit.CreateCanvas("PauseMenu", 55, transform).transform;
            var dim = UIKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.rectTransform.Stretch();
            _panel = dim.gameObject;

            var panel = UIKit.Framed(dim.transform, "Menu");
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(720f, 560f), Vector2.zero);
            var title = UIKit.Title(panel.transform, "休息", 56);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(600f, 80f), new Vector2(0f, -30f));

            var controls = UIKit.Label(panel.transform,
                "WASD：移動　マウス：照準\n左クリック：通常攻撃（押しっぱなし）\nQ / 右クリック：スキル1　E：スキル2\nSpace / Shift：回避（無敵）\n" +
                "<size=20><color=#bfb6aa>凍った敵を強打で砕く・印の付いた敵を狙う・倒れた仲間のそばに立つ</color></size>",
                24, TextAnchor.UpperCenter, UIKit.MutedText);
            controls.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(640f, 200f), new Vector2(0f, -120f));

            _resume = UIKit.Button(panel.transform, "続ける", 32, () => Toggle(false));
            _resume.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(360f, 72f), new Vector2(0f, 150f));
            var leave = UIKit.Button(panel.transform, "拠点へ戻る（ランを放棄）", 28, () => GameSession.ReturnToLobby(), UIKit.BloodColor);
            leave.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(460f, 72f), new Vector2(0f, 60f));
            _note = UIKit.Label(panel.transform, "", 20, TextAnchor.LowerCenter, UIKit.MutedText);
            _note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(640f, 40f), new Vector2(0f, 14f));

            _panel.SetActive(false);
        }

        private void OnDestroy() => IsOpen = false;

        private void Toggle(bool open)
        {
            _panel.SetActive(open);
            IsOpen = open;
            if (!open) return;
            var nm = NetworkManager.Singleton;
            _note.text = nm != null && nm.IsHost && nm.ConnectedClientsIds.Count > 1
                ? "※ あなたはホストです。戻ると全員のランが終わります"
                : "※ ゲームは止まりません（オンラインのため）";
            EventSystem.current?.SetSelectedGameObject(_resume.gameObject);
        }

        private void Update()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                           || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (pressed && !PerkView.IsShowing) Toggle(!IsOpen);
        }
    }
}
