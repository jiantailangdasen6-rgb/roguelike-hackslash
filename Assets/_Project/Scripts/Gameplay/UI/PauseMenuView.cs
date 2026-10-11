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
        private Button _invite;
        private Text _inviteLabel;
        private Text _leaveLabel;
        private Text _controls;
        private string _runControls;

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        /// <summary>
        /// In a run the host (and a solo player) takes the party back to the camp; a joiner can only leave the party.
        /// In the camp, leaving goes back to the title screen (for the host that closes the room).
        /// </summary>
        private void Leave()
        {
            if (!PlayArea.InCamp && IsHost) GameSession.ReturnToCamp();
            else GameSession.ReturnToLobby();
            Toggle(false);
        }

        private void Awake()
        {
            var root = UIKit.CreateCanvas("PauseMenu", 55, transform).transform;
            var dim = UIKit.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.rectTransform.Stretch();
            _panel = dim.gameObject;

            var panel = UIKit.Framed(dim.transform, "Menu");
            panel.Outer().Place(new Vector2(0.5f, 0.5f), new Vector2(720f, 720f), Vector2.zero);
            var title = UIKit.Title(panel.transform, "休息", 56);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(600f, 80f), new Vector2(0f, -30f));

            var controls = UIKit.Label(panel.transform,
                "WASD：移動　マウス：照準\n左クリック：通常攻撃（押しっぱなし）\nQ / 右クリック：スキル1　E：スキル2\nSpace / Shift：回避（無敵）　I / Tab：袋（装備）\n" +
                "<size=20><color=#bfb6aa>凍った敵を強打で砕く・印の付いた敵を狙う・倒れた仲間のそばに立つ</color></size>",
                24, TextAnchor.UpperCenter, UIKit.MutedText);
            controls.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(640f, 200f), new Vector2(0f, -120f));

            VolumeRow(panel.transform, "音楽", 300f, () => AudioPrefs.Music, v => AudioPrefs.Music = v);
            VolumeRow(panel.transform, "効果音", 244f, () => AudioPrefs.Sfx, v => AudioPrefs.Sfx = v);

            _resume = UIKit.Button(panel.transform, "続ける", 32, () => Toggle(false));
            _resume.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(360f, 72f), new Vector2(0f, 150f));
            var leave = UIKit.Button(panel.transform, "", 28, Leave, UIKit.BloodColor);
            leave.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(460f, 72f), new Vector2(0f, 60f));
            _leaveLabel = leave.GetComponentInChildren<Text>();
            _controls = controls;
            // Online rooms take late joiners: share the link from here too.
            _invite = UIKit.Button(panel.transform, "招待リンクをコピー（途中参加OK）", 24, CopyInvite);
            _invite.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(460f, 56f), new Vector2(0f, 372f));
            _inviteLabel = _invite.GetComponentInChildren<Text>();
            _note = UIKit.Label(panel.transform, "", 20, TextAnchor.LowerCenter, UIKit.MutedText);
            _note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(640f, 40f), new Vector2(0f, 14f));

            _panel.SetActive(false);
        }

        private void OnDestroy() => IsOpen = false;

        /// <summary>"音楽  [−]  70%  [+]" in 10% steps.</summary>
        private static void VolumeRow(Transform parent, string label, float y, System.Func<float> get, System.Action<float> set)
        {
            var name = UIKit.Label(parent, label, 26, TextAnchor.MiddleRight, UIKit.MutedText);
            name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(160f, 50f), new Vector2(-200f, y));
            var value = UIKit.Title(parent, "", 28, TextAnchor.MiddleCenter, Color.white);
            value.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(120f, 50f), new Vector2(20f, y));
            void Refresh() => value.text = $"{Mathf.RoundToInt(get() * 100f)}%";
            var minus = UIKit.Button(parent, "−", 30, () => { set(Mathf.Round(get() * 10f - 1f) / 10f); Refresh(); });
            minus.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(60f, 50f), new Vector2(-70f, y));
            var plus = UIKit.Button(parent, "+", 30, () => { set(Mathf.Round(get() * 10f + 1f) / 10f); Refresh(); });
            plus.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(60f, 50f), new Vector2(110f, y));
            Refresh();
        }

        private void Toggle(bool open)
        {
            _panel.SetActive(open);
            IsOpen = open;
            if (!open) return;
            var nm = NetworkManager.Singleton;
            bool party = nm != null && nm.IsHost && nm.ConnectedClientsIds.Count > 1;
            bool camp = PlayArea.InCamp;
            _runControls ??= _controls.text;
            _controls.text = camp
                ? "WASD：移動\n施設の前で F / Enter：準備をする\n全員で奥の門に立つと出発"
                : _runControls;
            _leaveLabel.text = camp ? (IsHost ? (party ? "タイトルへ（部屋を閉じる）" : "タイトルへ戻る") : "パーティを抜ける（タイトルへ）")
                : IsHost ? "拠点へ戻る（ランを放棄）" : "パーティを抜ける（タイトルへ）";
            _note.text = camp ? (party ? "※ 部屋を閉じると仲間もタイトルへ戻ります" : "")
                : party ? "※ あなたはホストです。戻ると全員が拠点へ戻ります"
                : IsHost ? "" : "※ ゲームは止まりません（オンラインのため）";
            _invite.gameObject.SetActive(GameSession.JoinCode != null);
            _inviteLabel.text = "招待リンクをコピー（途中参加OK）";
            EventSystem.current?.SetSelectedGameObject(_resume.gameObject);
        }

        private void CopyInvite()
        {
            if (GameSession.JoinCode == null) return;
            BrowserBridge.CopyText(BrowserBridge.InviteLink(GameSession.JoinCode));
            _inviteLabel.text = $"コピーしました（ルームコード {GameSession.JoinCode}）";
        }

        private void Update()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                           || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (pressed && !PerkView.IsShowing && !BagView.IsOpen && BagView.ClosedFrame != Time.frameCount
                && !CampView.PanelOpen && !CampView.ConsumedEscape) Toggle(!IsOpen);
        }
    }
}
