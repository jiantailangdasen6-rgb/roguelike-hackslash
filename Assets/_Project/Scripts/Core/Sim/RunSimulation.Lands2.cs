using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The fungal grotto and the cursed library (Biome.Grotto, Biome.Library): their monsters' choices, the spore clouds,
    /// the pages written on the floor, the two champions' tricks and the four lords' trials. The Mycelium Mother fills
    /// her hall with spore fog (only the air by the glowing caps is clean, and the caps wilt one by one); the Rot Giant
    /// plants a parasite that jumps to whoever stands close (cured at the lilies); the Archivist's forbidden tomes shine
    /// in an order that has to be struck back; the Ink Beast dives into the ink and bursts up under players until the
    /// candelabra are lit.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float SporeCloudRadius = 1.5f;
        public const float SporeCloudTime = 3.5f;

        public const float FogTime = 14f;
        public const float FogTick = 1f;
        public const float FogHurt = 0.04f;      // share of max HP per tick outside the clean air
        public const float CapRadius = 2.4f;
        public const float CapLife = 4.5f;

        public const float ParasiteTime = 16f;
        public const float ParasiteJumpEvery = 3f;
        public const float ParasiteReach = 2.6f;
        public const float ParasiteHurt = 0.03f;
        public const float CureRadius = 1.6f;
        public const float CureTime = 1.2f;

        public const float SequenceTime = 30f;
        public const float TomeShowGap = 0.85f;

        public const float DiveTime = 20f;
        public const float EruptEvery = 1.8f;
        public const float CandleRadius = 1.5f;
        public const float CandleTime = 1f;

        private readonly List<int> _caps = new();
        private float _capTimer, _fogTick;
        private readonly HashSet<ulong> _hosts = new();
        private readonly Dictionary<ulong, float> _cure = new();
        private float _jumpTimer, _parasiteTick, _lilyRespawn;
        private readonly List<int> _tomeOrder = new();
        private int _tomeStep, _showIndex;
        private float _showTimer, _tomeCool;
        private bool _showing;
        private float _eruptTimer;
        private readonly Dictionary<int, float> _candleHold = new();
        private readonly Dictionary<int, float> _capPuff = new();

        /// <summary>Who carries the Rot Giant's parasite (for tests and bots).</summary>
        public IReadOnlyCollection<ulong> ParasiteHosts => _hosts;

        private void ResetLands2()
        {
            _caps.Clear();
            _hosts.Clear();
            _cure.Clear();
            _tomeOrder.Clear();
            _showing = false;
            _candleHold.Clear();
            _capPuff.Clear();
            _lilyRespawn = 0f;
        }

        // ------------------------------------------------------------------ Choices

        /// <summary>Which move the grotto's and the library's monsters and lords use next.</summary>
        private static int NewLandAttack(EnemyDef def, float distance, int n, bool roomForMinions)
        {
            switch (def.Type)
            {
                case EnemyType.Mycelid:
                case EnemyType.Bookworm:
                    return distance > 3f && n % 2 == 1 ? 1 : 0;      // lunges in from afar
                case EnemyType.MossShaman:
                case EnemyType.Scribe:
                    return n % 3 == 2 ? 1 : 0;
                case EnemyType.GreatCap:
                    return distance < 2.4f ? 0 : 1;                   // spores from afar, a stamp up close
                case EnemyType.MyceliumMother:
                    // 0 fan of spores, 1 spore rain, 2 burst around her, 3 summon
                    if (distance < 3f && n % 2 == 0) return 2;
                    switch (n % 4)
                    {
                        case 1: return 1;
                        case 3: return roomForMinions ? 3 : 1;
                        default: return 0;
                    }
                case EnemyType.RotGiant:
                    // 0 fist, 1 charge, 2 stamp around him, 3 summon
                    if (distance < 3.2f) return n % 2 == 0 ? 2 : 0;
                    switch (n % 5)
                    {
                        case 1: case 3: return 1;
                        case 4: return roomForMinions ? 3 : 1;
                        default: return 0;
                    }
                case EnemyType.Archivist:
                    // 0 ring of pages, 1 curse rain, 2 a line of ink, 3 summon
                    switch (n % 5)
                    {
                        case 1: case 4: return 1;
                        case 2: return 2;
                        case 3: return roomForMinions ? 3 : 0;
                        default: return 0;
                    }
                case EnemyType.InkBeast:
                    return SecondLordAttack(def, distance, n, roomForMinions); // bite, charge, rain, summon
                default:
                    return 0;
            }
        }

        private void NewLordTrials(int b, EnemyType type, float hp)
        {
            switch (type)
            {
                case EnemyType.MyceliumMother:
                    if (Threshold(0, hp, 0.65f) || Threshold(1, hp, 0.3f)) BeginFog(b);
                    break;
                case EnemyType.RotGiant:
                    if (Threshold(0, hp, 0.6f) || Threshold(1, hp, 0.3f)) BeginParasite(b);
                    break;
                case EnemyType.Archivist:
                    if (Threshold(0, hp, 0.66f) || Threshold(1, hp, 0.33f)) BeginSequence(b);
                    break;
                case EnemyType.InkBeast:
                    if (Threshold(0, hp, 0.6f) || Threshold(1, hp, 0.3f)) BeginDive(b);
                    break;
            }
        }

        private void StepNewLordTrial(int b, float dt)
        {
            switch (ActiveTrial)
            {
                case BossTrial.Fog: StepFog(b, dt); break;
                case BossTrial.Parasite: StepParasite(b, dt); break;
                case BossTrial.Sequence: StepSequence(b, dt); break;
                case BossTrial.Dive: StepDive(b, dt); break;
            }
        }

        /// <summary>A free spot in the hall, away from these others.</summary>
        private Vector2 OpenSpot(List<Vector2> awayFrom, float distance)
        {
            var area = PlayRect;
            Vector2 best = area.center;
            for (int k = 0; k < 12; k++)
            {
                var p = new Vector2(_aiRng.Range(area.xMin + 2f, area.xMax - 2f), _aiRng.Range(area.yMin + 1.6f, area.yMax - 2f));
                p = Arena.Resolve(Layout, p, 1f);
                best = p;
                bool clear = true;
                foreach (var q in awayFrom) clear &= (q - p).sqrMagnitude >= distance * distance;
                if (clear) break;
            }
            return best;
        }

        private List<Vector2> LivingPropSpots(EnemyType type)
        {
            var list = new List<Vector2>();
            foreach (int id in _trialProps)
            {
                int i = Enemies.IndexOf(id);
                if (i >= 0 && !Enemies[i].Dead && Enemies[i].Type == type) list.Add(Enemies[i].Position);
            }
            return list;
        }

        /// <summary>Takes a trial object away quietly (no reward, no debris).</summary>
        private void Wither(int id)
        {
            int i = Enemies.IndexOf(id);
            if (i < 0 || Enemies[i].Dead) return;
            _vanished.Add(id);
            Enemies[i].Dead = true;
        }

        // ------------------------------------------------------------------ Mycelium Mother: the spore fog

        private void BeginFog(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Fog;
            TrialTimer = FogTime + (Wrath ? 3f : 0f);
            TrialTotal = TrialDone = 0;
            _fogTick = 1.2f;
            _capTimer = CapLife;
            _caps.Clear();
            foreach (var spot in TrialSpots(3, 8f, 4.2f, _aiRng.Range(0f, 120f))) SproutCap(spot);
            for (int k = 0; k <= PlayersIn(Room); k++) QueueSpawn(EnemyType.Sporeling, false);
            TrialEvent(TrialMoment.FogStart, boss.Position, TrialTimer);
        }

        private void SproutCap(Vector2 at)
        {
            int id = SpawnTrialProp(EnemyType.Glowcap, at, 1f);
            Enemies[Enemies.IndexOf(id)].Moving = true; // shown glowing
            _caps.Add(id);
            TrialEvent(TrialMoment.CapSprout, at);
        }

        private bool InCleanAir(Vector2 p)
        {
            foreach (int id in _caps)
            {
                int i = Enemies.IndexOf(id);
                if (i >= 0 && !Enemies[i].Dead && (Enemies[i].Position - p).sqrMagnitude <= CapRadius * CapRadius) return true;
            }
            return false;
        }

        private void StepFog(int b, float dt)
        {
            TrialTimer -= dt;
            // The oldest cap wilts and another sprouts somewhere else: the clean air moves.
            _capTimer -= dt;
            if (_capTimer <= 0f && _caps.Count > 0)
            {
                _capTimer = Wrath ? CapLife * 0.8f : CapLife;
                int old = _caps[0];
                _caps.RemoveAt(0);
                int i = Enemies.IndexOf(old);
                if (i >= 0) TrialEvent(TrialMoment.CapWilt, Enemies[i].Position);
                Wither(old);
                SproutCap(OpenSpot(LivingPropSpots(EnemyType.Glowcap), 5f));
            }
            _fogTick -= dt;
            if (_fogTick <= 0f)
            {
                _fogTick = FogTick;
                foreach (var p in Players)
                    if (p.Alive && p.RoomId == Room.Id && !InCleanAir(p.Position)) HurtPlayer(p, p.MaxHp * FogHurt + 1f);
            }
            if (TrialTimer <= 0f) BreakTrial(b, TrialMoment.FogClears); // outlasted: the fog settles and she is spent
        }

        // ------------------------------------------------------------------ Rot Giant: the parasite

        private void BeginParasite(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Parasite;
            _hosts.Clear();
            _cure.Clear();
            var candidates = new List<SimPlayer>();
            foreach (var p in Players) if (p.Alive && p.RoomId == Room.Id) candidates.Add(p);
            int n = Mathf.Max(1, (candidates.Count + 1) / 2);
            for (int k = 0; k < n && candidates.Count > 0; k++)
            {
                var p = candidates[_aiRng.Range(0, candidates.Count)];
                candidates.Remove(p);
                Infect(p);
            }
            TrialTotal = _hosts.Count;
            TrialDone = 0;
            TrialTimer = ParasiteTime - (Wrath ? 3f : 0f);
            _jumpTimer = ParasiteJumpEvery;
            _parasiteTick = 1f;
            _lilyRespawn = 0f;
            foreach (var spot in TrialSpots(2, 9.5f, 4.2f, _aiRng.Range(0f, 180f))) SpawnLily(spot);
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.SporeCloud, Pos = boss.Position, Value = 3f, Size = new Vector2(1.5f, 0f) });
        }

        private void Infect(SimPlayer p)
        {
            _hosts.Add(p.ClientId);
            _cure[p.ClientId] = 0f;
            TrialEvent(TrialMoment.ParasiteMark, p.Position, 0f, p.ClientId);
        }

        private void SpawnLily(Vector2 at)
        {
            int id = SpawnTrialProp(EnemyType.Lily, at, 1f);
            Enemies[Enemies.IndexOf(id)].Moving = true; // in bloom
            Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = at, Int = (int)EnemyType.Lily, Value = 0.3f });
        }

        private void StepParasite(int b, float dt)
        {
            TrialTimer -= dt;
            // The fallen take it with them.
            foreach (ulong c in new List<ulong>(_hosts))
            {
                var p = GetPlayer(c);
                if (p != null && p.Alive) continue;
                _hosts.Remove(c);
                TrialDone++;
            }
            _parasiteTick -= dt;
            if (_parasiteTick <= 0f)
            {
                _parasiteTick = 1f;
                foreach (ulong c in _hosts)
                {
                    var p = GetPlayer(c);
                    if (p != null) HurtPlayer(p, p.MaxHp * ParasiteHurt + 1f);
                }
            }
            // Standing by a lily in bloom cures it (and the lily wilts; another opens a little later).
            foreach (ulong c in new List<ulong>(_hosts))
            {
                var p = GetPlayer(c);
                int lily = -1;
                foreach (int id in _trialProps)
                {
                    int i = Enemies.IndexOf(id);
                    if (i < 0 || Enemies[i].Dead || Enemies[i].Type != EnemyType.Lily) continue;
                    if ((Enemies[i].Position - p.Position).sqrMagnitude <= CureRadius * CureRadius) lily = id;
                }
                _cure[c] = lily >= 0 ? _cure[c] + dt : Mathf.Max(0f, _cure[c] - dt);
                if (_cure[c] < CureTime) continue;
                _hosts.Remove(c);
                TrialDone++;
                TrialEvent(TrialMoment.ParasiteCured, p.Position, 0f, c);
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.HolyLight, Pos = p.Position, Value = 1.6f });
                Wither(lily);
                _lilyRespawn = 3f;
            }
            if (_lilyRespawn > 0f && (_lilyRespawn -= dt) <= 0f) SpawnLily(OpenSpot(LivingPropSpots(EnemyType.Lily), 7f));
            if (_hosts.Count == 0)
            {
                BreakTrial(b, TrialMoment.ParasiteGone);
                return;
            }
            // It jumps to whoever stands too close to a host: keep apart.
            _jumpTimer -= dt;
            if (_jumpTimer <= 0f)
            {
                _jumpTimer = Wrath ? ParasiteJumpEvery * 0.8f : ParasiteJumpEvery;
                foreach (ulong c in new List<ulong>(_hosts))
                {
                    var host = GetPlayer(c);
                    SimPlayer next = null;
                    float best = ParasiteReach * ParasiteReach;
                    foreach (var p in Players)
                    {
                        if (!p.Alive || p.RoomId != Room.Id || _hosts.Contains(p.ClientId)) continue;
                        float d = (p.Position - host.Position).sqrMagnitude;
                        if (d <= best) { best = d; next = p; }
                    }
                    if (next == null) continue;
                    Infect(next);
                    TrialTotal++;
                    Events.Add(new SimEvent(SimEventType.TrialEvent) { Int = (int)TrialMoment.ParasiteJump, Pos = host.Position, ClientId = next.ClientId, Vec = next.Position });
                }
            }
            if (TrialTimer > 0f) return;
            // Too slow: the parasites burst out of their hosts.
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.None;
            RemoveTrialProps();
            foreach (ulong c in _hosts)
            {
                var p = GetPlayer(c);
                if (p == null || !p.Alive) continue;
                p.InvulnerableTimer = 0f;
                HurtPlayer(p, p.MaxHp * 0.25f);
                for (int k = 0; k < 2; k++) QueueSpawn(EnemyType.Sporeling, false, ClampToRoom(p.Position + Rotate(Vector2.right, k * 180f) * 1.5f, 0.5f));
            }
            _hosts.Clear();
            TrialEvent(TrialMoment.ParasiteBurst, boss.Position);
        }

        // ------------------------------------------------------------------ Archivist: the forbidden tomes

        private void BeginSequence(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Sequence;
            CancelAttack(ref boss);
            boss.Position = Arena.Resolve(Layout, new Vector2(PlayRect.center.x, PlayRect.yMax - 1.6f), boss.Radius);
            int count = Wrath ? 5 : 4;
            var ids = new List<int>();
            foreach (var spot in TrialSpots(count, 8.5f, 4.2f, _aiRng.Range(0f, 90f)))
            {
                ids.Add(SpawnTrialProp(EnemyType.ForbiddenTome, spot, 1f));
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)EnemyType.ForbiddenTome, Value = 0.3f });
            }
            _tomeOrder.Clear();
            while (ids.Count > 0)
            {
                int k = _aiRng.Range(0, ids.Count);
                _tomeOrder.Add(ids[k]);
                ids.RemoveAt(k);
            }
            TrialTotal = count;
            TrialTimer = SequenceTime - (Wrath ? 4f : 0f);
            StartShow(1.4f);
            for (int k = 0; k < PlayersIn(Room); k++) QueueSpawn(EnemyType.InkWraith, false);
        }

        private void StartShow(float delay)
        {
            _showing = true;
            _showIndex = 0;
            _showTimer = delay;
            _tomeStep = 0;
            TrialDone = 0;
            foreach (int id in _tomeOrder)
            {
                int i = Enemies.IndexOf(id);
                if (i >= 0) Enemies[i].Moving = false;
            }
        }

        private void StepSequence(int b, float dt)
        {
            TrialTimer -= dt;
            _tomeCool -= dt;
            if (_showing)
            {
                _showTimer -= dt;
                if (_showTimer <= 0f)
                {
                    if (_showIndex < _tomeOrder.Count)
                    {
                        int i = Enemies.IndexOf(_tomeOrder[_showIndex]);
                        if (i >= 0) TrialEvent(TrialMoment.TomeShine, Enemies[i].Position, TomeShowGap * 0.8f, 0, _showIndex + 1);
                        _showIndex++;
                        _showTimer = TomeShowGap;
                    }
                    else _showing = false; // now strike them back in that order
                }
            }
            if (TrialTimer > 0f) return;
            // Too slow: the ink rises under everyone.
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.None;
            RemoveTrialProps();
            foreach (var p in Players)
                if (p.Alive && p.RoomId == Room.Id) QueueBurst(p.Position, 2.4f, 0.9f, 28f * EnemyDamageScale, AreaKind.InkBurst);
            TrialEvent(TrialMoment.TomesFail, boss.Position);
        }

        /// <summary>A forbidden tome struck: the next in the order lights; a wrong one bursts and the order shines again.</summary>
        private void TomeStruck(int index)
        {
            if (ActiveTrial != BossTrial.Sequence || _showing || _tomeCool > 0f || _tomeStep >= _tomeOrder.Count) return;
            ref var tome = ref Enemies[index];
            _tomeCool = 0.35f;
            if (tome.Id == _tomeOrder[_tomeStep])
            {
                tome.Moving = true; // lit
                _tomeStep++;
                TrialDone = _tomeStep;
                TrialEvent(TrialMoment.TomeRight, tome.Position, 0f, 0, _tomeStep);
                if (_tomeStep < _tomeOrder.Count) return;
                int b = BossIndex();
                if (b >= 0) BreakTrial(b, TrialMoment.TomesRead);
                return;
            }
            if (tome.Moving) return; // already read
            Room = RoomStates[tome.RoomId];
            TrialEvent(TrialMoment.TomeWrong, tome.Position);
            QueueBurst(tome.Position, 3f, 0.6f, 20f * EnemyDamageScale, AreaKind.InkBurst);
            StartShow(1.6f);
        }

        // ------------------------------------------------------------------ Ink Beast: the dive

        private void BeginDive(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Dive;
            CancelAttack(ref boss);
            TrialTotal = 3;
            TrialDone = 0;
            TrialTimer = DiveTime - (Wrath ? 3f : 0f);
            _eruptTimer = 1.4f;
            _candleHold.Clear();
            foreach (var spot in TrialSpots(3, 9.5f, 4.4f, _aiRng.Range(0f, 120f)))
            {
                int id = SpawnTrialProp(EnemyType.Candelabra, spot, 1f);
                _candleHold[id] = 0f;
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)EnemyType.Candelabra, Value = 0.3f });
            }
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.InkBurst, Pos = boss.Position, Value = 2.6f });
            TrialEvent(TrialMoment.InkDive, boss.Position, TrialTimer);
        }

        private void StepDive(int b, float dt)
        {
            TrialTimer -= dt;
            foreach (int id in new List<int>(_candleHold.Keys))
            {
                int i = Enemies.IndexOf(id);
                if (i < 0 || Enemies[i].Dead || Enemies[i].Moving) continue;
                bool someone = false;
                foreach (var p in Players)
                    if (p.Alive && p.RoomId == Room.Id && (p.Position - Enemies[i].Position).sqrMagnitude <= CandleRadius * CandleRadius) someone = true;
                float hold = someone ? _candleHold[id] + dt : Mathf.Max(0f, _candleHold[id] - dt);
                _candleHold[id] = hold;
                if (hold < CandleTime) continue;
                Enemies[i].Moving = true; // lit
                TrialDone++;
                TrialEvent(TrialMoment.CandleLit, Enemies[i].Position);
            }
            ref var boss = ref Enemies[b];
            if (TrialDone >= TrialTotal)
            {
                // Every candle burns: the ink shrinks from the light and throws the beast out.
                boss.Position = HallCentre;
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.InkBurst, Pos = boss.Position, Value = 3f });
                BreakTrial(b, TrialMoment.InkDriven);
                return;
            }
            _eruptTimer -= dt;
            if (_eruptTimer <= 0f)
            {
                _eruptTimer = Wrath ? EruptEvery * 0.75f : EruptEvery;
                var target = RandomLivingPlayer();
                if (target != null && target.RoomId == Room.Id)
                {
                    QueueBurst(target.Position, 1.7f, 0.9f, 24f * EnemyDamageScale, AreaKind.InkBurst);
                    boss.Position = target.Position; // it swims there under the floor
                }
            }
            if (TrialTimer > 0f) return;
            ActiveTrial = BossTrial.None;
            RemoveTrialProps();
            boss.Position = HallCentre;
            foreach (var p in Players)
                if (p.Alive && p.RoomId == Room.Id) QueueBurst(p.Position, 2.6f, 0.8f, 30f * EnemyDamageScale, AreaKind.InkBurst);
            TrialEvent(TrialMoment.InkFlood, boss.Position);
        }

        // ------------------------------------------------------------------ Pages on the floor

        /// <summary>A written page: someone steps on it, it glows, and a moment later it bursts (monsters too).</summary>
        private void StepGlyph(Hazard h, float dt)
        {
            if (h.Phase == 1)
            {
                h.Hold -= dt;
                if (h.Hold > 0f) return;
                h.Phase = 0;
                h.Clock = -Arena.GlyphRearm;
                h.PlayerMask = 0;
                h.HitEnemies.Clear();
                Events.Add(new SimEvent(SimEventType.HazardFire) { Id = h.Id, Int = (int)GimmickKind.Glyph, Pos = h.Def.Position, Size = new Vector2(Arena.GlyphRadius, Arena.GlyphRadius), Value = 0.3f });
                SpikeHits(h, Arena.GlyphDamage);
                return;
            }
            if (h.Clock < 0f)
            {
                h.Clock += dt;
                return;
            }
            foreach (var p in Players)
            {
                if (!p.Alive || p.RoomId != h.Room || (p.Position - h.Def.Position).sqrMagnitude > Arena.GlyphTrigger * Arena.GlyphTrigger) continue;
                h.Phase = 1;
                h.Hold = Arena.GlyphWarn;
                Events.Add(new SimEvent(SimEventType.HazardWarn)
                {
                    Id = h.Id, Int = (int)GimmickKind.Glyph, Pos = h.Def.Position, Size = new Vector2(Arena.GlyphRadius, Arena.GlyphRadius), Value = Arena.GlyphWarn,
                });
                return;
            }
        }

        // ------------------------------------------------------------------ Champions

        /// <summary>The great cap puffs spores around itself when struck (not more often than every 1.4 s).</summary>
        private void CapPuff(int index, HitFlags flags)
        {
            ref var e = ref Enemies[index];
            if ((flags & HitFlags.DamageOverTime) != 0) return;
            if (_capPuff.TryGetValue(e.Id, out float next) && Elapsed < next) return;
            _capPuff[e.Id] = Elapsed + 1.4f;
            QueueBurst(e.Position, 2.3f, 0.45f, 14f * EnemyDamageScale, AreaKind.SporeCloud);
        }

        /// <summary>The bookworm falls apart into two halves that go on fighting.</summary>
        private void SplitWorm(in Enemy e)
        {
            float health = EnemyHealthScale * 0.4f * (e.Champion ? ChampionHealth : 1f);
            for (int k = 0; k < 2; k++)
            {
                Vector2 at = ClampToRoom(e.Position + Rotate(Vector2.right, k * 180f + 30f) * 0.9f, 0.5f);
                int id = Enemies.Spawn(EnemyType.Wormling, at, health, EnemyDamageScale, e.Elite);
                Enemies[Enemies.IndexOf(id)].RoomId = e.RoomId;
            }
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.InkBurst, Pos = e.Position, Value = 1.4f });
        }
    }
}
