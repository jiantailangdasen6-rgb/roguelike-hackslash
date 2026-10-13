using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Gameplay
{
    /// <summary>
    /// What this device has built at the hearth shows in the camp: an anvil and its coals, a shelf of remedies,
    /// the gods' little shrine, the soul urn; the campfire burns higher with every level of the hearth.
    /// </summary>
    public sealed class CampUpgradeView : MonoBehaviour
    {
        private static readonly (CampUpgrade Id, string Sheet, Vector2 At, Color Light, float Radius)[] Props =
        {
            (CampUpgrade.Forge, "camp_anvil", new Vector2(-3.6f, -6.2f), new Color(1f, 0.5f, 0.2f), 3.2f),
            (CampUpgrade.Apothecary, "camp_shelf", new Vector2(7.1f, 3.3f), new Color(0.6f, 1f, 0.7f), 2f),
            (CampUpgrade.Altar, "camp_shrine", new Vector2(-4.4f, 5.2f), new Color(1f, 0.85f, 0.55f), 2.6f),
            (CampUpgrade.Urn, "camp_urn", new Vector2(-6.3f, 0.9f), new Color(0.45f, 0.8f, 1f), 2.4f),
        };

        private readonly GameObject[] _built = new GameObject[Props.Length];
        private Light2D _fire;
        private float _fireRadius = -1f;
        private float _fireIntensity;

        private void Start()
        {
            var fire = GameObject.Find("FireLight");
            if (fire != null) _fire = fire.GetComponent<Light2D>();
            if (_fire != null)
            {
                _fireRadius = _fire.pointLightOuterRadius;
                _fireIntensity = _fire.intensity;
            }
            ProfileStore.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy() => ProfileStore.Changed -= Refresh;

        private void Refresh()
        {
            var profile = ProfileStore.Current;
            for (int i = 0; i < Props.Length; i++)
            {
                var (id, sheet, at, light, radius) = Props[i];
                bool built = profile.CampLevel(id) > 0;
                if (built == (_built[i] != null)) continue;
                if (!built)
                {
                    Destroy(_built[i]);
                    _built[i] = null;
                    continue;
                }
                var go = new GameObject(sheet);
                go.transform.SetParent(transform, false);
                go.transform.position = at;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = SpriteSheet.Load(sheet, quiet: true)?.First;
                r.sortingOrder = EnemyView.SortOrder(at.y);
                go.AddComponent<SpriteLoop>().Configure(sheet, 6f);
                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.6f);
                var l = lightGo.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = light;
                l.intensity = 0.9f;
                l.pointLightOuterRadius = radius;
                l.pointLightInnerRadius = 0.2f;
                _built[i] = go;
            }
            // The hearth: the fire reaches further and burns brighter.
            if (_fire != null && _fireRadius > 0f)
            {
                int hearth = profile.CampLevel(CampUpgrade.Hearth);
                _fire.pointLightOuterRadius = _fireRadius * (1f + 0.12f * hearth);
                _fire.intensity = _fireIntensity * (1f + 0.1f * hearth);
                var flames = GameObject.Find("camp_fire");
                if (flames != null) flames.transform.localScale = Vector3.one * (1f + 0.08f * hearth);
            }
        }
    }
}
