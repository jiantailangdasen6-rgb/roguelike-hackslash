using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Player builds.
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.PlayerBuild.BuildWindows   (Builds/Windows/)
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.PlayerBuild.BuildWebGL     (Builds/WebGL/)
    /// </summary>
    public static class PlayerBuild
    {
        public const string WindowsPath = "Builds/Windows/LabyrinthSurvivor.exe";
        public const string WebGLPath = "Builds/WebGL";

        [MenuItem("Tools/Game/Build Windows Player")]
        public static void BuildWindows()
        {
            WindowSettings();
            Build(WindowsPath, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        }

        /// <summary>
        /// The desktop window: opens at 1280x720 and can be resized and maximised (the UI scales, the pixel-perfect
        /// camera keeps the art crisp); Alt+Enter still switches to full screen.
        /// </summary>
        private static void WindowSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
        }

        public const string ReleasePath = "Builds/Release/LabyrinthSurvivor/LabyrinthSurvivor.exe";

        /// <summary>The copy handed to friends (Tools/PackageWindows.ps1 zips it): no development overlay or debugger.
        /// Build it right after the web build (no builder run in between) so both share the build stamp.</summary>
        [MenuItem("Tools/Game/Build Windows Release")]
        public static void BuildWindowsRelease()
        {
            if (System.IO.Directory.Exists("Builds/Release/LabyrinthSurvivor")) System.IO.Directory.Delete("Builds/Release/LabyrinthSurvivor", true);
            WindowSettings();
            Build(ReleasePath, BuildTarget.StandaloneWindows64, BuildOptions.None);
        }

        [MenuItem("Tools/Game/Build WebGL Player")]
        public static void BuildWebGL()
        {
            // Lessons from the card game's browser build: Gzip + decompression fallback (Brotli failed on GitHub Pages),
            // Medium stripping with link.xml, no splash screen.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            // Our own page (Assets/WebGLTemplates/Labyrinth): the game fills the window at any size. Unity's default
            // page is a fixed 1280x720 box that gets cut off (HUD, skill bar) on laptop-sized windows.
            PlayerSettings.WebGL.template = "PROJECT:Labyrinth";
            Build(WebGLPath, BuildTarget.WebGL, BuildOptions.None);
        }

        private static void Build(string path, BuildTarget target, BuildOptions options)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                options = options,
            });
            var summary = report.summary;
            Debug.Log($"[PlayerBuild] {target} {summary.result} in {summary.totalTime.TotalSeconds:0}s, {summary.totalErrors} error(s), {summary.totalSize / 1048576f:0.0} MB -> {path}");
            if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
