using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Room gimmicks: spike traps and flame jets on a cycle (announced with HazardWarn, then HazardFire),
    /// healing fonts, and powder kegs that explode when broken. Traps hurt monsters too.
    /// </summary>
    public sealed partial class RunSimulation
    {
        /// <summary>Hazard ids: map room * <see cref="HazardIdsPerRoom"/> + the gimmick's index in the layout; keg explosions above <see cref="KegHazardIdBase"/>.</summary>
        public const int HazardIdsPerRoom = 64;
        public const int KegHazardIdBase = 100000;
        private const float HazardGrace = 2.5f;

        private sealed class Hazard
        {
            public int Id;
            public int Room;
            public GimmickDef Def;
            public float Clock;
            public int Phase;   // 0 idle, 1 warning, 2 active
            public int PlayerMask;
            public readonly List<int> HitEnemies = new();
            public float Tick;
            public float Hold;
            public bool Used;
        }

        private struct Explosion
        {
            public int Id;
            public int Room;
            public Vector2 Position;
            public float Timer;
        }

        private readonly List<Hazard> _hazards = new();
        private readonly List<Explosion> _explosions = new();

        /// <summary>A new area: no traps running anywhere.</summary>
        private void ResetHazards()
        {
            _hazards.Clear();
            _explosions.Clear();
        }

        /// <summary>A fight room wakes: its traps start their cycle (after a short grace).</summary>
        private void StartHazards(RoomState room)
        {
            for (int i = 0; i < room.Layout.Gimmicks.Length; i++)
            {
                var g = room.Layout.Gimmicks[i];
                if (g.Kind is GimmickKind.PowderKeg or GimmickKind.TreasureChest) continue;
                float period = g.Kind == GimmickKind.FlameJet ? Arena.FlamePeriod : Arena.SpikePeriod;
                _hazards.Add(new Hazard { Id = room.Id * HazardIdsPerRoom + i, Room = room.Id, Def = g, Clock = g.Phase * period - HazardGrace });
            }
        }

        /// <summary>Room cleared: its traps go quiet, healing fonts keep working.</summary>
        private void StopHazards(RoomState room)
        {
            _hazards.RemoveAll(h => h.Room == room.Id && h.Def.Kind != GimmickKind.HealingFont);
            _explosions.RemoveAll(x => x.Room == room.Id);
        }

        /// <summary>First visit: the room's kegs and chests appear (breakable "enemies").</summary>
        private void SpawnProps(RoomState room)
        {
            var chests = new List<int>();
            foreach (var g in room.Layout.Gimmicks)
            {
                if (g.Kind is not (GimmickKind.PowderKeg or GimmickKind.TreasureChest)) continue;
                int id = Enemies.Spawn(g.Kind == GimmickKind.PowderKeg ? EnemyType.PowderKeg : EnemyType.TreasureChest, g.Position, 1f, 1f, false);
                Enemies[Enemies.IndexOf(id)].RoomId = room.Id;
                if (g.Kind == GimmickKind.TreasureChest) chests.Add(id);
            }
            MaybeDisguise(room, chests); // RunSimulation.Events.cs
            if (room.Captive) SpawnCaptive(room);
        }

        /// <summary>A keg was broken: it hisses briefly, then explodes.</summary>
        private void IgniteKeg(in Enemy keg)
        {
            _explosions.Add(new Explosion { Id = KegHazardIdBase + keg.Id, Room = keg.RoomId, Position = keg.Position, Timer = Arena.KegFuse });
            Events.Add(new SimEvent(SimEventType.HazardWarn)
            {
                Id = KegHazardIdBase + keg.Id, Int = (int)GimmickKind.PowderKeg, Pos = keg.Position,
                Size = new Vector2(Arena.KegRadius, 0f), Value = Arena.KegFuse,
            });
        }

        private void StepHazards(float dt)
        {
            for (int k = _explosions.Count - 1; k >= 0; k--)
            {
                var x = _explosions[k];
                x.Timer -= dt;
                if (x.Timer > 0f)
                {
                    _explosions[k] = x;
                    continue;
                }
                _explosions.RemoveAt(k);
                Room = RoomStates[x.Room];
                Explode(x);
            }

            foreach (var h in _hazards)
            {
                Room = RoomStates[h.Room];
                switch (h.Def.Kind)
                {
                    case GimmickKind.SpikeTrap:
                        CycleTrap(h, dt, Arena.SpikePeriod, Arena.SpikeWarn, Arena.SpikeActive, new Vector2(Arena.SpikeSize, Arena.SpikeSize));
                        if (h.Phase == 2) SpikeHits(h);
                        break;
                    case GimmickKind.FlameJet:
                        CycleTrap(h, dt, Arena.FlamePeriod, Arena.FlameWarn, Arena.FlameActive, new Vector2(Arena.FlameLength, Arena.FlameWidth));
                        if (h.Phase == 2 && (h.Tick -= dt) <= 0f)
                        {
                            h.Tick = Arena.FlameTick;
                            FlameHits(h);
                        }
                        break;
                    case GimmickKind.HealingFont:
                        StepFont(h, dt);
                        break;
                }
            }
        }

        private void CycleTrap(Hazard h, float dt, float period, float warn, float active, Vector2 size)
        {
            h.Clock += dt;
            if (h.Clock < 0f) return;
            float t = Mathf.Repeat(h.Clock, period);
            int phase = t < period - warn - active ? 0 : t < period - active ? 1 : 2;
            if (phase == h.Phase) return;
            h.Phase = phase;
            if (phase == 1)
            {
                Events.Add(new SimEvent(SimEventType.HazardWarn) { Id = h.Id, Int = (int)h.Def.Kind, Pos = h.Def.Position, Vec = h.Def.Direction, Size = size, Value = warn });
            }
            else if (phase == 2)
            {
                h.PlayerMask = 0;
                h.HitEnemies.Clear();
                h.Tick = 0f;
                Events.Add(new SimEvent(SimEventType.HazardFire) { Id = h.Id, Int = (int)h.Def.Kind, Pos = h.Def.Position, Vec = h.Def.Direction, Size = size, Value = active });
            }
        }

        private void SpikeHits(Hazard h)
        {
            for (int pi = 0; pi < Players.Count; pi++)
            {
                var p = Players[pi];
                if (!p.Alive || (h.PlayerMask & (1 << pi)) != 0 || !Arena.InGimmick(h.Def, p.Position, SimPlayer.BodyRadius * 0.5f)) continue;
                h.PlayerMask |= 1 << pi;
                HurtPlayer(p, Arena.SpikeDamage * EnemyDamageScale);
            }
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Def.Flying || e.Def.Prop || h.HitEnemies.Contains(e.Id) || !Arena.InGimmick(h.Def, e.Position, e.Radius * 0.5f)) continue;
                h.HitEnemies.Add(e.Id);
                DamageEnemy(i, e.MaxHealth * (e.Def.Boss ? 0.025f : 0.3f), null, HitFlags.Heavy, Vector2.zero, 0f);
            }
        }

        private void FlameHits(Hazard h)
        {
            foreach (var p in Players)
                if (p.Alive && Arena.InGimmick(h.Def, p.Position, SimPlayer.BodyRadius * 0.5f))
                    HurtPlayer(p, Arena.FlameDamage * EnemyDamageScale);
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || !Arena.InGimmick(h.Def, e.Position, e.Radius * 0.5f)) continue;
                DamageEnemy(i, e.MaxHealth * (e.Def.Boss ? 0.008f : 0.09f) + 1f, null, HitFlags.None, Vector2.zero, 0f);
            }
        }

        private void StepFont(Hazard h, float dt)
        {
            if (h.Used) return;
            bool someone = false;
            foreach (var p in Players)
                if (p.Alive && (p.Position - h.Def.Position).sqrMagnitude <= Arena.FontRadius * Arena.FontRadius)
                    someone = true;
            h.Hold = someone ? h.Hold + dt : Mathf.Max(0f, h.Hold - dt);
            if (h.Hold < Arena.FontHoldTime) return;

            h.Used = true;
            foreach (var p in Players)
            {
                if ((p.Position - h.Def.Position).sqrMagnitude > Arena.FontHealRadius * Arena.FontHealRadius) continue;
                if (!p.Alive)
                {
                    p.ReviveProgress += 0.5f;
                    continue;
                }
                float before = p.Hp;
                p.Heal(p.MaxHp * Arena.FontHeal);
                Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
            }
            Events.Add(new SimEvent(SimEventType.HazardFire) { Id = h.Id, Int = (int)GimmickKind.HealingFont, Pos = h.Def.Position, Size = new Vector2(Arena.FontHealRadius, 0f) });
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.HolyLight, Pos = h.Def.Position, Value = Arena.FontHealRadius });
        }

        private void Explode(in Explosion x)
        {
            float r = Arena.KegRadius;
            Events.Add(new SimEvent(SimEventType.HazardFire) { Id = x.Id, Int = (int)GimmickKind.PowderKeg, Pos = x.Position, Size = new Vector2(r, 0f) });
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Explosion, Pos = x.Position, Value = r });
            foreach (var p in Players)
                if (p.Alive && (p.Position - x.Position).sqrMagnitude <= (r + SimPlayer.BodyRadius) * (r + SimPlayer.BodyRadius))
                    HurtPlayer(p, Arena.KegDamageToPlayers * EnemyDamageScale);

            Enemies.QueryCircle(x.Position, r, _hits);
            Vector2 center = x.Position;
            foreach (int i in _hits.ToArray())
            {
                ref var e = ref Enemies[i];
                float damage = e.Def.Prop ? 99f : e.Def.Boss ? e.MaxHealth * 0.05f : e.MaxHealth * 0.6f + 25f;
                Vector2 push = (e.Position - center).sqrMagnitude > 0.0001f ? (e.Position - center).normalized : Vector2.up;
                DamageEnemy(i, damage, null, HitFlags.Heavy, push, 9f);
            }
        }

        /// <summary>For tests and bots: is this point inside an active or warned trap?</summary>
        public bool HazardActiveAt(Vector2 p)
        {
            foreach (var h in _hazards)
                if (h.Phase > 0 && h.Def.Kind != GimmickKind.HealingFont && Arena.InGimmick(h.Def, p, 0f))
                    return true;
            return false;
        }
    }
}
