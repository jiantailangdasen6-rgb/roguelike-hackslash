using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>Recycles SpriteRenderers for the many short-lived visuals (enemies, projectiles, gems, effects).</summary>
    public sealed class SpritePool
    {
        private readonly Transform _parent;
        private readonly Stack<SpriteRenderer> _free = new();

        public SpritePool(Transform parent) => _parent = parent;

        public SpriteRenderer Get(Sprite sprite, Color color, int sortingOrder, Vector2 position, float scale = 1f)
        {
            SpriteRenderer r;
            if (_free.Count > 0)
            {
                r = _free.Pop();
                r.gameObject.SetActive(true);
            }
            else
            {
                r = new GameObject("Sprite").AddComponent<SpriteRenderer>();
                r.transform.SetParent(_parent, false);
            }

            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = sortingOrder;
            r.transform.SetPositionAndRotation(position, Quaternion.identity);
            r.transform.localScale = Vector3.one * scale;
            return r;
        }

        public void Release(SpriteRenderer r)
        {
            if (r == null) return;
            r.gameObject.SetActive(false);
            _free.Push(r);
        }
    }
}
