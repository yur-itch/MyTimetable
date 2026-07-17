// MyTimetable.Proxy — заглушка для CLI-разработки.
// Притворяется бэком: отдаёт мусорные данные на эндпоинтах, которых ещё нет.
// Запуск: dotnet run --project MyTimetable.Proxy [--port 9155]
// Флаг --seed генерирует детерминированные данные (для тестов).

using System.Collections.Concurrent;
using Microsoft.AspNetCore.WebUtilities;

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

// Plan state
var planQueue = new Dictionary<string, int>();

app.MapGet("/", () => "MyTimetable Proxy running");

// ── Auth ──────────────────────────────────────────────────────────
app.MapPost("/api/login", (LoginRequest req) =>
{
    if (req.Username == "admin" && req.Password == "admin")
        return Results.Ok(new LoginResponse("sess_" + MakeId(16), "admin", "Admin"));
    if (req.Username == "viewer" && req.Password == "viewer")
        return Results.Ok(new LoginResponse("sess_" + MakeId(16), "viewer", "Viewer"));
    return Results.Json(new { error = "invalid credentials" }, statusCode: 401);
});

app.MapGet("/api/check", (string session_id) =>
{
    if (string.IsNullOrEmpty(session_id) || !session_id.StartsWith("sess_"))
        return Results.Json(new { error = "invalid or expired session" }, statusCode: 401);
    return Results.Ok(new { user = "admin", role = "Admin" });
});

app.MapPost("/api/logout", (LogoutRequest req) =>
{
    return Results.Ok(new { ok = true });
});

// ── Plan ──────────────────────────────────────────────────────────
app.MapPatch("/App/Plan", async (HttpRequest req) =>
{
    // Parse query string manually since PATCH body parsing is inconsistent
    var queryString = req.QueryString.ToString();
    if (string.IsNullOrEmpty(queryString) || !queryString.StartsWith("?"))
        return Results.Json(new { error = "missing query" }, statusCode: 400);

    var parsed = QueryHelpers.ParseQuery(queryString.TrimStart('?'));

    // Auth from query param
    var sid = parsed.ContainsKey("session_id") ? parsed["session_id"].FirstOrDefault() : null;
    var authErr = CheckAuth(sid);
    if (authErr is not null) return authErr;

    var strategies = parsed.ContainsKey("strategies") ? parsed["strategies"].ToList() : new List<string>();

    var titles = new Dictionary<string, int>();
    foreach (var kv in parsed)
    {
        if (kv.Key.StartsWith("titles[") && kv.Key.EndsWith("]"))
        {
            var name = kv.Key.Substring(7, kv.Key.Length - 8);
            if (int.TryParse(kv.Value.FirstOrDefault(), out var count))
                titles[name] = count;
        }
    }

    lock (planQueue)
    {
        planQueue.Clear();
        foreach (var t in titles)
            planQueue[t.Key] = t.Value;
    }

    var rng = seed ? new Random(42) : Random.Shared;
    int totalRequested = titles.Values.Sum();
    int failure = (int)(totalRequested * 0.15);
    int placed = totalRequested - failure;

    // Build fake success result
    var successDict = new Dictionary<string, object>();
    var fakeDay = new DateOnly(2026, 7, 13);
    foreach (var t in titles)
    {
        var dayStr = fakeDay.AddDays(rng.Next(0, 5)).ToString("yyyy-MM-dd");
        if (!successDict.ContainsKey(dayStr))
            successDict[dayStr] = new List<object>();

        // Simplified structure
    }

    return Results.Ok(new
    {
        success = new { },
        failure
    });
});

app.MapPost("/App/Reset", () =>
{
    lock (planQueue) planQueue.Clear();
    return Results.Ok(new { cleared = new { custom = 5, deactivations = 3 } });
});

// ── Schedule ──────────────────────────────────────────────────────
var sessionRequestCount = new ConcurrentDictionary<string, int>();

app.MapGet("/api/schedule", (string? session_id, DateOnly? dateFrom, DateOnly? dateTo) =>
{
    var auth = CheckAuth(session_id);
    if (auth is not null) return auth;
    
    if (session_id is not null)
    {
        int count = sessionRequestCount.AddOrUpdate(session_id, 1, (_, c) => c + 1);
        if (count > 1)
            return Results.Json(new { error = "session expired" }, statusCode: 401);
    }

    var from = dateFrom ?? new DateOnly(2026, 9, 1);
    var to = dateTo ?? new DateOnly(2027, 6, 30);

    var rng = seed ? new Random(42) : Random.Shared;
    var titles = new[] { "Математический анализ", "Алгебра и геометрия", "Основы программирования", "Дискретные структуры", "История России", "Иностранный язык" };
    var types = new[] { "LECTURE", "PRACTICE", "SEMINAR" };
    var profs = new[] { "Иванов", "Петрова", "Сидоров", "Кузнецов", "" };
    var rooms = new[] { "332", "228", "101", "Online", "" };

    var days = new List<object>();
    for (var d = from; d <= to; d = d.AddDays(1))
    {
        if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
            continue;

        if (rng.NextDouble() > 0.6)
            continue;

        var slots = new List<object>();
        int lessonCount = rng.Next(1, 5);
        var used = new HashSet<int>();
        for (int i = 0; i < lessonCount; i++)
        {
            int num;
            do { num = rng.Next(1, 7); } while (!used.Add(num));

            var title = titles[rng.Next(titles.Length)];
            var type = types[rng.Next(types.Length)];
            var prof = profs[rng.Next(profs.Length)];
            slots.Add(new
            {
                number = num,
                @default = new
                {
                    title,
                    lesson_type = type,
                    professor = string.IsNullOrEmpty(prof) ? null : new { name = prof, is_shown = true },
                    room = rooms[rng.Next(rooms.Length)]
                },
                custom = (object?)null,
                hidden = false
            });
        }

        days.Add(new { date = d.ToString("yyyy-MM-dd"), slots });
    }

    return Results.Ok(new { days });
});

app.Run();

static string MakeId(int len)
{
    const string chars = "abcdef0123456789";
    return string.Create(len, (object?)null, (span, _) =>
    {
        for (int i = 0; i < span.Length; i++)
            span[i] = chars[Random.Shared.Next(chars.Length)];
    });
}

static IResult? CheckAuth(string? sessionId)
{
    if (string.IsNullOrEmpty(sessionId) || !sessionId.StartsWith("sess_"))
        return Results.Json(new { error = "unauthorized" }, statusCode: 401);
    return null;
}

record LoginRequest(string Username, string Password);
record LoginResponse(string SessionId, string User, string Role);
record LogoutRequest(string SessionId);
