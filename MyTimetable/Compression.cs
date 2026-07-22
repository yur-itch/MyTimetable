using System.IO.Compression;
using System.Text;

namespace MyTimetable
{
    public static class Compression
    {
        // UTF-8 string → brotli-сжатые байты (Content-Encoding: br).
        public static byte[] Brotli(string html, CompressionLevel level = CompressionLevel.Optimal)
        {
            return Brotli(Encoding.UTF8.GetBytes(html), level);
        }

        // Raw bytes → brotli (for binary payloads).
        public static byte[] Brotli(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
        {
            using var output = new MemoryStream();
            using (var brotli = new BrotliStream(output, level))
            {
                brotli.Write(data);
            }
            return output.ToArray();
        }
    }
}
