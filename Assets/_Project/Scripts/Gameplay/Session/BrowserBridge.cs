using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Game.Gameplay
{
    /// <summary>
    /// Browser features the engine doesn't cover (Plugins/WebGL/Browser.jslib), with desktop fallbacks:
    /// clipboard, whether the page is in the background, and the invite link to the published page.
    /// </summary>
    public static class BrowserBridge
    {
        public const string PublishedUrl = "https://jiantailangdasen6-rgb.github.io/roguelike-web/";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int BrowserCopyText(string text);
        [DllImport("__Internal")] private static extern int BrowserPageHidden();
#endif

        public static void CopyText(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BrowserCopyText(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        /// <summary>True while the browser tab is in the background (the engine then runs very slowly).</summary>
        public static bool PageHidden
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return BrowserPageHidden() != 0;
#else
                return false;
#endif
            }
        }

        /// <summary>The page friends open to join: this page (browser) or the published one (desktop).</summary>
        public static string InviteLink(string code)
        {
            string page = PublishedUrl;
            if (GameSession.IsBrowser && !string.IsNullOrEmpty(Application.absoluteURL))
            {
                page = Application.absoluteURL;
                int cut = page.IndexOfAny(new[] { '?', '#' });
                if (cut >= 0) page = page.Substring(0, cut);
            }
            return $"{page}?join={code}";
        }
    }
}
