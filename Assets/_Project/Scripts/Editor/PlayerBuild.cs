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
        public static void BuildWindows() => Build(WindowsPath, BuildTarget.StandaloneWindows64, BuildOptions.Development);

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
            Build(WebGLPath, BuildTarget.WebGL, BuildOptions.None);

            // The page shows the readable title rather than the product id.
            string index = System.IO.Path.Combine(WebGLPath, "index.html");
            if (System.IO.File.Exists(index))
            {
                string html = System.IO.File.ReadAllText(index)
                    .Replace("<title>Unity Web Player | LabyrinthSurvivor</title>", "<title>Labyrinth Survivor</title>")
                    .Replace(">LabyrinthSurvivor</div>", ">Labyrinth Survivor</div>");
                System.IO.File.WriteAllText(index, html);
            }
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
