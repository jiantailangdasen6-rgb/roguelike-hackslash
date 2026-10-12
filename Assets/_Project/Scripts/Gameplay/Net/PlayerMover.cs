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
        }

        private void OnDisable()
        {
            input.Skill1Pressed -= Skill1;
            input.Skill2Pressed -= Skill2;
            input.DodgePressed -= Dodge;
        }

        /// <summary>The host moved this player (a new area, the gathering bell): a jump everyone sees as one.</summary>
        public void Teleport(Vector2 position) => Teleport(position, network: true);

        private static RoomLayout CurrentLayout => PlayArea.Layout;

        private bool CanAct
        {
            get
            {
                var director = RunDirector.Instance;
                return director != null && director.IsRunning && !director.Halted && !player.Downed.Value && !PauseMenuView.IsOpen && !PerkView.IsShowing && !BagView.IsOpen;
            }
        }

        /// <summary>In the camp nobody fights: walking around is all, while no preparation panel is open.</summary>
        private static bool CanWalkInCamp => PlayArea.InCamp && !PauseMenuView.IsOpen && !CampView.PanelOpen;

        // ------------------------------------------------------------------ Actions (also used by the bot)

        public void Dodge()
        {
            if (!CanAct || player.Caged.Value || player.DodgeCooldown.Value > 0.05f || Time.time < _dashUntil) return;
            _dodge++;
            player.PredictAction(AttackAnim.Dodge);
            Vector2 move = CurrentMove();
            StartDash(move.sqrMagnitude > 0.01f ? move.normalized : _aim.normalized, ClassCatalog.DodgeSpeed, ClassCatalog.DodgeDuration);
            SendInput(); // send the press right away
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
            var bounds = PlayArea.Bounds;
            float r = SimPlayer.BodyRadius;
            to = Arena.Resolve(CurrentLayout, to, r);
            to.x = Mathf.Clamp(to.x, bounds.xMin + r, bounds.xMax - r);
            to.y = Mathf.Clamp(to.y, bounds.yMin + r, bounds.yMax - r);
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
                Vector2 velocity = Time.time < _dashUntil ? _dashDirection * _dashSpeed : move * player.MoveSpeed.Value * (_attackHeld ? 0.8f : 1f);
                Vector2 pos = (Vector2)transform.position + velocity * Time.deltaTime;

                var bounds = PlayArea.Bounds;
                float r = SimPlayer.BodyRadius;
                // Walking into an open doorway takes you to the next room.
                if (move.sqrMagnitude > 0.01f && !PlayArea.InCamp && TryDoor(pos, move)) return;
                pos = Arena.Resolve(CurrentLayout, pos, r);
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

        /// <summary>Half the width of a doorway (the gap in the wall you can walk through).</summary>
        public const float DoorHalfWidth = 1.4f;

        /// <summary>
        /// Pressing against the wall inside a doorway steps through it: the player appears just inside the door of the
        /// room on the other side. Returns false (blocked) for a door that is shut: the boss door until the whole team
        /// has gathered, and the boss room's doors while the lord fights.
        /// </summary>
        private bool TryDoor(Vector2 wanted, Vector2 move)
        {
            var director = RunDirector.Instance;
            if (director == null) return false;
            var map = director.Map;
            var room = director.LocalRoom;
            var b = room.Bounds;
            float r = SimPlayer.BodyRadius;
            for (int d = 0; d < 4; d++)
            {
                if (!room.HasDoor(d)) continue;
                Vector2 door = DungeonMap.DoorPosition(room, d);
                bool pushing = d switch
                {
                    Dir.N => wanted.y > b.yMax - r && move.y > 0.3f,
                    Dir.S => wanted.y < b.yMin + r && move.y < -0.3f,
                    Dir.E => wanted.x > b.xMax - r && move.x > 0.3f,
                    _ => wanted.x < b.xMin + r && move.x < -0.3f,
                };
                bool inGap = d is Dir.N or Dir.S ? Mathf.Abs(wanted.x - door.x) < DoorHalfWidth : Mathf.Abs(wanted.y - door.y) < DoorHalfWidth;
                if (!pushing || !inGap) continue;
                var next = map[room.Neighbor[d]];
                if (!director.DoorOpen(room, next)) return false;
                Teleport(DungeonMap.EntryPoint(next, Dir.Opposite(d)), network: true);
                return true;
            }
            return false;
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
            player.SendInputRpc(_aim, _attackHeld && CanAct, _skill1, _skill2, _dodge);
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

            /// <summary>Where to walk while nothing is fighting: a pedestal to use, the way down, the next unvisited room, the boss door.</summary>
            private Vector2 Explore(RunDirector director, MapRoom room, Vector2 pos)
            {
                var map = director.Map;
                Vector2 Toward(Vector2 at) => (at - pos).sqrMagnitude > 0.2f ? (at - pos).normalized : Vector2.zero;

                if (room.Kind == RoomKind.Boss && director.ExitOpen.Value) return Toward(room.Origin + RunSimulation.ExitOffset);
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
                // Walk to the doorway and on through the wall.
                Vector2 door = DungeonMap.DoorPosition(room, dir);
                Vector2 inside = DungeonMap.EntryPoint(room, dir);
                Vector2 beyond = door + (Vector2)Dir.Step(dir) * 1.5f;
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

                // Help downed allies in this room first.
                foreach (var other in NetPlayer.All)
                {
                    if (other == _player || !other.Downed.Value || director.Map.RoomAt(other.transform.position) != room) continue;
                    Vector2 toMate = (Vector2)other.transform.position - pos;
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
                // Pressure plates: run to the next unlit one.
                if (fighting && room.Kind == RoomKind.Puzzle && room.Puzzle is PuzzleKind.Plates or PuzzleKind.Gauntlet && view != null && view.Nearest(pos, EnemyType.PressurePlate) is Vector2 plate)
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
                    bool melee = _player.Class.Value is ClassId.Warrior or ClassId.Guardian or ClassId.Assassin;
                    float want = melee ? 1.4f : 5.5f;
                    float d = to.magnitude;
                    Move = d > want + 0.5f ? to.normalized : d < want - 1f ? -to.normalized : new Vector2(-to.y, to.x).normalized * 0.4f;
                    Attack = d < (melee ? 2.2f : _player.Class.Value == ClassId.Alchemist ? 8f : 10f);
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
                if (Time.time > _nextSkill2 && (nearest.HasValue || _player.Class.Value == ClassId.Mage))
                {
                    _nextSkill2 = Time.time + 3f;
                    mover.Skill2();
                }
            }
        }
    }
}
