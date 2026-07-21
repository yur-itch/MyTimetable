using System.IO.Compression;
using System.Text;

namespace MyTimetable
{
    public static class Compression
    {
        // UTF-8 HTML → brotli-сжатые байты (Content-Encoding: br).
        public static byte[] Brotli(string html, CompressionLevel level = CompressionLevel.Optimal)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            using var output = new MemoryStream();
            using (var brotli = new BrotliStream(output, level))
            {
                brotli.Write(bytes);
            }
            return output.ToArray();
        }
    }
}
