using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Trials made for working together. The twin plates: two plates at either end of a hall must be held at once
    /// until the seal breaks (alone, a plate stays lit a while after you step off). The dark hall: a fight in the dark
    /// where one player carries the lantern; monsters outside its light shrug off most of the damage.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public static readonly Vector2[] TwinSpots = { new(-8f, -3.5f), new(8f, -3.5f) }; // the outer two plate spots of the plate hall
        /// <summary>Seconds both plates must be held (solo: the overlap of two lingering plates).</summary>
        public float TwinHold => Players.Count <= 1 ? 1f : 4f;
        /// <summary>How long a plate stays held after you step off it.</summary>
        public float TwinLinger => Players.Count <= 1 ? 4.5f : 0.3f;

        public const float LanternRadius = 6.5f;
        public const float LanternPickup = 1.4f;
        /// <summary>Damage monsters outside the lantern's light still take.</summary>
        public const float DarkDamageFactor = 0.3f;

        private void StepTwin(RoomState room, System.Collections.Generic.List<int> props, float dt)
        {
            int held = 0;
            foreach (int id in props)
            {
                int i = Enemies.IndexOf(id);
                if (i < 0) continue;
                ref var plate = ref Enemies[i];
                bool on = false;
                foreach (var p in Players)
                    if (p.Alive && (p.Position - plate.Position).sqrMagnitude <= PlateRadius * PlateRadius) on = true;
                if (on)
                {
                    if (plate.StateTimer <= 0f) Events.Add(new SimEvent(SimEventType.TrialEvent) { Int = (int)TrialMoment.SigilLit, Pos = plate.Position });
                    plate.StateTimer = TwinLinger;
                }
                else plate.StateTimer -= dt;
                plate.Moving = plate.StateTimer > 0f; // shown lit
                if (plate.Moving) held++;
            }
            // The seal charges while both are held and slowly loses its charge otherwise.
            room.Progress = held >= props.Count ? room.Progress + dt : Mathf.Max(0f, room.Progress - dt * 0.5f);
            float charge = Mathf.Clamp01(room.Progress / Mathf.Max(0.1f, room.Target));
            foreach (int id in props)
            {
                int i = Enemies.IndexOf(id);
                if (i >= 0) Enemies[i].Health = charge;
            }
            if (room.Progress >= room.Target) EndPuzzle(room, solved: true);
        }

        // ------------------------------------------------------------------ The dark hall

        /// <summary>The dark hall of this area that is still being fought (null when none).</summary>
        public RoomState DarkRoom
        {
            get
            {
                foreach (var r in RoomStates) if (r.Dark && !r.Cleared) return r;
                return null;
            }
        }

        private void ResetLantern(RoomState room)
        {
            room.LanternBy = 0;
            room.LanternPos = room.Center + new Vector2(0f, -1.5f);
        }

        private void StepDark()
        {
            foreach (var room in RoomStates)
            {
                if (!room.Dark || room.Cleared) continue;
                if (room.LanternBy != 0)
                {
                    var holder = GetPlayer(room.LanternBy - 1);
                    if (holder == null || !holder.Alive || holder.RoomId != room.Id)
                    {
                        room.LanternBy = 0; // dropped where it was last carried in the room
                        Events.Add(new SimEvent(SimEventType.Lantern) { Id = room.Id, Int = 0, Pos = room.LanternPos, ClientId = holder?.ClientId ?? 0 });
                    }
                    else room.LanternPos = holder.Position;
                    continue;
                }
                foreach (var p in Players)
                {
                    if (!p.Alive || p.RoomId != room.Id || (p.Position - room.LanternPos).sqrMagnitude > LanternPickup * LanternPickup) continue;
                    room.LanternBy = p.ClientId + 1;
                    Events.Add(new SimEvent(SimEventType.Lantern) { Id = room.Id, Int = 1, Pos = room.LanternPos, ClientId = p.ClientId });
                    break;
                }
            }
        }

        /// <summary>A monster in an unlit part of a dark hall (blows mostly glance off it).</summary>
        private bool InDarkness(in Enemy e)
        {
            if (e.RoomId < 0 || e.RoomId >= RoomStates.Count) return false;
            var room = RoomStates[e.RoomId];
            return room.Dark && !room.Cleared && (e.Position - room.LanternPos).sqrMagnitude > LanternRadius * LanternRadius;
        }
    }
}
