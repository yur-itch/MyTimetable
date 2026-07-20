using System.Diagnostics;
using System.IO.Compression;
using System.Text;

// Build: dotnet-script bench_brotli.cs
// Or compile with csc, but easier to just run via dotnet script

int days = 300, slots = 6;
var rng = new Random(42);
var titles = new[] {"Матан Лекция [401]", "Алгем Практика [302]", "Прога Лаб [105]",
                     "Физра -", "МКН2 Лекция [201]", "Английский Практика [408]",
                     "История Лекция [301]", "-", "Прога Лекция [105]", "Матан Практика [401]"};

var sb = new StringBuilder();
sb.AppendLine("╔══════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╗");
sb.AppendLine("║ Date     ║ 1        ║ 2        ║ 3        ║ 4        ║ 5        ║ 6        ║");
sb.AppendLine("╠══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╣");

var start = new DateOnly(2025, 9, 1);
for (int d = 0; d < days; d++)
{
    var date = start.AddDays(d);
    sb.Append("║ ").Append(date.ToString("yyyy-MM-dd").PadRight(10));
    for (int s = 0; s < slots; s++)
        sb.Append(" ║ ").Append(titles[rng.Next(titles.Length)].PadRight(10));
    sb.AppendLine(" ║");
}
sb.AppendLine("╚══════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╝");
string table = sb.ToString();
byte[] data = Encoding.UTF8.GetBytes(table);
Console.WriteLine($"Uncompressed: {data.Length / 1024.0:F1} KB ({days * slots} cells)");

Bench("Fastest",     CompressionLevel.Fastest,     data);
Bench("Optimal",     CompressionLevel.Optimal,     data);
Bench("SmallestSize",CompressionLevel.SmallestSize,data);

static void Bench(string label, CompressionLevel level, byte[] data)
{
    int runs = 20;
    long total = 0;
    long min = long.MaxValue, max = 0;
    int outLen = 0;
    for (int i = 0; i < runs; i++)
    {
        var output = new MemoryStream();
        var sw = Stopwatch.StartNew();
        using (var bs = new BrotliStream(output, level, leaveOpen: false))
            bs.Write(data);
        sw.Stop();
        if (i == 0) outLen = (int)output.Length;
        long ms = sw.ElapsedMilliseconds;
        total += ms;
        if (ms < min) min = ms;
        if (ms > max) max = ms;
    }
    Console.WriteLine($"{label,-14}: {outLen/1024.0,5:F1} KB ({outLen*100.0/data.Length,4:F1}%), " +
                      $"avg {total/(double)runs,5:F1} ms (min {min}, max {max})");
}
