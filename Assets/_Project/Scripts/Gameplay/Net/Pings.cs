using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>What a ping says (picked from what is under the cursor).</summary>
    public enum PingKind : byte
    {
        Come,   // 来て！ (anywhere)
        Danger, // 危ない！ (on a monster)
        Look,   // ここ！ (a pedestal, a door, a chest...)
        Gather, // 集合！ (a room picked on the big map)
    }

    /// <summary>
    /// The co-op pings in flight on this device. They are not part of the simulation: the sender's NetPlayer sends
    /// them straight to everyone (NetPlayer.PingRpc) and each device shows them for a few seconds (PingView, MinimapView).
    /// </summary>
    public static class Pings
    {
        public const float Life = 6f;

        public struct Mark
        {
            public NetPlayer From;
            public Vector2 Position;
            public PingKind Kind;
            public float Time;
            public float Age => UnityEngine.Time.time - Time;
        }

        public static readonly List<Mark> Active = new();
        public static event Action<Mark> Added;

        public static void Add(NetPlayer from, Vector2 position, PingKind kind)
        {
            Active.RemoveAll(m => m.Age > Life || m.From == null || m.From == from && m.Kind == kind); // one of each kind per player
            var mark = new Mark { From = from, Position = position, Kind = kind, Time = Time.time };
            Active.Add(mark);
            Added?.Invoke(mark);
        }

        public static void Prune() => Active.RemoveAll(m => m.Age > Life || m.From == null);

        public static string Words(PingKind kind) => kind switch
        {
            PingKind.Danger => "危ない！",
            PingKind.Look => "ここ！",
            PingKind.Gather => "集合！",
            _ => "来て！",
        };

        /// <summary>Danger is always red and things to look at gold; come and gather wear the player's colour.</summary>
        public static Color ColorOf(in Mark m) => m.Kind switch
        {
            PingKind.Danger => new Color(1f, 0.32f, 0.26f),
            PingKind.Look => new Color(1f, 0.85f, 0.42f),
            _ => m.From != null ? m.From.Color : Color.white,
        };

        public static string Sheet(PingKind kind) => kind switch
        {
            PingKind.Danger => "ping_danger",
            PingKind.Look => "ping_look",
            PingKind.Gather => "ping_gather",
            _ => "ping_come",
        };
    }
}
