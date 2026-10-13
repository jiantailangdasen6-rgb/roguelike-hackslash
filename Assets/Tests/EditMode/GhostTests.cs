using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Downed players drift on as ghosts: the body stays where it fell, the ghost slows monsters and throws sparks.</summary>
    public class GhostTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds, System.Action<RunSimulation> eachTick = null)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                eachTick?.Invoke(sim);
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static (RunSimulation Sim, SimPlayer Down, SimPlayer Mate) TwoPlayers()
        {
            var sim = new RunSimulation(17);
            var down = sim.AddPlayer(0, new Loadout { classId = ClassId.Mage });
            var mate = sim.AddPlayer(1, new Loadout { classId = ClassId.Warrior });
            sim.Start();
            Run(sim, 0.1f);
            down.Position = sim.Map.Start.Origin + new Vector2(-4f, 0f);
            mate.Position = sim.Map.Start.Origin + new Vector2(8f, 4f);
            sim.HurtPlayer(down, 99999f);
            return (sim, down, mate);
        }

        [Test]
        public void TheBodyStays_AndTeammatesReviveItThere_WhereverTheGhostDrifts()
        {
            var (sim, down, mate) = TwoPlayers();
            Assert.IsFalse(down.Alive);
            Vector2 body = down.BodyPosition;
            Vector2 ghost = body + new Vector2(6f, -2f);

            // The mate stands by the ghost: nothing happens.
            Run(sim, 1f, s => { down.Position = ghost; mate.Position = ghost + new Vector2(0.5f, 0f); });
            Assert.Less(down.ReviveProgress, 0.05f, "the ghost is not the body");

            // By the body: up again, and the ghost is pulled back into it.
            var events = Run(sim, RunSimulation.ReviveTime + 0.5f, s => { if (!down.Alive) down.Position = ghost; mate.Position = body + new Vector2(0.6f, 0f); });
            Assert.IsTrue(down.Alive);
            Assert.Less((down.Position - body).magnitude, 0.01f);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Teleport && e.ClientId == down.ClientId));
        }

        [Test]
        public void AGhostSlowsMonstersNearby_AndThrowsSparks()
        {
            var (sim, down, mate) = TwoPlayers();
            sim.Enemies.Spawn(EnemyType.Skeleton, down.Position + new Vector2(3f, 0f), 20f, 1f, false);
            int e = sim.Enemies.Count - 1;
            sim.Enemies[e].RoomId = sim.Map.Start.Id;

            var events = Run(sim, 1.2f, s =>
            {
                down.Position = s.Enemies[e].Position + new Vector2(-0.8f, 0f);
                s.SetPlayerInput(down.ClientId, new PlayerInput { Aim = Vector2.right, Attack = true });
            });
            Assert.Greater(sim.Enemies[e].Haunted, 0f, "haunted");
            int sparks = events.Count(ev => ev.Type == SimEventType.ProjectileSpawn && (ProjectileKind)ev.Int == ProjectileKind.Wisp);
            Assert.GreaterOrEqual(sparks, 2);
            Assert.Less(sim.Enemies[e].Health, sim.Enemies[e].MaxHealth, "the sparks hurt");
        }

        [Test]
        public void AGhostStillGathersSouls()
        {
            var (sim, down, _) = TwoPlayers();
            Vector2 ghost = down.BodyPosition + new Vector2(5f, 0f);
            sim.Enemies.Spawn(EnemyType.Skeleton, ghost, 0.01f, 1f, false);
            int e = sim.Enemies.Count - 1;
            sim.Enemies[e].RoomId = sim.Map.Start.Id;
            sim.DamageEnemy(e, 999f, null, HitFlags.None, Vector2.zero, 0f);
            Run(sim, 0.1f, s => down.Position = down.BodyPosition); // the soul drops, the ghost is away
            Assert.AreEqual(1, sim.Souls.Count);
            Run(sim, 0.3f, s => down.Position = ghost);
            Assert.AreEqual(0, sim.Souls.Count, "the ghost picked it up");
        }
    }
}
