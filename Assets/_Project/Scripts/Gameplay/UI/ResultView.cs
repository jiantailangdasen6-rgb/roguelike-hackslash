using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>Victory/defeat screen with a button back to the lobby.</summary>
    public sealed class ResultView : MonoBehaviour
    {
        private GameObject _panel;
        private Text _title;
        private Text _body;
        private Button _back;

        private void Awake()
        {
            var root = UIKit.CreateCanvas("Result", 60, transform).transform;
            var panel = UIKit.Panel(root, "Panel", new Color(0f, 0f, 0f, 0.75f));
            panel.rectTransform.Stretch();
            _panel = panel.gameObject;

            _title = UIKit.Label(panel.transform, "", 80, TextAnchor.MiddleCenter, UIKit.AccentColor);
            _title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1200f, 120f), new Vector2(0f, 220f));
            _body = UIKit.Label(panel.transform, "", 34, TextAnchor.MiddleCenter);
            _body.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1200f, 260f), new Vector2(0f, 20f));

            _back = UIKit.Button(panel.transform, "ロビーへ戻る", 34, () => GameSession.ReturnToLobby());
            _back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(420f, 90f), new Vector2(0f, -200f));

            _panel.SetActive(false);
            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy() => RunDirector.EventReceived -= OnEvent;

        private void OnEvent(SimEvent e)
        {
            if (e.Type != SimEventType.RunEnded) return;
            _title.text = e.Int == 1 ? "迷宮踏破！" : "全滅…";
            _panel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(_back.gameObject);
        }

        private void Update()
        {
            // Refreshed while shown: leftover loot is auto-collected on the same RunEnded event.
            var d = RunDirector.Instance;
            if (!_panel.activeSelf || d == null) return;
            _body.text =
                $"到達: 部屋 {d.RoomIndex.Value + 1} / {RunSimulation.TotalRooms}\n" +
                $"チームレベル: {d.TeamLevel.Value}\n" +
                $"時間: {UIKit.FormatTime(d.Elapsed.Value)}\n" +
                $"獲得した装備: {LootSystem.ItemsFoundThisRun} 個（すべて持ち帰り）";
        }
    }
}
