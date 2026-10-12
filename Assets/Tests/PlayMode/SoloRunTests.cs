using System.Collections;
using Game.Core;
using Game.Gameplay;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Starts a real solo (host) session from the lobby and checks the run comes alive.</summary>
    public class SoloRunTests
    {
        private int _events;
        private int _offers;

        private void Count(SimEvent e)
        {
            _events++;
            if (e.Type == SimEventType.PerkOffer) _offers++;
        }

        [UnityTest]
        public IEnumerator Solo_StartsRun_SpawnsPlayerAndEnemies_AndStreamsEvents()
        {
            RunDirector.EventReceived += Count;
            try
            {
                yield return SceneManager.LoadSceneAsync(GameSession.LobbyScene);
                Assert.IsTrue(GameSession.StartSolo(), "host failed to start");

                // Solo play starts in the camp: the player appears by the fire, then sets out.
                float deadline = Time.realtimeSinceStartup + 15f;
                while ((CampDirector.Instance == null || NetPlayer.Local == null) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsNotNull(CampDirector.Instance, "Camp scene did not load");
                Assert.IsNotNull(NetPlayer.Local, "no avatar in the camp");
                Assert.IsTrue(PlayArea.InCamp);
                GameSession.StartGame();

                deadline = Time.realtimeSinceStartup + 15f;
                while ((RunDirector.Instance == null || !RunDirector.Instance.Started.Value) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsNotNull(RunDirector.Instance, "Game scene did not load");
                Assert.IsTrue(RunDirector.Instance.Started.Value, "run did not start");

                yield return null;
                Assert.IsNotNull(NetPlayer.Local, "local player was not spawned");
                Assert.Greater(NetPlayer.Local.MaxHp.Value, 0f);

                // The run starts in the quiet entrance: step into a fight room of the map (the host teleports the player).
                var start = RunDirector.Instance.Sim;
                start.MoveTeamTo(start.FindRoom(RoomKind.Combat));

                // Keep the player alive and pick upgrades so the run keeps going.
                deadline = Time.realtimeSinceStartup + 12f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    var sim = RunDirector.Instance.Sim;
                    foreach (var p in sim.Players)
                    {
                        p.InvulnerableTimer = 1f;
                        if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
                    }
                    yield return null;
                }

                var director = RunDirector.Instance;
                Assert.Greater(director.Sim.Enemies.Count + _events, 0);
                Assert.Greater(_events, 20, "few events reached the presentation layer");
                Assert.Greater(Object.FindAnyObjectByType<EnemyView>().VisibleCount, 0, "no enemies on screen");
                Assert.Greater(director.Elapsed.Value, 5f);
            }
            finally
            {
                RunDirector.EventReceived -= Count;
                if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            }
            yield return null;
        }
    }
}
