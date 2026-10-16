using System.Collections.Generic;
using Game.Core;
using Unity.Netcode.Components;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Owner-side control (enabled only on the owning client): movement, dodge and dash-slash travel, aiming,
    /// and sending input to the host at the network tick rate. The NetworkTransform replicates the position.
    /// With -botmove an AI plays instead (automated tests).
    /// </summary>
    public sealed class PlayerMover : MonoBehaviour
    {
        private const float SendInterval = 1f / 30f;

        [SerializeField] private NetPlayer player;
        [SerializeField] private PlayerInputReader input;

        private Camera _camera;
        private Vector2 _lastMove = Vector2.right;
        private Vector2 _aim = Vector2.right;
        private Vector2 _dashDirection;
        private float _dashSpeed;
        private float _dashUntil;
        private byte _skill1;
        private byte _skill2;
        private byte _dodge;
        private byte _ult;
        private float _sendTimer;
        private bool _attackHeld;
        private bool _bot;
        private Bot _botBrain;

        public Vector2 Aim => _aim;

        private void Awake()
        {
            _bot = CommandLine.Has("-botmove");
            if (_bot) _botBrain = new Bot(player);
        }

        private void OnEnable()
        {
            input.Skill1Pressed += Skill1;
            input.Skill2Pressed += Skill2;
            input.DodgePressed += Dodge;
            input.UltimatePressed += Ultimate;
        }

        private void OnDisable()
        {
            input.Skill1Pressed -= Skill1;
            input.Skill2Pressed -= Skill2;
            input.DodgePressed -= Dodge;
            input.UltimatePressed -= Ultimate;
        }

        /// <summary>The host moved this player (a new area, the gathering bell): a jump everyone sees as one.</summary>
        public void Teleport(Vector2 position) => Teleport(position, network: true);

        private static RoomLayout CurrentLayout => PlayArea.Layout;

        private bool CanAct
        {
            get
            {
                var director = RunDirector.Instance;
                return director != null && director.IsRunning && !director.Halted && !player.Downed.Value && !PauseMenuView.IsOpen && !PerkView.IsShowing && !BagView.IsOpen && !CodexView.IsOpen && !SettingsView.IsOpen;
            }
        }

        /// <summary>Downed: drifting on as a ghost (in the room where the body lies; no doors, through obstacles).</summary>
        private bool CanHaunt
        {
            get
            {
                var director = RunDirector.Instance;
                return director != null && director.IsRunning && !director.Halted && player.Downed.Value && !PauseMenuView.IsOpen && !PerkView.IsShowing && !BagView.IsOpen && !CodexView.IsOpen && !SettingsView.IsOpen;
            }
        }

        /// <summary>A ghost drifts a little faster than it walked.</summary>
        public const float GhostSpeed = 1.15f;

        /// <summary>In the camp nobody fights: walking around is all, while no preparation panel is open.</summary>
        private static bool CanWalkInCamp => PlayArea.InCamp && !PauseMenuView.IsOpen && !CampView.PanelOpen && !CodexView.IsOpen && !SettingsView.IsOpen;

        // ------------------------------------------------------------------ Actions (also used by the bot)

        // Dodges spent here that the host has not counted yet (its charge count arrives a round trip later).
        private int _unconfirmedDodges;
        private float _lastDodgeAt = -10f;
        private int _seenCharges = ClassCatalog.DodgeCharges;

        public void Dodge()
        {
            if (player.DodgeCharges.Value != _seenCharges || Time.time - _lastDodgeAt > 0.8f)
            {
                _seenCharges = player.DodgeCharges.Value;
                _unconfirmedDodges = 0;
            }
            if (!CanAct || player.Caged.Value || Time.time < _dashUntil || Time.time - _lastDodgeAt < ClassCatalog.DodgeCooldown) return;
            if (player.DodgeCharges.Value - _unconfirmedDodges <= 0) return;
            _unconfirmedDodges++;
            _lastDodgeAt = Time.time;
            _dodge++;
            player.PredictAction(AttackAnim.Dodge);
            Vector2 move = CurrentMove();
            StartDash(move.sqrMagnitude > 0.01f ? move.normalized : _aim.normalized, ClassCatalog.DodgeSpeed, ClassCatalog.DodgeDuration);
            SendInput(); // send the press right away
        }

        /// <summary>The ultimate (RunSimulation.Ultimates): only with a full focus gauge.</summary>
        public void Ultimate()
        {
            if (!CanAct || player.Focus.Value < 100) return;
            _ult++;
            player.PredictAction(AttackAnim.Skill2);
            SendInput();
        }

        public void Skill1()
        {
            if (!CanAct || player.Skill1Cooldown.Value > 0.05f) return;
            // Caged: skills that travel can't get out of the bars.
            if (player.Caged.Value && player.Class.Value is ClassId.Warrior or ClassId.Guardian or ClassId.Assassin) return;
            _skill1++;
            player.PredictAction(AttackAnim.Skill1);
            if (player.Class.Value == ClassId.Warrior)
                StartDash(_aim.normalized, ClassCatalog.DashSlashDistance / ClassCatalog.DashSlashDuration, ClassCatalog.DashSlashDuration);
            else if (player.Class.Value == ClassId.Guardian)
                StartDash(_aim.normalized, ClassCatalog.ShieldBashDistance / ClassCatalog.ShieldBashDuration, ClassCatalog.ShieldBashDuration);
            else if (player.Class.Value == ClassId.Assassin)
                ShadowStep();
            SendInput();
        }

        public void Skill2()
        {
            if (!CanAct || player.Skill2Cooldown.Value > 0.05f) return;
            _skill2++;
            player.PredictAction(AttackAnim.Skill2);
            SendInput();
        }

        /// <summary>Assassin: vanish and reappear at the cursor (within range, outside walls and obstacles).</summary>
        private void ShadowStep()
        {
            Vector2 from = transform.position;
            Vector2 to = from + Vector2.ClampMagnitude(_aim, ClassCatalog.ShadowStepRange);
            float r = SimPlayer.BodyRadius;
            to = Arena.Resolve(CurrentLayout, to, r);
            var director = RunDirector.Instance;
            if (director != null && director.IsSpawned)
            {
                // Back along the line until there is floor to land on (no stepping through walls).
                for (int k = 0; k < 12 && !director.Walkable(to, r); k++) to = Vector2.Lerp(to, from, 0.25f);
                if (!director.Walkable(to, r)) to = from;
            }
            else
            {
                var bounds = PlayArea.Bounds;
                to.x = Mathf.Clamp(to.x, bounds.xMin + r, bounds.xMax - r);
                to.y = Mathf.Clamp(to.y, bounds.yMin + r, bounds.yMax - r);
            }
            _dashUntil = 0f;
            // Teleport (not a slide): the others see the jump instead of interpolating across the room.
            var networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null && player.IsSpawned) networkTransform.Teleport(to, transform.rotation, transform.localScale);
            else transform.position = to;
        }

        private void StartDash(Vector2 direction, float speed, float duration)
        {
            _dashDirection = direction.sqrMagnitude > 0.0001f ? direction : Vector2.right;
            _dashSpeed = speed;
            _dashUntil = Time.time + duration;
        }

        // ------------------------------------------------------------------ Frame

        /// <summary>How quickly the walk on ice catches up with the stick (lower = more slide).</summary>
        private const float IceGrip = 2.6f;
        private Vector2 _slide;

        /// <summary>Wading slows you: shallow water, and the Drowned Saint's flood in her hall.</summary>
        private float GroundSpeed(Vector2 at)
        {
            if (PlayArea.InCamp) return 1f;
            float k = Arena.InZone(CurrentLayout, GimmickKind.Water, at) ? Arena.WaterSlow : 1f;
            var director = RunDirector.Instance;
            if (director != null && director.IsSpawned && director.Trial.Value == BossTrial.Flood && director.LocalRoom.Kind == RoomKind.Boss)
                k *= RunSimulation.FloodSlow;
            return k;
        }

        private Vector2 CurrentMove() => _bot
            ? Arena.Steer(CurrentLayout, transform.position, _botBrain.Move, SimPlayer.BodyRadius, player.Slot.Value)
            : Vector2.ClampMagnitude(input.Move, 1f);

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            if (_bot) _botBrain.Think(this);

            UpdateAim();
            bool wasHeld = _attackHeld;
            _attackHeld = _bot ? _botBrain.Attack : input.AttackHeld;
            if (_attackHeld && !wasHeld && CanAct) player.PredictAction(AttackAnim.BasicA);

            if ((CanAct && !player.Caged.Value) || CanWalkInCamp)
            {
                Vector2 move = CurrentMove();
                if (move.sqrMagnitude > 0.01f) _lastMove = move.normalized;
                Vector2 here = transform.position;
                Vector2 walk = move * player.MoveSpeed.Value * (_attackHeld ? 0.8f : 1f) * GroundSpeed(here);
                // On ice the feet keep sliding: the pace eases toward where you want to go.
                if (!PlayArea.InCamp && Arena.InZone(CurrentLayout, GimmickKind.Ice, here))
                    walk = Vector2.Lerp(_slide, walk, 1f - Mathf.Exp(-IceGrip * Time.deltaTime));
                Vector2 velocity = Time.time < _dashUntil ? _dashDirection * _dashSpeed : walk;
                _slide = velocity;
                Vector2 pos = here + velocity * Time.deltaTime;

                float r = SimPlayer.BodyRadius;
                var director = RunDirector.Instance;
                if (!PlayArea.InCamp && director != null && director.IsSpawned)
                {
                    // The labyrinth in one piece: floors and open corridors, walked from room to room (phase 5).
                    pos = director.Walk(here, pos, r);
                    var resolved = Arena.Resolve(CurrentLayout, pos, r);
                    pos = director.Walkable(resolved, r) ? resolved : pos;
                }
                else
                {
                    var bounds = PlayArea.Bounds;
                    pos = Arena.Resolve(CurrentLayout, pos, r);
                    pos.x = Mathf.Clamp(pos.x, bounds.xMin + r, bounds.xMax - r);
                    pos.y = Mathf.Clamp(pos.y, bounds.yMin + r, bounds.yMax - r);
                }
                transform.position = pos;
            }
            else if (CanHaunt)
            {
                // A ghost: floats through obstacles but stays in the room where its body lies.
                Vector2 move = _bot ? _botBrain.Move : Vector2.ClampMagnitude(input.Move, 1f);
                Vector2 pos = (Vector2)transform.position + move * player.MoveSpeed.Value * GhostSpeed * Time.deltaTime;
                var bounds = PlayArea.Bounds;
                float r = SimPlayer.BodyRadius;
                pos.x = Mathf.Clamp(pos.x, bounds.xMin + r, bounds.xMax - r);
                pos.y = Mathf.Clamp(pos.y, bounds.yMin + r, bounds.yMax - r);
                transform.position = pos;
            }
            else
            {
                _attackHeld = false;
            }

            bool left = _aim.x < -0.05f || (Mathf.Abs(_aim.x) <= 0.05f && player.FacingLeft.Value);
            if (player.FacingLeft.Value != left) player.FacingLeft.Value = left;

            _sendTimer -= Time.deltaTime;
            if (_sendTimer <= 0f) SendInput();
        }

        /// <summary>Jumps the player (not a slide: the others see the jump instead of interpolating across the map).</summary>
        private void Teleport(Vector2 to, bool network)
        {
            _dashUntil = 0f;
            var networkTransform = GetComponent<NetworkTransform>();
            if (network && networkTransform != null && player.IsSpawned) networkTransform.Teleport(to, transform.rotation, transform.localScale);
            else transform.position = to;
        }

        private void UpdateAim()
        {
            if (_bot)
            {
                _aim = _botBrain.Aim;
                return;
            }
            if (TouchInput.Active)
            {
                // Touch: aim at the nearest monster in reach (a chest or keg close by otherwise), else where you walk.
                Vector2 me = transform.position;
                var view = EnemyView.Instance;
                if (view != null && (view.NearestWhere(me, 12f, d => !d.Prop && !d.Untargetable) ?? view.NearestWhere(me, 3f, d => d.Prop && !d.Untargetable)) is Vector2 target)
                {
                    _aim = target - me;
                    return;
                }
                if (input.Move.sqrMagnitude > 0.04f) _aim = input.Move.normalized * 5f;
                return;
            }
            if (input.UsingMouse && _camera != null)
            {
                Vector2 world = _camera.ScreenToWorldPoint(input.AimScreenPosition);
                Vector2 offset = world - (Vector2)transform.position;
                if (offset.sqrMagnitude > 0.01f) _aim = offset;
                return;
            }
            Vector2 stick = input.AimStick;
            if (stick.sqrMagnitude > 0.09f) _aim = stick.normalized * 5f;
            else if (input.Move.sqrMagnitude > 0.04f) _aim = input.Move.normalized * 5f;
        }

        private void SendInput()
        {
            _sendTimer = SendInterval;
            if (!player.IsSpawned) return;
            player.SendInputRpc(_aim, _attackHeld && (CanAct || CanHaunt), _skill1, _skill2, _dodge, _ult);
        }

        // ------------------------------------------------------------------ Test bot

        /// <summary>Simple AI used by automated tests (-botmove): fights the nearest enemy, sidesteps telegraphs, revives allies.</summary>
        private sealed class Bot
        {
            private readonly NetPlayer _player;
            private float _nextSkill1;
            private float _nextSkill2;
            private float _nextDodge;
            public Vector2 Move;
            public Vector2 Aim = Vector2.right;
            public bool Attack;

            public Bot(NetPlayer player) => _player = player;

            private int _campStop;
            private float _nextBell;
            private float _bellSince = -1f;
            private float _campStopUntil;

            /// <summary>Visits a few stations (for camp screenshots), then waits in the gate.</summary>
            private void WanderCamp(Vector2 pos)
            {
                var stops = new[] { CampStation.Mirror, CampStation.Master, CampStation.Charms, CampStation.Tablet };
                int offset = _player.Slot.Value;
                Vector2 target = _campStop < stops.Length
                    ? Camp.Get(stops[(_campStop + offset) % stops.Length]).Front
                    : Camp.Get(CampStation.Gate).Front + new Vector2((offset - 1) * 0.8f, 0f);
                Vector2 to = target - pos;
                if (to.sqrMagnitude > 0.25f)
                {
                    Move = to.normalized;
                    _campStopUntil = Time.time + 1.5f;
                }
                else if (Time.time > _campStopUntil && _campStop < stops.Length)
                {
                    _campStop++;
                }
            }

            /// <summary>This bot is the living teammate closest to the dark hall's lantern.</summary>
            private bool NearestToLantern(RunDirector director, Vector2 pos)
            {
                Vector2 lantern = director.LanternPos.Value;
                float mine = (lantern - pos).sqrMagnitude;
                foreach (var other in NetPlayer.All)
                    if (other != _player && !other.Downed.Value && ((Vector2)other.transform.position - lantern).sqrMagnitude < mine) return false;
                return true;
            }

            /// <summary>Where to walk while nothing is fighting: a pedestal to use, the way down, the next unvisited room, the boss door.</summary>
            private Vector2 Explore(RunDirector director, MapRoom room, Vector2 pos)
            {
                var map = director.Map;
                Vector2 Toward(Vector2 at) => (at - pos).sqrMagnitude > 0.2f ? (at - pos).normalized : Vector2.zero;

                // Past the third lord bots take the way home (automated runs end there) unless told to go deeper (-deep);
                // before it they all agree on a way from the map's seed (so different runs see different lands).
                if (room.Kind == RoomKind.Boss && director.ExitOpen.Value)
                {
                    // -heart: bots take the middle way down to the heart when it is open (screenshots, tests).
                    if (CommandLine.Has("-heart") && director.HeartOpen.Value && RunSimulation.OffersHome(director.Area.Value) && director.Biome != Biome.Heart)
                        return Toward(room.Origin + RunSimulation.HeartOffset);
                    bool left = RunSimulation.OffersHome(director.Area.Value) ? !CommandLine.Has("-deep") : (director.MapSeed.Value + (ulong)director.Area.Value) % 2 == 0;
                    return Toward(room.Origin + (left ? RunSimulation.HomeOffset : RunSimulation.DeepOffset));
                }
                if (room.Kind == RoomKind.Campfire && CommandLine.Has("-tobell")) return Toward(room.Bounds.center + RunSimulation.BellSpot); // screenshots
                bool used = (_player.UsedPedestals.Value & (1 << room.Id)) != 0;
                if (RoomCatalog.HasPedestal(room.Kind) && !used) return Toward(RunSimulation.PedestalPosition(room.Bounds));

                // Breadth-first over open doors to the nearest room nobody has visited yet (the boss waits until the end).
                var first = new int[map.Rooms.Count];
                var seen = new bool[map.Rooms.Count];
                var queue = new Queue<int>();
                seen[room.Id] = true;
                queue.Enqueue(room.Id);
                int goal = -1;
                while (queue.Count > 0 && goal < 0)
                {
                    var r = map[queue.Dequeue()];
                    for (int d = 0; d < 4; d++)
                    {
                        var n = map[r.Neighbor[d]];
                        bool withKey = n != null && n.Kind == RoomKind.Treasure && director.Keys.Value > 0; // the key opens it on arrival
                        if (n == null || seen[n.Id] || (!director.DoorOpen(r, n) && !withKey)) continue;
                        seen[n.Id] = true;
                        first[n.Id] = r == room ? d : first[r.Id];
                        if (!director.IsVisited(n.Id) && n.Kind != RoomKind.Boss) { goal = n.Id; break; }
                        queue.Enqueue(n.Id);
                    }
                }
                if (goal < 0)
                {
                    // Everything seen: to the campfire, ring the bell if a teammate is elsewhere, wait at the boss door with the team, then in.
                    if (room.Kind == RoomKind.Campfire && !director.BossDoorOpen.Value && _nextBell < Time.time)
                        foreach (var mate in NetPlayer.All)
                        {
                            if (mate == _player || mate.Downed.Value || map.RoomAt(mate.transform.position) == room) continue;
                            Vector2 bell = room.Bounds.center + RunSimulation.BellSpot;
                            if ((bell - pos).sqrMagnitude < 0.8f)
                            {
                                // Stand under it long enough to ring it, then leave it be for its cooldown.
                                if (_bellSince < 0f) _bellSince = Time.time;
                                if (Time.time - _bellSince > RunSimulation.BellHoldTime + 0.6f)
                                {
                                    _bellSince = -1f;
                                    _nextBell = Time.time + RunSimulation.BellCooldown;
                                }
                                return Vector2.zero;
                            }
                            return Toward(bell);
                        }
                    if (room.Kind == RoomKind.Campfire && !director.BossDoorOpen.Value)
                        for (int d = 0; d < 4; d++)
                            if (room.Neighbor[d] == map.Boss.Id) return Toward(DungeonMap.EntryPoint(room, d));
                    goal = room.Kind == RoomKind.Campfire ? map.Boss.Id : map.Campfire.Id;
                    if (goal == room.Id) return Vector2.zero;
                    if (!seen[goal]) return Vector2.zero;
                }
                int dir = first[goal];
                // Walk to the doorway, then down the corridor into the next room.
                Vector2 door = DungeonMap.DoorPosition(room, dir);
                Vector2 inside = DungeonMap.EntryPoint(room, dir);
                var nextRoom = map[room.Neighbor[dir]];
                Vector2 beyond = nextRoom != null ? DungeonMap.EntryPoint(nextRoom, Dir.Opposite(dir)) : door + (Vector2)Dir.Step(dir) * 1.5f;
                bool lined = dir is Dir.N or Dir.S ? Mathf.Abs(pos.x - door.x) < 0.8f : Mathf.Abs(pos.y - door.y) < 0.8f;
                return lined ? Toward(beyond) : Toward(inside);
            }

            public void Think(PlayerMover mover)
            {
                var director = RunDirector.Instance;
                Vector2 pos = mover.transform.position;
                Move = Vector2.zero;
                Attack = false;
                if (PlayArea.InCamp)
                {
                    WanderCamp(pos);
                    return;
                }
                if (director == null || !director.IsRunning) return;

                var room = director.LocalRoom;
                bool fighting = director.IsFighting(room.Id);

                // A ghost: hover by the nearest monster (it slows them) and throw sparks at it.
                if (_player.Downed.Value)
                {
                    var near = EnemyView.Instance != null ? EnemyView.Instance.NearestWhere(pos, 14f, d => !d.Prop && !d.Untargetable) : null;
                    if (near is Vector2 target)
                    {
                        Vector2 to = target - pos;
                        Aim = to;
                        Attack = true;
                        Move = to.magnitude > 1.2f ? to.normalized : Vector2.zero;
                    }
                    return;
                }

                // Help downed allies in this room first.
                foreach (var other in NetPlayer.All)
                {
                    if (other == _player || !other.Downed.Value || director.Map.RoomAt(other.FallenAt) != room) continue;
                    Vector2 toMate = other.FallenAt - pos;
                    if (toMate.magnitude > 1f)
                    {
                        Move = toMate.normalized;
                        return;
                    }
                }

                var view = EnemyView.Instance;
                // Nothing to fight here: explore (pedestals, unvisited rooms, then the campfire and the boss door).
                if (!fighting && director.Trial.Value == BossTrial.None)
                {
                    var chest = view == null ? null : view.Nearest(pos, EnemyType.TreasureChest) ?? view.Nearest(pos, EnemyType.CrackedWall);
                    if (chest.HasValue)
                    {
                        Vector2 toChest = chest.Value - pos;
                        Aim = toChest;
                        Move = toChest.magnitude > 1.3f ? toChest.normalized : Vector2.zero;
                        Attack = toChest.magnitude < 2.5f;
                        return;
                    }
                    Move = Explore(director, room, pos);
                    return;
                }
                // Boss trials: stand on the unlit sigils, smash the braziers or the soul vessel first.
                var trial = director.Trial.Value;
                // Twin plates: in a team each bot keeps to one side; alone, run to whichever is dark.
                if (fighting && room.Kind == RoomKind.Puzzle && room.Puzzle == PuzzleKind.Twin && NetPlayer.All.Count > 1)
                {
                    Vector2 mine = room.Origin + RunSimulation.TwinSpots[_player.Slot.Value % 2];
                    Vector2 toMine = mine - pos;
                    Move = toMine.sqrMagnitude > 0.3f ? toMine.normalized : Vector2.zero;
                    return;
                }
                // A dark hall: whoever is nearest goes for the lantern while nobody carries it.
                if (fighting && room.Dark && director.LanternHolder.Value == 0 && NearestToLantern(director, pos))
                {
                    Vector2 toLantern = director.LanternPos.Value - pos;
                    Move = toLantern.sqrMagnitude > 0.3f ? toLantern.normalized : Vector2.zero;
                    return;
                }
                // Pressure plates: run to the next unlit one.
                if (fighting && room.Kind == RoomKind.Puzzle && room.Puzzle is PuzzleKind.Plates or PuzzleKind.Gauntlet or PuzzleKind.Twin && view != null && view.Nearest(pos, EnemyType.PressurePlate) is Vector2 plate)
                {
                    Vector2 toPlate = plate - pos;
                    Move = toPlate.sqrMagnitude > 0.3f ? toPlate.normalized : Vector2.zero;
                    return;
                }
                if (trial == BossTrial.BloodRitual && view != null && view.Nearest(pos, EnemyType.BloodSigil) is Vector2 sigil)
                {
                    Vector2 toSigil = sigil - pos;
                    Move = toSigil.sqrMagnitude > 0.3f ? toSigil.normalized : Vector2.zero;
                    return;
                }
                var nearest = view == null ? null
                    : trial == BossTrial.BoneWard ? view.Nearest(pos, EnemyType.BoneBrazier) ?? view.Nearest(pos)
                    : trial == BossTrial.Phylactery ? view.Nearest(pos, EnemyType.SoulVessel) ?? view.Nearest(pos)
                    : view.Nearest(pos);
                if (nearest.HasValue)
                {
                    Vector2 to = nearest.Value - pos;
                    Aim = to;
                    bool melee = _player.Class.Value is ClassId.Warrior or ClassId.Guardian or ClassId.Assassin or ClassId.Paladin;
                    float want = melee ? 1.4f : 5.5f;
                    float d = to.magnitude;
                    Move = d > want + 0.5f ? to.normalized : d < want - 1f ? -to.normalized : new Vector2(-to.y, to.x).normalized * 0.4f;
                    Attack = d < (melee ? 2.2f : _player.Class.Value is ClassId.Alchemist or ClassId.Deathcaller ? 8f : 10f);
                }
                else
                {
                    Move = -pos.normalized * Mathf.Min(1f, pos.magnitude * 0.3f);
                }

                // Step away from danger zones.
                var presenter = EventPresenter.Instance;
                if (presenter != null && presenter.DangerAt(pos, out Vector2 away))
                {
                    Move = away;
                    if (Time.time > _nextDodge)
                    {
                        _nextDodge = Time.time + 1.5f;
                        mover.Dodge();
                    }
                }

                if (Attack && Time.time > _nextSkill1)
                {
                    _nextSkill1 = Time.time + 2f;
                    mover.Skill1();
                }
                if (_player.Focus.Value >= 100 && nearest.HasValue) mover.Ultimate(); // the bots use their ultimate as soon as it is full
                if (Time.time > _nextSkill2 && (nearest.HasValue || _player.Class.Value == ClassId.Mage))
                {
                    _nextSkill2 = Time.time + 3f;
                    mover.Skill2();
                }
            }
        }
    }
}
