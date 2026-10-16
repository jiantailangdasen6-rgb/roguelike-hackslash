using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Elite affixes (EliteAffixCatalog): every elite carries one at random (two on hard, three in nightmare), so the
    /// same monster is a different fight each time. Swift (faster, attacks sooner), Regenerating (heals when left
    /// alone), Volatile (bursts when slain), Splitting (two smaller ones when slain), Summoner (calls help now and
    /// then) and Shielded (a ward that soaks blows and comes back when left alone). Clients learn them from the
    /// EliteAffixes event and see the shield come and go from EliteShield.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float SwiftSpeed = 1.4f;
        public const float SwiftCooldown = 0.75f;
        public const float RegenDelay = 2f;
        public const float RegenPerSecond = 0.03f;   // share of max HP
        public const float VolatileRadius = 2f;
        public const float VolatileDelay = 0.9f;
        public const float VolatileDamage = 18f;
        public const float SplitHealth = 0.35f;      // share of the elite's health scale, each
        public const float AffixSummonEvery = 8f;
        public const int AffixSummonCap = 2;
        public const float ShieldShare = 0.25f;      // of max HP
        public const float ShieldBack = 6f;          // seconds without a blow

        private readonly Dictionary<int, int> _summonedBy = new(); // summon id -> elite id

        public static int AffixCount(Difficulty d) => d switch
        {
            Difficulty.Hard => 2,
            Difficulty.Nightmare => 3,
            _ => 1,
        };

        /// <summary>A new elite draws its affixes.</summary>
        public void GiveAffixes(int id)
        {
            int i = Enemies.IndexOf(id);
            if (i < 0) return;
            ref var e = ref Enemies[i];
            if (!e.Elite || e.Champion || IsChampionKind(e.Type) || e.Def.Boss || e.Def.Prop || e.Def.Friendly) return; // champions have their own tricks
            var pool = new List<EliteAffix>(EliteAffixCatalog.All);
            if (e.Def.Speed <= 0.01f) pool.Remove(EliteAffix.Swift); // things that never move
            int count = Mathf.Min(AffixCount(Difficulty), pool.Count);
            EliteAffix bits = EliteAffix.None;
            for (int k = 0; k < count; k++)
            {
                int pick = _aiRng.Range(0, pool.Count);
                bits |= pool[pick];
                pool.RemoveAt(pick);
            }
            e.Affixes = bits;
            if ((bits & EliteAffix.Shielded) != 0) e.AffixShield = e.MaxHealth * ShieldShare;
            e.AffixTimer = AffixSummonEvery * 0.5f;
            Events.Add(new SimEvent(SimEventType.EliteAffixes) { Id = e.Id, Int = (int)bits, Pos = e.Position });
        }

        private static bool Has(in Enemy e, EliteAffix a) => (e.Affixes & a) != 0;

        /// <summary>Each tick: regeneration, the summoner's calls, the shield coming back.</summary>
        private void StepAffixes(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                ref var e = ref Enemies[i];
                if (e.Dead || e.Affixes == EliteAffix.None) continue;
                bool quiet = Elapsed - e.LastHurt > RegenDelay;
                if (Has(e, EliteAffix.Regenerating) && quiet && e.Health < e.MaxHealth)
                    e.Health = Mathf.Min(e.MaxHealth, e.Health + e.MaxHealth * RegenPerSecond * dt);
                if (Has(e, EliteAffix.Shielded) && e.AffixShield <= 0f && Elapsed - e.LastHurt > ShieldBack)
                {
                    e.AffixShield = e.MaxHealth * ShieldShare;
                    Events.Add(new SimEvent(SimEventType.EliteShield) { Id = e.Id, Int = 1, Pos = e.Position });
                }
                if (Has(e, EliteAffix.Summoner) && e.State == EnemyState.Chase)
                {
                    e.AffixTimer -= dt;
                    if (e.AffixTimer <= 0f)
                    {
                        e.AffixTimer = AffixSummonEvery;
                        Summon(i);
                    }
                }
            }
        }

        private void Summon(int index)
        {
            var e = Enemies[index];
            int alive = 0;
            foreach (var (summon, by) in _summonedBy)
            {
                if (by != e.Id) continue;
                int j = Enemies.IndexOf(summon);
                if (j >= 0 && !Enemies[j].Dead) alive++;
            }
            if (alive >= AffixSummonCap) return;
            var type = SummonType();
            Vector2 at = Room != null ? ClampToRoom(e.Position + Rotate(Vector2.right, _aiRng.Range(0f, 360f)) * 1.4f, 0.5f) : e.Position + Vector2.right;
            int id = Enemies.Spawn(type, at, EnemyHealthScale, EnemyDamageScale, false);
            int k = Enemies.IndexOf(id);
            Enemies[k].RoomId = e.RoomId;
            _summonedBy[id] = e.Id;
            Events.Add(new SimEvent(SimEventType.SpawnWarning) { Pos = at, Int = (int)type, Value = 0.3f });
            Events.Add(new SimEvent(SimEventType.EliteShield) { Id = e.Id, Int = 2, Pos = e.Position }); // the call
        }

        /// <summary>The land's weakest walker answers a summoner.</summary>
        private EnemyType SummonType()
        {
            foreach (var (t, _, _) in BiomeCatalog.Get(Biome).Pool)
            {
                var m = EnemyCatalog.Get(t);
                if (!m.Boss && !m.Prop && !m.Flying && m.Speed > 0.01f) return t;
            }
            return EnemyType.Skeleton;
        }

        /// <summary>From DamageEnemy: the shield soaks the blow first; any blow resets the quiet time.</summary>
        private float AffixDamage(int index, float damage, ref HitFlags flags)
        {
            ref var e = ref Enemies[index];
            e.LastHurt = Elapsed;
            if (!Has(e, EliteAffix.Shielded) || e.AffixShield <= 0f) return damage;
            float soaked = Mathf.Min(e.AffixShield, damage);
            e.AffixShield -= soaked;
            if (e.AffixShield <= 0f) Events.Add(new SimEvent(SimEventType.EliteShield) { Id = e.Id, Int = 0, Pos = e.Position });
            if (damage - soaked <= 0f) flags |= HitFlags.Blocked;
            return damage - soaked;
        }

        /// <summary>From ProcessDeaths: what a slain elite leaves behind.</summary>
        private void AffixFell(in Enemy e)
        {
            _summonedBy.Remove(e.Id);
            if (Has(e, EliteAffix.Volatile))
                QueueBurst(e.Position, VolatileRadius, VolatileDelay, VolatileDamage * EnemyDamageScale, AreaKind.Explosion);
            if (Has(e, EliteAffix.Splitting) && !e.Champion)
            {
                for (int k = 0; k < 2; k++)
                {
                    Vector2 at = Room != null ? ClampToRoom(e.Position + Rotate(Vector2.right, k * 180f + 20f) * 0.9f, 0.5f) : e.Position;
                    int id = Enemies.Spawn(e.Type, at, EnemyHealthScale * SplitHealth, EnemyDamageScale, false);
                    Enemies[Enemies.IndexOf(id)].RoomId = e.RoomId;
                }
                Events.Add(new SimEvent(SimEventType.AreaEffect) { Int = (int)AreaKind.SoulBurst, Pos = e.Position, Value = 1.2f });
            }
        }
    }
}
