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
        private float _dropAt;
        private float _wipeAt;
        private float _freezeAt;
        private float _failAt;
        private bool _bossHpSet;
        private float _fakeUnionAt;
        private float _spawnEliteAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!CommandLine.Has("-autoquit")) return;
            var go = new GameObject("AutoTestReporter");
            DontDestroyOnLoad(go);
            var reporter = go.AddComponent<AutoTestReporter>();
            reporter._quitAt = Time.time + CommandLine.GetFloat("-autoquit", 60f);
            if (CommandLine.Has("-downat")) reporter._downAt = Time.time + CommandLine.GetFloat("-downat", 30f);
            if (CommandLine.Has("-dropat")) reporter._dropAt = Time.time + CommandLine.GetFloat("-dropat", 30f);
            if (CommandLine.Has("-wipeat")) reporter._wipeAt = Time.time + CommandLine.GetFloat("-wipeat", 30f);
            if (CommandLine.Has("-freezeat")) reporter._freezeAt = Time.time + CommandLine.GetFloat("-freezeat", 30f);
            if (CommandLine.Has("-failat")) reporter._failAt = Time.time + CommandLine.GetFloat("-failat", 40f);
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
        /// <summary>Gear this player got, by rarity (balance runs read it from the FINAL line).</summary>
        private readonly int[] _loot = new int[4];
        private int _legendaryProcs;

        private void Count(Game.Core.SimEvent e)
        {
            _eventCount++;
            bool mine = NetworkManager.Singleton != null && e.ClientId == NetworkManager.Singleton.LocalClientId;
            if (!mine) return;
            if (e.Type == Game.Core.SimEventType.LootDropped && e.Int >= 0 && e.Int < 4) _loot[e.Int]++;
            else if (e.Type == Game.Core.SimEventType.LootPicked && e.Id == 0 && e.Int >= 0 && e.Int < 4) _loot[e.Int]++; // treasure door, straight into the bag
            else if (e.Type == Game.Core.SimEventType.LegendaryProc && e.Int != 0) _legendaryProcs++;
        }

        private float _longestFrame;
        private float _frameSum;
        private int _frames, _slowFrames;

        private void Update()
        {
            Measure();
            // Hitches: the longest frame of each report period, and every frame over a quarter second on its own.
            float frame = Time.unscaledDeltaTime;
            _longestFrame = Mathf.Max(_longestFrame, frame);
            if (frame > 0.25f && Time.frameCount > 10)
                Debug.Log($"[AutoTest] long frame {frame * 1000f:0}ms local={(NetPlayer.Local != null ? (Vector2)NetPlayer.Local.transform.position : Vector2.zero)}");
            if (Time.time >= _nextReport)
            {
                _nextReport = Time.time + 5f;
                BrowserBridge.ConsoleLog("[AutoTest] " + Status() + $" longest={_longestFrame * 1000f:0}ms ping={(NetPlayer.Local != null ? NetPlayer.Local.Ping.Value : 0)}");
                _longestFrame = 0f;
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
            // -bosshp <f> (host): the lord starts its fight at that share of health (screenshots of its stages).
            if (!_bossHpSet && CommandLine.Has("-bosshp") && RunDirector.Instance?.Sim != null)
            {
                var enemies = RunDirector.Instance.Sim.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].Dead || !enemies[i].Def.Boss) continue;
                    ref var boss = ref enemies[i];
                    boss.Health = boss.MaxHealth * CommandLine.GetFloat("-bosshp", 0.5f);
                    _bossHpSet = true;
                    Debug.Log($"[AutoTest] lord set to {boss.Health:0} hp");
                    break;
                }
            }
            // -fakeunion <s>: the look of a union technique beside the local player (screenshots; one player can't make one).
            if (_fakeUnionAt == 0f && CommandLine.Has("-fakeunion")) _fakeUnionAt = Time.time + CommandLine.GetFloat("-fakeunion", 20f);
            if (_fakeUnionAt > 0f && Time.time >= _fakeUnionAt && NetPlayer.Local != null && EventPresenter.Instance != null)
            {
                _fakeUnionAt = -1f;
                Vector2 at = NetPlayer.Local.transform.position;
                EventPresenter.Instance.ShowUnion(at, at + new Vector2(3.5f, 0.5f), Game.Core.UnionCatalog.Pair(Game.Core.ClassId.Mage, Game.Core.ClassId.Alchemist));
            }
            // -spawnelite <s> (host): three elites with affixes beside the local player (screenshots of the affixes).
            if (_spawnEliteAt == 0f && CommandLine.Has("-spawnelite")) _spawnEliteAt = Time.time + CommandLine.GetFloat("-spawnelite", 10f);
            if (_spawnEliteAt > 0f && Time.time >= _spawnEliteAt && NetPlayer.Local != null && RunDirector.Instance?.Sim != null)
            {
                _spawnEliteAt = -1f;
                var sim = RunDirector.Instance.Sim;
                Vector2 at = NetPlayer.Local.transform.position;
                var room = sim.Map.RoomAt(at);
                foreach (var (type, offset) in new[] { (Game.Core.EnemyType.Skeleton, new Vector2(3f, 1f)), (Game.Core.EnemyType.Ghoul, new Vector2(-3f, 1.5f)), (Game.Core.EnemyType.SkeletonArcher, new Vector2(0f, 3.5f)) })
                {
                    int id = sim.Enemies.Spawn(type, at + offset, sim.EnemyHealthScale, sim.EnemyDamageScale, true);
                    int i = sim.Enemies.IndexOf(id);
                    if (room != null) sim.Enemies[i].RoomId = room.Id;
                    sim.GiveAffixes(id);
                }
                Debug.Log("[AutoTest] spawned elites");
            }
            // -fullfocus (host): the focus gauge kept full (screenshots of the ultimates; bots use them when full).
            if (CommandLine.Has("-fullfocus") && RunDirector.Instance?.Sim != null && Time.frameCount % 120 == 0)
                foreach (var p in RunDirector.Instance.Sim.Players) p.Focus = Game.Core.RunSimulation.FocusMax;
            // -wipeat <s> (host): the whole party falls, to test the result screen and going back to the camp together.
            if (_wipeAt > 0f && Time.time >= _wipeAt && RunDirector.Instance?.Sim != null && RunDirector.Instance.IsRunning)
            {
                _wipeAt = 0f;
                var sim = RunDirector.Instance.Sim;
                foreach (var p in sim.Players)
                {
                    p.InvulnerableTimer = 0f;
                    p.PhoenixUsed = true;
                    p.SecondWindUsed = true; // a start perk (the blessed wind) could otherwise keep one on 1 HP
                    p.Shield = 0f;
                    sim.HurtPlayer(p, 99999f);
                }
                Debug.Log("[AutoTest] wiped the party");
            }
            // -freezeat <s> -freezefor <s>: the main thread stalls once (a long hitch); packets pile up meanwhile.
            if (_freezeAt > 0f && Time.time >= _freezeAt)
            {
                _freezeAt = 0f;
                float seconds = CommandLine.GetFloat("-freezefor", 3f);
                Debug.Log($"[AutoTest] freezing for {seconds}s");
                System.Threading.Thread.Sleep(Mathf.RoundToInt(seconds * 1000f));
            }
            // -failat <s> (host): act as if the connection broke mid-run (the run is kept for "続きから", see -autoresume).
            if (_failAt > 0f && Time.time >= _failAt && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && RunDirector.Instance != null && RunDirector.Instance.IsRunning)
            {
                _failAt = 0f;
                Debug.Log("[AutoTest] simulating a transport failure");
                GameSession.SimulateTransportFailure();
            }
            // -dropat <s> (client): leave the session once; -autojoin/-onlinejoinfile then bring it back (reconnect test).
            if (_dropAt > 0f && Time.time >= _dropAt && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsHost)
            {
                _dropAt = 0f;
                Debug.Log("[AutoTest] dropping out to test reconnecting");
                GameSession.ReturnToLobby("（テスト）接続を切りました");
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
                BrowserBridge.ConsoleLog("[AutoTest] FINAL " + Status());
                Application.Quit();
            }
        }

        /// <summary>-perf: every 5 s a [Perf] line (average frame, share of slow frames, what is drawn, memory), for Tools/WebPerf.ps1.</summary>
        private void Measure()
        {
            if (!CommandLine.Has("-perf") || Time.frameCount < 10) return;
            float frame = Time.unscaledDeltaTime;
            _frameSum += frame;
            _frames++;
            if (frame > 1f / 30f) _slowFrames++;
            if (Time.time < _nextPerf) return;
            _nextPerf = Time.time + 5f;
            int sprites = 0, visible = 0, lights = 0;
            foreach (var r in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                sprites++;
                if (r.isVisible) visible++;
            }
            foreach (var l in FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None))
                if (l.enabled && l.gameObject.activeInHierarchy) lights++;
            float avg = _frames > 0 ? _frameSum / _frames * 1000f : 0f;
            BrowserBridge.ConsoleLog($"[Perf] t={Time.realtimeSinceStartup:0} avg={avg:0.0}ms slow={(_frames > 0 ? 100f * _slowFrames / _frames : 0f):0}% sprites={sprites} visible={visible} lights={lights} " +
                $"tex={Texture.currentTextureMemory / 1048576}MB mono={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576}MB total={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576}MB");
            _frameSum = 0f;
            _frames = _slowFrames = 0;
        }
        private float _nextPerf;

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
                : $"started={d.Started.Value} phase={d.Phase.Value} room={d.RoomIndex.Value} level={d.TeamLevel.Value} paused={d.Paused.Value} elapsed={d.Elapsed.Value} splits={d.RunSplits.Value}";
            string player = local == null ? "local=none" : $"local=P{local.Slot.Value + 1}:{local.Class.Value} hp={local.Hp.Value}/{local.MaxHp.Value} downed={local.Downed.Value} pos={(Vector2)local.transform.position}";
            return $"role={role} peers={peers} players={NetPlayer.All.Count} {run} enemiesVisible={enemyView?.VisibleCount ?? 0} events={_eventCount} {player} kills={(d != null && d.IsSpawned ? d.Kills.Value : 0)} shards={ProfileStore.Current.shards} loot={_loot[0]}/{_loot[1]}/{_loot[2]}/{_loot[3]} procs={_legendaryProcs}";
        }
    }
}
