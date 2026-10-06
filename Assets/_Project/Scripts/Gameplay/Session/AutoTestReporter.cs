using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Automated smoke tests of built players: with -autoquit &lt;seconds&gt;, logs a status line every few
    /// seconds and quits. Combined with -autosolo / -autohost / -autojoin / -botmove (see LobbyView, PlayerMover).
    /// -screenshot &lt;path&gt; [-screenshotat &lt;seconds&gt;] saves a capture (needs a windowed, non-batchmode run).
    /// </summary>
    public sealed class AutoTestReporter : MonoBehaviour
    {
        private float _quitAt;
        private float _nextReport;
        private int _eventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!CommandLine.Has("-autoquit")) return;
            var go = new GameObject("AutoTestReporter");
            DontDestroyOnLoad(go);
            var reporter = go.AddComponent<AutoTestReporter>();
            reporter._quitAt = Time.time + CommandLine.GetFloat("-autoquit", 60f);
            if (CommandLine.GetValue("-screenshot") != null)
                reporter._screenshotAt = Time.time + CommandLine.GetFloat("-screenshotat", 20f);
        }

        private float _screenshotAt;

        private void OnEnable() => RunDirector.EventReceived += Count;
        private void OnDisable() => RunDirector.EventReceived -= Count;
        private void Count(Game.Core.SimEvent _) => _eventCount++;

        private void Update()
        {
            if (Time.time >= _nextReport)
            {
                _nextReport = Time.time + 5f;
                Debug.Log("[AutoTest] " + Status());
            }
            if (_screenshotAt > 0f && Time.time >= _screenshotAt)
            {
                _screenshotAt = 0f;
                string path = CommandLine.GetValue("-screenshot");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[AutoTest] screenshot -> {path}");
            }
            if (Time.time >= _quitAt)
            {
                Debug.Log("[AutoTest] FINAL " + Status());
                Application.Quit();
            }
        }

        private string Status()
        {
            var nm = NetworkManager.Singleton;
            string role = nm == null ? "none" : nm.IsHost ? "host" : nm.IsClient ? "client" : "offline";
            int peers = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : -1;
            var d = RunDirector.Instance;
            var enemyView = FindAnyObjectByType<EnemyView>();
            var local = NetPlayer.Local;
            string run = d == null || !d.IsSpawned
                ? "run=none"
                : $"started={d.Started.Value} phase={d.Phase.Value} room={d.RoomIndex.Value} level={d.TeamLevel.Value} paused={d.Paused.Value} elapsed={d.Elapsed.Value}";
            string player = local == null ? "local=none" : $"local=P{local.Slot.Value + 1} hp={local.Hp.Value}/{local.MaxHp.Value} downed={local.Downed.Value} pos={(Vector2)local.transform.position}";
            return $"role={role} peers={peers} players={NetPlayer.All.Count} {run} enemiesVisible={enemyView?.VisibleCount ?? 0} events={_eventCount} {player} stash={StashStore.Current.Items.Count}";
        }
    }
}
