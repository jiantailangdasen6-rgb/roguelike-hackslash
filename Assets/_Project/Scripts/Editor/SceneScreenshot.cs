using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Renders the main scene's camera (plus overlay UI) to a PNG without entering play mode,
    /// so progress can be checked without opening the editor.
    ///
    /// Batch mode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.SceneScreenshot.Capture
    /// Output: Logs/screenshot.png
    /// </summary>
    public static class SceneScreenshot
    {
        private const string ScenePath = "Assets/_Project/Scenes/Dungeon.unity";
        private const string OutputPath = "Logs/screenshot.png";
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Tools/Game/Capture Scene Screenshot")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camera = Camera.main;
            var target = new RenderTexture(Width, Height, 24);
            camera.targetTexture = target;

            // Overlay canvases are not drawn by Camera.Render, so draw them through the camera for the capture.
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();

            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, image.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);

            // Discard the temporary canvas changes.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[SceneScreenshot] Saved {Path.GetFullPath(OutputPath)}");
        }
    }
}
