using Game.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// What the follow camera looks at: the local player, clamped so the view never shows past the walls of the room
    /// they stand in (the view size comes from the camera, so it works for any screen shape). Stepping through a door
    /// into another room of the map moves the camera's confiner with it and cuts there instead of panning across.
    /// </summary>
    public sealed class CameraTarget : MonoBehaviour
    {
        private Transform _player;
        private Camera _camera;
        private CinemachineConfiner2D _confiner;
        private Vector3 _confinerHome;
        private Vector2 _roomCenter;
        private bool _placed;

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
            var b = PlayArea.Bounds;
            if (_camera != null)
            {
                float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
                // The black wall tops are one tile wide around the floor; the back wall is three tiles tall.
                float xMin = b.xMin - 1f + halfW, xMax = b.xMax + 1f - halfW;
                float yMin = b.yMin - 1f + halfH, yMax = b.yMax + 3f - halfH;
                p.x = xMin <= xMax ? Mathf.Clamp(p.x, xMin, xMax) : b.center.x;
                p.y = yMin <= yMax ? Mathf.Clamp(p.y, yMin, yMax) : b.center.y;
            }

            // Another room (a door, or the next area): move the confiner there and cut instead of gliding.
            Vector2 center = PlayArea.InCamp ? b.center : RunDirector.Instance != null ? RunDirector.Instance.LocalRoom.Bounds.center : b.center;
            if (!_placed || (center - _roomCenter).sqrMagnitude > 1f)
            {
                MoveConfiner(center);
                Vector3 delta = (Vector3)(p - (Vector2)transform.position);
                transform.position = p;
                if (_placed) CinemachineCore.OnTargetObjectWarped(transform, delta);
                _roomCenter = center;
                _placed = true;
                return;
            }
            transform.position = p;
        }

        private void MoveConfiner(Vector2 roomCenter)
        {
            if (PlayArea.InCamp) return;
            if (_confiner == null)
            {
                _confiner = FindAnyObjectByType<CinemachineConfiner2D>();
                if (_confiner == null || _confiner.BoundingShape2D == null) return;
                _confinerHome = _confiner.BoundingShape2D.transform.position;
            }
            if (_confiner.BoundingShape2D == null) return;
            // The confiner was built around the arena at the origin; every room has the same size.
            _confiner.BoundingShape2D.transform.position = _confinerHome + (Vector3)(roomCenter - RunSimulation.DefaultBounds.center);
            _confiner.InvalidateBoundingShapeCache();
        }
    }
}
