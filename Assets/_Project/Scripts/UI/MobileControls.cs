using UnityEngine;

namespace Game.UI
{
    /// <summary>Shows the on-screen stick/buttons only on mobile (or when forced for testing in the editor).</summary>
    public sealed class MobileControls : MonoBehaviour
    {
        [SerializeField] private bool forceShowInEditor;

        private void Awake()
        {
            bool show = Application.isMobilePlatform || (Application.isEditor && forceShowInEditor);
            gameObject.SetActive(show);
        }
    }
}
