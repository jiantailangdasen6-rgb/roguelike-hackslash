using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// What the follow camera looks at: the local player, clamped so the view never shows past the arena's walls
    /// (the view size comes from the camera, so it works for any screen shape).
    /// </summary>
    public sealed class CameraTarget : MonoBehaviour
    {
        private Transform _player;
        private Camera _camera;

        public static CameraTarget Create(Transform player)
        {
            var go = new GameObject("CameraTarget");
            var target = go.AddComponent<CameraTarget>();
            target._player = player;
            go.transform.position = player.position;
            return target;
        }

        private void LateUpdate()
        {
            if (_player == null)
            {
                Destroy(gameObject);
                return;
            }
            if (_camera == null) _camera = Camera.main;
            Vector2 p = _player.position;
            if (_camera != null)
            {
                var b = PlayArea.Bounds;
                float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
                // The black wall tops are one tile wide around the floor; the back wall is three tiles tall.
                float xMin = b.xMin - 1f + halfW, xMax = b.xMax + 1f - halfW;
                float yMin = b.yMin - 1f + halfH, yMax = b.yMax + 3f - halfH;
                p.x = xMin <= xMax ? Mathf.Clamp(p.x, xMin, xMax) : b.center.x;
                p.y = yMin <= yMax ? Mathf.Clamp(p.y, yMin, yMax) : b.center.y;
            }
            transform.position = p;
        }
    }
}
