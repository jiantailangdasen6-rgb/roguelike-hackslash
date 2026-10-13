using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The whole run, simulated on the host only: the area maps and their rooms, enemy AI with telegraphed attacks,
    /// class abilities, statuses and synergies, XP and perk picks. Pure C# so it can be unit-tested and
    /// run headless; the network layer feeds player positions/inputs in and ships <see cref="Events"/> out.
    /// Split across RunSimulation.*.cs (core loop and spawning here, the map and its rooms in Dungeon, Combat, Enemies...).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const int RoomsPerArea = 4; // strength tiers per area (room index = area * 4 + tier)
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

        /// <summary>Walkable floor of one room, around its centre (inside the walls).</summary>
        public static readonly Rect DefaultBounds = new(-14f, -8f, 28f, 16f);

        public readonly List<SimPlayer> Players = new();
        public readonly EnemySimulation Enemies;
        public readonly List<Projectile> Projectiles = new();
        public readonly List<Soul> Souls = new();
        public readonly List<PendingSpawn> PendingSpawns = new();
        public readonly List<SimEvent> Events = new();

        private readonly GameRandom _spawnRng;
        private readonly GameRandom _perkRng;
        private readonly GameRandom _combatRng;
        private readonly GameRandom _aiRng;
        private readonly List<int> _hits = new();
        private int _nextProjectileId = 1;
        private int _nextSoulId = 1;

        public readonly Difficulty Difficulty;
        public readonly DifficultyDef DifficultyDef;

        /// <summary>
        /// The room being worked on right now: spawning, monster strength and collision all read it. The loops set
        /// it to the room of the monster, trap or fight they handle (several rooms can be busy at once).
        /// </summary>
        public RoomState Room { get; private set; }
        /// <summary>Obstacles and gimmicks of <see cref="Room"/> (in world coordinates).</summary>
        public RoomLayout Layout => Room.Layout;
        /// <summary>Floor of <see cref="Room"/>.</summary>
        public Rect Bounds => Room.Bounds;

        public RunPhase Phase { get; private set; } = RunPhase.Playing;
        public float Elapsed { get; private set; }
        public int TeamLevel { get; private set; } = 1;
        public float TeamXp { get; private set; }
        public int Kills { get; private set; }
        public int BossesKilled { get; private set; }
        public int RoomsCleared { get; private set; }
        public float XpToNext => XpRequired(TeamLevel, Players.Count);

        public RunSimulation(ulong seed, Difficulty difficulty = Difficulty.Normal)
        {
            Seed = seed;
            Enemies = new EnemySimulation(DungeonMap.WorldBounds);
            Difficulty = difficulty;
            DifficultyDef = DifficultyCatalog.Get(difficulty);
            var root = new GameRandom(seed);
            _spawnRng = root.Fork(1);
            _perkRng = root.Fork(2);
            _combatRng = root.Fork(3);
            _aiRng = root.Fork(4);
            _lootRng = root.Fork(7);
            BuildArea(0);
        }

        /// <summary>Bosses arrive with this share of their health (automated screenshots of the trials: -bosshp).</summary>
        public float BossStartHealth = 1f;

        public static float XpRequired(int level, int playerCount) =>
            (12f + 7f * (level - 1)) * (1f + 0.5f * Mathf.Max(0, playerCount - 1));

        public static float HealthMultiplier(int roomIndex, int playerCount) =>
            (1f + 0.18f * roomIndex) * (1f + 0.45f * Mathf.Max(0, playerCount - 1));

        public static float DamageMultiplier(int roomIndex) => 1f + 0.12f * roomIndex;

        /// <summary>Monster health for the current room, the players in it and the difficulty.</summary>
        public float EnemyHealthScale => HealthMultiplier(Room.Index, PlayersIn(Room)) * DifficultyDef.EnemyHealth;
        public float EnemyDamageScale => DamageMultiplier(Room.Index) * DifficultyDef.EnemyDamage;
        public float EliteChance => DifficultyDef.EliteChance;

        /// <summary>The boss of the current area.</summary>
        public EnemyType AreaBoss => EnemyCatalog.AreaBosses[Mathf.Min(Area, EnemyCatalog.AreaBosses.Length - 1)];

        // ------------------------------------------------------------------ Players

        public SimPlayer AddPlayer(ulong clientId, Loadout loadout = null)
        {
            var existing = GetPlayer(clientId);
            if (existing != null) return existing;
            var player = new SimPlayer(clientId, Map.Start.Origin + StartPosition(Players.Count), loadout);
            player.SetDifficulty(DifficultyDef);
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
            p.CagedBy = 0;
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
                player = new SimPlayer(clientId, Map.Start.Origin + StartPosition(Players.Count), loadout);
                player.SetLevel(TeamLevel);
                for (int level = 1; level < TeamLevel; level++) player.StockedChoices.Enqueue(ChoiceKind.LevelUp);
                for (int b = 0; b < BlessingsGranted; b++) player.StockedChoices.Enqueue(ChoiceKind.Blessing);
                player.Hp = player.MaxHp;
            }
            player.SetDifficulty(DifficultyDef);

            foreach (var mate in Players)
            {
                if (!mate.Alive) continue;
                player.Position = mate.Position + new Vector2(0.8f, -0.6f);
                break;
            }
            player.RoomId = Map.RoomAt(player.Position)?.Id ?? -1; // arriving in a fight: picks wait
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

        /// <summary>Where the team stands on arriving in a room (around its centre, a little low).</summary>
        private static Vector2 StartPosition(int index) => index switch
        {
            0 => new Vector2(0f, -4f),
            1 => new Vector2(-1.5f, -4.8f),
            2 => new Vector2(1.5f, -4.8f),
            _ => new Vector2(0f, -5.5f),
        };

        // ------------------------------------------------------------------ Main loop

        /// <param name="firstRoom">
        /// Debug: start deeper in the run for testing and screenshots. Area = firstRoom / 4; a last tier (3, 7, 11)
        /// starts in the boss room, tier 2 at the campfire.
        /// </param>
        public void Start(int firstRoom = 0)
        {
            firstRoom = Mathf.Clamp(firstRoom, 0, TotalRooms - 1);
            int area = firstRoom / RoomsPerArea, tier = firstRoom % RoomsPerArea;
            if (area != Area) BuildArea(area);
            EnterArea(tier == 3 ? Map.Boss : tier == 2 ? Map.Campfire : Map.Start);
            if (tier == 3) BossDoorOpen = true;
            foreach (var p in Players) GiveLoaners(p);
            foreach (var p in Players) if (p.HasStartPerk) p.StockedChoices.Enqueue(ChoiceKind.Start);
            OfferAll();
        }

        public void Step(float dt)
        {
            if (Phase != RunPhase.Playing) return;

            TickOffers(dt);
            Elapsed += dt;
            Enemies.BeginStep();
            UpdatePlayers(dt);
            UpdateRooms(dt);
            UpdateSpawning(dt);
            StepEnemies(dt);
            StepBossTrials(dt);
            StepPlayersCombat(dt);
            StepDelayedAreas(dt);
            StepZones(dt);
            StepHazards(dt);
            StepProjectiles(dt);
            ProcessDeaths(giveXp: true);
            CollectSouls();
            CollectLoot();
            UpdateObjectives(dt);
            StepPuzzles(dt);
            StepBell(dt);
            StepGods(dt);
            StepDark();
            UpdateDoors(dt);
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
                if (DifficultyDef.Has(DifficultyMod.Mercy)) helpSpeed *= DifficultyCatalog.MercyRevive;

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
                Room = RoomStates[s.Room];
                if (Room.Cleared) continue;
                var def = EnemyCatalog.Get(s.Type);
                Vector2 at = def.Flying ? s.Position : Arena.Resolve(Layout, s.Position, def.Radius);
                int id = Enemies.Spawn(s.Type, at, EnemyHealthScale, EnemyDamageScale, s.Elite);
                Enemies[Enemies.IndexOf(id)].RoomId = s.Room;
                if (s.Champion) MakeChampion(Enemies.IndexOf(id));
                if (def.Boss)
                {
                    Enemies[Enemies.IndexOf(id)].Health *= Mathf.Clamp(BossStartHealth, 0.01f, 1f);
                    Events.Add(new SimEvent(SimEventType.BossSpawned) { Id = id, Pos = at, Int = (int)s.Type });
                }
            }

            foreach (var room in RoomStates)
            {
                if (!room.Fighting) continue;
                Room = room;
                int hostile = HostilesIn(room) + PendingIn(room);
                switch (room.Objective)
                {
                    case RoomObjective.Waves:
                    {
                        room.WaveTimer -= dt;
                        bool waveDone = hostile <= Mathf.Max(1, room.LastWaveSize / 4) || room.WaveTimer <= 0f;
                        // A room nobody stands in waits for them to come back.
                        if (waveDone && room.Wave < room.WaveCount && PlayersIn(room, alive: true) > 0) StartWave();
                        break;
                    }
                    case RoomObjective.Survive:
                    {
                        room.SpawnTimer -= dt;
                        int target = Mathf.RoundToInt((6 + room.Area * 2) * PlayerScale * DifficultyDef.EnemyCount);
                        if (room.SpawnTimer <= 0f && hostile < target)
                        {
                            room.SpawnTimer = 0.8f;
                            QueueSpawn(RandomEnemyType(), _spawnRng.Chance(EliteChance + 0.01f));
                        }
                        break;
                    }
                    case RoomObjective.Plates:
                    case RoomObjective.Twin:
                    case RoomObjective.Crystal:
                    {
                        // A steady trickle that keeps the puzzle from being a walk in the park.
                        room.SpawnTimer -= dt;
                        int target = Mathf.RoundToInt((room.Objective == RoomObjective.Crystal ? 5 + room.Area * 2 : 3 + room.Area) * PlayerScale * DifficultyDef.EnemyCount);
                        if (room.SpawnTimer <= 0f && hostile < target && PlayersIn(room, alive: true) > 0)
                        {
                            room.SpawnTimer = room.Objective == RoomObjective.Crystal ? 1.1f : 1.8f;
                            QueueSpawn(RandomEnemyType(), _spawnRng.Chance(EliteChance));
                        }
                        break;
                    }
                    case RoomObjective.Boss:
                    {
                        room.SpawnTimer -= dt;
                        if (room.SpawnTimer <= 0f && hostile < 1 + 2 * room.Area)
                        {
                            room.SpawnTimer = 9f;
                            QueueSpawn(RandomEnemyType(), false);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>Living players (or all) standing in a room; at least 1 for scaling.</summary>
        public int PlayersIn(RoomState room, bool alive = false)
        {
            int n = 0;
            foreach (var p in Players)
                if (p.RoomId == room.Id && (!alive || p.Alive)) n++;
            return alive ? n : Mathf.Max(1, n);
        }

        private int HostilesIn(RoomState room)
        {
            int n = 0;
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (!e.Dead && e.RoomId == room.Id && !e.Def.Prop) n++;
            }
            return n;
        }

        private int PendingIn(RoomState room)
        {
            int n = 0;
            foreach (var s in PendingSpawns) if (s.Room == room.Id) n++;
            return n;
        }

        private float PlayerScale => 1f + 0.6f * (PlayersIn(Room) - 1);

        private void StartWave()
        {
            Room.Wave++;
            Room.WaveTimer = 20f;
            if (Room.Kind == RoomKind.MiniBoss)
            {
                SpawnChampion();
                Room.LastWaveSize = 4;
                Events.Add(new SimEvent(SimEventType.WaveStarted) { Int = Room.Wave, Value = Room.WaveCount, Id = Room.Id });
                return;
            }
            int size = Mathf.RoundToInt((5f + Room.Index * 0.5f + Room.Area * 1.5f) * PlayerScale * DifficultyDef.EnemyCount);
            Room.LastWaveSize = size;
            bool eliteRoom = Room.Kind == RoomKind.Elite;
            for (int i = 0; i < size; i++)
                QueueSpawn(RandomEnemyType(), (eliteRoom && i == 0) || _spawnRng.Chance(EliteChance));
            Events.Add(new SimEvent(SimEventType.WaveStarted) { Int = Room.Wave, Value = Room.WaveCount, Id = Room.Id });
        }

        // Spawn weights per area (index = EnemyType). Each area adds new monsters to the mix.
        private static readonly (EnemyType Type, int Weight)[][] AreaPools =
        {
            new[] { (EnemyType.Skeleton, 38), (EnemyType.SkeletonArcher, 20), (EnemyType.Ghoul, 18), (EnemyType.Bat, 9), (EnemyType.ShieldSkeleton, 10) },
            new[]
            {
                (EnemyType.Skeleton, 20), (EnemyType.SkeletonArcher, 15), (EnemyType.Ghoul, 14), (EnemyType.Cultist, 14),
                (EnemyType.Bat, 7), (EnemyType.Bloater, 14), (EnemyType.DreadKnight, 11), (EnemyType.Flagellant, 14),
            },
            new[]
            {
                (EnemyType.Skeleton, 10), (EnemyType.SkeletonArcher, 12), (EnemyType.Ghoul, 12), (EnemyType.Cultist, 11), (EnemyType.Bat, 6),
                (EnemyType.Bloater, 11), (EnemyType.DreadKnight, 12), (EnemyType.Necromancer, 9), (EnemyType.Wraith, 13),
                (EnemyType.AbyssEye, 11), (EnemyType.ShadeStalker, 11),
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

        /// <summary>A monster appears in <see cref="Room"/> after a warning rune (at a random free spot unless given).</summary>
        public void QueueSpawn(EnemyType type, bool elite, Vector2? at = null)
        {
            Vector2 pos = at ?? RandomSpawnPoint();
            PendingSpawns.Add(new PendingSpawn { Room = Room.Id, Type = type, Position = pos, Timer = SpawnWarningTime, Elite = elite });
            Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = pos, Int = (int)type, Value = SpawnWarningTime });

            // Bats come in flocks.
            if (type == EnemyType.Bat && at == null)
                for (int k = 0; k < 2; k++)
                {
                    Vector2 near = ClampToRoom(pos + Rotate(Vector2.right, k * 140f + 30f) * 0.9f, 0.5f);
                    PendingSpawns.Add(new PendingSpawn { Room = Room.Id, Type = type, Position = near, Timer = SpawnWarningTime + 0.1f * (k + 1) });
                    Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = near, Int = (int)type, Value = SpawnWarningTime + 0.1f * (k + 1) });
                }
        }

        /// <summary>Keeps a point on <see cref="Room"/>'s floor (what is left of it after a collapse).</summary>
        private Vector2 ClampToRoom(Vector2 p, float radius)
        {
            var area = PlayRect;
            return new Vector2(Mathf.Clamp(p.x, area.xMin + radius, area.xMax - radius), Mathf.Clamp(p.y, area.yMin + radius, area.yMax - radius));
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
            var area = PlayRect;
            Vector2 best = area.center;
            float bestDistance = -1f;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                var p = new Vector2(
                    _spawnRng.Range(area.xMin + 1f, area.xMax - 1f),
                    _spawnRng.Range(area.yMin + 1f, area.yMax - 1.5f));
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
        // Fighting and preparing are kept apart: picks (level-ups, pedestals) are banked and offered to each player
        // while the room they stand in is quiet. Nobody's pick pauses anyone else.

        /// <summary>This player can choose now: the room they stand in is not being fought.</summary>
        public bool CanChoose(SimPlayer p) => p.RoomId < 0 || p.RoomId >= RoomStates.Count || !RoomStates[p.RoomId].Fighting;

        public bool AnyPicksLeft
        {
            get
            {
                foreach (var p in Players) if (p.PicksLeft > 0) return true;
                return false;
            }
        }

        /// <summary>Someone has a choice waiting on screen.</summary>
        public bool AwaitingChoices
        {
            get
            {
                foreach (var p in Players) if (p.PendingOffer != null) return true;
                return false;
            }
        }

        private void OfferAll()
        {
            foreach (var p in Players) OfferNext(p);
        }

        private void OfferNext(SimPlayer p)
        {
            if (!CanChoose(p) || p.PendingOffer != null || p.StockedChoices.Count == 0) return;
            var kind = p.StockedChoices.Dequeue();
            var shown = kind;
            p.PendingOffer = kind switch
            {
                ChoiceKind.Upgrade => PerkRoller.RollUpgrade(_perkRng, p),
                ChoiceKind.Shop => PerkRoller.RollShop(_perkRng, p),
                ChoiceKind.Blessing => PerkRoller.RollBlessing(_perkRng, p),
                _ => null,
            };
            if (p.PendingOffer == null)
            {
                if (kind is ChoiceKind.Upgrade or ChoiceKind.Blessing) shown = ChoiceKind.Boon; // nothing to upgrade (or every blessing taken): a new perk instead
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
            if (p.OfferKind == ChoiceKind.Shop)
            {
                // The merchant: index past the wares (or one the team can't afford) buys nothing.
                var wares = p.PendingOffer;
                p.PendingOffer = null;
                if (index >= 0 && index < wares.Length) Buy(p, wares[index]);
                OfferNext(p);
                return;
            }
            var option = p.PendingOffer[Mathf.Clamp(index, 0, p.PendingOffer.Length - 1)];
            p.PendingOffer = null;
            PerkRoller.Apply(p, option);
            if (option.IsPerk) BlessingTaken(p, (PerkId)option.Perk);
            OfferNext(p);
        }

        /// <summary>The "buy nothing" choice at the merchant.</summary>
        public const int ShopSkip = PerkRoller.OptionCount;

        /// <summary>Soul shards the team would take home now: the merchant is paid out of this.</summary>
        public int Purse => RewardCatalog.Purse(RoomsCleared, BossesKilled, Kills, Difficulty, BonusShards);

        private void Buy(SimPlayer p, PerkOption ware)
        {
            int price = RewardCatalog.Price(ware, Area);
            if (price > Purse) return;
            BonusShards -= price;
            if (ware.IsShopTreasure) GrantTreasureTo(p, 0);
            else if (ware.IsShopVigor)
            {
                float before = p.Hp;
                p.AddVigor(1);
                if (p.Alive) p.Hp = p.MaxHp;
                if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
            }
            else PerkRoller.Apply(p, ware);
            Events.Add(new SimEvent(SimEventType.ItemSalvaged) { ClientId = p.ClientId, Value = -price }); // shards spent (negative salvage)
        }

        /// <summary>Someone away from the keyboard can't hold a choice forever: the first option is taken for them.</summary>
        private void TickOffers(float dt)
        {
            foreach (var p in Players)
            {
                if (p.PendingOffer == null) continue;
                p.OfferTimer -= dt;
                if (p.OfferTimer <= 0f) ChoosePerk(p.ClientId, p.OfferKind == ChoiceKind.Shop ? ShopSkip : 0);
            }
        }

        private void CheckDefeat()
        {
            if (Players.Count == 0) return;
            foreach (var p in Players) if (p.Alive) return;
            SplitRunEnded();
            Phase = RunPhase.Defeat;
            Events.Add(new SimEvent(SimEventType.RunEnded) { Int = 0 });
        }
    }
}
