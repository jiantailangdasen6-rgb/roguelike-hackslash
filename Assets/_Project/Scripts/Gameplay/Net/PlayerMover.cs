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

        public void Teleport(Vector2 position)
        {
            transform.position = position;
            _dashUntil = 0f;
        }

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
            private float _doorsSeenAt = -1f;
            public Vector2 Move;
            public Vector2 Aim = Vector2.right;
            public bool Attack;

            public Bot(NetPlayer player) => _player = player;

            private int _campStop;
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

                var doors = director.Doors;
                if (doors.Length == 0) _doorsSeenAt = -1f;
                else if (_doorsSeenAt < 0f) _doorsSeenAt = Time.time;
                // -doorwait <s>: linger before choosing so screenshots can catch the open doors.
                if (doors.Length > 0 && Time.time - _doorsSeenAt < CommandLine.GetFloat("-doorwait", 0f))
                {
                    Vector2 toView = new Vector2(0f, 3f) - pos; // close enough that the camera frames every door
                    Move = toView.sqrMagnitude > 0.3f ? toView.normalized : Vector2.zero;
                    return;
                }
                if (doors.Length > 0)
                {
                    // Every bot derives the same door from the room number, so they gather at one exit.
                    int pick = director.RoomIndex.Value % doors.Length;
                    Vector2 toExit = RunDirector.DoorPosition(pick, doors.Length) - pos;
                    Move = toExit.sqrMagnitude > 0.2f ? toExit.normalized : Vector2.zero;
                    return;
                }
                if (director.RoomCleared.Value) return;

                // Help downed allies first.
                foreach (var other in NetPlayer.All)
                {
                    if (other == _player || !other.Downed.Value) continue;
                    Vector2 toMate = (Vector2)other.transform.position - pos;
                    if (toMate.magnitude > 1f)
                    {
                        Move = toMate.normalized;
                        return;
                    }
                }

                var view = EnemyView.Instance;
                // Boss trials: stand on the unlit sigils, smash the braziers or the soul vessel first.
                var trial = director.Trial.Value;
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
