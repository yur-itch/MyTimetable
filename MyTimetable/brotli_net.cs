// File-based .NET 10 app: compress with the SAME code path the repo ships.
//   Compression.cs -> BrotliStream(output, CompressionLevel.Optimal)
//
// Two modes:
//   1) stdin  : `dotnet run brotli_net.cs -- [optimal|smallest|fastest|none]`
//               -> prints compressed byte length of stdin at that level.
//   2) batch  : `dotnet run brotli_net.cs -- --dir <folder>`
//               -> for every *.html in <folder> (sorted), prints one TSV line:
//                  name <TAB> optimal <TAB> smallest <TAB> fastest <TAB> raw
//               where `optimal` is exactly what the repo emits today.
using System.IO.Compression;

static int Size(byte[] bytes, CompressionLevel level)
{
    using var output = new MemoryStream();
    using (var brotli = new BrotliStream(output, level))
        brotli.Write(bytes);
    // ToArray() works even after the stream is closed (same as repo's Compression.Brotli).
    return output.ToArray().Length;
}

if (args.Length >= 2 && args[0] == "--dir")
{
    var dir = args[1];
    foreach (var path in Directory.GetFiles(dir, "*.html").OrderBy(p => p))
    {
        byte[] bytes = File.ReadAllBytes(path);
        string name = Path.GetFileNameWithoutExtension(path);
        Console.Out.WriteLine(
            $"{name}\t{Size(bytes, CompressionLevel.Optimal)}\t" +
            $"{Size(bytes, CompressionLevel.SmallestSize)}\t" +
            $"{Size(bytes, CompressionLevel.Fastest)}\t{bytes.Length}");
    }
    return;
}

var lvl = (args.Length > 0 ? args[0] : "optimal").ToLowerInvariant() switch
{
    "smallest" => CompressionLevel.SmallestSize,
    "fastest"  => CompressionLevel.Fastest,
    "none"     => CompressionLevel.NoCompression,
    _          => CompressionLevel.Optimal,
};

using var stdin = Console.OpenStandardInput();
using var raw = new MemoryStream();
stdin.CopyTo(raw);
Console.Out.Write(Size(raw.ToArray(), lvl));
