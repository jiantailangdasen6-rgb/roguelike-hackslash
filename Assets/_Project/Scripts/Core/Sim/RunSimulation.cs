using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The whole run, simulated on the host only: rooms and waves, enemy AI with telegraphed attacks,
    /// class abilities, statuses and synergies, XP and perk picks. Pure C# so it can be unit-tested and
    /// run headless; the network layer feeds player positions/inputs in and ships <see cref="Events"/> out.
    /// Split across RunSimulation.*.cs (core loop/rooms here, Combat, Enemies).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const int RoomsPerArea = 4;
        public const int AreaCount = 3;
        public const int TotalRooms = RoomsPerArea * AreaCount;

        public const float ChoiceTimeout = 25f;
        public const float ExitRadius = 1.6f;
        public const float ExitHoldTime = 1.2f;
        public const float ReviveRadius = 1.8f;
        public const float ReviveTime = 3f;
        /// <summary>Extra reach for the host-side revive check beyond the drawn ring (covers network lag).</summary>
        public const float ReviveGrace = 0.4f;
        public const float HitInvulnerability = 0.35f;
        public const float SpawnWarningTime = 0.9f;
        public const float MinSpawnDistance = 5f;
        public const float SurviveSeconds = 40f;

        /// <summary>Walkable floor of the arena (inside the walls).</summary>
        public static readonly Rect DefaultBounds = new(-14f, -8f, 28f, 16f);

        public readonly Rect Bounds;
        public readonly List<SimPlayer> Players = new();
        public readonly EnemySimulation Enemies;
        public readonly List<Projectile> Projectiles = new();
        public readonly List<Soul> Souls = new();
        public readonly List<PendingSpawn> PendingSpawns = new();
        public readonly List<SimEvent> Events = new();
        public readonly RoomState Room = new();

        private readonly GameRandom _spawnRng;
        private readonly GameRandom _perkRng;
        private readonly GameRandom _combatRng;
        private readonly GameRandom _aiRng;
        private readonly List<int> _hits = new();
        private int _nextProjectileId = 1;
        private int _nextSoulId = 1;
        private float _spawnTimer;
        private float _waveTimer;
        private int _lastWaveSize;

        public readonly Difficulty Difficulty;
        public readonly DifficultyDef DifficultyDef;
        /// <summary>Obstacles and gimmicks of the current room.</summary>
        public RoomLayout Layout { get; private set; } = Arena.Get(0);

        public RunPhase Phase { get; private set; } = RunPhase.Playing;
        public float Elapsed { get; private set; }
        public int TeamLevel { get; private set; } = 1;
        public float TeamXp { get; private set; }
        public int Kills { get; private set; }
        public int BossesKilled { get; private set; }
        public int RoomsCleared { get; private set; }
        public float XpToNext => XpRequired(TeamLevel, Players.Count);

        public RunSimulation(ulong seed, Rect? bounds = null, Difficulty difficulty = Difficulty.Normal)
        {
            Bounds = bounds ?? DefaultBounds;
            Enemies = new EnemySimulation(Bounds);
            Difficulty = difficulty;
            DifficultyDef = DifficultyCatalog.Get(difficulty);
            var root = new GameRandom(seed);
            _spawnRng = root.Fork(1);
            _perkRng = root.Fork(2);
            _combatRng = root.Fork(3);
            _aiRng = root.Fork(4);
            _layoutRng = root.Fork(5);
            _doorRng = root.Fork(6);
            _lootRng = root.Fork(7);
        }

        private readonly GameRandom _layoutRng;

        public static float XpRequired(int level, int playerCount) =>
            (12f + 7f * (level - 1)) * (1f + 0.5f * Mathf.Max(0, playerCount - 1));

        public static float HealthMultiplier(int roomIndex, int playerCount) =>
            (1f + 0.18f * roomIndex) * (1f + 0.45f * Mathf.Max(0, playerCount - 1));

        public static float DamageMultiplier(int roomIndex) => 1f + 0.12f * roomIndex;

        /// <summary>Monster health for the current room, players and difficulty.</summary>
        public float EnemyHealthScale => HealthMultiplier(Room.Index, Players.Count) * DifficultyDef.EnemyHealth * (Room.Trial ? TrialHealth : 1f);
        public float EnemyDamageScale => DamageMultiplier(Room.Index) * DifficultyDef.EnemyDamage * (Room.Trial ? TrialDamage : 1f);
        public float EliteChance => DifficultyDef.EliteChance + (Room.Trial ? TrialEliteChance : 0f);

        /// <summary>The boss of the current area.</summary>
        public EnemyType AreaBoss => EnemyCatalog.AreaBosses[Mathf.Min(Room.Area, EnemyCatalog.AreaBosses.Length - 1)];

        // ------------------------------------------------------------------ Players

        public SimPlayer AddPlayer(ulong clientId, Loadout loadout = null)
        {
            var existing = GetPlayer(clientId);
            if (existing != null) return existing;
            var player = new SimPlayer(clientId, StartPosition(Players.Count), loadout);
            Players.Add(player);
            return player;
        }

        public void RemovePlayer(ulong clientId)
        {
            Players.RemoveAll(p => p.ClientId == clientId);
        }

        private readonly Dictionary<string, SimPlayer> _departed = new();

        /// <summary>A player dropped out mid-run: keep their character (perks, HP) so they can come back.</summary>
        public void DetachPlayer(ulong clientId, string playerKey)
        {
            var p = GetPlayer(clientId);
            if (p == null) return;
            if (p.PendingOffer != null) p.StockedChoices.Enqueue(p.OfferKind); // shown again when they return
            p.PendingOffer = null;
            p.Input = default;
            if (!string.IsNullOrEmpty(playerKey)) _departed[playerKey] = p;
            RemovePlayer(clientId);
        }

        /// <summary>
        /// Someone joins a run in progress. A returning player gets their old character back; a newcomer starts at
        /// the team level with one random perk per level so far (catch-up), placed next to a teammate.
        /// </summary>
        public SimPlayer JoinMidRun(ulong clientId, Loadout loadout, out bool returning)
        {
            returning = false;
            var existing = GetPlayer(clientId);
            if (existing != null) return existing;

            string key = loadout?.playerKey;
            SimPlayer player;
            if (!string.IsNullOrEmpty(key) && _departed.TryGetValue(key, out player))
            {
                _departed.Remove(key);
                player.ClientId = clientId;
                player.SetLevel(TeamLevel);
                returning = true;
            }
            else
            {
                player = new SimPlayer(clientId, StartPosition(Players.Count), loadout);
                player.SetLevel(TeamLevel);
                for (int level = 1; level < TeamLevel; level++) player.StockedChoices.Enqueue(ChoiceKind.LevelUp);
                player.Hp = player.MaxHp;
            }

            foreach (var mate in Players)
            {
                if (!mate.Alive) continue;
                player.Position = mate.Position + new Vector2(0.8f, -0.6f);
                break;
            }
            player.SetGearLevelCap(GearLevelCap);
            if (!returning) GiveLoaners(player);
            Players.Add(player);
            OfferNext(player);
            return player;
        }

        public SimPlayer GetPlayer(ulong clientId)
        {
            foreach (var p in Players) if (p.ClientId == clientId) return p;
            return null;
        }

        public void SetPlayerPosition(ulong clientId, Vector2 position)
        {
            var p = GetPlayer(clientId);
            if (p != null) p.Position = position;
        }

        public void SetPlayerInput(ulong clientId, PlayerInput input)
        {
            var p = GetPlayer(clientId);
            if (p == null) return;
            p.Input = input;
            if (input.Aim.sqrMagnitude > 0.0001f) p.Aim = input.Aim;
        }

        private static Vector2 StartPosition(int index) => index switch
        {
            0 => new Vector2(0f, -4f),
            1 => new Vector2(-1.5f, -4.8f),
            2 => new Vector2(1.5f, -4.8f),
            _ => new Vector2(0f, -5.5f),
        };

        // ------------------------------------------------------------------ Main loop

        /// <param name="firstRoom">Debug: start deeper in the run (e.g. a boss room) for testing and screenshots.</param>
        public void Start(int firstRoom = 0)
        {
            StartRoom(Mathf.Clamp(firstRoom, 0, TotalRooms - 1));
            foreach (var p in Players) GiveLoaners(p);
            foreach (var p in Players) if (p.HasStartPerk) p.StockedChoices.Enqueue(ChoiceKind.Start);
            HoldIfPicksLeft();
        }

        public void Step(float dt)
        {
            if (Phase != RunPhase.Playing) return;

            TickOffers(dt);
            if (HoldingForPicks)
            {
                if (AnyPicksLeft)
                {
                    UpdatePlayers(dt); // free to walk about; nothing spawns yet
                    return;
                }
                HoldingForPicks = false;
            }

            Elapsed += dt;
            Enemies.BeginStep();
            UpdatePlayers(dt);
            UpdateSpawning(dt);
            StepEnemies(dt);
            StepPlayersCombat(dt);
            StepDelayedAreas(dt);
            StepZones(dt);
            StepHazards(dt);
            StepProjectiles(dt);
            ProcessDeaths(giveXp: true);
            CollectSouls();
            CollectLoot();
            UpdateRoom(dt);
            CheckDefeat();
            CheckLevelUp();
        }

        private void UpdatePlayers(float dt)
        {
            foreach (var p in Players)
            {
                p.InvulnerableTimer -= dt;
                p.TickBuffs(dt);
                if (p.Alive)
                {
                    p.Heal(p.Stats.Get(StatType.Regen) * dt);
                    continue;
                }

                // The host sees remote players slightly behind (network interpolation), so the check is a bit wider
                // than the drawn ring: standing anywhere inside it always counts.
                float helpSpeed = 0f;
                float helpRadius = ReviveRadius + ReviveGrace;
                foreach (var other in Players)
                    if (other != p && other.Alive && (other.Position - p.Position).sqrMagnitude <= helpRadius * helpRadius)
                        helpSpeed = Mathf.Max(helpSpeed, other.Stats.Get(StatType.ReviveSpeed));

                p.ReviveProgress = helpSpeed > 0f
                    ? p.ReviveProgress + dt / ReviveTime * helpSpeed
                    : Mathf.Max(0f, p.ReviveProgress - dt / ReviveTime * 0.4f);

                if (p.ReviveProgress >= 1f) Revive(p, 0.5f, guardian: AnyHelperHas(p, PerkId.GuardianBond));
            }
        }

        private bool AnyHelperHas(SimPlayer downed, PerkId perk)
        {
            foreach (var other in Players)
                if (other != downed && other.Alive && other.PerkLevel(perk) > 0 &&
                    (other.Position - downed.Position).sqrMagnitude <= ReviveRadius * ReviveRadius * 1.5f)
                    return true;
            return false;
        }

        private void Revive(SimPlayer p, float healthFraction, bool guardian)
        {
            p.Downed = false;
            p.ReviveProgress = 0f;
            p.Hp = p.MaxHp * healthFraction;
            p.InvulnerableTimer = guardian ? 2.5f : 1.5f;
            Events.Add(new SimEvent(SimEventType.PlayerRevived) { ClientId = p.ClientId, Pos = p.Position });
        }

        // ------------------------------------------------------------------ Spawning

        private void UpdateSpawning(float dt)
        {
            for (int i = PendingSpawns.Count - 1; i >= 0; i--)
            {
                var s = PendingSpawns[i];
                s.Timer -= dt;
                if (s.Timer > 0f)
                {
                    PendingSpawns[i] = s;
                    continue;
                }
                PendingSpawns.RemoveAt(i);
                var def = EnemyCatalog.Get(s.Type);
                Vector2 at = def.Flying ? s.Position : Arena.Resolve(Layout, s.Position, def.Radius);
                int id = Enemies.Spawn(s.Type, at, EnemyHealthScale, EnemyDamageScale, s.Elite);
                if (def.Boss) Events.Add(new SimEvent(SimEventType.BossSpawned) { Id = id, Pos = at, Int = (int)s.Type });
            }

            if (Room.Cleared) return;
            int hostile = Enemies.HostileCount + PendingSpawns.Count;
            switch (Room.Objective)
            {
                case RoomObjective.Waves:
                {
                    _waveTimer -= dt;
                    bool waveDone = hostile <= Mathf.Max(1, _lastWaveSize / 4) || _waveTimer <= 0f;
                    if (waveDone && Room.Wave < Room.WaveCount) StartWave();
                    break;
                }
                case RoomObjective.Survive:
                {
                    _spawnTimer -= dt;
                    int target = Mathf.RoundToInt((6 + Room.Area * 2) * PlayerScale * DifficultyDef.EnemyCount);
                    if (_spawnTimer <= 0f && hostile < target)
                    {
                        _spawnTimer = 0.8f;
                        QueueSpawn(RandomEnemyType(), _spawnRng.Chance(EliteChance + 0.01f));
                    }
                    break;
                }
                case RoomObjective.Boss:
                {
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f && hostile < 1 + 2 * Room.Area)
                    {
                        _spawnTimer = 9f;
                        QueueSpawn(RandomEnemyType(), false);
                    }
                    break;
                }
            }
        }

        private float PlayerScale => 1f + 0.6f * Mathf.Max(0, Players.Count - 1);

        private void StartWave()
        {
            Room.Wave++;
            _waveTimer = 20f;
            int size = Mathf.RoundToInt((5f + Room.Index * 0.5f + Room.Area * 1.5f) * PlayerScale * DifficultyDef.EnemyCount);
            _lastWaveSize = size;
            bool eliteRoom = IsEliteRoom(Room.Index);
            for (int i = 0; i < size; i++)
                QueueSpawn(RandomEnemyType(), (eliteRoom && i == 0) || _spawnRng.Chance(EliteChance));
            Events.Add(new SimEvent(SimEventType.WaveStarted) { Int = Room.Wave, Value = Room.WaveCount });
        }

        // Spawn weights per area (index = EnemyType). Each area adds new monsters to the mix.
        private static readonly (EnemyType Type, int Weight)[][] AreaPools =
        {
            new[] { (EnemyType.Skeleton, 40), (EnemyType.SkeletonArcher, 22), (EnemyType.Ghoul, 20), (EnemyType.Bat, 10) },
            new[]
            {
                (EnemyType.Skeleton, 20), (EnemyType.SkeletonArcher, 15), (EnemyType.Ghoul, 14), (EnemyType.Cultist, 14),
                (EnemyType.Bat, 7), (EnemyType.Bloater, 14), (EnemyType.DreadKnight, 11),
            },
            new[]
            {
                (EnemyType.Skeleton, 10), (EnemyType.SkeletonArcher, 12), (EnemyType.Ghoul, 12), (EnemyType.Cultist, 11), (EnemyType.Bat, 6),
                (EnemyType.Bloater, 11), (EnemyType.DreadKnight, 12), (EnemyType.Necromancer, 9), (EnemyType.Wraith, 13),
            },
        };

        private EnemyType RandomEnemyType()
        {
            var pool = AreaPools[Mathf.Min(Room.Area, AreaPools.Length - 1)];
            int total = 0;
            foreach (var (_, w) in pool) total += w;
            int pick = _spawnRng.Range(0, total);
            foreach (var (type, w) in pool)
            {
                if (pick < w) return type;
                pick -= w;
            }
            return EnemyType.Skeleton;
        }

        public void QueueSpawn(EnemyType type, bool elite, Vector2? at = null)
        {
            Vector2 pos = at ?? RandomSpawnPoint();
            PendingSpawns.Add(new PendingSpawn { Type = type, Position = pos, Timer = SpawnWarningTime, Elite = elite });
            Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = pos, Int = (int)type, Value = SpawnWarningTime });

            // Bats come in flocks.
            if (type == EnemyType.Bat && at == null)
                for (int k = 0; k < 2; k++)
                {
                    Vector2 near = Enemies.ClampToBounds(pos + Rotate(Vector2.right, k * 140f + 30f) * 0.9f, 0.5f);
                    PendingSpawns.Add(new PendingSpawn { Type = type, Position = near, Timer = SpawnWarningTime + 0.1f * (k + 1) });
                    Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = near, Int = (int)type, Value = SpawnWarningTime + 0.1f * (k + 1) });
                }
        }

        private bool SpawnBlocked(Vector2 p)
        {
            if (Arena.Overlaps(Layout, p, 0.9f)) return true;
            foreach (var g in Layout.Gimmicks)
                if (Arena.InGimmick(g, p, 0.4f)) return true;
            return false;
        }

        private Vector2 RandomSpawnPoint()
        {
            Vector2 best = Vector2.zero;
            float bestDistance = -1f;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                var p = new Vector2(
                    _spawnRng.Range(Bounds.xMin + 1f, Bounds.xMax - 1f),
                    _spawnRng.Range(Bounds.yMin + 1f, Bounds.yMax - 1.5f));
                if (SpawnBlocked(p)) continue;
                float nearest = float.MaxValue;
                foreach (var pl in Players) nearest = Mathf.Min(nearest, (pl.Position - p).magnitude);
                if (nearest >= MinSpawnDistance) return p;
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = p;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ XP

        private void DropSoul(Vector2 position, int value)
        {
            var soul = new Soul { Id = _nextSoulId++, Position = position, Value = value };
            Souls.Add(soul);
            Events.Add(new SimEvent(SimEventType.SoulSpawn) { Id = soul.Id, Pos = position, Int = value });
        }

        private void CollectSouls()
        {
            for (int s = Souls.Count - 1; s >= 0; s--)
            {
                var soul = Souls[s];
                foreach (var p in Players)
                {
                    if (!p.Alive) continue;
                    float r = p.Stats.Get(StatType.PickupRange);
                    if ((p.Position - soul.Position).sqrMagnitude > r * r) continue;
                    TeamXp += soul.Value;
                    int harvest = p.PerkLevel(PerkId.SoulHarvest);
                    if (harvest > 0) p.Heal(harvest);
                    Events.Add(new SimEvent(SimEventType.SoulCollected) { Id = soul.Id, ClientId = p.ClientId });
                    Souls.RemoveAt(s);
                    break;
                }
            }
        }

        private void CheckLevelUp()
        {
            if (Players.Count == 0) return;
            while (TeamXp >= XpToNext)
            {
                TeamXp -= XpToNext;
                TeamLevel++;
                foreach (var p in Players)
                {
                    p.SetLevel(TeamLevel);
                    p.StockedChoices.Enqueue(ChoiceKind.LevelUp);
                }
                Events.Add(new SimEvent(SimEventType.TeamLevelUp) { Int = TeamLevel });
            }
            OfferAll();
        }

        // ------------------------------------------------------------------ Perk choices
        //
        // Fighting and preparing are kept apart: picks earned in a fight (level-ups) are banked and offered once
        // the room is cleared; door rewards are chosen on entering the next room, before its monsters come.
        // Nobody's pick pauses anyone else - each player works through their own queue.

        /// <summary>Between fights: a cleared room, or a new room holding its monsters until everyone has picked.</summary>
        public bool OffersOpen => Room.Cleared || HoldingForPicks;

        /// <summary>A new room waits for the team's picks (door rewards, start perks) before spawning anything.</summary>
        public bool HoldingForPicks { get; private set; }

        public bool AnyPicksLeft
        {
            get
            {
                foreach (var p in Players) if (p.PicksLeft > 0) return true;
                return false;
            }
        }

        /// <summary>Someone is still choosing during a preparation window.</summary>
        public bool AwaitingChoices => OffersOpen && AnyPicksLeft;

        private void OfferAll()
        {
            foreach (var p in Players) OfferNext(p);
        }

        private void OfferNext(SimPlayer p)
        {
            if (!OffersOpen || p.PendingOffer != null || p.StockedChoices.Count == 0) return;
            var kind = p.StockedChoices.Dequeue();
            var shown = kind;
            p.PendingOffer = kind == ChoiceKind.Upgrade ? PerkRoller.RollUpgrade(_perkRng, p) : null;
            if (p.PendingOffer == null)
            {
                if (kind == ChoiceKind.Upgrade) shown = ChoiceKind.Boon; // nothing to upgrade yet: a new perk instead
                p.PendingOffer = PerkRoller.Roll(_perkRng, p);
            }
            p.OfferKind = shown;
            p.OfferTimer = ChoiceTimeout;
            Events.Add(new SimEvent(SimEventType.PerkOffer) { ClientId = p.ClientId, Seed = PerkOption.Pack(p.PendingOffer), Int = (int)shown, Value = p.StockedChoices.Count });
        }

        public void ChoosePerk(ulong clientId, int index)
        {
            var p = GetPlayer(clientId);
            if (p?.PendingOffer == null) return;
            var option = p.PendingOffer[Mathf.Clamp(index, 0, p.PendingOffer.Length - 1)];
            p.PendingOffer = null;
            PerkRoller.Apply(p, option);
            OfferNext(p);
        }

        /// <summary>Someone away from the keyboard can't hold the team up forever: the first option is taken for them.</summary>
        private void TickOffers(float dt)
        {
            foreach (var p in Players)
            {
                if (p.PendingOffer == null) continue;
                p.OfferTimer -= dt;
                if (p.OfferTimer <= 0f) ChoosePerk(p.ClientId, 0);
            }
        }

        /// <summary>Promised picks for everyone (door rewards).</summary>
        private void StockForAll(ChoiceKind kind, int times)
        {
            foreach (var p in Players)
                for (int i = 0; i < times; i++) p.StockedChoices.Enqueue(kind);
        }

        /// <summary>After entering a room: wait for the team's picks before the fight starts.</summary>
        private void HoldIfPicksLeft()
        {
            HoldingForPicks = AnyPicksLeft;
            OfferAll();
        }

        // ------------------------------------------------------------------ Rooms

        private void StartRoom(int index)
        {
            Room.Index = index;
            Room.Progress = 0f;
            Room.Wave = 0;
            Room.Cleared = false;
            Room.Trial = _enterTrial;
            _enterTrial = false;
            Room.Doors.Clear();
            Room.HoldDoor = -1;
            Room.ExitHold = 0f;
            _spawnTimer = 1.5f;
            foreach (var p in Players)
            {
                p.SecondWindUsed = false;
                p.SetGearLevelCap(GearLevelCap);
                int blessing = p.PerkLevel(PerkId.Blessing);
                if (blessing > 0 && p.Alive) p.Shield = Mathf.Max(p.Shield, BlessingShield * blessing);
            }

            Layout = Arena.Get(Bounds == DefaultBounds ? Arena.Pick(_layoutRng, index, Layout.Id) : 0);
            ResetHazards();

            Room.Objective = ObjectiveOf(index);
            switch (Room.Objective)
            {
                case RoomObjective.Waves:
                    Room.WaveCount = 4 + (Room.Area >= 1 ? 1 : 0);
                    Room.Target = Room.WaveCount;
                    _waveTimer = 1.2f;
                    break;
                case RoomObjective.Survive:
                    Room.Target = SurviveSeconds;
                    break;
                case RoomObjective.Rest:
                    Layout = Arena.Get(Arena.RestLayout);
                    ResetHazards();
                    Room.Target = 0f;
                    break;
                default:
                    Room.Objective = RoomObjective.Boss;
                    Room.Target = 1f;
                    _spawnTimer = 12f;
                    PendingSpawns.Add(new PendingSpawn { Type = AreaBoss, Position = new Vector2(0f, 3f), Timer = 1.6f });
                    Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = new Vector2(0f, 3f), Int = (int)AreaBoss, Value = 1.6f });
                    break;
            }
            Events.Add(new SimEvent(SimEventType.RoomStarted) { Int = index, Id = Layout.Id, Value = Room.Trial ? 1f : 0f });
            if (Room.Objective == RoomObjective.Rest) ClearRoom(); // nothing to fight: the doors are open from the start
            SpawnProps();
        }

        /// <summary>A rest room chest broke open: a ring of souls (about a wave's worth of XP for the team).</summary>
        private void SpillChest(in Enemy chest)
        {
            int total = Mathf.RoundToInt((10f + 6f * Room.Area) * (1f + 0.5f * Mathf.Max(0, Players.Count - 1)));
            const int pieces = 6;
            for (int s = 0; s < pieces; s++)
                DropSoul(chest.Position + Rotate(Vector2.right, s * 60f + 30f) * 0.9f, Mathf.Max(1, total / pieces));
            // And a piece of gear for each player (personal loot, fine or better).
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                float angle = (i - (Players.Count - 1) * 0.5f) * 40f - 90f;
                DropFor(p, RollItem(p, 1f, Rarity.Magic), chest.Position + Rotate(Vector2.right, angle) * 1.3f);
            }
        }

        /// <summary>
        /// The pace of an area: a fight, a harder fight (a 40 s stand in the crypt, elite packs deeper down),
        /// a breather by a campfire, then the area's lord.
        /// </summary>
        public static RoomObjective ObjectiveOf(int roomIndex) => (roomIndex % RoomsPerArea) switch
        {
            0 => RoomObjective.Waves,
            1 => roomIndex / RoomsPerArea == 0 ? RoomObjective.Survive : RoomObjective.Waves,
            2 => RoomObjective.Rest,
            _ => RoomObjective.Boss,
        };

        /// <summary>Rooms whose every wave is led by an elite.</summary>
        public static bool IsEliteRoom(int roomIndex) => roomIndex % RoomsPerArea == 1 && ObjectiveOf(roomIndex) == RoomObjective.Waves;

        public const float RestHealRadius = 3.4f;
        /// <summary>Share of max HP restored per second while sitting by the rest room's fire.</summary>
        public const float RestHealPerSecond = 0.12f;
        private float _restHealPulse;

        /// <summary>Rest room: the campfire heals everyone close to it (and gets the fallen back up).</summary>
        private void WarmByTheFire(float dt)
        {
            _restHealPulse -= dt;
            bool pulse = _restHealPulse <= 0f;
            if (pulse) _restHealPulse = 1f;
            foreach (var p in Players)
            {
                if ((p.Position - Arena.RestFire).sqrMagnitude > RestHealRadius * RestHealRadius) continue;
                if (!p.Alive)
                {
                    Revive(p, 0.35f, guardian: false);
                    continue;
                }
                float before = p.Hp;
                p.Heal(p.MaxHp * RestHealPerSecond * dt);
                p.HealedByFire += p.Hp - before;
                if (pulse && p.HealedByFire > 0.5f)
                {
                    Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.HealedByFire });
                    p.HealedByFire = 0f;
                }
            }
        }

        private void UpdateRoom(float dt)
        {
            if (!Room.Cleared)
            {
                switch (Room.Objective)
                {
                    case RoomObjective.Survive:
                        Room.Progress += dt;
                        if (Room.Progress >= Room.Target) ClearRoom();
                        break;
                    case RoomObjective.Waves:
                        Room.Progress = Mathf.Max(0, Room.Wave - 1);
                        if (Room.Wave >= Room.WaveCount && Enemies.HostileCount == 0 && PendingSpawns.Count == 0)
                        {
                            Room.Progress = Room.WaveCount;
                            ClearRoom();
                        }
                        break;
                    case RoomObjective.Boss:
                        if (Room.Progress >= 1f) ClearRoom();
                        break;
                }
                return;
            }

            if (Room.Objective == RoomObjective.Rest) WarmByTheFire(dt);

            // Everyone standing (downed players are carried along) must agree on one door, once all picks are made.
            if (AnyPicksLeft)
            {
                Room.HoldDoor = -1;
                Room.ExitHold = 0f;
                return;
            }
            int chosen = -1;
            bool first = true;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                int at = DoorAt(p.Position);
                if (first) chosen = at;
                else if (at != chosen) chosen = -1;
                first = false;
            }
            if (chosen != Room.HoldDoor) Room.ExitHold = 0f;
            Room.HoldDoor = chosen;
            Room.ExitHold = chosen >= 0 ? Room.ExitHold + dt : 0f;
            if (Room.ExitHold >= ExitHoldTime) NextRoom(chosen);
        }

        private void ClearRoom()
        {
            Room.Cleared = true;
            RoomsCleared++;

            // Leftover enemies crumble (no XP) and pending spawns are cancelled.
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Dead = true;
            ProcessDeaths(giveXp: false);
            PendingSpawns.Clear();
            foreach (var proj in Projectiles) Events.Add(new SimEvent(SimEventType.ProjectileEnd) { Id = proj.Id, Pos = proj.Position });
            Projectiles.Clear();
            _delayedAreas.Clear();
            _zones.Clear();
            _rainTargets.Clear();
            StopHazards();

            foreach (var p in Players)
                if (p.Alive)
                {
                    float before = p.Hp;
                    p.Heal(p.MaxHp * 0.2f);
                    if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
                }

            if (Room.Index >= TotalRooms - 1)
            {
                Phase = RunPhase.Victory;
                Events.Add(new SimEvent(SimEventType.RunEnded) { Int = 1 });
                return;
            }
            RollDoors();
            Events.Add(new SimEvent(SimEventType.RoomCleared) { Int = Room.Index, Seed = (ulong)Door.Pack(Room.Doors) });
            OfferAll(); // the banked level-ups
        }

        private void NextRoom(int doorIndex)
        {
            var door = Room.Doors[Mathf.Clamp(doorIndex, 0, Room.Doors.Count - 1)];
            foreach (var soul in Souls)
            {
                TeamXp += soul.Value;
                Events.Add(new SimEvent(SimEventType.SoulCollected) { Id = soul.Id, ClientId = Players.Count > 0 ? Players[0].ClientId : 0 });
            }
            Souls.Clear();
            SweepLoot();
            _zones.Clear();
            // Whatever is left standing (unopened chests, kegs) stays behind.
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Dead = true;
            ProcessDeaths(giveXp: false);

            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Position = StartPosition(i);
                if (p.Downed) Revive(p, 0.35f, guardian: false); // everyone who makes it through gets back up
                Events.Add(new SimEvent(SimEventType.Teleport) { ClientId = p.ClientId, Pos = p.Position });
            }
            _enterTrial = door.Trial;
            StartRoom(Room.Index + 1);
            GrantReward(door.Reward, door.Trial ? 2 : 1);
            CheckLevelUp(); // souls picked up on the way out
            HoldIfPicksLeft();
        }

        private void CheckDefeat()
        {
            if (Players.Count == 0) return;
            foreach (var p in Players) if (p.Alive) return;
            Phase = RunPhase.Defeat;
            Events.Add(new SimEvent(SimEventType.RunEnded) { Int = 0 });
        }
    }
}
