using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds a Windows development player to Builds/Windows/ for multi-instance network tests.
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.PlayerBuild.BuildWindows
    /// </summary>
    public static class PlayerBuild
    {
        public const string WindowsPath = "Builds/Windows/LabyrinthOfTheDead.exe";

        [MenuItem("Tools/Game/Build Windows Player")]
        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = WindowsPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[PlayerBuild] {summary.result} in {summary.totalTime.TotalSeconds:0}s, {summary.totalErrors} error(s) -> {WindowsPath}");
            if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
