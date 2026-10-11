using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>Room layouts and gimmicks, new monsters and bosses, difficulty, skins.</summary>
    public class Round2Tests
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

        private static void StartRoom(RunSimulation sim, int index) =>
            typeof(RunSimulation).GetMethod("StartRoom", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(sim, new object[] { index });

        private static void UseLayout(RunSimulation sim, int id) =>
            typeof(RunSimulation).GetProperty("Layout").GetSetMethod(true).Invoke(sim, new object[] { Arena.Get(id) });

        private static void Invulnerable(RunSimulation sim)
        {
            foreach (var p in sim.Players)
            {
                p.InvulnerableTimer = 1f;
                if (p.PendingOffer != null) sim.ChoosePerk(p.ClientId, 0);
            }
        }

        private static int Spawn(RunSimulation sim, EnemyType type, Vector2 at, float hpMul = 1f)
        {
            sim.Enemies.Spawn(type, at, hpMul, 1f, false);
            return sim.Enemies.Count - 1;
        }

        // ------------------------------------------------------------------ Layouts

        [Test]
        public void Layouts_KeepStartExitAndBossSpawnClear()
        {
            var starts = new[] { new Vector2(0f, -4f), new Vector2(-1.5f, -4.8f), new Vector2(1.5f, -4.8f) };
            var exit = new Vector2(0f, RunSimulation.DefaultBounds.yMax - 1.2f);
            foreach (var layout in Arena.Layouts)
            {
                foreach (var s in starts) Assert.IsFalse(Arena.Overlaps(layout, s, 0.6f), $"{layout.Name}: start blocked");
                Assert.IsFalse(Arena.Overlaps(layout, exit, RunSimulation.ExitRadius), $"{layout.Name}: exit blocked");
                if (layout.Boss) Assert.IsFalse(Arena.Overlaps(layout, new Vector2(0f, 3f), 1f), $"{layout.Name}: boss spawn blocked");
                foreach (var g in layout.Gimmicks)
                {
                    Assert.IsTrue(RunSimulation.DefaultBounds.Contains(g.Position) || g.Kind == GimmickKind.FlameJet, $"{layout.Name}: gimmick outside");
                    foreach (var s in starts) Assert.IsFalse(Arena.InGimmick(g, s, 0.3f), $"{layout.Name}: trap on a start point");
                }
            }
        }

        [Test]
        public void Resolve_PushesCirclesOutOfObstacles()
        {
            var layout = Arena.Get(1); // four pillars
            var pillar = layout.Obstacles[0];
            Vector2 inside = pillar.Position;
            Vector2 pushed = Arena.Resolve(layout, inside, 0.4f);
            Assert.IsFalse(Arena.Overlaps(layout, pushed, 0.39f));
            Assert.IsTrue(Arena.BlocksShot(layout, pillar.Position), "pillars stop projectiles");
        }

        [Test]
        public void Rooms_UseVariedLayouts_FirstOpen_BossThrone()
        {
            var sim = new RunSimulation(21);
            sim.AddPlayer(0);
            sim.Start();
            Assert.AreEqual(0, sim.Layout.Id);
            var seen = new HashSet<int>();
            for (int room = 1; room < RunSimulation.TotalRooms; room++)
            {
                StartRoom(sim, room);
                if (room % RunSimulation.RoomsPerArea == RunSimulation.RoomsPerArea - 1) Assert.IsTrue(sim.Layout.Boss);
                else seen.Add(sim.Layout.Id);
            }
            Assert.GreaterOrEqual(seen.Count, 4, "rooms should not all look the same");
        }

        // ------------------------------------------------------------------ Gimmicks

        [Test]
        public void SpikeTrap_WarnsThenHurtsPlayersAndMonsters()
        {
            var sim = new RunSimulation(22);
            var p = sim.AddPlayer(0);
            sim.Start();
            StartRoom(sim, 4); // spike hall or whatever layout comes; force the spike hall below
            UseLayout(sim, 4);
            typeof(RunSimulation).GetMethod("ResetHazards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sim, null);

            var trap = Arena.Get(4).Gimmicks[0];
            int e = Spawn(sim, EnemyType.Skeleton, trap.Position, 1f);
            sim.Enemies[e].Cooldown = 99f;
            float hp = sim.Enemies[e].Health;
            p.Position = trap.Position + new Vector2(0.2f, 0f);
            float playerHp = p.Hp;

            var events = Run(sim, 8f, s =>
            {
                s.SetPlayerPosition(0, trap.Position + new Vector2(0.2f, 0f));
                for (int i = 0; i < s.Enemies.Count; i++) { s.Enemies[i].Position = trap.Position; s.Enemies[i].Cooldown = 99f; }
            });

            Assert.That(events.Any(x => x.Type == SimEventType.HazardWarn && x.Int == (int)GimmickKind.SpikeTrap), "spikes warn first");
            Assert.That(events.Any(x => x.Type == SimEventType.HazardFire && x.Int == (int)GimmickKind.SpikeTrap));
            Assert.Less(p.Hp, playerHp, "spikes hurt players");
            Assert.That(events.Any(x => x.Type == SimEventType.EnemyHit && (x.Int & (int)HitFlags.Heavy) != 0) || sim.Enemies.Count == 0 || sim.Enemies[0].Health < hp, "spikes hurt monsters");
        }

        [Test]
        public void PowderKeg_ExplodesWhenBroken_AndHurtsMonsters()
        {
            var sim = new RunSimulation(23);
            sim.AddPlayer(0).Position = new Vector2(-10f, -6f);
            int keg = Spawn(sim, EnemyType.PowderKeg, Vector2.zero);
            int skel = Spawn(sim, EnemyType.Skeleton, new Vector2(1.2f, 0f), 1f);
            sim.Enemies[skel].Cooldown = 99f;
            Assert.AreEqual(1, sim.Enemies.HostileCount, "kegs are not monsters");

            sim.DamageEnemy(keg, 5f, sim.Players[0], HitFlags.None, Vector2.zero, 0f);
            var events = Run(sim, 1f, s => { for (int i = 0; i < s.Enemies.Count; i++) s.Enemies[i].Cooldown = 99f; });

            Assert.That(events.Any(x => x.Type == SimEventType.HazardWarn && x.Int == (int)GimmickKind.PowderKeg), "fuse");
            Assert.That(events.Any(x => x.Type == SimEventType.AreaEffect && x.Int == (int)AreaKind.Explosion), "boom");
            Assert.That(events.Any(x => x.Type == SimEventType.EnemyDied && x.Int == (int)EnemyType.Skeleton), "the blast kills the nearby skeleton");
            Assert.AreEqual(1, sim.Kills, "the keg itself is not a kill");
        }

        [Test]
        public void HealingFont_HealsEveryoneNearby_Once()
        {
            var sim = new RunSimulation(24);
            var a = sim.AddPlayer(0);
            var b = sim.AddPlayer(1);
            sim.Start();
            UseLayout(sim, 7);
            typeof(RunSimulation).GetMethod("ResetHazards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(sim, null);
            var font = Arena.Get(7).Gimmicks.First(g => g.Kind == GimmickKind.HealingFont);
            a.Hp = 10f;
            b.Hp = 10f;
            var events = Run(sim, 1.5f, s =>
            {
                Invulnerable(s);
                s.SetPlayerPosition(0, font.Position);
                s.SetPlayerPosition(1, font.Position + new Vector2(2.5f, 0f));
            });
            Assert.Greater(a.Hp, 30f);
            Assert.Greater(b.Hp, 30f, "teammates nearby are healed too");
            Assert.AreEqual(1, events.Count(x => x.Type == SimEventType.HazardFire && x.Int == (int)GimmickKind.HealingFont));
        }

        // ------------------------------------------------------------------ Monsters

        [Test]
        public void DreadKnight_ArmourBlocks_UntilMarked()
        {
            var sim = new RunSimulation(25);
            sim.AddPlayer(0);
            int k = Spawn(sim, EnemyType.DreadKnight, new Vector2(3f, 0f), 10f);
            float hp = sim.Enemies[k].Health;
            sim.DamageEnemy(k, 100f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(100f * EnemyCatalog.ArmorFactor, hp - sim.Enemies[k].Health, 0.01f);

            hp = sim.Enemies[k].Health;
            sim.Enemies[k].Marked = 3f;
            sim.Enemies[k].MarkBonus = 0f;
            sim.DamageEnemy(k, 100f, null, HitFlags.None, Vector2.zero, 0f);
            Assert.AreEqual(100f, hp - sim.Enemies[k].Health, 0.01f, "a marked knight takes full damage");
        }

        [Test]
        public void Bloater_BurstsNextToPlayer_AndDies()
        {
            var sim = new RunSimulation(26);
            var p = sim.AddPlayer(0);
            p.Position = Vector2.zero;
            int b = Spawn(sim, EnemyType.Bloater, new Vector2(1.2f, 0f));
            sim.Enemies[b].Cooldown = 0f;
            var events = Run(sim, 1.6f);
            Assert.That(events.Any(x => x.Type == SimEventType.AreaEffect && x.Int == (int)AreaKind.Explosion));
            Assert.That(events.Any(x => x.Type == SimEventType.EnemyDied && x.Int == (int)EnemyType.Bloater));
            Assert.That(events.Any(x => x.Type == SimEventType.PlayerHurt));
        }

        [Test]
        public void Wraith_BlinksToWhereItTelegraphed()
        {
            var sim = new RunSimulation(27);
            var p = sim.AddPlayer(0);
            p.Position = Vector2.zero;
            int w = Spawn(sim, EnemyType.Wraith, new Vector2(6f, 0f));
            sim.Enemies[w].Cooldown = 0f;
            var events = Run(sim, 1.2f, s => s.SetPlayerPosition(0, new Vector2(-2f, 0f)));
            var tele = events.First(x => x.Type == SimEventType.EnemyTelegraph);
            Assert.AreEqual((int)AttackShape.Circle, tele.Int);
            Assert.That(events.Any(x => x.Type == SimEventType.EnemyStrike && x.Int == (int)AttackKind.Blink));
            Assert.Less(Vector2.Distance(sim.Enemies[0].Position, tele.Pos), 1.5f, "it reappears inside its circle");
        }

        [Test]
        public void EachArea_HasItsOwnBoss()
        {
            for (int area = 0; area < RunSimulation.AreaCount; area++)
            {
                var sim = new RunSimulation((ulong)(30 + area));
                sim.AddPlayer(0);
                sim.Start();
                StartRoom(sim, area * RunSimulation.RoomsPerArea + RunSimulation.RoomsPerArea - 1);
                var events = Run(sim, 2.5f, Invulnerable);
                var boss = events.First(x => x.Type == SimEventType.BossSpawned);
                Assert.AreEqual((int)EnemyCatalog.AreaBosses[area], boss.Int);
                // Let it fight for a while: every boss pattern runs without errors.
                events = Run(sim, 25f, Invulnerable);
                Assert.That(events.Any(x => x.Type == SimEventType.EnemyTelegraph), $"area {area} boss attacks");
            }
        }

        [Test]
        public void BossCharge_IntoAPillar_StunsIt()
        {
            var sim = new RunSimulation(40);
            var p = sim.AddPlayer(0);
            sim.Start();
            UseLayout(sim, 10);
            var pillar = Arena.Get(10).Obstacles[0]; // (-9, 3.5)
            int k = Spawn(sim, EnemyType.BoneKing, pillar.Position + new Vector2(6f, 0f), 10f);
            p.Position = pillar.Position + new Vector2(-1.5f, 0f); // behind the pillar
            sim.Enemies[k].Cooldown = 0f;
            var events = Run(sim, 3f, s => { Invulnerable(s); s.SetPlayerPosition(0, pillar.Position + new Vector2(-1.5f, 0f)); });
            Assert.That(events.Any(x => x.Type == SimEventType.StatusApplied && x.Int == (int)StatusKind.Stunned), "the king staggers after hitting the pillar");
        }

        // ------------------------------------------------------------------ Difficulty and meta

        [Test]
        public void Difficulty_ScalesMonsters_AndShards()
        {
            var easy = new RunSimulation(50, null, Difficulty.Easy);
            var hard = new RunSimulation(50, null, Difficulty.Hard);
            easy.AddPlayer(0);
            hard.AddPlayer(0);
            Assert.Less(easy.EnemyHealthScale, hard.EnemyHealthScale);
            Assert.Less(easy.EnemyDamageScale, hard.EnemyDamageScale);
            Assert.Greater(MetaCatalog.ShardsForRun(4, 1, 80, false, Difficulty.Hard), MetaCatalog.ShardsForRun(4, 1, 80, false, Difficulty.Normal));
        }

        [Test]
        public void Nightmare_UnlocksAfterAHardVictory()
        {
            var profile = ProfileData.Default();
            Assert.IsFalse(profile.IsDifficultyOpen(Difficulty.Nightmare));
            profile.RecordVictory(Difficulty.Normal);
            Assert.IsFalse(profile.IsDifficultyOpen(Difficulty.Nightmare));
            profile.RecordVictory(Difficulty.Hard);
            Assert.IsTrue(profile.IsDifficultyOpen(Difficulty.Nightmare));
            Assert.AreEqual((int)Difficulty.Hard, ProfileData.FromJson(profile.ToJson()).bestVictory);
        }

        [Test]
        public void Skins_BoughtWithShards_TravelInTheLoadout()
        {
            var profile = ProfileData.Default();
            profile.selectedClass = ClassId.Mage;
            profile.SetSkin(ClassId.Mage, 2);
            Assert.AreEqual(0, profile.ToLoadout().skin, "locked skins fall back to the default look");
            profile.shards = 500;
            var skin = SkinCatalog.Get(ClassId.Mage, 2);
            Assert.IsTrue(profile.TryBuy(skin.Id, skin.Cost));
            Assert.AreEqual(2, profile.ToLoadout().skin);
            Assert.AreEqual("mage_s2", SkinCatalog.Sheet(ClassId.Mage, 2));
            Assert.AreEqual(2, Loadout.FromJson(profile.ToLoadout().ToJson()).skin);
        }

        [Test]
        public void FullRun_WithBots_OnHard_RunsWithoutErrors()
        {
            var sim = new RunSimulation(60, null, Difficulty.Hard);
            sim.AddPlayer(0, new Loadout { classId = ClassId.Warrior });
            sim.AddPlayer(1, new Loadout { classId = ClassId.Mage });
            sim.Start();
            // Jump through every room, letting each run a little with players invulnerable.
            for (int room = 0; room < RunSimulation.TotalRooms; room++)
            {
                StartRoom(sim, room);
                Run(sim, 6f, s =>
                {
                    Invulnerable(s);
                    foreach (var p in s.Players)
                    {
                        int n = s.Enemies.FindNearest(p.Position, 30f);
                        s.SetPlayerInput(p.ClientId, new PlayerInput { Aim = n >= 0 ? s.Enemies[n].Position - p.Position : Vector2.right, Attack = true });
                    }
                });
            }
            Assert.Greater(sim.Kills, 0);
        }
    }
}
