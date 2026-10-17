using UnityEngine;

namespace Game.Core
{
    /// <summary>Each class's ultimate (必殺技): its name and what it does, for the HUD and the codex.</summary>
    public static class UltimateCatalog
    {
        public static string Name(ClassId c) => c switch
        {
            ClassId.Warrior => "ブレードストーム",
            ClassId.Mage => "グレイシャー",
            ClassId.Ranger => "アローレイン",
            ClassId.Guardian => "フォートレス",
            ClassId.Assassin => "サウザンドシャドウ",
            ClassId.Alchemist => "ビッグボム",
            ClassId.Deathcaller => "デッドマーチ",
            _ => "ディヴァインジャッジメント",
        };

        public static string Text(ClassId c) => c switch
        {
            ClassId.Warrior => "しばらく剣を振り回し、周りの敵を斬り続ける（受けるダメージ半減）",
            ClassId.Mage => "広い範囲の敵を凍らせ、氷の刃で斬る",
            ClassId.Ranger => "照準の先にアローレイン。中の敵にマークを付ける",
            ClassId.Guardian => "近くの仲間に大きな盾と、受けるダメージを減らす聖域",
            ClassId.Assassin => "近くの敵6体まで、一瞬で斬り抜ける（必ずクリティカル）",
            ClassId.Alchemist => "照準の先へ大きな爆薬。燃える床が残る",
            ClassId.Deathcaller => "ボーンサーヴァントを4体呼び、周りの敵すべてを呪う",
            _ => "部屋の敵にジャッジメントを落とし、仲間を癒す",
        };
    }

    /// <summary>
    /// Ultimates and the focus gauge (闘志): blows dealt, perfect dodges, unions and combos fill it; when it is full
    /// the ultimate key (R) unleashes the class's big move. One press empties it.
    /// </summary>
    public sealed partial class RunSimulation
    {
        public const float FocusMax = 100f;
        public const float FocusPerDamage = 0.1f;   // per point dealt
        public const float FocusPerHurt = 0.15f;    // per point taken
        public const float FocusPerfectDodge = 18f;
        public const float FocusUnion = 25f;
        /// <summary>An ultimate's own blows don't fill the gauge back up.</summary>
        private bool _ultimateCasting;
        public const float StormTime = 2.4f;
        public const float StormTick = 0.3f;
        public const float StormRadius = 3f;
        public const float RainTime = 3f;
        public const float RainRadius = 4f;

        public void GainFocus(SimPlayer p, float amount)
        {
            if (p == null || !p.Alive || amount <= 0f) return;
            p.Focus = Mathf.Min(FocusMax, p.Focus + amount);
        }

