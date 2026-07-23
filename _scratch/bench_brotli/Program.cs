using System.IO.Compression;
using System.Diagnostics;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: bench_brotli <inputfile>");
    return 1;
}

var path = args[0];
byte[] raw = File.ReadAllBytes(path);
Console.WriteLine($"Input: {raw.Length:N0} bytes\n");

Test("Fastest", CompressionLevel.Fastest, raw, 20);
Test("Optimal", CompressionLevel.Optimal, raw, 20);
Test("SmallestSize", CompressionLevel.SmallestSize, raw, 5);
TestGzip(raw, 20);

return 0;

static void Test(string name, CompressionLevel level, byte[] raw, int iters)
{
    Compress(raw, level); // warmup
    var sw = Stopwatch.StartNew();
    int size = 0;
    for (int i = 0; i < iters; i++) size = Compress(raw, level);
    sw.Stop();
    double ms = sw.Elapsed.TotalMilliseconds / iters;
    Console.WriteLine($"{name,-14} {size,7:N0} bytes  {ms,7:F1} ms/op  ({raw.Length / (double)size:F1}×)");
}

static void TestGzip(byte[] raw, int iters)
{
    GzipCompress(raw); // warmup
    var sw = Stopwatch.StartNew();
    int size = 0;
    for (int i = 0; i < iters; i++) size = GzipCompress(raw);
    sw.Stop();
    double ms = sw.Elapsed.TotalMilliseconds / iters;
    Console.WriteLine($"{"gzip-best",-14} {size,7:N0} bytes  {ms,7:F1} ms/op  ({raw.Length / (double)size:F1}×)");
}

static int Compress(byte[] d, CompressionLevel l) { using var m = new MemoryStream(); using (var b = new BrotliStream(m, l, true)) b.Write(d); return m.ToArray().Length; }
static int GzipCompress(byte[] d) { using var m = new MemoryStream(); using (var g = new GZipStream(m, CompressionLevel.SmallestSize, true)) g.Write(d); return m.ToArray().Length; }
