using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay
{
    /// <summary>
    /// First-run guidance, one hint at a time above the toasts, shown where it matters: how to walk through a door
    /// (near one, until you have gone through), how to use a pedestal (in its room, until you have used one), what
    /// the locked treasure room needs. Each is remembered in the profile once done (or after being on screen for a
    /// while), so it stops showing for players who know the game.
    /// </summary>
    public sealed class GuideHints
    {
        private const float MaxShown = 14f; // seconds on screen before it counts as learnt anyway

        private readonly GameObject _root;
        private readonly Text _text;
        private string _current;
        private float _shownFor;
        private MapRoom _startRoom;
        private float _alpha;
        private readonly CanvasGroup _group;

        public GuideHints(Transform parent)
        {
            var frame = UIKit.Framed(parent, "Guide", new Color(0.05f, 0.04f, 0.06f, 0.9f));
            var outer = frame.Outer();
            outer.Place(new Vector2(0.5f, 0f), new Vector2(1000f, 70f), new Vector2(0f, 420f));
            _group = outer.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _text = UIKit.Label(frame.transform, "", 27, TextAnchor.MiddleCenter);
            _text.rectTransform.Stretch(8f);
            _root = outer.gameObject;
            _group.alpha = 0f;
        }

        private static bool Seen(string key) => ProfileStore.Current.guideSeen.Contains(key);

        private static void Learn(string key)
        {
            if (Seen(key)) return;
            ProfileStore.Current.guideSeen.Add(key);
            ProfileStore.Save();
        }

        /// <param name="hidden">Something else owns the screen (a choice, the big map): fade out but keep counting nothing.</param>
        public void Update(RunDirector d, NetPlayer local, bool hidden)
        {
            string key = null, text = null;
            if (d != null && local != null && d.IsRunning)
            {
                var room = d.LocalRoom;
                _startRoom ??= room;
                Pick(d, local, room, ref key, ref text);
            }

            if (key != _current)
            {
                _current = key;
                _shownFor = 0f;
                if (text != null) _text.text = $"<color=#e0b45a>◆</color> {text}";
            }
            if (_current != null && !hidden)
            {
                _shownFor += Time.deltaTime;
                if (_shownFor > MaxShown) Learn(_current);
            }
            float target = _current != null && !hidden ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * 3f);
            _group.alpha = _alpha;
            _root.SetActive(_alpha > 0.001f);
        }

        /// <summary>The most useful hint right now (null when there is nothing new to say).</summary>
        private void Pick(RunDirector d, NetPlayer local, MapRoom room, ref string key, ref string text)
        {
            Vector2 me = local.transform.position;

            // Doors: once you have left the room you started in, you know how.
            if (!Seen("door"))
            {
                if (room != _startRoom) Learn("door");
                else if (!d.IsFighting(room.Id) && NearDoor(room, me, 6f))
                {
                    key = "door";
                    text = "壁の切れ目から通路を歩いて、隣の部屋へ進める";
                    return;
                }
            }

            // Pedestals: stand on it; learnt once any has been used.
            if (!Seen("pedestal"))
            {
                if (local.UsedPedestals.Value != 0) Learn("pedestal");
                else if (RoomCatalog.HasPedestal(room.Kind))
                {
                    key = "pedestal";
                    text = "中央の祭壇の上にしばらく立つと使える（1人1回）";
                    return;
                }
            }

            // The treasure room next door is locked: where keys come from, then how to use one.
            for (int dir = 0; dir < 4; dir++)
            {
                var next = d.Map[room.Neighbor[dir]];
                if (next == null || next.Kind != RoomKind.Treasure || d.IsUnlocked(next.Id)) continue;
                if (d.Keys.Value == 0 && !Seen("key"))
                {
                    key = "key";
                    text = "鍵のかかった宝物庫。鍵は精鋭の主の間と仕掛けの間で手に入る";
                    return;
                }
                if (d.Keys.Value > 0 && !Seen("unlock"))
                {
                    key = "unlock";
                    text = "鍵を持っている。宝物庫の扉の前に立つと開く";
                    return;
                }
            }
            if (!Seen("unlock"))
                foreach (var r in d.Map.Rooms)
                    if (r.Kind == RoomKind.Treasure && d.IsUnlocked(r.Id)) Learn("unlock");

            // The campfire: the boss door needs everyone.
            if (room.Kind == RoomKind.Campfire && !d.BossDoorOpen.Value && !Seen("bossdoor") && NetPlayer.All.Count > 1)
            {
                key = "bossdoor";
                text = "主の扉は全員がそろうと開く。集合の鐘で仲間を呼べる";
            }
        }

        private static bool NearDoor(MapRoom room, Vector2 at, float radius)
        {
            for (int dir = 0; dir < 4; dir++)
                if (room.HasDoor(dir) && (DungeonMap.DoorPosition(room, dir) - at).sqrMagnitude < radius * radius)
                    return true;
            return false;
        }
    }
}
