using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Owner-side movement (enabled only on the owning client). The NetworkTransform replicates the result.</summary>
    public sealed class PlayerMover : MonoBehaviour
    {
        private const float DashSpeed = 14f;
        private const float DashDuration = 0.15f;
        private const float DashCooldown = 1.2f;

        [SerializeField] private NetPlayer player;
        [SerializeField] private PlayerInputReader input;

        private Vector2 _facing = Vector2.down;
        private Vector2 _dashDirection;
        private float _dashTime;
        private float _dashReadyTime;
        private bool _bot;

        public float DashCooldownRemaining => Mathf.Max(0f, _dashReadyTime - Time.time);

        private void Awake() => _bot = CommandLine.Has("-botmove");

        private void OnEnable() => input.DashPressed += Dash;
        private void OnDisable() => input.DashPressed -= Dash;

        public void Teleport(Vector2 position) => transform.position = position;

        private bool CanMove
        {
            get
            {
                var director = RunDirector.Instance;
                return director != null && director.IsRunning && !director.Paused.Value
                       && !player.Downed.Value && !InventoryView.IsOpen;
            }
        }

        private void Dash()
        {
            if (!CanMove || Time.time < _dashReadyTime) return;
            _dashDirection = _facing;
            _dashTime = Time.time + DashDuration;
            _dashReadyTime = Time.time + DashCooldown;
        }

        private void Update()
        {
            if (!CanMove) return;

            Vector2 move = _bot ? BotInput() : Vector2.ClampMagnitude(input.Move, 1f);
            if (move.sqrMagnitude > 0.01f) _facing = move.normalized;

            Vector2 velocity = Time.time < _dashTime ? _dashDirection * DashSpeed : move * player.MoveSpeed.Value;
            Vector2 pos = (Vector2)transform.position + velocity * Time.deltaTime;

            var bounds = RunSimulation.DefaultBounds;
            float r = SimPlayer.BodyRadius;
            pos.x = Mathf.Clamp(pos.x, bounds.xMin + 1f + r, bounds.xMax - 1f - r);
            pos.y = Mathf.Clamp(pos.y, bounds.yMin + 1f + r, bounds.yMax - 1f - r);
            transform.position = pos;
        }

        /// <summary>
        /// Automated-test movement (-botmove): head for the exit once the room is cleared, otherwise kite away
        /// from nearby enemies, drift toward downed teammates to revive them, and wander.
        /// </summary>
        private Vector2 BotInput()
        {
            var director = RunDirector.Instance;
            Vector2 pos = transform.position;
            if (director.RoomCleared.Value)
            {
                Vector2 toExit = director.ExitPosition.Value - pos;
                return toExit.sqrMagnitude > 0.25f ? toExit.normalized : Vector2.zero;
            }

            Vector2 desire = Vector2.zero;
            foreach (var other in NetPlayer.All)
            {
                if (other == player || !other.Downed.Value) continue;
                Vector2 toMate = (Vector2)other.transform.position - pos;
                if (toMate.sqrMagnitude > 1f) desire += toMate.normalized * 1.5f;
            }

            var enemies = EnemyView.Instance;
            if (enemies != null)
            {
                // Keep enemies at sword's length: back off when crowded, close in when they are far.
                Vector2 away = enemies.ThreatAvoidance(pos, 2.2f);
                desire += away * 2.5f + new Vector2(-away.y, away.x) * 0.8f;
                var nearest = enemies.Nearest(pos);
                if (nearest.HasValue && away.sqrMagnitude < 0.05f)
                {
                    Vector2 toEnemy = nearest.Value - pos;
                    if (toEnemy.sqrMagnitude > 3.5f * 3.5f) desire += toEnemy.normalized * 0.8f;
                }
            }

            float t = Time.time * 0.4f + player.Slot.Value * 2f;
            desire += new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * 0.3f - pos * 0.02f; // wander, stay central
            return desire.sqrMagnitude > 0.0001f ? Vector2.ClampMagnitude(desire, 1f) : Vector2.zero;
        }
    }
}
