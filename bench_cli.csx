// Quick benchmark: CliRenderer.Render + Brotli for full academic year
#r "nuget: System.Text.Json, 9.0.0"

using System.Diagnostics;
using System.IO.Compression;
using System.Text;

// Simulate 300 days × 6 slots with realistic cell data
int days = 300;
int slots = 6;
var lines = new List<string>();

// Header bar
lines.Add("╔══════════╦══════════╦══════════╦══════════╦══════════╦══════════╦══════════╗");
lines.Add("║ Date     ║ 1        ║ 2        ║ 3        ║ 4        ║ 5        ║ 6        ║");
lines.Add("╠══════════╬══════════╬══════════╬══════════╬══════════╬══════════╬══════════╣");

var rng = new Random(42);
var titles = new[] {"Матан Лекция [401]", "Алгем Практика [302]", "Прога Лаб [105]",
                     "Физра -", "МКН2 Лекция [201]", "Английский Практика [408]",
                     "История Лекция [301]", "-", "Прога Лекция [105]", "Матан Практика [401]"};

var start = new DateOnly(2025, 9, 1);
for (int d = 0; d < days; d++)
{
    var date = start.AddDays(d);
    var parts = new List<string> { date.ToString("yyyy-MM-dd") };
    for (int s = 0; s < slots; s++)
        parts.Add(titles[rng.Next(titles.Length)]);
    lines.Add("║ " + string.Join(" ║ ", parts.Select((p,i) => i==0 ? p.PadRight(10) : p.PadRight(10))) + " ║");
}
lines.Add("╚══════════╩══════════╩══════════╩══════════╩══════════╩══════════╩══════════╝");
string table = string.Join("\n", lines);

// Measure
int runs = 10;
long totalRenderMs = 0;
long totalCompressMs = 0;
long totalBytes = 0;

for (int i = 0; i < runs; i++)
{
    var sw = Stopwatch.StartNew();
    byte[] utf8 = Encoding.UTF8.GetBytes(table);
    using var output = new MemoryStream();
    using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
        brotli.Write(utf8);
    brotli.Flush(); // ensure all written
    sw.Stop();
    totalCompressMs += sw.ElapsedMilliseconds;
    if (i == 0) totalBytes = output.Length;
}

Console.WriteLine($"Days: {days}, Slots: {slots}, Total cells: {days * slots}");
Console.WriteLine($"Uncompressed: {Encoding.UTF8.GetByteCount(table) / 1024.0:F1} KB");
Console.WriteLine($"Compressed:   {totalBytes / 1024.0:F1} KB ({(double)totalBytes / Encoding.UTF8.GetByteCount(table) * 100:F1}%)");
Console.WriteLine($"Brotli level 11: avg {totalCompressMs / (double)runs:F1} ms over {runs} runs");
