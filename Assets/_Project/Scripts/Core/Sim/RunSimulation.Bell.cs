using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The gathering bell by the campfire. Standing under it for a moment rings it: everyone alive who is not in the
    /// middle of a fight is called to the fire (they appear around it), so nobody waits long at the boss door.
    /// Players still fighting stay where they are; downed players wait for help.
    /// </summary>
    public sealed partial class RunSimulation
    {
        /// <summary>Where the bell hangs, from the campfire room's centre (clear of the fire, crates, chests and doors).</summary>
        public static readonly Vector2 BellSpot = new(6.5f, 4.2f);
        public const float BellRadius = 1.3f;
        public const float BellHoldTime = 0.8f;
        public const float BellCooldown = 15f;

        public Vector2 BellPosition => Map.Campfire.Bounds.center + BellSpot;
        /// <summary>Seconds until the bell can ring again.</summary>
        public float BellWait { get; private set; }
        private float _bellHold;

        private void StepBell(float dt)
        {
            BellWait = Mathf.Max(0f, BellWait - dt);
            var fire = Map.Campfire;
            SimPlayer ringer = null;
            foreach (var p in Players)
                if (p.Alive && p.RoomId == fire.Id && (p.Position - BellPosition).sqrMagnitude <= BellRadius * BellRadius)
                    ringer = p;
            _bellHold = ringer != null && BellWait <= 0f ? _bellHold + dt : 0f;
            if (_bellHold < BellHoldTime || Players.Count < 2) return;
            _bellHold = 0f;
            RingBell(ringer);
        }

        private void RingBell(SimPlayer ringer)
        {
            BellWait = BellCooldown;
            var fire = Map.Campfire;
            int called = 0, slot = 0;
            foreach (var p in Players)
            {
                if (p == ringer || !p.Alive || p.RoomId == fire.Id) continue;
                if (p.RoomId >= 0 && p.RoomId < RoomStates.Count && RoomStates[p.RoomId].Fighting) continue; // finish the fight first
                p.Position = fire.Bounds.center + Arena.RestFire + Rotate(Vector2.down, -40f + 80f * slot++) * 2.6f;
                p.CagedBy = 0;
                called++;
                Events.Add(new SimEvent(SimEventType.Teleport) { ClientId = p.ClientId, Pos = p.Position });
            }
            Events.Add(new SimEvent(SimEventType.BellRung) { ClientId = ringer.ClientId, Pos = BellPosition, Int = called });
        }
    }
}
