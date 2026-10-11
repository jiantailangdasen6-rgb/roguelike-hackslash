using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Automated smoke tests of built players: with -autoquit &lt;seconds&gt;, logs a status line every few
    /// seconds and quits. Combined with -autosolo / -autohost / -autojoin / -botmove (see LobbyView, PlayerMover).
    /// -screenshot &lt;path&gt; [-screenshotat &lt;s1,s2,...&gt;] saves captures (path_1.png, path_2.png… when several;
    /// needs a windowed, non-batchmode run).
    /// </summary>
    public sealed class AutoTestReporter : MonoBehaviour
    {
        private float _quitAt;
        private float _nextReport;
        private int _eventCount;
        private float _downAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!CommandLine.Has("-autoquit")) return;
            var go = new GameObject("AutoTestReporter");
            DontDestroyOnLoad(go);
            var reporter = go.AddComponent<AutoTestReporter>();
            reporter._quitAt = Time.time + CommandLine.GetFloat("-autoquit", 60f);
            if (CommandLine.Has("-downat")) reporter._downAt = Time.time + CommandLine.GetFloat("-downat", 30f);
            if (CommandLine.GetValue("-screenshot") != null)
            {
                string times = CommandLine.GetValue("-screenshotat") ?? "20";
                foreach (var t in times.Split(','))
                    if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s))
                        reporter._shots.Add(Time.time + s);
            }
        }

        private readonly System.Collections.Generic.List<float> _shots = new();
        private int _shotIndex;

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
            // -downat <s> (host): knock a teammate down to check the downed/revive markers.
            if (_downAt > 0f && Time.time >= _downAt && RunDirector.Instance?.Sim != null)
            {
                _downAt = 0f;
                var sim = RunDirector.Instance.Sim;
                foreach (var p in sim.Players)
                {
                    if (p.ClientId == NetworkManager.Singleton.LocalClientId) continue;
                    p.InvulnerableTimer = 0f;
                    sim.HurtPlayer(p, 99999f);
                    Debug.Log($"[AutoTest] downed client {p.ClientId}");
                    break;
                }
            }
            if (_shotIndex < _shots.Count && Time.time >= _shots[_shotIndex])
            {
                _shotIndex++;
                string path = CommandLine.GetValue("-screenshot");
                if (_shots.Count > 1) path = System.IO.Path.ChangeExtension(path, null) + "_" + _shotIndex + ".png";
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
            string player = local == null ? "local=none" : $"local=P{local.Slot.Value + 1}:{local.Class.Value} hp={local.Hp.Value}/{local.MaxHp.Value} downed={local.Downed.Value} pos={(Vector2)local.transform.position}";
            return $"role={role} peers={peers} players={NetPlayer.All.Count} {run} enemiesVisible={enemyView?.VisibleCount ?? 0} events={_eventCount} {player} kills={(d != null && d.IsSpawned ? d.Kills.Value : 0)} shards={ProfileStore.Current.shards}";
        }
    }
}
