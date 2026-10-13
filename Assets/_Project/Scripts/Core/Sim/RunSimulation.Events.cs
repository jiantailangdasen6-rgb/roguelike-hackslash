using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>What a <see cref="SimEventType.RoomEvent"/> tells.</summary>
    public enum RoomEventKind : byte
    {
        MimicRevealed, // a campfire chest springs to life
        MimicSlain,    // ... and spills its hoard
        CaptiveFreed,  // the cage broke: the adventurer thanks the team
    }

    /// <summary>
    /// Surprises in the rooms. The mimic: in some campfire rooms one chest is a monster that wakes when hit and
    /// leaves a hoard when slain. The captive: one fight room per area holds a caged adventurer; breaking the cage
    /// sets them free and they leave gear for everyone in the room. The wandering spirit (a pedestal room): offer
    /// a tenth of your max HP for a god's blessing.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float MimicChance = 0.5f;
        /// <summary>Chance a campfire hides a mimic (screenshots: -mimic makes it certain).</summary>
        public float MimicOdds = MimicChance;
        public const float SpiritOffering = 0.1f;
        public static readonly Vector2 CaptiveSpot = new(0f, 2.6f);

        private struct MimicWake
        {
            public int Room;
            public Vector2 Position;
            public float Timer;
        }

        private readonly List<MimicWake> _mimicWakes = new();

        /// <summary>Some campfires hide a mimic among their chests.</summary>
        private void MaybeDisguise(RoomState room, List<int> chests)
        {
            if (room.Kind != RoomKind.Campfire || chests.Count == 0 || !_lootRng.Chance(MimicOdds)) return;
            int i = Enemies.IndexOf(chests[_lootRng.Range(0, chests.Count)]);
            if (i >= 0) Enemies[i].Disguised = true;
        }

        private void SpawnCaptive(RoomState room)
        {
            Vector2 at = Arena.Resolve(room.Layout, room.Center + CaptiveSpot, 0.8f);
            int id = Enemies.Spawn(EnemyType.Captive, at, 1f, 1f, false);
            Enemies[Enemies.IndexOf(id)].RoomId = room.Id;
        }

        /// <summary>A disguised chest was hit: it is gone, and a mimic stands up in its place a moment later.</summary>
        private void WakeMimic(ref Enemy chest)
        {
            chest.Disguised = false;
            chest.Dead = true;
            chest.Health = 0f;
            _mimicWakes.Add(new MimicWake { Room = chest.RoomId, Position = chest.Position, Timer = 0.05f });
            // Remembered so the chest's death shows no treasure.
            _vanished.Add(chest.Id);
        }

        private readonly HashSet<int> _vanished = new();

        private void StepRoomEvents(float dt)
        {
            for (int k = _mimicWakes.Count - 1; k >= 0; k--)
            {
                var w = _mimicWakes[k];
                w.Timer -= dt;
                if (w.Timer > 0f)
                {
                    _mimicWakes[k] = w;
                    continue;
                }
                _mimicWakes.RemoveAt(k);
                Room = RoomStates[w.Room];
                int id = Enemies.Spawn(EnemyType.Mimic, w.Position, EnemyHealthScale, EnemyDamageScale, false);
                int i = Enemies.IndexOf(id);
                Enemies[i].RoomId = w.Room;
                Enemies[i].Cooldown = 0.8f;
                RoomStates[w.Room].Ambushed = true;
                Events.Add(new SimEvent(SimEventType.RoomEvent) { Int = (int)RoomEventKind.MimicRevealed, Pos = w.Position, Id = w.Room });
            }
        }

        /// <summary>The mimic's hoard: plenty of souls and something rare for everyone in the room.</summary>
        private void MimicSlain(in Enemy mimic)
        {
            int total = Mathf.RoundToInt((18f + 8f * Area) * (1f + 0.5f * Mathf.Max(0, Players.Count - 1)));
            const int pieces = 8;
            for (int s = 0; s < pieces; s++) DropSoul(mimic.Position + Rotate(Vector2.right, s * 45f + 20f) * 1f, Mathf.Max(1, total / pieces));
            int n = 0;
            foreach (var p in Players)
            {
                if (p.RoomId != mimic.RoomId) continue;
                DropFor(p, RollItem(p, 2f, Rarity.Rare), mimic.Position + Rotate(Vector2.right, -90f + (n++ - 1) * 40f) * 1.3f);
            }
            Events.Add(new SimEvent(SimEventType.RoomEvent) { Int = (int)RoomEventKind.MimicSlain, Pos = mimic.Position, Id = mimic.RoomId });
            RoomStates[mimic.RoomId].Ambushed = false;
            OfferAll(); // picks banked during the fight
        }

        /// <summary>The cage broke: the adventurer is free, heals whoever is around and leaves them gear.</summary>
        private void CaptiveFreed(in Enemy cage)
        {
            int n = 0;
            foreach (var p in Players)
            {
                if (p.RoomId != cage.RoomId) continue;
                if (p.Alive)
                {
                    float before = p.Hp;
                    p.Heal(p.MaxHp * 0.25f);
                    if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
                }
                DropFor(p, RollItem(p, 1.5f, Rarity.Magic), cage.Position + Rotate(Vector2.right, -90f + (n++ - 1) * 40f) * 1.3f);
            }
            for (int s = 0; s < 4; s++) DropSoul(cage.Position + Rotate(Vector2.right, s * 90f + 45f) * 0.8f, 3 + Area * 2);
            Events.Add(new SimEvent(SimEventType.RoomEvent) { Int = (int)RoomEventKind.CaptiveFreed, Pos = cage.Position, Id = cage.RoomId });
        }

        /// <summary>The wandering spirit: a tenth of max HP for the rest of the run, for a god's blessing.</summary>
        private void OfferToSpirit(SimPlayer p)
        {
            p.AddOffering();
            p.StockedChoices.Enqueue(ChoiceKind.Blessing);
        }
    }
}
