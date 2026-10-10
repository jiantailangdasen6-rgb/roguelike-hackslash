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
        public const float HitInvulnerability = 0.35f;
        public const float SpawnWarningTime = 0.9f;
        public const float MinSpawnDistance = 5f;
        public const float SurviveSeconds = 50f;

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
        private float _choiceTimer;
        private bool _levelUpChoices;
        private int _lastWaveSize;

        public RunPhase Phase { get; private set; } = RunPhase.Playing;
        public float Elapsed { get; private set; }
        public int TeamLevel { get; private set; } = 1;
        public float TeamXp { get; private set; }
        public int PendingLevelUps { get; private set; }
        public bool AwaitingChoices { get; private set; }
        public int Kills { get; private set; }
        public int BossesKilled { get; private set; }
        public int RoomsCleared { get; private set; }
        public float XpToNext => XpRequired(TeamLevel, Players.Count);

        public RunSimulation(ulong seed, Rect? bounds = null)
        {
            Bounds = bounds ?? DefaultBounds;
            Enemies = new EnemySimulation(Bounds);
            var root = new GameRandom(seed);
            _spawnRng = root.Fork(1);
            _perkRng = root.Fork(2);
            _combatRng = root.Fork(3);
            _aiRng = root.Fork(4);
        }

        public static float XpRequired(int level, int playerCount) =>
            (12f + 7f * (level - 1)) * (1f + 0.5f * Mathf.Max(0, playerCount - 1));

        public static float HealthMultiplier(int roomIndex, int playerCount) =>
            (1f + 0.18f * roomIndex) * (1f + 0.45f * Mathf.Max(0, playerCount - 1));

        public static float DamageMultiplier(int roomIndex) => 1f + 0.12f * roomIndex;

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
            if (AwaitingChoices) FinishChoicesIfDone();
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
            var starters = new List<SimPlayer>();
            foreach (var p in Players) if (p.HasStartPerk) starters.Add(p);
            if (starters.Count > 0) BeginChoices(starters, levelUp: false);
        }

        public void Step(float dt)
        {
            if (Phase != RunPhase.Playing) return;

            if (AwaitingChoices)
            {
                _choiceTimer -= dt;
                if (_choiceTimer <= 0f) AutoChooseRemaining();
                return;
            }

            Elapsed += dt;
            Enemies.BeginStep();
            UpdatePlayers(dt);
            UpdateSpawning(dt);
            StepEnemies(dt);
            StepPlayersCombat(dt);
            StepDelayedAreas(dt);
            StepProjectiles(dt);
            ProcessDeaths(giveXp: true);
            CollectSouls();
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

                float helpSpeed = 0f;
                foreach (var other in Players)
                    if (other != p && other.Alive && (other.Position - p.Position).sqrMagnitude <= ReviveRadius * ReviveRadius)
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
                int id = Enemies.Spawn(s.Type, s.Position, HealthMultiplier(Room.Index, Players.Count), DamageMultiplier(Room.Index), s.Elite);
                if (EnemyCatalog.Get(s.Type).Boss) Events.Add(new SimEvent(SimEventType.BossSpawned) { Id = id, Pos = s.Position });
            }

            if (Room.Cleared) return;
            switch (Room.Objective)
            {
                case RoomObjective.Waves:
                {
                    _waveTimer -= dt;
                    int alive = Enemies.Count + PendingSpawns.Count;
                    bool waveDone = alive <= Mathf.Max(1, _lastWaveSize / 4) || _waveTimer <= 0f;
                    if (waveDone && Room.Wave < Room.WaveCount) StartWave();
                    break;
                }
                case RoomObjective.Survive:
                {
                    _spawnTimer -= dt;
                    int target = Mathf.RoundToInt((6 + Room.Area * 2) * PlayerScale);
                    if (_spawnTimer <= 0f && Enemies.Count + PendingSpawns.Count < target)
                    {
                        _spawnTimer = 0.8f;
                        QueueSpawn(RandomEnemyType(), _spawnRng.Chance(0.06f));
                    }
                    break;
                }
                case RoomObjective.Boss:
                {
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f && Enemies.Count + PendingSpawns.Count < 1 + 2 * Room.Area)
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
            int size = Mathf.RoundToInt((5f + Room.Index * 0.5f + Room.Area * 1.5f) * PlayerScale);
            _lastWaveSize = size;
            bool eliteRoom = Room.Index % RoomsPerArea == 2;
            for (int i = 0; i < size; i++)
                QueueSpawn(RandomEnemyType(), (eliteRoom && i == 0) || _spawnRng.Chance(0.05f));
            Events.Add(new SimEvent(SimEventType.WaveStarted) { Int = Room.Wave, Value = Room.WaveCount });
        }

        private EnemyType RandomEnemyType()
        {
            int area = Room.Area;
            float r = _spawnRng.Value();
            if (area == 0) return r < 0.5f ? EnemyType.Skeleton : r < 0.75f ? EnemyType.SkeletonArcher : EnemyType.Ghoul;
            if (area == 1) return r < 0.35f ? EnemyType.Skeleton : r < 0.55f ? EnemyType.SkeletonArcher : r < 0.8f ? EnemyType.Ghoul : EnemyType.Cultist;
            return r < 0.25f ? EnemyType.Skeleton : r < 0.45f ? EnemyType.SkeletonArcher : r < 0.7f ? EnemyType.Ghoul : EnemyType.Cultist;
        }

        public void QueueSpawn(EnemyType type, bool elite, Vector2? at = null)
        {
            Vector2 pos = at ?? RandomSpawnPoint();
            PendingSpawns.Add(new PendingSpawn { Type = type, Position = pos, Timer = SpawnWarningTime, Elite = elite });
            Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = pos, Int = (int)type, Value = SpawnWarningTime });
        }

        private Vector2 RandomSpawnPoint()
        {
            Vector2 best = Vector2.zero;
            float bestDistance = -1f;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var p = new Vector2(
                    _spawnRng.Range(Bounds.xMin + 1f, Bounds.xMax - 1f),
                    _spawnRng.Range(Bounds.yMin + 1f, Bounds.yMax - 1.5f));
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
                PendingLevelUps++;
                foreach (var p in Players) p.SetLevel(TeamLevel);
            }
            if (PendingLevelUps > 0 && !AwaitingChoices) BeginChoices(Players, levelUp: true);
        }

        // ------------------------------------------------------------------ Perk choices

        private void BeginChoices(IEnumerable<SimPlayer> who, bool levelUp)
        {
            AwaitingChoices = true;
            _levelUpChoices = levelUp;
            _choiceTimer = ChoiceTimeout;
            foreach (var p in who)
            {
                p.PendingOffer = PerkRoller.Roll(_perkRng, p);
                Events.Add(new SimEvent(SimEventType.PerkOffer) { ClientId = p.ClientId, Seed = PerkOption.Pack(p.PendingOffer) });
            }
        }

        public void ChoosePerk(ulong clientId, int index)
        {
            var p = GetPlayer(clientId);
            if (p?.PendingOffer == null) return;
            var option = p.PendingOffer[Mathf.Clamp(index, 0, p.PendingOffer.Length - 1)];
            p.PendingOffer = null;
            PerkRoller.Apply(p, option);
            FinishChoicesIfDone();
        }

        private void FinishChoicesIfDone()
        {
            foreach (var p in Players) if (p.PendingOffer != null) return;
            AwaitingChoices = false;
            if (_levelUpChoices) PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);
            if (PendingLevelUps > 0) BeginChoices(Players, levelUp: true);
        }

        private void AutoChooseRemaining()
        {
            var waiting = new List<ulong>();
            foreach (var p in Players) if (p.PendingOffer != null) waiting.Add(p.ClientId);
            foreach (var id in waiting) ChoosePerk(id, 0);
        }

        // ------------------------------------------------------------------ Rooms

        private void StartRoom(int index)
        {
            Room.Index = index;
            Room.Progress = 0f;
            Room.Wave = 0;
            Room.Cleared = false;
            Room.ExitHold = 0f;
            Room.ExitPosition = new Vector2(0f, Bounds.yMax - 1.2f);
            _spawnTimer = 1.5f;
            foreach (var p in Players) p.SecondWindUsed = false;

            switch (index % RoomsPerArea)
            {
                case 0:
                case 2:
                    Room.Objective = RoomObjective.Waves;
                    Room.WaveCount = 4 + (Room.Area >= 1 ? 1 : 0);
                    Room.Target = Room.WaveCount;
                    _waveTimer = 1.2f;
                    break;
                case 1:
                    Room.Objective = RoomObjective.Survive;
                    Room.Target = SurviveSeconds;
                    break;
                default:
                    Room.Objective = RoomObjective.Boss;
                    Room.Target = 1f;
                    _spawnTimer = 12f;
                    PendingSpawns.Add(new PendingSpawn { Type = EnemyType.BoneKing, Position = new Vector2(0f, 3f), Timer = 1.6f });
                    Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = new Vector2(0f, 3f), Int = (int)EnemyType.BoneKing, Value = 1.6f });
                    break;
            }
            Events.Add(new SimEvent(SimEventType.RoomStarted) { Int = index });
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
                        if (Room.Wave >= Room.WaveCount && Enemies.Count == 0 && PendingSpawns.Count == 0)
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

            bool anyAlive = false;
            bool allAtExit = true;
            foreach (var p in Players)
            {
                if (!p.Alive) continue;
                anyAlive = true;
                if ((p.Position - Room.ExitPosition).sqrMagnitude > ExitRadius * ExitRadius) allAtExit = false;
            }
            Room.ExitHold = anyAlive && allAtExit ? Room.ExitHold + dt : 0f;
            if (Room.ExitHold >= ExitHoldTime) NextRoom();
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
            Events.Add(new SimEvent(SimEventType.RoomCleared) { Int = Room.Index, Pos = Room.ExitPosition });
        }

        private void NextRoom()
        {
            foreach (var soul in Souls)
            {
                TeamXp += soul.Value;
                Events.Add(new SimEvent(SimEventType.SoulCollected) { Id = soul.Id, ClientId = Players.Count > 0 ? Players[0].ClientId : 0 });
            }
            Souls.Clear();

            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Position = StartPosition(i);
                if (p.Downed) Revive(p, 0.35f, guardian: false); // everyone who makes it through gets back up
                Events.Add(new SimEvent(SimEventType.Teleport) { ClientId = p.ClientId, Pos = p.Position });
            }
            StartRoom(Room.Index + 1);
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
