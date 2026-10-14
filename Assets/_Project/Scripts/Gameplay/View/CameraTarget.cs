using Game.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// What the follow camera looks at: the local player. In the camp it is clamped so the view never shows past the
    /// camp's walls (the confiner). In a run the labyrinth is in one piece (phase 5): the camera follows the player down
    /// the corridors from room to room, kept inside the area map's outline; a jump (the next area, a teleport) cuts.
    /// </summary>
    public sealed class CameraTarget : MonoBehaviour
    {
        private Transform _player;
        private Camera _camera;
        private CinemachineConfiner2D _confiner;
        private Vector3 _confinerHome;
        private Vector2 _last;
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
            if (_camera == null)
            {
                _camera = Camera.main;
                // -overview <size> (screenshots): pull the camera far back to see the map in one piece.
                float overview = CommandLine.GetFloat("-overview", 0f);
                if (overview > 0f && _camera != null)
                {
                    var perfect = _camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
                    if (perfect != null) perfect.enabled = false;
                    var vcam = FindAnyObjectByType<CinemachineCamera>();
                    if (vcam != null) vcam.Lens.OrthographicSize = overview;
                }
            }
            Vector2 p = _player.position;
            var director = RunDirector.Instance;
            bool run = !PlayArea.InCamp && director != null && director.IsSpawned;
            // The black wall tops are one tile wide around the floor; the back wall is three tiles tall.
            var b = run ? director.Map.Extent : PlayArea.Bounds;
            if (_camera != null)
            {
                float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
                float xMin = b.xMin - 1f + halfW, xMax = b.xMax + 1f - halfW;
                float yMin = b.yMin - 1f + halfH, yMax = b.yMax + 3f - halfH;
                p.x = xMin <= xMax ? Mathf.Clamp(p.x, xMin, xMax) : b.center.x;
                p.y = yMin <= yMax ? Mathf.Clamp(p.y, yMin, yMax) : b.center.y;
            }
            SetConfiner(!run);

            // A jump (a new area, a teleport): cut instead of gliding across the map.
            if (!_placed || (p - _last).sqrMagnitude > 64f)
            {
                Vector3 delta = (Vector3)(p - (Vector2)transform.position);
                transform.position = p;
                if (_placed) CinemachineCore.OnTargetObjectWarped(transform, delta);
                _placed = true;
            }
            else transform.position = p;
            _last = p;
        }

        /// <summary>The camp keeps its confiner at home; a run follows the map without one.</summary>
        private void SetConfiner(bool on)
        {
            if (_confiner == null)
            {
                _confiner = FindAnyObjectByType<CinemachineConfiner2D>();
                if (_confiner == null || _confiner.BoundingShape2D == null) return;
                _confinerHome = _confiner.BoundingShape2D.transform.position;
            }
            if (_confiner.enabled == on) return;
            _confiner.enabled = on;
            if (on && _confiner.BoundingShape2D != null)
            {
                _confiner.BoundingShape2D.transform.position = _confinerHome;
                _confiner.InvalidateBoundingShapeCache();
            }
        }
    }
}
