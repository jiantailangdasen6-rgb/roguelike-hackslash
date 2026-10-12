using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>A boss's trial: a phase where plain damage isn't enough and the team has to do something else.</summary>
    public enum BossTrial : byte
    {
        None,
        BoneWard,     // Bone King, half health: warded until his bone braziers are smashed
        BloodRitual,  // Blood Bishop, 70% / 35%: light every blood sigil before the ritual ends
        Collapse,     // Lich, 66% / 33%: the floor's edge falls into the abyss (warning, then the arena shrinks)
        Phylactery,   // Lich at 0: break the soul vessel before he rises again
    }

    /// <summary>Moments of the trials, for the presentation (TrialEvent's Int).</summary>
    public enum TrialMoment : byte
    {
        CageClosed,    // ClientId caged at Pos
        CageBroken,    // ClientId freed
        CageCrushed,   // ClientId hurt by the cage
        WardUp,        // Pos = boss
        WardBroken,    // Pos = boss
        RitualStart,   // Pos = boss, Value = seconds
        SigilLit,      // Pos = sigil
        RitualBroken,  // Pos = boss
        RitualFailed,  // Pos = boss
        CollapseWarn,  // Value = seconds, Int2 (Id) = the stage about to fall
        Collapsed,     // Id = stage
        VesselRise,    // Pos = vessel, Value = seconds
        VesselBroken,  // Pos = vessel
        LichRises,     // Pos = boss
    }

    /// <summary>
    /// Boss trials (gimmicks) and other hostile bursts. Each area's lord has one: the Bone King's braziers and
    /// bone cage, the Blood Bishop's sigil ritual, the Lich's collapsing floor and phylactery. Breaking a trial
    /// leaves the boss stunned and exposed (more damage taken) for a few seconds - the reward for teamwork.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float CageTime = 4f;
        public const float CageCrush = 0.3f;     // share of max HP
        public const float ExposedTime = 4.5f;
        public const float ExposedBonus = 0.5f;
        public const float SigilRadius = 1.4f;
        public const float SigilChargeTime = 1f;
        public const float RitualTorrent = 0.35f; // share of max HP when a ritual completes
        public const float CollapseWarnTime = 2.6f;
        public const float VesselTime = 12f;

        public BossTrial ActiveTrial { get; private set; }
        /// <summary>Seconds left on a timed trial (ritual, vessel) or until the floor falls (0 = no timer).</summary>
        public float TrialTimer { get; private set; }
        public int TrialDone { get; private set; }
        public int TrialTotal { get; private set; }
        /// <summary>How much of the Lich's floor has fallen (0..2): the walkable area is <see cref="SafeArea"/>.</summary>
        public int CollapseStage { get; private set; }

        private int _trialsPassed; // HP thresholds already used (bit per threshold)
        private float _exposed;
        private float _ritualRain;
        private readonly List<int> _trialProps = new();

        private struct HostileBurst
        {
            public int Id;
            public Vector2 Position;
            public float Radius;
            public float Timer;
            public float Damage;
            public AreaKind Fx;
        }

        private readonly List<HostileBurst> _bursts = new();
        private int _nextBurstId = -1; // negative: never clashes with a monster id in the telegraph events

        private bool Wrath => DifficultyDef.Has(DifficultyMod.LordsWrath);

        /// <summary>The floor left after <paramref name="stage"/> collapses (the whole room at stage 0).</summary>
        public static Rect SafeArea(Rect bounds, int stage)
        {
            if (stage <= 0) return bounds;
            float ix = 2.6f * stage, iy = 1.6f * stage;
            return new Rect(bounds.xMin + ix, bounds.yMin + iy, bounds.width - 2f * ix, bounds.height - 2f * iy);
        }

        /// <summary>Where players and walkers may stand right now.</summary>
        public Rect PlayRect => Room.Kind == RoomKind.Boss ? SafeArea(Bounds, CollapseStage) : Bounds;

        /// <summary>The boss is warded: hits do nothing.</summary>
        private bool BossWarded => ActiveTrial is BossTrial.BoneWard or BossTrial.BloodRitual or BossTrial.Phylactery;

        /// <summary>The boss stands still channelling (no attacks).</summary>
        private bool BossChannelling => ActiveTrial == BossTrial.BloodRitual;

        private void ResetBossTrials()
        {
            ActiveTrial = BossTrial.None;
            TrialTimer = 0f;
            TrialDone = TrialTotal = 0;
            CollapseStage = 0;
            _trialsPassed = 0;
            _exposed = 0f;
            _trialProps.Clear();
            _bursts.Clear();
            foreach (var p in Players) p.CagedBy = 0;
        }

        /// <summary>Armoured: dread knights always, elites too under "iron guard".</summary>
        private bool IsArmored(in Enemy e) => e.Def.Armored || (e.Elite && DifficultyDef.Has(DifficultyMod.IronGuard));

        private SimPlayer RandomLivingPlayer()
        {
            int alive = 0;
            foreach (var p in Players) if (p.Alive && p.CagedBy == 0) alive++;
            if (alive == 0) return null;
            int pick = _aiRng.Range(0, alive);
            foreach (var p in Players)
            {
                if (!p.Alive || p.CagedBy != 0) continue;
                if (pick-- == 0) return p;
            }
            return null;
        }

        private void TrialEvent(TrialMoment moment, Vector2 at, float value = 0f, ulong client = 0, int id = 0) =>
            Events.Add(new SimEvent(SimEventType.TrialEvent) { Int = (int)moment, Pos = at, Value = value, ClientId = client, Id = id });

        private int BossIndex()
        {
            for (int i = 0; i < Enemies.Count; i++)
                if (!Enemies[i].Dead && Enemies[i].Def.Boss) return i;
            return -1;
        }

        // ------------------------------------------------------------------ Per tick

        private void StepBossTrials(float dt)
        {
            StepCages(dt);
            StepBursts(dt);
            var bossRoom = RoomStates[Map.Boss.Id];
            if (!bossRoom.Fighting) return;
            Room = bossRoom;
            _exposed -= dt;

            int b = BossIndex();
            if (b < 0) return;
            ref var boss = ref Enemies[b];
            float hp = boss.Health / boss.MaxHealth;

            switch (ActiveTrial)
            {
                case BossTrial.None:
                    switch (boss.Type)
                    {
                        case EnemyType.BoneKing:
                            if (Threshold(0, hp, Wrath ? 0.6f : 0.5f)) BeginBoneWard(b);
                            break;
                        case EnemyType.BloodBishop:
                            if (Threshold(0, hp, 0.7f) || Threshold(1, hp, 0.35f)) BeginRitual(b);
                            break;
                        case EnemyType.Lich:
                            if (Threshold(0, hp, 0.66f) || Threshold(1, hp, 0.33f)) BeginCollapse();
                            break;
                    }
                    break;
                case BossTrial.BoneWard:
                    TrialDone = TrialTotal - LivingTrialProps();
                    if (TrialDone >= TrialTotal) BreakTrial(b, TrialMoment.WardBroken);
                    break;
                case BossTrial.BloodRitual:
                    StepRitual(b, dt);
                    break;
                case BossTrial.Collapse:
                    TrialTimer -= dt;
                    if (TrialTimer <= 0f) Collapse();
                    break;
                case BossTrial.Phylactery:
                    TrialTimer -= dt;
                    if (LivingTrialProps() == 0)
                    {
                        // The vessel is broken: the Lich falls for good.
                        ActiveTrial = BossTrial.None;
                        TrialEvent(TrialMoment.VesselBroken, boss.Position);
                        boss.Dead = true;
                    }
                    else if (TrialTimer <= 0f)
                    {
                        RemoveTrialProps();
                        ActiveTrial = BossTrial.None;
                        boss.Health = boss.MaxHealth * 0.3f;
                        TrialEvent(TrialMoment.LichRises, boss.Position);
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.SoulBurst, Pos = boss.Position, Value = 4f });
                    }
                    break;
            }
        }

        /// <summary>True once when health first drops below a threshold.</summary>
        private bool Threshold(int bit, float hp, float at)
        {
            if (hp > at || (_trialsPassed & (1 << bit)) != 0) return false;
            _trialsPassed |= 1 << bit;
            return true;
        }

        private int LivingTrialProps()
        {
            int n = 0;
            foreach (int id in _trialProps)
            {
                int i = Enemies.IndexOf(id);
                if (i >= 0 && !Enemies[i].Dead) n++;
            }
            return n;
        }

        private void RemoveTrialProps()
        {
            foreach (int id in _trialProps)
            {
                int i = Enemies.IndexOf(id);
                if (i >= 0) Enemies[i].Dead = true;
            }
            _trialProps.Clear();
        }

        /// <summary>A trial overcome: the boss reels, exposed to everyone's blows.</summary>
        private void BreakTrial(int bossIndex, TrialMoment moment)
        {
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.None;
            TrialTimer = 0f;
            RemoveTrialProps();
            boss.Stunned = Mathf.Max(boss.Stunned, ExposedTime);
            boss.State = EnemyState.Chase;
            _rainTargets.Remove(boss.Id);
            _exposed = ExposedTime;
            TrialEvent(moment, boss.Position, ExposedTime);
            Events.Add(new SimEvent(SimEventType.StatusApplied) { Id = boss.Id, Int = (int)StatusKind.Stunned, Value = ExposedTime });
        }

        /// <summary>Spots spread around the room for trial objects (clear of obstacles, inside the floor left).</summary>
        private List<Vector2> TrialSpots(int count, float rx, float ry, float startAngle)
        {
            var spots = new List<Vector2>();
            var area = PlayRect;
            for (int k = 0; k < count; k++)
            {
                float a = (startAngle + k * 360f / count) * Mathf.Deg2Rad;
                var p = area.center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                p.x = Mathf.Clamp(p.x, area.xMin + 1.2f, area.xMax - 1.2f);
                p.y = Mathf.Clamp(p.y, area.yMin + 1.2f, area.yMax - 1.6f);
                spots.Add(Arena.Resolve(Layout, p, 1f));
            }
            return spots;
        }

        private int SpawnTrialProp(EnemyType type, Vector2 at, float healthScale)
        {
            int id = Enemies.Spawn(type, at, healthScale, 1f, false);
            Enemies[Enemies.IndexOf(id)].RoomId = Room.Id;
            _trialProps.Add(id);
            return id;
        }

        // ------------------------------------------------------------------ Bone King

        private void BeginBoneWard(int bossIndex)
        {
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.BoneWard;
            CancelAttack(ref boss);
            boss.Cooldown = 1.5f;
            int count = Wrath ? 4 : 3;
            foreach (var spot in TrialSpots(count, 9f, 5f, 90f))
            {
                SpawnTrialProp(EnemyType.BoneBrazier, spot, EnemyHealthScale);
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)EnemyType.BoneBrazier, Value = 0.3f });
            }
            TrialTotal = count;
            TrialDone = 0;
            TrialEvent(TrialMoment.WardUp, boss.Position);
        }

        /// <summary>Bone King's cage: closes on everyone caught in the circle.</summary>
        private void CloseCages(Vector2 at, float radius)
        {
            foreach (var p in Players)
            {
                if (!p.Alive || p.CagedBy != 0) continue;
                float r = radius + SimPlayer.BodyRadius;
                if ((p.Position - at).sqrMagnitude > r * r) continue;
                // A little in front of the player so the bars are drawn over them.
                int id = Enemies.Spawn(EnemyType.BoneCage, p.Position + new Vector2(0f, -0.06f), EnemyHealthScale, 1f, false);
                Enemies[Enemies.IndexOf(id)].RoomId = Room.Id;
                p.CagedBy = id;
                p.CageTimer = Wrath ? CageTime * 0.8f : CageTime;
                TrialEvent(TrialMoment.CageClosed, p.Position, p.CageTimer, p.ClientId);
            }
        }

        private void StepCages(float dt)
        {
            foreach (var p in Players)
            {
                if (p.CagedBy == 0) continue;
                int i = Enemies.IndexOf(p.CagedBy);
                if (i < 0 || Enemies[i].Dead || !p.Alive)
                {
                    if (i >= 0) Enemies[i].Dead = true;
                    p.CagedBy = 0;
                    TrialEvent(TrialMoment.CageBroken, p.Position, 0f, p.ClientId);
                    continue;
                }
                p.CageTimer -= dt;
                if (p.CageTimer > 0f) continue;
                Enemies[i].Dead = true;
                p.CagedBy = 0;
                TrialEvent(TrialMoment.CageCrushed, p.Position, 0f, p.ClientId);
                p.InvulnerableTimer = 0f;
                HurtPlayer(p, p.MaxHp * CageCrush);
            }
        }

        // ------------------------------------------------------------------ Blood Bishop

        private void BeginRitual(int bossIndex)
        {
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.BloodRitual;
            CancelAttack(ref boss);
            Vector2 center = Arena.Resolve(Layout, PlayRect.center + new Vector2(0f, 1f), boss.Radius);
            boss.Position = center;
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Slam, Pos = center, Value = 2.4f });

            TrialTotal = 3;
            TrialDone = 0;
            float start = _aiRng.Range(0f, 120f);
            foreach (var spot in TrialSpots(TrialTotal, 8.5f, 4.6f, start))
            {
                int id = SpawnTrialProp(EnemyType.BloodSigil, spot, 1f);
                ref var sigil = ref Enemies[Enemies.IndexOf(id)];
                sigil.Health = 0f;
                sigil.MaxHealth = 1f;
            }
            TrialTimer = (Players.Count == 1 ? 14f : 11f) - (Wrath ? 2.5f : 0f);
            _ritualRain = 1.5f;
            for (int k = 0; k < PlayersIn(Room); k++) QueueSpawn(EnemyType.Cultist, false);
            TrialEvent(TrialMoment.RitualStart, center, TrialTimer);
        }

        private void StepRitual(int bossIndex, float dt)
        {
            TrialTimer -= dt;
            foreach (int id in _trialProps)
            {
                int i = Enemies.IndexOf(id);
                if (i < 0) continue;
                ref var sigil = ref Enemies[i];
                if (sigil.Moving) continue; // lit
                bool someone = false;
                foreach (var p in Players)
                    if (p.Alive && (p.Position - sigil.Position).sqrMagnitude <= SigilRadius * SigilRadius) someone = true;
                sigil.Health = someone
                    ? sigil.Health + dt / SigilChargeTime
                    : Mathf.Max(0f, sigil.Health - dt / SigilChargeTime * 0.5f);
                if (sigil.Health < 1f) continue;
                sigil.Health = 1f;
                sigil.Moving = true; // shown lit
                TrialDone++;
                TrialEvent(TrialMoment.SigilLit, sigil.Position);
            }
            if (TrialDone >= TrialTotal)
            {
                BreakTrial(bossIndex, TrialMoment.RitualBroken);
                return;
            }

            // Blood falls on everyone while the ritual goes on.
            _ritualRain -= dt;
            if (_ritualRain <= 0f)
            {
                _ritualRain = Wrath ? 2f : 2.6f;
                foreach (var p in Players)
                    if (p.Alive && p.RoomId == Room.Id) QueueBurst(p.Position, 1.6f, 1.2f, 18f * EnemyDamageScale, AreaKind.Slam);
            }

            if (TrialTimer > 0f) return;
            // Too slow: the blood torrent hits everyone and the bishop drinks.
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.None;
            TrialTimer = 0f;
            RemoveTrialProps();
            boss.Health = Mathf.Min(boss.MaxHealth, boss.Health + boss.MaxHealth * 0.1f);
            boss.Cooldown = 1f;
            TrialEvent(TrialMoment.RitualFailed, boss.Position);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.BloodTorrent, Pos = boss.Position, Value = 14f });
            foreach (var p in Players)
            {
                if (!p.Alive || p.RoomId != Room.Id) continue;
                p.InvulnerableTimer = 0f;
                HurtPlayer(p, p.MaxHp * RitualTorrent);
            }
        }

        // ------------------------------------------------------------------ Lich

        private void BeginCollapse()
        {
            if (CollapseStage >= 2) return;
            ActiveTrial = BossTrial.Collapse;
            TrialTimer = CollapseWarnTime;
            TrialEvent(TrialMoment.CollapseWarn, PlayRect.center, CollapseWarnTime, 0, CollapseStage + 1);
        }

        private void Collapse()
        {
            ActiveTrial = BossTrial.None;
            TrialTimer = 0f;
            CollapseStage++;
            var area = PlayRect;
            float r = SimPlayer.BodyRadius;
            foreach (var p in Players)
            {
                if (p.RoomId != Room.Id || area.Contains(p.Position)) continue;
                var safe = new Vector2(Mathf.Clamp(p.Position.x, area.xMin + r + 0.4f, area.xMax - r - 0.4f), Mathf.Clamp(p.Position.y, area.yMin + r + 0.4f, area.yMax - r - 0.4f));
                p.Position = Arena.Resolve(Layout, safe, r);
                Events.Add(new SimEvent(SimEventType.Teleport) { ClientId = p.ClientId, Pos = p.Position });
                if (!p.Alive) continue;
                p.InvulnerableTimer = 0f;
                HurtPlayer(p, p.MaxHp * 0.15f);
            }
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.RoomId != Room.Id) continue;
                if (e.Def.Prop && !area.Contains(e.Position)) e.Dead = true; // kegs fall
                else e.Position = Confine(e.Position, e.Radius, e.Def.Flying);
            }
            TrialEvent(TrialMoment.Collapsed, area.center, 0f, 0, CollapseStage);
        }

        /// <summary>The Lich took a deadly blow: instead of dying he sends his soul into a vessel somewhere in the room.</summary>
        private void RaisePhylactery(int lichIndex)
        {
            ref var lich = ref Enemies[lichIndex];
            lich.Health = 1f;
            CancelAttack(ref lich);
            ActiveTrial = BossTrial.Phylactery;
            TrialTimer = Wrath ? VesselTime * 0.75f : VesselTime;
            TrialDone = 0;
            TrialTotal = 1;
            Vector2 spot = Arena.Resolve(Layout, BlinkSpot(lich.Position), 1f);
            var area = PlayRect;
            spot.x = Mathf.Clamp(spot.x, area.xMin + 1.2f, area.xMax - 1.2f);
            spot.y = Mathf.Clamp(spot.y, area.yMin + 1.2f, area.yMax - 1.6f);
            SpawnTrialProp(EnemyType.SoulVessel, spot, EnemyHealthScale);
            TrialEvent(TrialMoment.VesselRise, spot, TrialTimer);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.SoulBurst, Pos = lich.Position, Value = 3f });
        }

        /// <summary>Damage to a boss: nothing while warded, extra while exposed, and the Lich's last breath.</summary>
        private float BossDamage(int index, float damage, ref HitFlags flags)
        {
            ref var e = ref Enemies[index];
            if (BossWarded)
            {
                flags |= HitFlags.Blocked;
                return 0f;
            }
            if (_exposed > 0f) damage *= 1f + ExposedBonus;
            if (e.Type == EnemyType.Lich && e.Health - damage <= 0f)
            {
                RaisePhylactery(index);
                return 0f;
            }
            return damage;
        }

        // ------------------------------------------------------------------ Hostile bursts (rituals, vengeful dead)

        private void QueueBurst(Vector2 at, float radius, float delay, float damage, AreaKind fx)
        {
            var burst = new HostileBurst { Id = _nextBurstId--, Position = at, Radius = radius, Timer = delay, Damage = damage, Fx = fx };
            _bursts.Add(burst);
            Events.Add(new SimEvent(SimEventType.EnemyTelegraph)
            {
                Id = burst.Id, Int = (int)AttackShape.Circle, Pos = at, Vec = Vector2.right, Size = new Vector2(radius, 0f), Value = delay,
            });
        }

        private void StepBursts(float dt)
        {
            for (int k = _bursts.Count - 1; k >= 0; k--)
            {
                var burst = _bursts[k];
                burst.Timer -= dt;
                if (burst.Timer > 0f)
                {
                    _bursts[k] = burst;
                    continue;
                }
                _bursts.RemoveAt(k);
                Events.Add(new SimEvent(SimEventType.EnemyStrike) { Id = burst.Id, Int = (int)AttackKind.Rain, Pos = burst.Position });
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)burst.Fx, Pos = burst.Position, Value = burst.Radius });
                float r = burst.Radius + SimPlayer.BodyRadius;
                foreach (var p in Players)
                    if (p.Alive && (p.Position - burst.Position).sqrMagnitude <= r * r) HurtPlayer(p, burst.Damage);
            }
        }

        private void ClearBursts()
        {
            foreach (var b in _bursts) Events.Add(new SimEvent(SimEventType.EnemyStrike) { Id = b.Id, Int = (int)AttackKind.Rain, Pos = b.Position });
            _bursts.Clear();
        }
    }
}
