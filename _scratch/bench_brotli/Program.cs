using System.IO.Compression;
using System.Diagnostics;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: bench_brotli <inputfile>");
    return 1;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 1;
}

byte[] raw = File.ReadAllBytes(path);
Console.WriteLine($"Input: {raw.Length:N0} bytes\n");

foreach (var level in new[] { ("Fastest", CompressionLevel.Fastest), ("Optimal", CompressionLevel.Optimal), ("SmallestSize", CompressionLevel.SmallestSize) })
{
    // Warmup
    Compress(raw, level.Item2);

    int iterations = 100;
    var sw = Stopwatch.StartNew();
    int size = 0;
    for (int i = 0; i < iterations; i++)
        size = Compress(raw, level.Item2);
    sw.Stop();

    double ms = sw.Elapsed.TotalMilliseconds / iterations;
    double ratio = (double)raw.Length / size;
    Console.WriteLine($"{level.Item1,-14} {size,7:N0} bytes  {ms,8:F3} ms/op  ({ratio:F1}× ratio)");
}

// gzip
{
    var sw = Stopwatch.StartNew();
    int sizeGz = 0;
    for (int i = 0; i < 100; i++)
        sizeGz = GzipCompress(raw);
    sw.Stop();
    double ms = sw.Elapsed.TotalMilliseconds / 100;
    double ratio = (double)raw.Length / sizeGz;
    Console.WriteLine($"gzip-best      {sizeGz,7:N0} bytes  {ms,8:F3} ms/op  ({ratio:F1}× ratio)");
}

return 0;

static int Compress(byte[] data, CompressionLevel level)
{
    using var ms = new MemoryStream();
    using (var bs = new BrotliStream(ms, level, leaveOpen: false))
        bs.Write(data);
    return ms.ToArray().Length;
}

static int GzipCompress(byte[] data)
{
    using var ms = new MemoryStream();
    using (var gz = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: false))
        gz.Write(data);
    return ms.ToArray().Length;
}
