using System.IO;
using System.Threading;

namespace Game.EditorTools.Art
{
    public static class ArtIO
    {
        /// <summary>Writes a file, retrying briefly when Windows still holds it (antivirus scan, import worker).</summary>
        public static void WriteBytes(string path, byte[] bytes)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.WriteAllBytes(path, bytes);
                    return;
                }
                catch (IOException) when (attempt < 8)
                {
                    Thread.Sleep(250);
                }
            }
        }
    }
}
