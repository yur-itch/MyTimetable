using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyTimetable.Entities;
using MyTimetable.Models;
using MyTimetable.Planning;

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
        private readonly PlanPage _planPage;

        public AppController(ScheduleData data, AppDbContext db, CacheRebuilder rebuilder, Planner planner, ScheduleBuilder scheduleBuilder, ChangesetApplier applier, TimeProvider time, IReadOnlyDictionary<string, IPlanningSelectorFactory> strategies, PlanPage planPage)
        {
            _data = data;
            _db = db;
            _rebuilder = rebuilder;
            _planner = planner;
            _builder = scheduleBuilder;
            _applier = applier;
            _time = time;
            _strategies = strategies;
            _planPage = planPage;
        }

        [HttpGet]
        [HttpGet("/")]
        public IActionResult Get()
        {
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
        public IActionResult GetOne(DateOnly date) => PartialFor(date);

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
        public async Task<IActionResult> Plan([FromQuery] Dictionary<string, int> titles, [FromQuery] List<string> strategies)
        {
            _planner.LoadQueue(titles);
            CalendarSchedule days = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            ScheduleChangeset schedule = new();
            List<IPlanningSelectorFactory> factories = new();
            foreach (var strategy in strategies)
            {
                if (!_strategies.TryGetValue(strategy, out var selectorFactory))
                {
                    return BadRequest($"strategy '{strategy}' does not exist");
                }
                factories.Add(selectorFactory);
            }
            PlanningSelectorFactory factory = new((dict, slots) => new CompositePlanningSelector(dict, slots, factories));
            List<DateOnly> dates = _planner.Plan(factory, days, schedule);
            await _applier.Apply(_db, schedule);
            await _db.SaveChangesAsync(); // контроллер владеет единицей работы запроса — он и коммитит
            await _rebuilder.Rebuild(_db, dates);
            await _planPage.Rebuild(_planner.Queue); // очередь изменилась (остатки) — обновляем форму
            Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
            var response = new { success = rendered, failure = _planner.Queue.Values.Sum() };
            return Ok(response);
        }

        [HttpGet("Plan")]
        public IActionResult Plan()
            => Content(_planPage.Html, "text/html; charset=utf-8");

        // Сброс для отладки: сносит всё, что наставил планировщик/пользователь (кастомные уроки и скрытия),
        // и сразу пересобирает кэш из очищенной БД. Дефолтные уроки не трогаем — это данные API.
        // Только в Development: в проде эндпоинта нет (404), чтобы не снести данные случайным запросом.
        [HttpPost("Reset")]
        public async Task<IActionResult> Reset([FromServices] IHostEnvironment env)
        {
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
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            List<Slot> conflicts = _planner.GetConflictingSlots(schedule).ToList();
            return Ok(conflicts);
        }

        [HttpPatch("ResolveConflicts")]
        public async Task<IActionResult> ResolveConflicts()
        {
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
    }
}
