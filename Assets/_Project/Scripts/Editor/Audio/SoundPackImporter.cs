using UnityEditor;
using UnityEngine;

namespace Game.EditorTools.Audio
{
    /// <summary>
    /// Source recordings in Audio/Pack are only read by <see cref="SoundPack"/> (they never ship): import them as
    /// plain PCM so the editor can read their samples.
    /// </summary>
    public sealed class SoundPackImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SoundPack.SourceDir)) return;
            var importer = (AudioImporter)assetImporter;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
