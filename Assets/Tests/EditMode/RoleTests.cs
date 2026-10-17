using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Warbands: guards cover their band, healers mend it.</summary>
    public class RoleTests
    {
        private const float Dt = 1f / 30f;

        private static List<SimEvent> Run(RunSimulation sim, float seconds)
        {
            var events = new List<SimEvent>();
            for (float t = 0; t < seconds; t += Dt)
            {
                foreach (var p in sim.Players) p.InvulnerableTimer = 999f;
                sim.Step(Dt);
                events.AddRange(sim.Events);
                sim.Events.Clear();
            }
            return events;
        }

        private static (RunSimulation, SimPlayer) Setup()
        {
            var sim = new RunSimulation(37);
            var p = sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            p.Position = new Vector2(-12f, 0f);
            return (sim, p);
        }

        private static int Spawn(RunSimulation sim, EnemyType type, Vector2 at, EnemyRole role)
        {
            int id = sim.Enemies.Spawn(type, at, 5f, 1f, false);
            int i = sim.Enemies.IndexOf(id);
            sim.Enemies[i].Role = role;
            sim.Enemies[i].Cooldown = 99f;
            return id;
        }

        [Test]
        public void MonstersBesideAGuard_TakeLess()
        {
            var (sim, p) = Setup();
            int archer = Spawn(sim, EnemyType.SkeletonArcher, Vector2.zero, EnemyRole.None);
            int i = sim.Enemies.IndexOf(archer);
            float hp = sim.Enemies[i].Health;
            sim.DamageEnemy(i, 100f, null, HitFlags.None, Vector2.zero, 0f); // no attacker: no critical hits
            Assert.AreEqual(100f, hp - sim.Enemies[i].Health, 0.5f, "no guard: all of it");
            Spawn(sim, EnemyType.ShieldSkeleton, new Vector2(1f, 0f), EnemyRole.Guard);
            hp = sim.Enemies[i].Health;
            sim.DamageEnemy(i, 100f, null, HitFlags.None, Vector2.zero, 0f); // no attacker: no critical hits
            Assert.AreEqual(100f * (1f - RunSimulation.GuardCover), hp - sim.Enemies[i].Health, 0.5f, "covered");
        }

        [Test]
        public void AHealer_MendsTheBand()
        {
            var (sim, p) = Setup();
            int healer = Spawn(sim, EnemyType.SkeletonArcher, Vector2.zero, EnemyRole.Healer);
            int friend = Spawn(sim, EnemyType.Skeleton, new Vector2(1.5f, 0f), EnemyRole.None);
            int f = sim.Enemies.IndexOf(friend);
            sim.Enemies[f].Health = sim.Enemies[f].MaxHealth * 0.5f;
            var events = Run(sim, 0.5f);
            f = sim.Enemies.IndexOf(friend);
            Assert.Greater(sim.Enemies[f].Health, sim.Enemies[f].MaxHealth * 0.55f, "mended");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.EnemyRole && e.Int == 100 && e.Id == healer), "seen");
        }

        [Test]
        public void Warbands_ComeWithRoles()
        {
            var sim = new RunSimulation(41) { ForcedWarband = true };
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.Start(RunSimulation.RoomsPerArea - 2);
            var events = new List<SimEvent>();
            foreach (var room in sim.Map.Rooms.Where(r => r.Kind == RoomKind.Combat))
            {
                sim.MoveTeamTo(room);
                events.AddRange(Run(sim, 3f));
                if (events.Any(e => e.Type == SimEventType.EnemyRole && e.Int == (int)EnemyRole.Guard)) break;
            }
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.EnemyRole && e.Int == (int)EnemyRole.Guard), "guards");
        }
    }
}
