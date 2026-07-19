using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var port = 9155;
var seed = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[i + 1]);
    if (args[i] == "--seed") seed = true;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://localhost:{port}");
var app = builder.Build();

app.MapGet("/", () => "MyTimetable Proxy running");

app.MapPost("/Cli/login", (LoginRequest req) =>
{
    if (req.Username == "admin" && req.Password == "admin")
        return Results.Text(MakeId(16));
    if (req.Username == "viewer" && req.Password == "viewer")
        return Results.Text(MakeId(16));
    return Results.Text("invalid credentials", statusCode: 401);
});

app.MapGet("/Cli", async (HttpContext context) =>
{
    var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault();
    if (string.IsNullOrEmpty(sessionId) || sessionId.Length != 32 || !sessionId.All(c => Uri.IsHexDigit(c)))
    {
        context.Response.StatusCode = 401;
        return;
    }

    var from = new DateOnly(2026, 9, 1);
    var to = new DateOnly(2027, 6, 30);
    var rng = seed ? new Random(42) : Random.Shared;
    var (slotCount, scrollTarget, days) = GenerateScheduleData(rng, from, to);
    string rendered = RenderSchedule(slotCount, scrollTarget, days);

    var payload = JsonSerializer.Serialize(new { slotCount, scrollTarget, data = rendered });
    var compressed = CompressGzip(payload);
    context.Response.ContentType = "application/json";
    context.Response.Headers.ContentEncoding = "gzip";
    context.Response.Headers.CacheControl = "public, max-age=3600";
    await context.Response.Body.WriteAsync(compressed);
});

app.MapPatch("/Planner", async (HttpRequest httpReq) =>
{
    var sessionId = httpReq.Headers["X-Session-Id"].FirstOrDefault();
    if (string.IsNullOrEmpty(sessionId) || sessionId.Length != 32 || !sessionId.All(c => Uri.IsHexDigit(c)))
        return Results.Json(new { error = "unauthorized" }, statusCode: 401);

    PlanRequest? req = null;
    try
    {
        using var reader = new StreamReader(httpReq.Body);
        var body = await reader.ReadToEndAsync();
        req = JsonSerializer.Deserialize<PlanRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = $"parse: {ex.Message}" }, statusCode: 400);
    }

    var rng = seed ? new Random(42) : Random.Shared;
    int totalRequested = req?.Titles?.Values.Sum() ?? 0;
    int failure = (int)(totalRequested * 0.15);
    int success = totalRequested - failure;
    return Results.Ok(new { success, failure });
});

app.Run();

static string MakeId(int byteLen) =>
    Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(byteLen));

static byte[] CompressGzip(string text)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    using var ms = new MemoryStream();
    using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
    {
        gzip.Write(bytes, 0, bytes.Length);
        gzip.Flush();
    }
    return ms.ToArray();
}

static string ShortName(string t)
{
    if (t.Contains("Физическая культура") || t.Contains("Элективные дисциплины")) return "Физра";
    if (t.Contains("Алгебра и геометрия")) return "Алгем";
    if (t.Contains("Математический анализ")) return "Матан";
    if (t.Contains("Основы программирования")) return "Прога";
    if (t.Contains("Дискретные структуры")) return "МКН2";
    if (t.Contains("Иностранный язык")) return "Английский";
    if (t.Contains("История России")) return "История";
    return t;
}

static (int, int, List<Dictionary<string, object>>) GenerateScheduleData(Random rng, DateOnly from, DateOnly to)
{
    var titles = new[] { "Математический анализ", "Алгебра и геометрия", "Основы программирования", "Дискретные структуры", "История России", "Иностранный язык" };
    var types = new[] { "LECTURE", "PRACTICE", "SEMINAR" };
    var rooms = new[] { "332", "228", "101", "Online", "" };
    var slotCount = 6;
    var days = new List<Dictionary<string, object>>();
    var today = DateOnly.FromDateTime(DateTime.Now);
    int scrollTarget = 0, dayIndex = 0;

    for (var d = from; d <= to; d = d.AddDays(1))
    {
        if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
        if (rng.NextDouble() > 0.6) continue;
        var cells = new string[slotCount];
        for (int s = 0; s < slotCount; s++) cells[s] = "-";
        int lessonCount = rng.Next(1, 5);
        var used = new HashSet<int>();
        for (int i = 0; i < lessonCount; i++)
        {
            int num; do { num = rng.Next(1, slotCount + 1); } while (!used.Add(num));
            var title = titles[rng.Next(titles.Length)];
            var type = types[rng.Next(types.Length)];
            var room = rooms[rng.Next(rooms.Length)];
            var sn = ShortName(title);
            cells[num - 1] = string.IsNullOrEmpty(room) ? $"{sn} {type[0]}" : $"{sn} {type[0]} [{room}]";
        }
        if (d == today) scrollTarget = dayIndex;
        days.Add(new Dictionary<string, object> { ["date"] = d.ToString("yyyy-MM-dd"), ["cells"] = cells });
        dayIndex++;
    }
    if (scrollTarget == 0 && dayIndex > 0) scrollTarget = dayIndex / 2;
    return (slotCount, scrollTarget, days);
}

static string RenderSchedule(int slotCount, int scrollTarget, List<Dictionary<string, object>> days)
{
    int[] cw = new int[slotCount + 1];
    for (int i = 0; i <= slotCount; i++) cw[i] = 10;
    foreach (var day in days)
    {
        string date = (string)day["date"];
        if (date.Length > cw[0]) cw[0] = date.Length;
        var cells = (string[])day["cells"];
        for (int s = 0; s < slotCount; s++)
            if (cells[s].Length > cw[s + 1]) cw[s + 1] = cells[s].Length;
    }
    var sb = new StringBuilder();
    string MakeBar(string l, string h, string j, string r)
    {
        var b = new StringBuilder(); b.Append(l);
        for (int i = 0; i <= slotCount; i++) { if (i > 0) b.Append(j); b.Append(new string(h[0], cw[i])); }
        b.Append(r); return b.ToString();
    }
    string MakeRow(string[] p)
    {
        var row = new StringBuilder(); row.Append("║ "); row.Append(p[0].PadRight(cw[0]));
        for (int i = 1; i <= slotCount; i++) { row.Append(" ║ "); var v = p[i] ?? "-"; row.Append(v.Length <= cw[i] ? v.PadRight(cw[i]) : v[..cw[i]]); }
        row.Append(" ║"); return row.ToString();
    }
    sb.AppendLine(MakeBar("╔", "═", "╦", "╗"));
    var hdr = new string[slotCount + 1]; hdr[0] = "Date";
    for (int s = 1; s <= slotCount; s++) hdr[s] = s.ToString();
    sb.AppendLine(MakeRow(hdr));
    sb.AppendLine(MakeBar("╠", "═", "╬", "╣"));
    foreach (var day in days)
    {
        var parts = new string[slotCount + 1]; parts[0] = (string)day["date"];
        var cells = (string[])day["cells"];
        for (int s = 0; s < slotCount; s++) parts[s + 1] = cells[s];
        sb.AppendLine(MakeRow(parts));
    }
    sb.Append(MakeBar("╚", "═", "╩", "╝"));
    return sb.ToString();
}

record LoginRequest(string Username, string Password);
record LoginResponse(string SessionId, string User, string Role);
record PlanRequest(Dictionary<string, int>? Titles, List<string>? Strategies);
