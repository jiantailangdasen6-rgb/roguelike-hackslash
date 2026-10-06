using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The whole run, simulated on the host only: rooms, spawning, weapons, damage, XP, level-ups, loot.
    /// Pure C# so it can be unit-tested and run headless; the network layer feeds player positions in
    /// and ships <see cref="Events"/> out to clients.
    /// </summary>
    public sealed class RunSimulation
    {
        public const int RoomsPerArea = 4;
        public const int AreaCount = 3;
        public const int TotalRooms = RoomsPerArea * AreaCount;

        public const float ChoiceTimeout = 20f;
        public const float ExitRadius = 1.8f;
        public const float ExitHoldTime = 1.5f;
        public const float ReviveRadius = 1.8f;
        public const float ReviveTime = 3f;
        public const float HitInvulnerability = 0.5f;
        public const float SpawnRadius = 13f;
        public const float MinSpawnDistance = 9f;
        public const int MaxGems = 500;

        public static readonly Rect DefaultBounds = new(-30f, -20f, 60f, 40f);

        public readonly Rect Bounds;
        public readonly List<SimPlayer> Players = new();
        public readonly EnemySimulation Enemies;
        public readonly List<PlayerProjectile> Axes = new();
        public readonly List<HostileShot> Shots = new();
        public readonly List<XpGem> Gems = new();
        public readonly List<SimEvent> Events = new();
        public readonly RoomState Room = new();

        private readonly GameRandom _spawnRng;
        private readonly GameRandom _lootRng;
        private readonly GameRandom _upgradeRng;
        private readonly List<int> _hits = new();
        private readonly List<HostileShot> _newShots = new();
        private int _nextProjectileId = 1;
        private int _nextGemId = 1;
        private float _spawnBudget;
        private float _choiceTimer;

        public RunPhase Phase { get; private set; } = RunPhase.Playing;
        public float Elapsed { get; private set; }
        public int TeamLevel { get; private set; } = 1;
        public float TeamXp { get; private set; }
        public int PendingLevelUps { get; private set; }
        public bool AwaitingChoices { get; private set; }
        public float ChoiceTimeLeft => _choiceTimer;
        public float XpToNext => XpRequired(TeamLevel, Players.Count);

        public RunSimulation(ulong seed, Rect? bounds = null)
        {
            Bounds = bounds ?? DefaultBounds;
            Enemies = new EnemySimulation(Bounds);
            var root = new GameRandom(seed);
            _spawnRng = root.Fork(1);
            _lootRng = root.Fork(2);
            _upgradeRng = root.Fork(3);
        }

        public static float XpRequired(int level, int playerCount)
        {
            int l = level - 1;
            return (5f + 6f * l + 0.35f * l * l) * (1f + 0.5f * Mathf.Max(0, playerCount - 1));
        }

        public static float HealthMultiplier(int roomIndex, int playerCount) =>
            (1f + 0.3f * roomIndex) * (1f + 0.35f * Mathf.Max(0, playerCount - 1));

        // ------------------------------------------------------------------ Players

        public SimPlayer AddPlayer(ulong clientId)
        {
            var existing = GetPlayer(clientId);
            if (existing != null) return existing;
            var player = new SimPlayer(clientId, StartPosition(Players.Count));
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

        /// <summary>Called each tick with the position reported by the owning client.</summary>
        public void SetPlayerPosition(ulong clientId, Vector2 position)
        {
            var p = GetPlayer(clientId);
            if (p == null) return;
            Vector2 delta = position - p.Position;
            if (delta.sqrMagnitude > 0.000025f) p.Facing = delta.normalized;
            p.Position = position;
        }

        private static Vector2 StartPosition(int index) => index switch
        {
            0 => new Vector2(0f, 0f),
            1 => new Vector2(-1.2f, -0.6f),
            2 => new Vector2(1.2f, -0.6f),
            _ => new Vector2(0f, -1.2f),
        };

        // ------------------------------------------------------------------ Main loop

        public void Start() => StartRoom(0);

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
            UpdatePlayers(dt);
            if (!Room.Cleared) SpawnEnemies(dt);

            _newShots.Clear();
            Enemies.Step(dt, Players, _newShots);
            foreach (var shot in _newShots) AddShot(shot);

            foreach (var p in Players)
                if (p.Alive) StepWeapons(p, dt);

            StepAxes(dt);
            StepShots(dt);
            ContactDamage();
            ProcessDeaths(dropLoot: true);
            CollectGems();
            UpdateRoom(dt);
            CheckDefeat();
            CheckLevelUp();
        }

        private void UpdatePlayers(float dt)
        {
            foreach (var p in Players)
            {
                p.InvulnerableTimer -= dt;
                if (p.Alive)
                {
                    p.Heal(p.Stats.Get(StatType.Regen) * dt);
                    continue;
                }

                bool helped = false;
                foreach (var other in Players)
                    if (other != p && other.Alive && (other.Position - p.Position).sqrMagnitude <= ReviveRadius * ReviveRadius)
                        helped = true;

                p.ReviveProgress = helped
                    ? p.ReviveProgress + dt / ReviveTime
                    : Mathf.Max(0f, p.ReviveProgress - dt / ReviveTime * 0.5f);

                if (p.ReviveProgress >= 1f)
                {
                    p.Downed = false;
                    p.ReviveProgress = 0f;
                    p.Hp = p.MaxHp * 0.5f;
                    p.InvulnerableTimer = 2f;
                    Events.Add(new SimEvent(SimEventType.PlayerRevived) { ClientId = p.ClientId, Pos = p.Position });
                }
            }
        }

        // ------------------------------------------------------------------ Spawning

        private void SpawnEnemies(float dt)
        {
            int n = Mathf.Max(1, Players.Count);
            int d = Room.Index;
            float rate = (1.3f + 0.5f * d) * (1f + 0.6f * (n - 1));
            if (Room.Objective == RoomObjective.Boss) rate *= 0.5f;
            int maxAlive = (int)Mathf.Min(350f, (60f + 22f * d) * (1f + 0.5f * (n - 1)));

            _spawnBudget += rate * dt;
            while (_spawnBudget >= 1f)
            {
                _spawnBudget -= 1f;
                if (Enemies.Count >= maxAlive)
                {
                    _spawnBudget = 0f;
                    break;
                }
                TrySpawnOne(d, n);
            }
        }

        private void TrySpawnOne(int roomIndex, int playerCount)
        {
            var anchor = RandomLivingPlayer();
            if (anchor == null) return;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                float angle = _spawnRng.Range(0f, Mathf.PI * 2f);
                float distance = SpawnRadius * _spawnRng.Range(0.9f, 1.15f);
                Vector2 pos = anchor.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                pos.x = Mathf.Clamp(pos.x, Bounds.xMin + 0.5f, Bounds.xMax - 0.5f);
                pos.y = Mathf.Clamp(pos.y, Bounds.yMin + 0.5f, Bounds.yMax - 0.5f);
                if (!FarFromAllPlayers(pos, MinSpawnDistance)) continue;

                float archerChance = roomIndex == 0 ? 0.08f : 0.2f;
                var type = _spawnRng.Chance(archerChance) ? EnemyType.GoblinArcher : EnemyType.Slime;
                bool elite = _spawnRng.Chance(0.02f + 0.004f * roomIndex);
                Enemies.Spawn(type, pos, HealthMultiplier(roomIndex, playerCount), elite);
                return;
            }
        }

        private SimPlayer RandomLivingPlayer()
        {
            int alive = 0;
            foreach (var p in Players) if (p.Alive) alive++;
            if (alive == 0) return null;
            int pick = _spawnRng.Range(0, alive);
            foreach (var p in Players)
                if (p.Alive && pick-- == 0) return p;
            return null;
        }

        private bool FarFromAllPlayers(Vector2 pos, float minDistance)
        {
            foreach (var p in Players)
                if ((p.Position - pos).sqrMagnitude < minDistance * minDistance) return false;
            return true;
        }

        // ------------------------------------------------------------------ Weapons

        /// <summary>Effective aura radius for rendering (0 if the player has no aura).</summary>
        public static float AuraRadius(SimPlayer p)
        {
            var aura = p.GetWeapon(WeaponId.HolyAura);
            return aura == null ? 0f : aura.Stats.Area * p.Stats.Get(StatType.Area);
        }

        private void StepWeapons(SimPlayer p, float dt)
        {
            float might = p.Stats.Get(StatType.Might);
            float area = p.Stats.Get(StatType.Area);
            float cooldown = p.CooldownMultiplier;

            foreach (var w in p.Weapons)
            {
                w.Timer -= dt;
                if (w.Timer > 0f) continue;
                var s = w.Stats;

                switch (w.Id)
                {
                    case WeaponId.Greatsword:
                    {
                        w.Timer = s.Cooldown * cooldown;
                        float radius = s.Area * area;
                        // Swing toward the nearest enemy in reach (so kiting still hits), else where the player faces.
                        int nearest = Enemies.FindNearest(p.Position, radius + 1f);
                        Vector2 dir = p.Facing;
                        if (nearest >= 0)
                        {
                            Vector2 toNearest = Enemies[nearest].Position - p.Position;
                            if (toNearest.sqrMagnitude > 0.0001f) dir = toNearest.normalized;
                        }

                        float minDot = Mathf.Cos(WeaponCatalog.SlashHalfAngle * Mathf.Deg2Rad);
                        Enemies.QueryCircle(p.Position, radius, _hits);
                        foreach (int i in _hits)
                        {
                            Vector2 to = Enemies[i].Position - p.Position;
                            if (to.sqrMagnitude < 0.04f || Vector2.Dot(to.normalized, dir) >= minDot)
                                DamageEnemy(i, s.Damage * might);
                        }
                        Events.Add(new SimEvent(SimEventType.Slash) { ClientId = p.ClientId, Pos = p.Position, Vec = dir, Value = radius });
                        break;
                    }
                    case WeaponId.ThrowingAxe:
                    {
                        int target = Enemies.FindNearest(p.Position, WeaponCatalog.AxeTargetRange);
                        if (target < 0)
                        {
                            w.Timer = 0.2f; // re-check soon
                            break;
                        }
                        w.Timer = s.Cooldown * cooldown;
                        Vector2 aim = Enemies[target].Position - p.Position;
                        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : p.Facing;
                        int count = s.Count + Mathf.RoundToInt(p.Stats.Get(StatType.Amount));
                        for (int k = 0; k < count; k++)
                        {
                            float offset = (k - (count - 1) * 0.5f) * 12f * Mathf.Deg2Rad;
                            Vector2 dir = Rotate(aim, offset);
                            var axe = new PlayerProjectile
                            {
                                Id = _nextProjectileId++,
                                Position = p.Position,
                                Velocity = dir * WeaponCatalog.AxeSpeed,
                                Damage = s.Damage * might,
                                Radius = s.Area * area,
                                PierceLeft = s.Pierce,
                                Life = WeaponCatalog.AxeLifetime,
                            };
                            Axes.Add(axe);
                            Events.Add(new SimEvent(SimEventType.AxeSpawn) { Id = axe.Id, Pos = axe.Position, Vec = axe.Velocity, Value = axe.Life });
                        }
                        break;
                    }
                    case WeaponId.HolyAura:
                    {
                        w.Timer = s.Cooldown * cooldown;
                        Enemies.QueryCircle(p.Position, s.Area * area, _hits);
                        foreach (int i in _hits) DamageEnemy(i, s.Damage * might);
                        break;
                    }
                }
            }
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private void StepAxes(float dt)
        {
            for (int a = Axes.Count - 1; a >= 0; a--)
            {
                var axe = Axes[a];
                axe.Position += axe.Velocity * dt;
                axe.Life -= dt;
                bool spent = false;

                Enemies.QueryCircle(axe.Position, axe.Radius, _hits);
                foreach (int i in _hits)
                {
                    int enemyId = Enemies[i].Id;
                    if (axe.HitEnemyIds.Contains(enemyId)) continue;
                    axe.HitEnemyIds.Add(enemyId);
                    DamageEnemy(i, axe.Damage);
                    if (--axe.PierceLeft < 0)
                    {
                        spent = true;
                        break;
                    }
                }

                if (spent || axe.Life <= 0f)
                {
                    Events.Add(new SimEvent(SimEventType.AxeEnd) { Id = axe.Id });
                    Axes.RemoveAt(a);
                }
            }
        }

        // ------------------------------------------------------------------ Enemy attacks

        private void AddShot(HostileShot shot)
        {
            shot.Id = _nextProjectileId++;
            Shots.Add(shot);
            Events.Add(new SimEvent(SimEventType.ShotSpawn) { Id = shot.Id, Pos = shot.Position, Vec = shot.Velocity, Value = shot.Life });
        }

        private void StepShots(float dt)
        {
            for (int s = Shots.Count - 1; s >= 0; s--)
            {
                var shot = Shots[s];
                shot.Position += shot.Velocity * dt;
                shot.Life -= dt;
                bool hit = false;
                foreach (var p in Players)
                {
                    if (!p.Alive) continue;
                    float r = SimPlayer.BodyRadius + 0.15f;
                    if ((p.Position - shot.Position).sqrMagnitude <= r * r)
                    {
                        HurtPlayer(p, shot.Damage);
                        hit = true;
                        break;
                    }
                }

                if (hit || shot.Life <= 0f || !Bounds.Contains(shot.Position))
                {
                    Events.Add(new SimEvent(SimEventType.ShotEnd) { Id = shot.Id });
                    Shots.RemoveAt(s);
                }
                else
                {
                    Shots[s] = shot;
                }
            }
        }

        private void ContactDamage()
        {
            foreach (var p in Players)
            {
                if (!p.Alive || p.InvulnerableTimer > 0f) continue;
                Enemies.QueryCircle(p.Position, SimPlayer.BodyRadius, _hits);
                float worst = 0f;
                foreach (int i in _hits) worst = Mathf.Max(worst, Enemies[i].ContactDamage);
                if (worst > 0f) HurtPlayer(p, worst);
            }
        }

        private void HurtPlayer(SimPlayer p, float damage)
        {
            if (p.InvulnerableTimer > 0f || !p.Alive) return;
            float dealt = Mathf.Max(1f, damage - p.Stats.Get(StatType.Armor));
            p.Hp -= dealt;
            p.InvulnerableTimer = HitInvulnerability;
            Events.Add(new SimEvent(SimEventType.PlayerHurt) { ClientId = p.ClientId, Pos = p.Position, Value = dealt });

            if (p.Hp <= 0f)
            {
                p.Hp = 0f;
                p.Downed = true;
                p.ReviveProgress = 0f;
                Events.Add(new SimEvent(SimEventType.PlayerDowned) { ClientId = p.ClientId, Pos = p.Position });
            }
        }

        // ------------------------------------------------------------------ Deaths, XP, loot

        private void DamageEnemy(int index, float amount)
        {
            ref var e = ref Enemies[index];
            if (e.Dead) return;
            e.Health -= amount;
            Events.Add(new SimEvent(SimEventType.EnemyHit) { Id = e.Id, Pos = e.Position, Value = amount });
            if (e.Health <= 0f) e.Dead = true;
        }

        private void ProcessDeaths(bool dropLoot)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (!e.Dead) continue;
                var def = e.Def;

                Events.Add(new SimEvent(SimEventType.EnemyDied) { Id = e.Id, Pos = e.Position, Int = (int)e.Type });
                int xp = Mathf.Max(1, Mathf.RoundToInt(def.Xp * (e.Elite ? EnemyCatalog.EliteXpMultiplier : 1) * (1f + 0.2f * Room.Index)));
                SpawnGem(e.Position, xp);

                if (!dropLoot) continue;
                if (!Room.Cleared && Room.Objective == RoomObjective.Kill) Room.Progress++;
                if (def.IsBoss) Room.Progress = 1f;

                float chance = def.IsBoss ? 1f : e.Elite ? 0.6f : 0.006f;
                int drops = def.IsBoss ? 2 : 1;
                float boost = def.IsBoss ? 3f : e.Elite ? 1f : 0f;
                foreach (var p in Players)
                    for (int d = 0; d < drops; d++)
                        if (_lootRng.Chance(chance)) DropLoot(p, e.Position, boost);
            }
            Enemies.RemoveDead();
        }

        private void DropLoot(SimPlayer p, Vector2 position, float rarityBoost)
        {
            Vector2 jitter = new(_lootRng.Range(-0.6f, 0.6f), _lootRng.Range(-0.6f, 0.6f));
            Events.Add(new SimEvent(SimEventType.LootDrop)
            {
                ClientId = p.ClientId,
                Pos = position + jitter,
                Seed = _lootRng.NextULong(),
                Int = 1 + Room.Index * 2,
                Value = rarityBoost,
            });
        }

        private void SpawnGem(Vector2 position, int value)
        {
            if (Gems.Count >= MaxGems)
            {
                // Too many on the floor: fold the XP straight into the team.
                TeamXp += value;
                return;
            }
            var gem = new XpGem { Id = _nextGemId++, Position = position, Value = value };
            Gems.Add(gem);
            Events.Add(new SimEvent(SimEventType.GemSpawn) { Id = gem.Id, Pos = position, Int = value });
        }

        private void CollectGems()
        {
            for (int g = Gems.Count - 1; g >= 0; g--)
            {
                var gem = Gems[g];
                foreach (var p in Players)
                {
                    if (!p.Alive) continue;
                    float r = p.Stats.Get(StatType.PickupRange);
                    if ((p.Position - gem.Position).sqrMagnitude > r * r) continue;
                    TeamXp += gem.Value;
                    Events.Add(new SimEvent(SimEventType.GemCollected) { Id = gem.Id, ClientId = p.ClientId });
                    Gems.RemoveAt(g);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ Level-ups

        private void CheckLevelUp()
        {
            if (Players.Count == 0) return;
            while (TeamXp >= XpToNext)
            {
                TeamXp -= XpToNext;
                TeamLevel++;
                PendingLevelUps++;
            }
            if (PendingLevelUps > 0 && !AwaitingChoices) BeginChoices();
        }

        private void BeginChoices()
        {
            AwaitingChoices = true;
            _choiceTimer = ChoiceTimeout;
            foreach (var p in Players)
            {
                p.PendingOffer = UpgradeRoller.Roll(_upgradeRng, p);
                Events.Add(new SimEvent(SimEventType.LevelUpOffer) { ClientId = p.ClientId, Seed = UpgradeOption.Pack(p.PendingOffer) });
            }
        }

        public void ChooseUpgrade(ulong clientId, int index)
        {
            var p = GetPlayer(clientId);
            if (p?.PendingOffer == null) return;
            var option = p.PendingOffer[Mathf.Clamp(index, 0, p.PendingOffer.Length - 1)];
            p.PendingOffer = null;
            UpgradeRoller.Apply(p, option);
            FinishChoicesIfDone();
        }

        private void FinishChoicesIfDone()
        {
            foreach (var p in Players) if (p.PendingOffer != null) return;
            AwaitingChoices = false;
            PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);
            if (PendingLevelUps > 0) BeginChoices();
        }

        private void AutoChooseRemaining()
        {
            var waiting = new List<ulong>();
            foreach (var p in Players) if (p.PendingOffer != null) waiting.Add(p.ClientId);
            foreach (var id in waiting)
            {
                var p = GetPlayer(id);
                if (p?.PendingOffer != null) ChooseUpgrade(id, 0);
            }
        }

        // ------------------------------------------------------------------ Rooms

        private void StartRoom(int index)
        {
            Room.Index = index;
            Room.Progress = 0f;
            Room.Cleared = false;
            Room.ExitHold = 0f;
            Room.ExitPosition = new Vector2(0f, Bounds.yMax - 3f);
            _spawnBudget = 0f;

            switch (index % RoomsPerArea)
            {
                case 0:
                    Room.Objective = RoomObjective.Survive;
                    Room.Target = 60f;
                    break;
                case 1:
                    Room.Objective = RoomObjective.Kill;
                    Room.Target = 50 + 12 * index;
                    break;
                case 2:
                    Room.Objective = RoomObjective.Survive;
                    Room.Target = 75f;
                    break;
                default:
                    Room.Objective = RoomObjective.Boss;
                    Room.Target = 1f;
                    SpawnBoss();
                    break;
            }
            Events.Add(new SimEvent(SimEventType.RoomStarted) { Int = index });
        }

        private void SpawnBoss()
        {
            Vector2 pos = new(0f, Mathf.Min(Bounds.yMax - 3f, 10f));
            float hp = HealthMultiplier(Room.Index, Players.Count) * (1f + 0.5f * Room.Area);
            int id = Enemies.Spawn(EnemyType.KingSlime, pos, hp, elite: false);
            Events.Add(new SimEvent(SimEventType.BossSpawned) { Id = id, Pos = pos });
        }

        private void UpdateRoom(float dt)
        {
            if (!Room.Cleared)
            {
                if (Room.Objective == RoomObjective.Survive) Room.Progress += dt;
                if (Room.Progress >= Room.Target) ClearRoom();
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
            Room.Progress = Room.Target;

            // Remaining enemies burst into XP (no loot) so clearing feels rewarding.
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Dead = true;
            ProcessDeaths(dropLoot: false);

            foreach (var shot in Shots) Events.Add(new SimEvent(SimEventType.ShotEnd) { Id = shot.Id });
            Shots.Clear();

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
            // Auto-collect leftover gems.
            ulong collector = Players.Count > 0 ? Players[0].ClientId : 0;
            foreach (var gem in Gems)
            {
                TeamXp += gem.Value;
                Events.Add(new SimEvent(SimEventType.GemCollected) { Id = gem.Id, ClientId = collector });
            }
            Gems.Clear();

            foreach (var axe in Axes) Events.Add(new SimEvent(SimEventType.AxeEnd) { Id = axe.Id });
            Axes.Clear();

            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Position = StartPosition(i);
                if (p.Downed)
                {
                    // Everyone who reaches the next room is back on their feet.
                    p.Downed = false;
                    p.ReviveProgress = 0f;
                    p.Hp = p.MaxHp * 0.3f;
                    Events.Add(new SimEvent(SimEventType.PlayerRevived) { ClientId = p.ClientId, Pos = p.Position });
                }
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
