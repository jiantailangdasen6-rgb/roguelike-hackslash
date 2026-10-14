using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The lands added after the first three (Biome.cs): their monsters' choices and deaths (the frozen dead shatter,
    /// slag keeps burning) and their lords' trials. The Drowned Saint floods her hall until its seals are broken;
    /// the Frost Queen hides behind two reflections that have her face but no shadow; the Forge Colossus turns
    /// red-hot and has to be lured into the water of a broken cistern. Water and ice in the rooms are Arena zones.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float FloodTime = 16f;
        public const float FloodTick = 1f;
        public const float FloodHurt = 0.03f;     // share of max HP per tick
        public const float FloodTorrent = 0.22f;  // share of max HP when the flood is not drained in time
        public const float FloodSlow = 0.75f;     // players wading through the flood (PlayerMover)
        public const float MirrorTime = 16f;
        public const float OverheatTime = 24f;
        public const float OverheatArmor = 0.25f; // damage that gets through red-hot iron
        public const float OverheatSpeed = 1.3f;
        public const float QuenchRadius = 2.2f;
        public const float QuenchPoolTime = 9f;
        public const float SlagPoolTime = 4f;
        public const float SlagPoolRadius = 1.6f;

        private float _floodTick;
        private float _lavaRain;

        private struct LandPool
        {
            public Vector2 Position;
            public float Radius;
            public float Timer;
            public float Tick;
            public bool Hot;   // molten slag (hurts) or water (quenches the colossus)
            public bool Spore; // a hot pool that is a cloud of spores (RunSimulation.Lands2.cs)
            public int Room;
        }

        private readonly List<LandPool> _pools = new();

        private void ResetLandTrials()
        {
            _floodTick = 0f;
            _lavaRain = 0f;
            _pools.Clear();
            ResetLords2(); // RunSimulation.Lords2.cs
            ResetChampions(); // RunSimulation.Champions.cs
            ResetLands2(); // RunSimulation.Lands2.cs
        }

        // ------------------------------------------------------------------ Choices

        /// <summary>Which move the new lands' monsters use next (0 for everyone else).</summary>
        private static int ChooseLandAttack(EnemyDef def, float distance, int n, bool roomForMinions)
        {
            switch (def.Type)
            {
                case EnemyType.Drowned:
                    return distance > 2.6f && n % 3 == 2 ? 1 : 0;   // spits water from afar now and then
                case EnemyType.TideWitch:
                    return n % 3 == 2 ? 1 : 0;
                case EnemyType.IronSentinel:
                    return distance > 2.6f && n % 2 == 1 ? 1 : 0;   // shoulders in from afar
                case EnemyType.DrownedSaint:
                    // 0 wave of water, 1 whirlpools, 2 burst around her, 3 summon, 4 glide
                    if (distance < 2.6f && n % 2 == 0) return 2;
                    switch (n % 6)
                    {
                        case 1: case 4: return 1;
                        case 2: return distance > 5f ? 4 : 0;
                        case 5: return roomForMinions ? 3 : 0;
                        default: return 0;
                    }
                case EnemyType.FrostQueen:
                    // 0 breath, 1 icicle rain, 2 ice lances, 3 ice step, 4 summon
                    switch (n % 6)
                    {
                        case 0: return distance < 6f ? 0 : 2;
                        case 1: case 4: return 1;
                        case 2: return 2;
                        case 3: return 3;
                        default: return roomForMinions ? 4 : 2;
                    }
                case EnemyType.GraveReaper: case EnemyType.ChoirMistress: case EnemyType.VoidEye:
                case EnemyType.GreatCrab: case EnemyType.GreatWolf: case EnemyType.Executioner:
                    return SecondLordAttack(def, distance, n, roomForMinions); // RunSimulation.Lords2.cs
                case EnemyType.AshenKing:
                    return AshenAttack(distance, n, roomForMinions); // RunSimulation.Heart.cs
                case EnemyType.Mycelid: case EnemyType.MossShaman: case EnemyType.GreatCap: case EnemyType.Scribe: case EnemyType.Bookworm:
                case EnemyType.MyceliumMother: case EnemyType.RotGiant: case EnemyType.Archivist: case EnemyType.InkBeast:
                    return NewLandAttack(def, distance, n, roomForMinions); // RunSimulation.Lands2.cs
                // The lands' champions (RunSimulation.Champions.cs)
                case EnemyType.BoneDigger:
                case EnemyType.IceKnight:
                    return distance > 3f && n % 2 == 1 ? 1 : 0;     // charges in from afar
                case EnemyType.TwinAcolyte:
                    return n % 3 == 2 ? 1 : 0;                      // a circle of blood under the target now and then
                case EnemyType.ShadeLord:
                    return distance > 3f ? 0 : n % 2;               // steps in from afar, slashes up close
                case EnemyType.LeechMother:
                    return distance > 3f ? 1 : 0;
                case EnemyType.Bombsmith:
                    return distance < 2.4f && n % 2 == 0 ? 0 : 1;   // kegs, unless someone is at his anvil
                case EnemyType.ForgeColossus:
                    // 0 hammer, 1 shockwave, 2 molten spray, 3 charge, 4 summon
                    if (distance < 3f && n % 3 == 0) return 1;
                    switch (n % 6)
                    {
                        case 1: return 2;
                        case 2: case 5: return distance > 5f ? 3 : 0;
                        case 4: return roomForMinions ? 4 : 2;
                        default: return 0;
                    }
                default:
                    return 0;
            }
        }

        // ------------------------------------------------------------------ Deaths

        /// <summary>The frozen dead shatter into ice (a telegraphed burst); slag leaves a burning puddle.</summary>
        private void LandMonsterFell(in Enemy e)
        {
            switch (e.Type)
            {
                case EnemyType.Icebound:
                    QueueBurst(e.Position, 2f, 0.6f, 16f * EnemyDamageScale, AreaKind.Shatter);
                    break;
                case EnemyType.SlagBloater:
                    AddPool(e.Position, SlagPoolRadius, SlagPoolTime, hot: true);
                    break;
                case EnemyType.Sporeling:
                    AddPool(e.Position, SporeCloudRadius, SporeCloudTime, hot: true, spore: true);
                    break;
            }
        }

        private void AddPool(Vector2 at, float radius, float seconds, bool hot, bool spore = false)
        {
            _pools.Add(new LandPool { Position = at, Radius = radius, Timer = seconds, Hot = hot, Spore = spore, Room = Room.Id });
            if (spore) Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.SporeCloud, Pos = at, Value = radius, Size = new Vector2(seconds, 0f) });
            else if (hot) Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.SlagPool, Pos = at, Value = radius, Size = new Vector2(seconds, 0f) });
        }

        /// <summary>Puddles of slag hurt whoever stands in them; water from a cistern waits for the colossus.</summary>
        private void StepPools(float dt)
        {
            for (int k = _pools.Count - 1; k >= 0; k--)
            {
                var pool = _pools[k];
                pool.Timer -= dt;
                if (pool.Timer <= 0f)
                {
                    _pools.RemoveAt(k);
                    continue;
                }
                if (pool.Hot && (pool.Tick -= dt) <= 0f)
                {
                    pool.Tick = 0.5f;
                    var room = RoomStates[pool.Room];
                    float scale = DamageMultiplier(room.Index) * DifficultyDef.EnemyDamage;
                    float r = pool.Radius + SimPlayer.BodyRadius * 0.5f;
                    foreach (var p in Players)
                        if (p.Alive && p.RoomId == pool.Room && (p.Position - pool.Position).sqrMagnitude <= r * r) HurtPlayer(p, 6f * scale);
                }
                _pools[k] = pool;
            }
        }

        // ------------------------------------------------------------------ Urns by the walls

        /// <summary>
        /// First visit: two to five urns stand along the walls (not in doorways, traps or the lord's hall), the same for
        /// every peer. One blow breaks one; now and then souls spill out.
        /// </summary>
        private void SpawnUrns(RoomState room)
        {
            if (room.Kind is RoomKind.Boss or RoomKind.Start) return;
            var rng = new GameRandom(MapSeed ^ (ulong)(room.Id * 2654435761u + 977u) ^ (ulong)Area << 40);
            int count = 2 + rng.Range(0, 4);
            var b = room.Bounds;
            var placed = new List<Vector2>();
            for (int k = 0, guard = 0; k < count && guard < 40; guard++)
            {
                bool side = rng.Chance(0.5f);
                Vector2 p = side
                    ? new Vector2(rng.Chance(0.5f) ? b.xMin + 0.8f : b.xMax - 0.8f, rng.Range(b.yMin + 1f, b.yMax - 1.5f))
                    : new Vector2(rng.Range(b.xMin + 1f, b.xMax - 1f), rng.Chance(0.5f) ? b.yMin + 0.8f : b.yMax - 1.2f);
                if (Mathf.Abs(p.x - b.center.x) < 3f || Mathf.Abs(p.y - b.center.y) < 2.5f) continue; // keep the doorways clear
                if (Arena.Overlaps(room.Layout, p, 0.7f)) continue;
                bool trap = false;
                foreach (var g in room.Layout.Gimmicks) trap |= Arena.InGimmick(g, p, 0.6f) || Arena.InZone(g, p) || (g.Position - p).sqrMagnitude < 1.5f;
                if (trap || placed.Exists(q => (q - p).sqrMagnitude < 1.2f)) continue;
                placed.Add(p);
                k++;
                int id = Enemies.Spawn(EnemyType.Urn, p, 1f, 1f, false);
                Enemies[Enemies.IndexOf(id)].RoomId = room.Id;
            }
        }

        private void UrnBroken(in Enemy e)
        {
            Room = RoomStates[e.RoomId];
            var rng = new GameRandom((ulong)e.Id * 7919ul + Seed);
            if (rng.Chance(0.35f)) DropSoul(e.Position, 1 + Area);
        }

        // ------------------------------------------------------------------ Drowned Saint: the flood

        private void BeginFlood(int bossIndex)
        {
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.Flood;
            CancelAttack(ref boss);
            boss.Cooldown = 1.2f;
            TrialTotal = Wrath ? 4 : 3;
            TrialDone = 0;
            foreach (var spot in TrialSpots(TrialTotal, 10.5f, 5.6f, _aiRng.Range(0f, 90f)))
            {
                SpawnTrialProp(EnemyType.FloodSeal, spot, EnemyHealthScale);
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)EnemyType.FloodSeal, Value = 0.3f });
            }
            TrialTimer = (Players.Count == 1 ? FloodTime + 3f : FloodTime) - (Wrath ? 3f : 0f);
            _floodTick = FloodTick;
            TrialEvent(TrialMoment.FloodRise, PlayRect.center, TrialTimer);
        }

        private void StepFlood(int bossIndex, float dt)
        {
            TrialTimer -= dt;
            TrialDone = TrialTotal - LivingTrialProps();
            if (TrialDone >= TrialTotal)
            {
                BreakTrial(bossIndex, TrialMoment.FloodDrained);
                return;
            }
            // The water pulls at everyone in the hall.
            _floodTick -= dt;
            if (_floodTick <= 0f)
            {
                _floodTick = FloodTick;
                foreach (var p in Players)
                    if (p.Alive && p.RoomId == Room.Id) HurtPlayer(p, p.MaxHp * FloodHurt + 1f);
            }
            if (TrialTimer > 0f) return;

            // Too slow: the water sweeps the hall and the saint is refreshed by it.
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.None;
            TrialTimer = 0f;
            RemoveTrialProps();
            boss.Health = Mathf.Min(boss.MaxHealth, boss.Health + boss.MaxHealth * 0.1f);
            TrialEvent(TrialMoment.FloodTorrent, boss.Position);
            foreach (var p in Players)
            {
                if (!p.Alive || p.RoomId != Room.Id) continue;
                p.InvulnerableTimer = 0f;
                HurtPlayer(p, p.MaxHp * FloodTorrent);
            }
        }

        // ------------------------------------------------------------------ Frost Queen: the reflections

        private void BeginMirrors(int bossIndex)
        {
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.Mirror;
            CancelAttack(ref boss);
            boss.Cooldown = 1.4f;
            // She and her two reflections stand apart; which one is she? (the one with a shadow)
            var spots = TrialSpots(3, 8f, 4.2f, _aiRng.Range(0f, 120f));
            int real = _aiRng.Range(0, 3);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.FrostNova, Pos = boss.Position, Value = 2.4f });
            boss.Position = spots[real];
            for (int k = 0; k < 3; k++)
            {
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.FrostNova, Pos = spots[k], Value = 1.6f });
                if (k == real) continue;
                int id = SpawnTrialProp(EnemyType.IceMirror, spots[k], EnemyHealthScale);
                ref var mirror = ref Enemies[Enemies.IndexOf(id)];
                mirror.Facing = Enemies[bossIndex].Facing;
                mirror.Cooldown = 1.4f;
            }
            TrialTotal = 2;
            TrialDone = 0;
            TrialTimer = (Players.Count == 1 ? MirrorTime + 3f : MirrorTime) - (Wrath ? 3f : 0f);
            TrialEvent(TrialMoment.MirrorsRise, Enemies[bossIndex].Position, TrialTimer);
        }

        private void StepMirrors(int bossIndex, float dt)
        {
            TrialTimer -= dt;
            TrialDone = TrialTotal - LivingTrialProps();
            if (TrialDone >= TrialTotal)
            {
                BreakTrial(bossIndex, TrialMoment.MirrorsShattered);
                return;
            }
            if (TrialTimer > 0f) return;

            // Too slow: the reflections burst into cold and are gone; the queen is mended by the ice.
            foreach (int id in _trialProps)
            {
                int i = Enemies.IndexOf(id);
                if (i < 0 || Enemies[i].Dead) continue;
                QueueBurst(Enemies[i].Position, 3f, 0.8f, 26f * EnemyDamageScale, AreaKind.FrostNova);
            }
            RemoveTrialProps();
            ref var boss = ref Enemies[bossIndex];
            ActiveTrial = BossTrial.None;
            TrialTimer = 0f;
            boss.Health = Mathf.Min(boss.MaxHealth, boss.Health + boss.MaxHealth * 0.08f);
            TrialEvent(TrialMoment.MirrorsBurst, boss.Position);
        }

        /// <summary>A reflection struck down: it shatters (no souls, no loot).</summary>
        private void MirrorShattered(in Enemy e)
        {
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Shatter, Pos = e.Position, Value = 1.6f });
            TrialEvent(TrialMoment.MirrorBroken, e.Position);
        }

        // ------------------------------------------------------------------ Forge Colossus: red-hot iron

        private void BeginOverheat(int bossIndex)
        {
            ActiveTrial = BossTrial.Overheat;
            TrialTotal = 1;
            TrialDone = 0;
            // Two cisterns of water by the side walls: break one where he will walk through it.
            foreach (var spot in TrialSpots(2, 10.5f, 2.5f, 0f))
            {
                SpawnTrialProp(EnemyType.QuenchCistern, spot, 1f);
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)EnemyType.QuenchCistern, Value = 0.3f });
            }
            TrialTimer = OverheatTime - (Wrath ? 4f : 0f);
            _lavaRain = 2f;
            Vector2 at = Enemies[bossIndex].Position; // the spawns may have moved the array
            TrialEvent(TrialMoment.OverheatStart, at, TrialTimer);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Explosion, Pos = at, Value = 2.6f });
        }

        private void StepOverheat(int bossIndex, float dt)
        {
            TrialTimer -= dt;
            ref var boss = ref Enemies[bossIndex];
            // Lured into water: the steam bursts, he cools at once and reels.
            foreach (var pool in _pools)
            {
                if (pool.Hot || pool.Room != Room.Id) continue;
                float r = pool.Radius + boss.Radius * 0.5f;
                if ((boss.Position - pool.Position).sqrMagnitude > r * r) continue;
                _pools.RemoveAll(x => !x.Hot);
                TrialDone = 1;
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Steam, Pos = boss.Position, Value = 3f });
                BreakTrial(bossIndex, TrialMoment.Quenched);
                return;
            }
            // Molten iron spatters around everyone while he burns.
            _lavaRain -= dt;
            if (_lavaRain <= 0f)
            {
                _lavaRain = Wrath ? 1.8f : 2.4f;
                foreach (var p in Players)
                    if (p.Alive && p.RoomId == Room.Id)
                        QueueBurst(ClampToRoom(p.Position + Rotate(Vector2.right, _aiRng.Range(0f, 360f)) * _aiRng.Range(0f, 1.6f), 1f), 1.4f, 1.1f, 18f * EnemyDamageScale, AreaKind.Explosion);
            }
            if (TrialTimer > 0f) return;

            // He cooled by himself: the heat leaves in one great blast.
            ActiveTrial = BossTrial.None;
            TrialTimer = 0f;
            RemoveTrialProps();
            _pools.RemoveAll(x => !x.Hot);
            QueueBurst(boss.Position, 6f, 1.2f, 30f * EnemyDamageScale, AreaKind.Explosion);
            TrialEvent(TrialMoment.CooledDown, boss.Position);
        }

        /// <summary>A cistern broken: its water spreads on the floor for a while.</summary>
        private void CisternBroken(in Enemy e)
        {
            AddPool(e.Position, QuenchRadius, QuenchPoolTime, hot: false);
            TrialEvent(TrialMoment.CisternBroken, e.Position, QuenchPoolTime);
        }

        /// <summary>For the presentation and bots: the water of broken cisterns (positions).</summary>
        public IEnumerable<Vector2> QuenchPools()
        {
            foreach (var p in _pools) if (!p.Hot) yield return p.Position;
        }
    }
}
