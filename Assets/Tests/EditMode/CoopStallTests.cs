using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Co-op soak: three players of different classes wander the map apart (room to room, often into fights another
    /// player started), attacking and casting. The host's step must never throw, and a woken fight must always end.
    /// </summary>
    public class CoopStallTests
    {
        private const float Dt = 1f / 30f;

        [Test]
        public void ThreePlayersWanderingApart_NeverBreakTheStep()
        {
            var classes = new[] { ClassId.Warrior, ClassId.Mage, ClassId.Ranger, ClassId.Guardian, ClassId.Assassin, ClassId.Alchemist };
            var failures = new List<string>();
            for (ulong seed = 1; seed <= 24; seed++)
            {
                var sim = new RunSimulation(seed);
                var rng = new System.Random((int)seed);
                for (int i = 0; i < 3; i++)
                    sim.AddPlayer((ulong)i, new Loadout { classId = classes[(int)((seed + (ulong)i) % (ulong)classes.Length)] });
                sim.Start();
                var target = new Vector2[3];
                float t = 0f;
                try
                {
                    for (; t < 240f && sim.Phase == RunPhase.Playing; t += Dt)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            var p = sim.Players[i];
                            p.InvulnerableTimer = 999f;
                            foreach (var o in sim.Players) for (int k = 0; k < 10 && o.PendingOffer != null; k++) sim.ChoosePerk(o.ClientId, rng.Next(3));
                            // Now and then walk into another room (the owner moves; the host just reads positions).
                            if (rng.NextDouble() < 0.01)
                            {
                                var room = sim.Map.Rooms[rng.Next(sim.Map.Rooms.Count)];
                                if (room.Kind == RoomKind.Boss && rng.NextDouble() < 0.7) room = sim.Map.Start;
                                sim.SetPlayerPosition(p.ClientId, room.Origin + new Vector2(rng.Next(-3, 4), rng.Next(-3, 4)));
                            }
                            sim.SetPlayerInput(p.ClientId, new PlayerInput
                            {
                                Aim = new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f).normalized,
                                Attack = true,
                                Skill1Count = (byte)(t * 0.5f),
                                Skill2Count = (byte)(t * 0.3f),
                                DodgeCount = (byte)(t * 0.2f),
                            });
                        }
                        sim.Step(Dt);
                        sim.Events.Clear();
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"seed {seed} t={t:0.0}: {ex}");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n\n", failures));
        }
    }
}
