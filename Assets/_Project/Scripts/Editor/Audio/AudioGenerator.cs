using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Audio
{
    /// <summary>
    /// Renders all sound effects and music to WAV files in Resources/Audio (imported as compressed clips).
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Game.EditorTools.Audio.AudioGenerator.Generate
    /// </summary>
    public static class AudioGenerator
    {
        public const string Dir = "Assets/_Project/Resources/Audio";

        [MenuItem("Tools/Game/Generate Audio")]
        public static void Generate()
        {
            Directory.CreateDirectory(Dir);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var (name, samples) in SfxDesign.All())
            {
                string path = $"{Dir}/sfx_{name}.wav";
                Dsp.WriteWav(path, samples, 1, SfxDesign.Rate);
                Import(path, music: false);
            }
            Debug.Log($"[AudioGenerator] SFX done in {watch.Elapsed.TotalSeconds:0.0}s");

            foreach (var (name, song) in Compositions.All())
            {
                string path = $"{Dir}/{name}.wav";
                Dsp.WriteWav(path, MusicRenderer.Render(song), 2, MusicRenderer.Rate);
                Import(path, music: true);
                Debug.Log($"[AudioGenerator] {name}: {song.Seconds:0.0}s rendered ({watch.Elapsed.TotalSeconds:0.0}s elapsed)");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[AudioGenerator] Done");
        }

        private static void Import(string path, bool music)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.55f : 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