        /// <summary>The ultimate key: only with a full gauge.</summary>
        private void TryUltimate(SimPlayer p, Vector2 dir)
        {
            if (p.Focus < FocusMax) return;
            p.Focus = 0f;
            float might = p.Stats.Get(StatType.Might);
            Vector2 target = p.Position + Vector2.ClampMagnitude(p.Aim, 8f);
            if (Room != null) target = ClampToRoom(target, 0.5f);
            float radius = 0f;
            switch (p.ClassId)
            {
                case ClassId.Warrior:
                    p.UltTimer = StormTime;
                    p.UltTick = 0f;
                    radius = StormRadius;
                    break;
                case ClassId.Mage:
                    radius = 7f;
                    foreach (int i in Around(p.Position, radius))
                    {
                        ApplyStatus(i, StatusKind.Frozen, Enemies[i].Def.Boss ? 1.2f : 3.5f, p);
                        DamageEnemy(i, 60f * might, p, HitFlags.None, (Enemies[i].Position - p.Position).normalized, 2f);
                    }
                    break;
                case ClassId.Ranger:
                    p.UltTimer = RainTime;
                    p.UltTick = 0f;
                    p.UltTarget = target;
                    radius = RainRadius;
                    break;
                case ClassId.Guardian:
                    radius = 8f;
                    foreach (var ally in Players)
                        if (ally.Alive && (ally.Position - p.Position).sqrMagnitude <= radius * radius)
                            ally.Shield = Mathf.Max(ally.Shield, 60f);
                    _zones.Add(new Zone { Kind = AreaKind.Sanctuary, Owner = p, Position = p.Position, Radius = 6f, Timer = 6f, Boost = 1.5f });
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Sanctuary, Pos = p.Position, Value = 6f, Size = new Vector2(6f, 0f) });
                    break;
                case ClassId.Assassin:
                {
                    radius = 8f;
                    var hits = Around(p.Position, radius);
                    hits.Sort((a, b) => (Enemies[a].Position - p.Position).sqrMagnitude.CompareTo((Enemies[b].Position - p.Position).sqrMagnitude));
                    for (int k = 0; k < hits.Count && k < 6; k++)
                    {
                        int i = hits[k];
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.ShadowStep, Pos = Enemies[i].Position, Value = 1.2f });
                        DamageEnemy(i, 80f * might * CritMultiplier(p), p, HitFlags.Heavy | HitFlags.Crit, (Enemies[i].Position - p.Position).normalized, 4f);
                    }
                    break;
                }
                case ClassId.Alchemist:
                    radius = 4.5f;
                    foreach (int i in Around(target, radius))
                        DamageEnemy(i, 120f * might, p, HitFlags.Heavy, (Enemies[i].Position - target).normalized, 7f);
                    _zones.Add(new Zone { Kind = AreaKind.FirePool, Owner = p, Position = target, Radius = 3.5f, Timer = 6f });
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Explosion, Pos = target, Value = radius });
                    Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.FirePool, Pos = target, Value = 3.5f, Size = new Vector2(6f, 0f) });
                    break;
                case ClassId.Deathcaller:
                    radius = 6f;
                    for (int k = 0; k < 4; k++) RaiseServant(p, p.RoomId, p.Position + Rotate(Vector2.right, k * 90f + 45f) * 1.4f, overCap: true);
                    foreach (int i in Around(p.Position, radius)) ApplyStatus(i, StatusKind.Cursed, 8f, p);
                    break;
                default: // paladin
                    radius = 9f;
                    foreach (int i in Around(p.Position, radius))
                    {
                        Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Judgment, Pos = Enemies[i].Position, Value = 1.4f });
                        ApplyStatus(i, StatusKind.Stunned, Enemies[i].Def.Boss ? 0.4f : 1f, p);
                        DamageEnemy(i, 70f * might, p, HitFlags.Heavy, Vector2.zero, 0f);
                    }
                    foreach (var ally in Players)
                        if (ally.Alive && (ally.Position - p.Position).sqrMagnitude <= radius * radius)
                            ally.Hp = Mathf.Min(ally.MaxHp, ally.Hp + ally.MaxHp * 0.25f);
                    break;
            }
            Events.Add(new SimEvent(SimEventType.Ultimate) { ClientId = p.ClientId, Int = (int)p.ClassId, Pos = p.ClassId is ClassId.Ranger or ClassId.Alchemist ? target : p.Position, Vec = dir, Value = radius });
        }

        /// <summary>Ultimates that last: the warrior's blade storm, the ranger's rain of arrows.</summary>
        private void StepUltimate(SimPlayer p, float dt)
        {
            if (p.UltTimer <= 0f) return;
            p.UltTimer -= dt;
            p.UltTick -= dt;
            if (p.UltTick > 0f) return;
            float might = p.Stats.Get(StatType.Might);
            if (p.ClassId == ClassId.Warrior)
            {
                p.UltTick = StormTick;
                foreach (int i in Around(p.Position, StormRadius))
                    DamageEnemy(i, 26f * might, p, HitFlags.None, (Enemies[i].Position - p.Position).normalized, 3f);
                Events.Add(new SimEvent(SimEventType.AreaEffect) { ClientId = p.ClientId, Int = (int)AreaKind.Slam, Pos = p.Position, Value = StormRadius * 0.8f });
            }
            else if (p.ClassId == ClassId.Ranger)
            {
                p.UltTick = 0.25f;
                foreach (int i in Around(p.UltTarget, RainRadius))
                {
                    ApplyStatus(i, StatusKind.Marked, 3f, p);
                    DamageEnemy(i, 16f * might, p, HitFlags.None, Vector2.zero, 0f);
                }
            }
        }

        private System.Collections.Generic.List<int> Around(Vector2 at, float radius)
        {
            Enemies.QueryCircle(at, radius, _hits);
            var list = new System.Collections.Generic.List<int>();
            foreach (int i in _hits.ToArray())
                if (!Enemies[i].Dead && !Enemies[i].Def.Friendly && !Enemies[i].Def.Untargetable) list.Add(i);
            return list;
        }
    }
}
