using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>What waits in an encounter room (MapRoom.Encounter).</summary>
    public enum EncounterKind : byte
    {
        LostSoul,  // 迷い人: help (everyone healed in full) / rob (gear for each, and a curse each)
        Statue,    // 呪われた像: pray (a power each, a tenth of max HP each) / smash (two elites with affixes)
        Gambler,   // 賭けの商人: bet (half: two upgrades each, half: a quarter of max HP lost) / decline (soul shards)
        BloodFont, // 血の泉: drink (an upgrade and a curse each) / cleanse (each loses a curse)
    }

    /// <summary>
    /// Encounter rooms (出来事の間): something met on the way that asks the team to choose. Two circles stand before
    /// it; when every living player in the room stands in the same circle for <see cref="VoteTime"/>, that choice is
    /// made for everyone there (alone, a player decides alone). Once chosen the room is spent.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float VoteTime = 1.5f;
        public const float VoteRadius = 1.1f;
        public const float GambleLoss = 0.25f;
        public const int DeclineShards = 15;
        public static readonly Vector2 EncounterSpot = new(0f, 0.8f);

        /// <summary>Where the choice circles (0 = left, 1 = right) stand in an encounter room.</summary>
        public static Vector2 ChoiceSpot(Rect bounds, int choice) => bounds.center + new Vector2(choice == 0 ? -2.8f : 2.8f, -2.4f);

        public static int EncounterCount => 4;

        /// <summary>Each tick for an encounter room: who stands where, and the vote.</summary>
        private void StepEncounter(RoomState room, float dt)
        {
            if (room.EncounterChoice >= 0) return;
            int present = 0, vote = -1;
            bool split = false;
            foreach (var p in Players)
            {
                if (!p.Alive || p.RoomId != room.Id) continue;
                present++;
                int at = -1;
                for (int c = 0; c < 2; c++)
                    if ((p.Position - ChoiceSpot(room.Bounds, c)).sqrMagnitude <= VoteRadius * VoteRadius) at = c;
                if (at < 0 || (vote >= 0 && vote != at)) split = true;
                vote = at;
            }
            if (present == 0 || split || vote < 0)
            {
                room.VoteHold = 0f;
                return;
            }
            if (vote != room.VoteFor) room.VoteHold = 0f;
            room.VoteFor = vote;
            room.VoteHold += dt;
            if (room.VoteHold < VoteTime) return;
            room.EncounterChoice = vote;
            ResolveEncounter(room, Map[room.Id].Encounter, vote);
        }

        private void ResolveEncounter(RoomState room, EncounterKind kind, int choice)
        {
            var here = new List<SimPlayer>();
            foreach (var p in Players)
                if (p.Alive && p.RoomId == room.Id) here.Add(p);
            Vector2 at = room.Bounds.center + EncounterSpot;
            int outcome = 0; // the gambler: 1 won, 2 lost
            switch (kind)
            {
                case EncounterKind.LostSoul:
                    foreach (var p in here)
                    {
                        if (choice == 0)
                        {
                            float before = p.Hp;
                            p.Hp = p.MaxHp;
                            if (p.Hp > before) Events.Add(new SimEvent(SimEventType.PlayerHealed) { ClientId = p.ClientId, Pos = p.Position, Value = p.Hp - before });
                        }
                        else
                        {
                            GrantTreasureTo(p, 0);
                            Curse(p);
                        }
                    }
                    break;
                case EncounterKind.Statue:
                    if (choice == 0)
                        foreach (var p in here)
                        {
                            p.AddOffering();
                            p.StockedChoices.Enqueue(ChoiceKind.Boon);
                        }
                    else
                        for (int k = 0; k < 2; k++)
                        {
                            Vector2 spot = at + new Vector2(k == 0 ? -1.4f : 1.4f, -0.4f);
                            int id = Enemies.Spawn(StatueGuard(), spot, EnemyHealthScale, EnemyDamageScale, true);
                            Enemies[Enemies.IndexOf(id)].RoomId = room.Id;
                            GiveAffixes(id);
                            Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = spot, Int = (int)Enemies[Enemies.IndexOf(id)].Type, Value = 0.3f });
                        }
                    break;
                case EncounterKind.Gambler:
                    if (choice == 0)
                    {
                        bool won = _perkRng.Chance(0.5f);
                        outcome = won ? 1 : 2;
                        foreach (var p in here)
                        {
                            if (won)
                            {
                                p.StockedChoices.Enqueue(ChoiceKind.Upgrade);
                                p.StockedChoices.Enqueue(ChoiceKind.Upgrade);
                            }
                            else p.Hp = Mathf.Max(1f, p.Hp - p.MaxHp * GambleLoss);
                        }
                    }
                    else BonusShards += DeclineShards * here.Count;
                    break;
                case EncounterKind.BloodFont:
                    foreach (var p in here)
                    {
                        if (choice == 0)
                        {
                            p.StockedChoices.Enqueue(ChoiceKind.Upgrade);
                            Curse(p);
                        }
                        else
                        {
                            for (int c = 0; c < p.CurseStacks.Length; c++)
                                if (p.CurseStacks[c] > 0)
                                {
                                    p.CurseStacks[c]--;
                                    p.SetCurses(p.CurseStacks);
                                    break;
                                }
                        }
                    }
                    break;
            }
            foreach (var p in Players) room.UsedBy.Add(p.ClientId); // spent for everyone (the marks go dim)
            Events.Add(new SimEvent(SimEventType.EncounterResolved) { Id = room.Id, Int = choice, Value = (int)kind, Pos = at, Size = new Vector2(outcome, here.Count) });
            foreach (var p in here) OfferNext(p);
        }

        private void Curse(SimPlayer p)
        {
            var curse = (CurseKind)_perkRng.Range(0, RewardCatalog.CurseKinds);
            p.CurseStacks[(int)curse]++;
            p.SetCurses(p.CurseStacks);
        }

        /// <summary>What steps out of a smashed statue: the land's toughest common walker.</summary>
        private EnemyType StatueGuard()
        {
            EnemyType best = EnemyType.Skeleton;
            float hp = 0f;
            foreach (var (t, _, _) in BiomeCatalog.Get(Biome).Pool)
            {
                var m = EnemyCatalog.Get(t);
                if (m.Boss || m.Prop || m.Flying || m.Speed <= 0.01f || IsChampionKind(t) || m.Health <= hp) continue;
                hp = m.Health;
                best = t;
            }
            return best;
        }
    }
}
