// In-process Brotli timing — same BrotliStream the server uses.
// Usage: dotnet script bench_brotli.cs <inputfile>
using System.IO.Compression;
using System.Diagnostics;

var path = args[0];
byte[] raw = File.ReadAllBytes(path);
Console.WriteLine($"Input: {raw.Length:N0} bytes\n");

foreach (var level in new[] { "Fastest", "Optimal", "SmallestSize" })
{
    var cl = level switch
    {
        "Fastest" => CompressionLevel.Fastest,
        "Optimal" => CompressionLevel.Optimal,
        _ => CompressionLevel.SmallestSize
    };

    // Warmup
    Compress(raw, cl);

    // Timed
    int iterations = 100;
    var sw = Stopwatch.StartNew();
    int size = 0;
    for (int i = 0; i < iterations; i++)
        size = Compress(raw, cl);
    sw.Stop();

    double ms = sw.Elapsed.TotalMilliseconds / iterations;
    Console.WriteLine($"{level,-14} {size,7:N0} bytes  {ms,8:F3} ms/op  ({raw.Length * 100 / size}× ratio)");
}

// Also gzip for reference
{
    var sw = Stopwatch.StartNew();
    int sizeGz = 0;
    for (int i = 0; i < 100; i++)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.SmallestSize))
            gz.Write(raw);
        sizeGz = ms.ToArray().Length;
    }
    sw.Stop();
    double ms = sw.Elapsed.TotalMilliseconds / 100;
    Console.WriteLine($"gzip-best      {sizeGz,7:N0} bytes  {ms,8:F3} ms/op  ({raw.Length * 100 / sizeGz}× ratio)");
}

static int Compress(byte[] data, CompressionLevel level)
{
    using var ms = new MemoryStream();
    using (var bs = new BrotliStream(ms, level, leaveOpen: false))
        bs.Write(data);
    return ms.ToArray().Length;
}
