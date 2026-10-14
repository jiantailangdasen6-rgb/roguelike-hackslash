using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Each land's second lord (Biome.AltLord) and their trials, each asking something else of the team:
    /// the Grave Reaper hides in one of four coffins (find the one that rattles; empty ones let ghouls out), the Choir
    /// Mistress sweeps the hall with beams of holy light (outlast them), the Void Eye stares at one player after
    /// another (stand behind a pillar), the Great Crab shuts its shell (only a powder keg bursting by it cracks it), the
    /// Great Wolf marks a player and charges (lead it into a pillar), and the Executioner drops rows of blades across
    /// the hall while three chain posts hold him (break them).
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float BeamLength = 14f, BeamWidth = 1.3f, BeamTurn = 38f, BeamTime = 10f;
        public const float GazeAim = 1.4f;
        public const float ShellArmor = 0.1f;

        private int _trueCoffin;
        private float _rattle;
        private float _beamAngle, _beamTick;
        private int _gazesLeft;
        private float _gazeTimer;
        private bool _gazeAiming;
        private ulong _gazeTarget;
        private bool _shellCracked;
        private int _chargesLeft;
        private float _huntTimer;
        private bool _huntCharging;
        private ulong _hunted;
        private bool _marked;
        private float _rowTimer;
        private int _rowIndex;

        private void ResetLords2()
        {
            _trueCoffin = 0;
            _shellCracked = false;
            _gazeAiming = _huntCharging = false;
            _marked = false;
        }

        // ------------------------------------------------------------------ Choices and thresholds

        /// <summary>The second lords' four moves: 0 close, 1 and 2 at range, 3 a summons.</summary>
        private static int SecondLordAttack(EnemyDef def, float distance, int n, bool roomForMinions)
        {
            switch (n % 5)
            {
                case 0: return distance < def.Attacks[0].Range + 0.5f ? 0 : 1;
                case 1: return 2;
                case 2: return distance < def.Attacks[0].Range + 0.5f ? 0 : 1;
                case 3: return roomForMinions ? 3 : 2;
                default: return 1;
            }
        }

        private static bool IsSecondLord(EnemyType t) =>
            t is EnemyType.GraveReaper or EnemyType.ChoirMistress or EnemyType.VoidEye or EnemyType.GreatCrab or EnemyType.GreatWolf or EnemyType.Executioner;

        private void SecondLordTrials(int b, EnemyType type, float hp)
        {
            switch (type)
            {
                case EnemyType.GraveReaper:
                    if (Threshold(0, hp, 0.6f) || Threshold(1, hp, 0.3f)) BeginCoffins(b);
                    break;
                case EnemyType.ChoirMistress:
                    if (Threshold(0, hp, 0.66f) || Threshold(1, hp, 0.33f)) BeginBeams(b);
                    break;
                case EnemyType.VoidEye:
                    if (Threshold(0, hp, 0.7f) || Threshold(1, hp, 0.4f)) BeginGaze(b);
                    break;
                case EnemyType.GreatCrab:
                    if (Threshold(0, hp, 0.6f) || Threshold(1, hp, 0.3f)) BeginShell(b);
                    break;
                case EnemyType.GreatWolf:
                    if (Threshold(0, hp, 0.65f) || Threshold(1, hp, 0.3f)) BeginHunt(b);
                    break;
                case EnemyType.Executioner:
                    if (Threshold(0, hp, Wrath ? 0.6f : 0.5f)) BeginRows(b);
                    break;
            }
        }

        /// <summary>One tick of a second lord's trial (false when the trial is not one of theirs).</summary>
        private bool StepSecondLordTrial(int b, float dt)
        {
            switch (ActiveTrial)
            {
                case BossTrial.Coffins: StepCoffins(b, dt); return true;
                case BossTrial.Beams: StepBeams(b, dt); return true;
                case BossTrial.Gaze: StepGaze(b, dt); return true;
                case BossTrial.Shell: StepShell(b, dt); return true;
                case BossTrial.Hunt: StepHunt(b, dt); return true;
                case BossTrial.Rows: StepRows(b, dt); return true;
                default: return false;
            }
        }

        private Vector2 HallCentre => Arena.Resolve(Layout, PlayRect.center + new Vector2(0f, 1f), 1f);

        // ------------------------------------------------------------------ Grave Reaper: the coffins

        private void BeginCoffins(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Coffins;
            CancelAttack(ref boss);
            TrialTotal = 1;
            TrialDone = 0;
            var spots = TrialSpots(4, 9f, 4.8f, _aiRng.Range(0f, 90f));
            int real = _aiRng.Range(0, spots.Count);
            Vector2 from = Enemies[b].Position;
            for (int k = 0; k < spots.Count; k++)
            {
                int id = SpawnTrialProp(EnemyType.Coffin, spots[k], EnemyHealthScale);
                if (k == real) _trueCoffin = id;
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spots[k], Int = (int)EnemyType.Coffin, Value = 0.3f });
            }
            Enemies[b].Position = spots[real];
            TrialTimer = (Players.Count == 1 ? 20f : 17f) - (Wrath ? 3f : 0f);
            _rattle = 1.4f;
            Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.SoulBurst, Pos = from, Value = 2.6f });
            TrialEvent(TrialMoment.CoffinsRise, from, TrialTimer);
        }

        private void StepCoffins(int b, float dt)
        {
            TrialTimer -= dt;
            int i = Enemies.IndexOf(_trueCoffin);
            if (i < 0 || Enemies[i].Dead)
            {
                BreakTrial(b, TrialMoment.ReaperFound);
                return;
            }
            Enemies[b].Position = Enemies[i].Position; // he waits inside
            _rattle -= dt;
            if (_rattle <= 0f)
            {
                _rattle = Wrath ? 3f : 2.2f;
                TrialEvent(TrialMoment.CoffinRattle, Enemies[i].Position);
            }
            if (TrialTimer > 0f) return;
            // Too slow: he bursts out of his coffin in a ring of souls.
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.None;
            RemoveTrialProps();
            boss.Health = Mathf.Min(boss.MaxHealth, boss.Health + boss.MaxHealth * 0.05f);
            QueueBurst(boss.Position, 4f, 0.5f, 30f * EnemyDamageScale, AreaKind.SoulBurst);
            TrialEvent(TrialMoment.ReaperBursts, boss.Position);
        }

        /// <summary>An empty coffin broken while he hides: the dead inside climb out.</summary>
        private void CoffinBroken(in Enemy e)
        {
            if (ActiveTrial != BossTrial.Coffins || e.Id == _trueCoffin) return;
            Room = RoomStates[e.RoomId];
            for (int k = 0; k < 2; k++) QueueSpawn(EnemyType.Ghoul, false, ClampToRoom(e.Position + Rotate(Vector2.right, k * 180f) * 0.8f, 0.5f));
            TrialEvent(TrialMoment.CoffinWrong, e.Position);
        }

        // ------------------------------------------------------------------ Choir Mistress: the beams

        private void BeginBeams(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Beams;
            CancelAttack(ref boss);
            boss.Position = HallCentre;
            TrialTimer = BeamTime + (Wrath ? 2f : 0f);
            _beamAngle = _aiRng.Range(0f, 120f);
            _beamTick = 1f; // a breath before the light starts to burn
            float turn = Wrath ? BeamTurn * 1.25f : BeamTurn;
            Events.Add(new SimEvent(SimEventType.TrialEvent)
            {
                Int = (int)TrialMoment.BeamsStart, Pos = boss.Position, Value = TrialTimer, Id = Mathf.RoundToInt(turn),
                Vec = Rotate(Vector2.right, _beamAngle),
            });
        }

        private void StepBeams(int b, float dt)
        {
            TrialTimer -= dt;
            float turn = Wrath ? BeamTurn * 1.25f : BeamTurn;
            _beamAngle += turn * dt;
            Vector2 centre = Enemies[b].Position;
            _beamTick -= dt;
            if (_beamTick <= 0f)
            {
                _beamTick = 0.3f;
                for (int k = 0; k < 3; k++)
                {
                    Vector2 dir = Rotate(Vector2.right, _beamAngle + k * 120f);
                    foreach (var p in Players)
                        if (p.Alive && p.RoomId == Room.Id && InBeam(centre, dir, p.Position, SimPlayer.BodyRadius)) HurtPlayer(p, 12f * EnemyDamageScale);
                    for (int i = 0; i < Enemies.Count; i++)
                    {
                        ref var e = ref Enemies[i];
                        if (e.Dead || e.Def.Boss || e.Def.Prop || e.RoomId != Room.Id || !InBeam(centre, dir, e.Position, e.Radius)) continue;
                        DamageEnemy(i, e.MaxHealth * 0.08f + 1f, null, HitFlags.None, Vector2.zero, 0f);
                    }
                }
            }
            if (TrialTimer <= 0f) BreakTrial(b, TrialMoment.BeamsEnd); // outlasted: the hymn breaks and she reels
        }

        private static bool InBeam(Vector2 centre, Vector2 dir, Vector2 p, float margin)
        {
            Vector2 to = p - centre;
            float along = Vector2.Dot(to, dir);
            float across = Mathf.Abs(dir.x * to.y - dir.y * to.x);
            return along >= 0.6f && along <= BeamLength && across <= BeamWidth * 0.5f + margin;
        }

        // ------------------------------------------------------------------ Void Eye: the gaze

        private void BeginGaze(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Gaze;
            CancelAttack(ref boss);
            boss.Position = HallCentre;
            _gazesLeft = Wrath ? 5 : 4;
            TrialTotal = _gazesLeft;
            TrialDone = 0;
            _gazeTimer = 1f;
            _gazeAiming = false;
        }

        private void StepGaze(int b, float dt)
        {
            _gazeTimer -= dt;
            if (_gazeTimer > 0f) return;
            Vector2 eye = Enemies[b].Position;
            if (!_gazeAiming)
            {
                var target = RandomLivingPlayer();
                if (target == null || target.RoomId != Room.Id)
                {
                    _gazeTimer = 0.5f;
                    return;
                }
                _gazeTarget = target.ClientId;
                _gazeAiming = true;
                _gazeTimer = GazeAim;
                TrialEvent(TrialMoment.GazeAim, eye, GazeAim, target.ClientId);
                return;
            }
            // The gaze lands: a pillar between the eye and its target takes it.
            _gazeAiming = false;
            _gazesLeft--;
            TrialDone++;
            _gazeTimer = 1.1f;
            var victim = GetPlayer(_gazeTarget);
            if (victim != null && victim.Alive)
            {
                bool blocked = false;
                Vector2 to = victim.Position - eye;
                int steps = Mathf.CeilToInt(to.magnitude / 0.3f);
                for (int s = 1; s < steps && !blocked; s++) blocked = Arena.BlocksShot(Layout, eye + to * (s / (float)steps));
                if (!blocked)
                {
                    victim.InvulnerableTimer = 0f;
                    HurtPlayer(victim, 32f * EnemyDamageScale);
                }
                Events.Add(new SimEvent(SimEventType.TrialEvent) { Int = (int)TrialMoment.GazeFire, Pos = victim.Position, Id = blocked ? 1 : 0, ClientId = victim.ClientId });
            }
            if (_gazesLeft <= 0) BreakTrial(b, TrialMoment.GazeEnd);
        }

        // ------------------------------------------------------------------ Great Crab: the shell

        private void BeginShell(int b)
        {
            ActiveTrial = BossTrial.Shell;
            TrialTotal = 1;
            TrialDone = 0;
            _shellCracked = false;
            TrialTimer = 20f - (Wrath ? 4f : 0f);
            // Powder kegs roll in (they stay in the hall): burst one beside it.
            foreach (var spot in TrialSpots(3, 8.5f, 4.6f, _aiRng.Range(0f, 120f)))
            {
                int id = Enemies.Spawn(EnemyType.PowderKeg, spot, 1f, 1f, false);
                Enemies[Enemies.IndexOf(id)].RoomId = Room.Id;
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)EnemyType.PowderKeg, Value = 0.3f });
            }
            TrialEvent(TrialMoment.ShellUp, Enemies[b].Position, TrialTimer);
        }

        private void StepShell(int b, float dt)
        {
            TrialTimer -= dt;
            if (_shellCracked)
            {
                TrialDone = 1;
                BreakTrial(b, TrialMoment.ShellCracked);
                return;
            }
            if (TrialTimer > 0f) return;
            ActiveTrial = BossTrial.None;
            TrialEvent(TrialMoment.ShellOpen, Enemies[b].Position);
        }

        /// <summary>A keg burst: if the crab is shut in its shell close by, the shell cracks.</summary>
        private void KegBurstNearCrab(Vector2 at)
        {
            if (ActiveTrial != BossTrial.Shell) return;
            int b = BossIndex();
            if (b < 0) return;
            float r = Arena.KegRadius + Enemies[b].Radius;
            if ((Enemies[b].Position - at).sqrMagnitude <= r * r) _shellCracked = true;
        }

        // ------------------------------------------------------------------ Great Wolf: the hunt

        private void BeginHunt(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Hunt;
            CancelAttack(ref boss);
            _chargesLeft = Wrath ? 5 : 4;
            TrialTotal = _chargesLeft;
            TrialDone = 0;
            _huntTimer = 1f;
            _huntCharging = false;
            for (int k = 0; k < PlayersIn(Room); k++) QueueSpawn(EnemyType.FrostWolf, false);
        }

        private void StepHunt(int b, float dt)
        {
            ref var boss = ref Enemies[b];
            boss.Cooldown = Mathf.Max(boss.Cooldown, 1f); // no other moves while it hunts
            if (_huntCharging)
            {
                // The charge ends: into a pillar it reels (StepLunge stuns a lord that hits one).
                if (boss.State != EnemyState.Recover && boss.State != EnemyState.Chase) return;
                _huntCharging = false;
                if (boss.Stunned > 0f)
                {
                    BreakTrial(b, TrialMoment.HuntCrash);
                    return;
                }
                TrialDone++;
                if (--_chargesLeft <= 0)
                {
                    ActiveTrial = BossTrial.None;
                    TrialEvent(TrialMoment.HuntOver, boss.Position);
                    return;
                }
                _huntTimer = 0.8f;
                return;
            }
            _huntTimer -= dt;
            if (!_marked)
            {
                if (_huntTimer > 0f) return;
                var prey = RandomLivingPlayer();
                if (prey == null || prey.RoomId != Room.Id) return;
                _hunted = prey.ClientId;
                _marked = true;
                _huntTimer = 1.3f;
                TrialEvent(TrialMoment.HuntMark, prey.Position, _huntTimer, prey.ClientId);
                return;
            }
            if (_huntTimer > 0f || boss.State != EnemyState.Chase) return;
            var marked = GetPlayer(_hunted);
            _marked = false;
            if (marked == null || !marked.Alive) return;
            BeginAttack(ref boss, boss.Def, 1, marked); // the long charge
            _huntCharging = true;
        }

        // ------------------------------------------------------------------ Executioner: the rows

        private void BeginRows(int b)
        {
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.Rows;
            CancelAttack(ref boss);
            boss.Position = Arena.Resolve(Layout, new Vector2(PlayRect.center.x, PlayRect.yMax - 1.5f), boss.Radius);
            TrialTotal = 3;
            TrialDone = 0;
            foreach (var spot in TrialSpots(3, 10.5f, 5.2f, 210f))
            {
                SpawnTrialProp(EnemyType.ChainPost, spot, EnemyHealthScale);
                Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)EnemyType.ChainPost, Value = 0.3f });
            }
            TrialTimer = 22f - (Wrath ? 4f : 0f);
            _rowTimer = 1.2f;
            _rowIndex = _aiRng.Range(0, 5);
            TrialEvent(TrialMoment.RowsStart, Enemies[b].Position, TrialTimer);
        }

        private void StepRows(int b, float dt)
        {
            TrialTimer -= dt;
            TrialDone = TrialTotal - LivingTrialProps();
            if (TrialDone >= TrialTotal)
            {
                BreakTrial(b, TrialMoment.RowsBroken);
                return;
            }
            _rowTimer -= dt;
            if (_rowTimer <= 0f)
            {
                // A row of blades across the hall with a gap somewhere along it.
                _rowTimer = Wrath ? 1.1f : 1.45f;
                var area = PlayRect;
                _rowIndex = (_rowIndex + 2 + _aiRng.Range(0, 2)) % 5;
                float y = Mathf.Lerp(area.yMin + 1.2f, area.yMax - 2.2f, _rowIndex / 4f);
                int count = Mathf.FloorToInt((area.width - 2f) / 2.1f);
                int gap = _aiRng.Range(1, count - 2);
                for (int k = 0; k < count; k++)
                {
                    if (k == gap || k == gap + 1) continue;
                    QueueBurst(new Vector2(area.xMin + 1.5f + k * 2.1f, y), 1.15f, 1.05f, 22f * EnemyDamageScale, AreaKind.Slam);
                }
            }
            if (TrialTimer > 0f) return;
            // Too slow: the great axe falls on the whole hall.
            ref var boss = ref Enemies[b];
            ActiveTrial = BossTrial.None;
            RemoveTrialProps();
            QueueBurst(HallCentre, 9f, 1.3f, 30f * EnemyDamageScale, AreaKind.Explosion);
            TrialEvent(TrialMoment.RowsSlam, boss.Position);
        }
    }
}
