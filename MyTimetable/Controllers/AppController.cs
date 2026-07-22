using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyTimetable.Entities;
using MyTimetable.Models;
using MyTimetable.Planning;
using MyTimetable.Security;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public sealed class AppController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ScheduleData _data;
        private readonly CacheRebuilder _rebuilder;
        private readonly Planner _planner;
        private readonly ScheduleBuilder _builder;
        private readonly ChangesetApplier _applier;
        private readonly TimeProvider _time;
        private readonly IReadOnlyDictionary<string, IPlanningSelectorFactory> _strategies;
        private readonly IReadOnlyDictionary<string, IPickerFactory> _pickers;
        private readonly IReadOnlyDictionary<string, ISlotterFactory> _slotters;
        private readonly PlanPage _planPage;
        private readonly AuthProvider _auth;
        private readonly SessionIdProvider _sessGen;

        public AppController(ScheduleData data, AppDbContext db, CacheRebuilder rebuilder, Planner planner,
            ScheduleBuilder scheduleBuilder, ChangesetApplier applier, TimeProvider time,
            IReadOnlyDictionary<string, IPlanningSelectorFactory> strategies,
            IReadOnlyDictionary<string, IPickerFactory> pickers,
            IReadOnlyDictionary<string, ISlotterFactory> slotters,
            PlanPage planPage, AuthProvider auth, SessionIdProvider sessGen)
        {
            _data = data;
            _db = db;
            _rebuilder = rebuilder;
            _planner = planner;
            _builder = scheduleBuilder;
            _applier = applier;
            _time = time;
            _strategies = strategies;
            _pickers = pickers;
            _slotters = slotters;
            _planPage = planPage;
            _auth = auth;
            _sessGen = sessGen;
        }

        private string? SID => Request.Headers["X-Session-Id"].FirstOrDefault()
                              ?? Request.Cookies["mytimetable.session"];

        private async Task<bool> CanView() => await _auth.IsViewer(_db, SID);
        private async Task<bool> CanEdit() => await _auth.IsEditor(_db, SID) && await _auth.IsViewer(_db, SID);

        [HttpGet]
        [HttpGet("/")]
        public async Task<IActionResult> Get()
        {
            if (!await CanView()) return RedirectToAction("Login");
            // Кэш всегда тёплый (прогрев на старте + пересборка на каждый Hide/Unhide).
            // Невалиден только если показывать нечего: пустая БД и API не засеял.
            if (!_data.StateValid)
            {
                return StatusCode(503, "Расписание временно недоступно");
            }
            Response.Headers.ContentEncoding = "br";
            return File(_data.ViewResult, "text/html; charset=utf-8");
        }

        [HttpGet("GetOne")]
        public async Task<IActionResult> GetOne(DateOnly date)
        {
            if (!await CanView()) return Unauthorized();
            return PartialFor(date);
        }

        // Свежий партиал дня из тёплого кэша. Его же возвращают Hide/Unhide, чтобы клиент
        // вставил обновлённый день без отдельного GET (один round trip на переключение).
        private IActionResult PartialFor(DateOnly date)
        {
            if (!_data.StateValid)
            {
                return StatusCode(503, "Расписание временно недоступно");
            }
            if (_data.PartialViewResult.TryGetValue(date, out string? cached))
                return Content(cached, "text/html; charset=utf-8");
            return NotFound();
        }

        [HttpPatch("Hide")]
        public async Task<IActionResult> Hide(DateOnly date, int lessonNumber)
        {
            if (!await CanEdit()) return Unauthorized();
            DefaultLessonEntry? lesson = await _db.DefaultLessons.FindAsync(new object[] { date, lessonNumber });
            if (lesson == null)
            {
                return NotFound();
            }

            LessonDeactivation? deactivation = await _db.Deactivations.FindAsync(new object[] { date, lessonNumber });
            if (deactivation == null)
            {
                LessonDeactivation newDeactivation = new LessonDeactivation()
                {
                    Date = date,
                    Number = lessonNumber
                };
                await _db.Deactivations.AddAsync(newDeactivation);
                await _db.SaveChangesAsync();
                await _rebuilder.Rebuild(_db, [date]); // держим кэш всегда тёплым — пересобираем сразу
            }
            return PartialFor(date);
        }

        [HttpPatch("Unhide")]
        public async Task<IActionResult> Unhide(DateOnly date, int lessonNumber)
        {
            if (!await CanEdit()) return Unauthorized();
            DefaultLessonEntry? lesson = await _db.DefaultLessons.FindAsync(new object[] { date, lessonNumber });
            if (lesson == null)
            {
                return NotFound();
            }

            LessonDeactivation? deactivation = await _db.Deactivations.FindAsync(new object[] { date, lessonNumber });
            if (deactivation != null)
            {
                _db.Deactivations.Remove(deactivation);
                await _db.SaveChangesAsync();
                await _rebuilder.Rebuild(_db, [date]); // держим кэш всегда тёплым — пересобираем сразу
            }
            return PartialFor(date);
        }

        [HttpPatch("Plan")]
        public async Task<IActionResult> Plan(
            [FromQuery] Dictionary<string, int> titles,
            [FromBody] List<StrategySpec> specifications)
        {
            if (!await CanEdit()) return Unauthorized();
            _planner.LoadQueue(titles);
            CalendarSchedule days = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            ScheduleChangeset schedule = new();
            List<IPlanningSelectorFactory> factories = new();
            foreach (var spec in specifications)
            {
                IActionResult? error = ResolveSpec(spec, out var sf);
                if (error != null) return error;
                factories.Add(sf);
            }
            PlanningSelectorFactory composite = new((dict, slots) => new CompositePlanningSelector(dict, slots, factories));
            List<DateOnly> dates = _planner.Plan(composite, days, schedule);
            List<DateOnly> dates = _planner.Plan(factory, days, schedule);
            await _applier.Apply(_db, schedule);
            await _db.SaveChangesAsync(); // контроллер владеет единицей работы запроса — он и коммитит
            await _rebuilder.Rebuild(_db, dates);
            await _planPage.Rebuild(_planner.Queue); // очередь изменилась (остатки) — обновляем форму
            Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
            var response = new { success = rendered, failure = _planner.Queue.Values.Sum() };
            return Ok(response);
        }

        private IActionResult? ResolveSpec(StrategySpec spec, out IPlanningSelectorFactory factory)
        {
            switch (spec)
            {
                case StrategySpec.Prebuilt p:
                    if (!_strategies.TryGetValue(p.Name, out var sf))
                    {
                        factory = null!;
                        return BadRequest($"strategy '{p.Name}' does not exist");
                    }
                    factory = sf;
                    return null;
                case StrategySpec.Composed c:
                    if (!_pickers.TryGetValue(c.Picker, out var pf))
                    {
                        factory = null!;
                        return BadRequest($"picker '{c.Picker}' does not exist");
                    }
                    if (!_slotters.TryGetValue(c.Slotter, out var slf))
                    {
                        factory = null!;
                        return BadRequest($"slotter '{c.Slotter}' does not exist");
                    }
                    factory = new PlanningSelectorFactory(
                        (q, s) => new DualSelector(q, s, pf, slf));
                    return null;
                default:
                    factory = null!;
                    return BadRequest("unknown strategy spec type");
            }
        }

        [HttpGet("Plan")]
        public async Task<IActionResult> Plan()
        {
            if (!await CanEdit()) return RedirectToAction("Login");
            return Content(_planPage.Html, "text/html; charset=utf-8");
        }

        // Сброс для отладки: сносит всё, что наставил планировщик/пользователь (кастомные уроки и скрытия),
        // и сразу пересобирает кэш из очищенной БД. Дефолтные уроки не трогаем — это данные API.
        // Только в Development: в проде эндпоинта нет (404), чтобы не снести данные случайным запросом.
        [HttpPost("Reset")]
        public async Task<IActionResult> Reset([FromServices] IHostEnvironment env)
        {
            if (!await CanEdit()) return Unauthorized();
            if (!env.IsDevelopment())
            {
                return NotFound();
            }
            int custom = await _db.CustomLessons.ExecuteDeleteAsync();
            int deactivations = await _db.Deactivations.ExecuteDeleteAsync();
            await _rebuilder.Rebuild(_db); // полная пересборка из очищенного состояния
            return Ok(new { cleared = new { custom, deactivations } });
        }

        [HttpGet("Conflicts")]
        public async Task<IActionResult> Conflicts()
        {
            if (!await CanView()) return Unauthorized();
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            List<Slot> conflicts = _planner.GetConflictingSlots(schedule).ToList();
            return Ok(conflicts);
        }

        [HttpPatch("ResolveConflicts")]
        public async Task<IActionResult> ResolveConflicts()
        {
            if (!await CanEdit()) return Unauthorized();
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            ScheduleChangeset changeset = new();
            var dates = _planner.ResolveConflicts(schedule, changeset);
            await _applier.Apply(_db, changeset);
            await _db.SaveChangesAsync(); // контроллер владеет единицей работы запроса — он и коммитит
            await _rebuilder.Rebuild(_db, dates);
            await _planPage.Rebuild(_planner.Queue); // вытесненные предметы вернулись в очередь — обновляем форму
            Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
            // Снятие конфликтов не может «не влезть» (только убирает кастомные уроки), поэтому failure нет.
            return Ok(new { success = rendered });
        }

        [HttpGet("Login")]
        public IActionResult Login() => View();

        public record LoginRequest(string Username, string Password);

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Username and password required." });

            if (!await _auth.CanLogIn(_db, req.Username, req.Password))
                return Unauthorized(new { error = "Invalid credentials." });

            string sessionId = _sessGen.Generate();
            await _auth.AddSessionFor(_db, sessionId, req.Username);
            await _db.SaveChangesAsync();

            Response.Cookies.Append("mytimetable.session", sessionId, new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(7)
            });

            return Ok(new { sessionId, username = req.Username });
        }

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            string? sid = SID;
            if (sid != null)
            {
                Session? session = await _db.Sessions.FindAsync(sid);
                if (session != null)
                {
                    _db.Sessions.Remove(session);
                    await _db.SaveChangesAsync();
                }
            }
            Response.Cookies.Delete("mytimetable.session", new CookieOptions { Path = "/" });
            return Ok();
        }
    }
}
