using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Each land's own champion (BiomeDef.Champion), with a trick of its own: the bone digger falls into a pile of bones
    /// and stands up again unless the pile is smashed; twin acolytes raise each other unless both fall close together;
    /// the shade lord leaves doubles that die to one blow; the leech mother heals and hardens in water; the ice knight's
    /// shell cracks after enough blows and grows back; the bombsmith throws lit kegs whose blasts hurt him too.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float PileTime = 4f;
        public const float TwinReviveTime = 4.5f;
        public const float ShadeSplitEvery = 7f;
        public const float DecoyLife = 6f;
        public const int IceShellHits = 6;
        public const float IceShellDown = 7f;
        public const float KegFuseThrown = 1.6f;

        private readonly Dictionary<int, (float Timer, bool Champion, bool Elite)> _piles = new();
        private readonly HashSet<int> _risen = new();
        private readonly List<(Vector2 Position, float Timer, int Partner, int Room, bool Champion)> _twinRevives = new();
        private readonly Dictionary<int, float> _shadeSplit = new();
        private readonly Dictionary<int, float> _decoys = new();
        private readonly Dictionary<int, (int Hits, float Down)> _iceShells = new();
        private readonly Dictionary<int, float> _kegFuses = new();

        private void ResetChampions()
        {
            _piles.Clear();
            _risen.Clear();
            _twinRevives.Clear();
            _shadeSplit.Clear();
            _decoys.Clear();
            _iceShells.Clear();
            _kegFuses.Clear();
        }

        /// <summary>
        /// A monster fell (ProcessDeaths). True when that was not the end of it (a digger in its bones, a twin its sister
        /// will raise) or it was only a double or a heap of bones: no kill and no reward then.
        /// </summary>
        private bool ChampionFell(in Enemy e)
        {
            _shadeSplit.Remove(e.Id);
            _iceShells.Remove(e.Id);
            switch (e.Type)
            {
                case EnemyType.ShadeDecoy:
                case EnemyType.BonePile:
                    return true;
                case EnemyType.BoneDigger when !_risen.Contains(e.Id):
                {
                    int id = Enemies.Spawn(EnemyType.BonePile, e.Position, EnemyHealthScale * (e.Champion ? 2f : 1f), 1f, false);
                    Enemies[Enemies.IndexOf(id)].RoomId = e.RoomId;
                    _piles[id] = (PileTime, e.Champion, e.Elite);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.BoneBurst, Pos = e.Position, Value = 1.4f });
                    return true;
                }
                case EnemyType.TwinAcolyte:
                    // The other twin, if it still stands, raises this one a little later.
                    for (int i = 0; i < Enemies.Count; i++)
                    {
                        var other = Enemies[i];
                        if (other.Dead || other.Id == e.Id || other.Type != EnemyType.TwinAcolyte || other.RoomId != e.RoomId) continue;
                        _twinRevives.Add((e.Position, TwinReviveTime, other.Id, e.RoomId, e.Champion));
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Curse, Pos = e.Position, Value = 1.2f, Size = new Vector2(TwinReviveTime, 0f) });
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>The champions' tricks, every tick.</summary>
        private void StepChampions(float dt)
        {
            // Bone piles gather themselves.
            if (_piles.Count > 0)
                foreach (int id in new List<int>(_piles.Keys))
                {
                    var (timer, champion, elite) = _piles[id];
                    int i = Enemies.IndexOf(id);
                    if (i < 0 || Enemies[i].Dead)
                    {
                        _piles.Remove(id);
                        continue;
                    }
                    timer -= dt;
                    if (timer > 0f)
                    {
                        _piles[id] = (timer, champion, elite);
                        continue;
                    }
                    _piles.Remove(id);
                    Vector2 at = Enemies[i].Position;
                    int room = Enemies[i].RoomId;
                    _vanished.Add(id);
                    Enemies[i].Dead = true;
                    Room = RoomStates[room];
                    int risen = Enemies.Spawn(EnemyType.BoneDigger, at, EnemyHealthScale * 0.5f, EnemyDamageScale, elite);
                    int ri = Enemies.IndexOf(risen);
                    Enemies[ri].RoomId = room;
                    if (champion) MakeChampion(ri);
                    _risen.Add(risen);
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Raise, Pos = at, Value = 1.6f });
                }

            // Twins raise each other.
            for (int k = _twinRevives.Count - 1; k >= 0; k--)
            {
                var t = _twinRevives[k];
                int partner = Enemies.IndexOf(t.Partner);
                if (partner < 0 || Enemies[partner].Dead)
                {
                    _twinRevives.RemoveAt(k); // both fell: no one left to pray
                    continue;
                }
                t.Timer -= dt;
                if (t.Timer > 0f)
                {
                    _twinRevives[k] = t;
                    continue;
                }
                _twinRevives.RemoveAt(k);
                Room = RoomStates[t.Room];
                if (Room.Cleared) continue;
                int id = Enemies.Spawn(EnemyType.TwinAcolyte, t.Position, EnemyHealthScale * 0.5f, EnemyDamageScale, t.Champion);
                int i = Enemies.IndexOf(id);
                Enemies[i].RoomId = t.Room;
                if (t.Champion) MakeChampion(i);
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.HolyLight, Pos = t.Position, Value = 1.6f });
            }

            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead) continue;
                switch (e.Type)
                {
                    case EnemyType.ShadeLord:
                    {
                        // Now and then: a step through the shadows, leaving two doubles behind.
                        float next = _shadeSplit.TryGetValue(e.Id, out var s) ? s - dt : ShadeSplitEvery * 0.6f;
                        if (next > 0f)
                        {
                            _shadeSplit[e.Id] = next;
                            break;
                        }
                        _shadeSplit[e.Id] = ShadeSplitEvery;
                        Room = RoomStates[e.RoomId];
                        Vector2 from = e.Position;
                        bool champion = e.Champion;
                        e.Position = Confine(BlinkSpot(from), e.Radius, false);
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.ShadowStep, Pos = from, Value = 1.4f });
                        int room = e.RoomId;
                        for (int k = 0; k < 2; k++)
                        {
                            Vector2 at = ClampToRoom(from + Rotate(Vector2.right, k * 180f + 45f) * 1.6f, 0.5f);
                            int id = Enemies.Spawn(EnemyType.ShadeDecoy, at, 1f, EnemyDamageScale, false);
                            int di = Enemies.IndexOf(id);
                            Enemies[di].RoomId = room;
                            if (champion) Enemies[di].Radius *= 1.15f;
                            _decoys[id] = DecoyLife;
                        }
                        e = ref Enemies[i];
                        break;
                    }
                    case EnemyType.LeechMother:
                        // Water feeds her.
                        if (Arena.InZone(RoomStates[e.RoomId].Layout, GimmickKind.Water, e.Position))
                            e.Health = Mathf.Min(e.MaxHealth, e.Health + e.MaxHealth * 0.015f * dt);
                        break;
                    case EnemyType.IceKnight:
                        if (_iceShells.TryGetValue(e.Id, out var shell) && shell.Down > 0f)
                        {
                            shell.Down -= dt;
                            if (shell.Down <= 0f)
                            {
                                shell = (0, 0f);
                                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.FrostNova, Pos = e.Position, Value = 1.2f });
                            }
                            _iceShells[e.Id] = shell;
                        }
                        break;
                }
            }

            // Doubles fade; thrown kegs burn down their fuses.
            if (_decoys.Count > 0)
                foreach (int id in new List<int>(_decoys.Keys))
                {
                    int i = Enemies.IndexOf(id);
                    float left = _decoys[id] - dt;
                    if (i < 0 || Enemies[i].Dead) { _decoys.Remove(id); continue; }
                    if (left > 0f) { _decoys[id] = left; continue; }
                    _decoys.Remove(id);
                    _vanished.Add(id);
                    Enemies[i].Dead = true;
                }
            if (_kegFuses.Count > 0)
                foreach (int id in new List<int>(_kegFuses.Keys))
                {
                    int i = Enemies.IndexOf(id);
                    float left = _kegFuses[id] - dt;
                    if (i < 0 || Enemies[i].Dead) { _kegFuses.Remove(id); continue; }
                    if (left > 0f) { _kegFuses[id] = left; continue; }
                    _kegFuses.Remove(id);
                    Enemies[i].Dead = true; // ProcessDeaths lights it (IgniteKeg)
                }
        }

        /// <summary>Damage to a champion-kind monster: the leech mother hardens in water, the ice knight's shell takes most.</summary>
        private float ChampionDamage(int index, float damage, ref HitFlags flags)
        {
            ref var e = ref Enemies[index];
            switch (e.Type)
            {
                case EnemyType.LeechMother when Arena.InZone(RoomStates[e.RoomId].Layout, GimmickKind.Water, e.Position):
                    return damage * 0.5f;
                case EnemyType.IceKnight:
                {
                    var shell = _iceShells.TryGetValue(e.Id, out var s) ? s : default;
                    if (shell.Down > 0f) return damage; // cracked: it takes everything
                    if ((flags & HitFlags.DamageOverTime) == 0) shell.Hits++;
                    if (shell.Hits >= IceShellHits)
                    {
                        shell = (0, IceShellDown);
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Shatter, Pos = e.Position, Value = 1.6f });
                        Events.Add(new SimEvent(SimEventType.StatusApplied) { Id = e.Id, Int = (int)StatusKind.Stunned, Value = 1.2f });
                        e.Stunned = Mathf.Max(e.Stunned, 1.2f);
                    }
                    _iceShells[e.Id] = shell;
                    flags |= HitFlags.Blocked;
                    return damage * 0.15f;
                }
                default:
                    return damage;
            }
        }

        private static bool IsChampionKind(EnemyType t) => t is EnemyType.LeechMother or EnemyType.IceKnight;

        /// <summary>The bombsmith's throw: a lit keg lands where each target stood.</summary>
        private void ThrowKegs(List<Vector2> spots, int room)
        {
            foreach (var s in spots)
            {
                Vector2 at = Arena.Resolve(RoomStates[room].Layout, ClampToRoom(s, 0.6f), 0.5f);
                int id = Enemies.Spawn(EnemyType.LitKeg, at, 1f, 1f, false);
                Enemies[Enemies.IndexOf(id)].RoomId = room;
                _kegFuses[id] = KegFuseThrown;
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.Explosion, Pos = at, Value = 0.6f });
            }
        }
    }
}
