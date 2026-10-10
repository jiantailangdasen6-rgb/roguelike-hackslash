using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>Victory/defeat screen. Awards soul shards (out-of-run currency) to this device's profile.</summary>
    public sealed class ResultView : MonoBehaviour
    {
        private GameObject _panel;
        private Text _title;
        private Text _body;
        private Button _back;
        private bool _victory;
        private int _shards = -1;

        private void Awake()
        {
            var root = UIKit.CreateCanvas("Result", 60, transform).transform;
            var panel = UIKit.Panel(root, "Panel", new Color(0f, 0f, 0f, 0.82f));
            panel.rectTransform.Stretch();
            _panel = panel.gameObject;

            _title = UIKit.Title(panel.transform, "", 96);
            _title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1400f, 140f), new Vector2(0f, 240f));
            _body = UIKit.Label(panel.transform, "", 32, TextAnchor.MiddleCenter);
            _body.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1200f, 300f), new Vector2(0f, 10f));

            _back = UIKit.Button(panel.transform, "拠点へ戻る", 34, () => GameSession.ReturnToLobby());
            _back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(420f, 90f), new Vector2(0f, -220f));

            _panel.SetActive(false);
            RunDirector.EventReceived += OnEvent;
        }

        private void OnDestroy() => RunDirector.EventReceived -= OnEvent;

        private void OnEvent(SimEvent e)
        {
            if (e.Type != SimEventType.RunEnded) return;
            _victory = e.Int == 1;
            _title.text = _victory ? "迷宮を踏破した" : "力尽きた…";
            _title.color = _victory ? UIKit.AccentColor : new Color(0.75f, 0.15f, 0.15f);
            _shards = -1;
            _panel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(_back.gameObject);
        }

        private void Update()
        {
            var d = RunDirector.Instance;
            if (!_panel.activeSelf || d == null) return;

            // NetworkVariables can land a frame after the event, so award shards once they settle.
            if (_shards < 0 && d.Phase.Value != RunPhase.Playing)
            {
                _shards = MetaCatalog.ShardsForRun(d.RoomsCleared.Value, d.BossesKilled.Value, d.Kills.Value, _victory);
                ProfileStore.Current.shards += _shards;
                ProfileStore.Save();
            }

            _body.text =
                $"到達：{HudView.RoomTitle(d.RoomIndex.Value)}（{d.RoomIndex.Value + 1}/{RunSimulation.TotalRooms}）\n" +
                $"チームレベル：{d.TeamLevel.Value}　　討伐数：{d.Kills.Value}　　時間：{UIKit.FormatTime(d.Elapsed.Value)}\n\n" +
                $"<color=#9fd8ff>魂の欠片 +{Mathf.Max(0, _shards)}</color>　（所持 {ProfileStore.Current.shards}）\n" +
                "<size=24>拠点で「熟練」と「お守り」に使えます</size>";
        }
    }
}
