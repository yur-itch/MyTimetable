using Microsoft.AspNetCore.Mvc;
using MyTimetable.Entities;
using MyTimetable.Models;
using MyTimetable.Planning;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public sealed class AppController : Controller
    {
        private AppDbContext _db;
        private ScheduleData _data;
        private CacheRebuilder _rebuilder;
        private Planner _planner;
        private ScheduleBuilder _builder;
        private ChangesetApplier _applier;
        private TimeProvider _time;

        public AppController(ScheduleData data, AppDbContext db, CacheRebuilder rebuilder, Planner planner, ScheduleBuilder scheduleBuilder, ChangesetApplier applier, TimeProvider time)
        {
            _data = data;
            _db = db;
            _rebuilder = rebuilder;
            _planner = planner;
            _builder = scheduleBuilder;
            _applier = applier;
            _time = time;
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
        public async Task<IActionResult> Plan([FromQuery] Dictionary<string, int> titles)
        {
            _planner.LoadQueue(titles);
            CalendarSchedule days = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            ScheduleChangeset schedule = new();
            IPlanningSelectorFactory selectorFactory = new PlanningSelectorFactory(
                (queue, slots) => new RoundRobinSelector(queue, slots));
            List<DateOnly> dates = _planner.Plan(selectorFactory, days, schedule);
            await _applier.Apply(_db, schedule);
            await _db.SaveChangesAsync(); // контроллер владеет единицей работы запроса — он и коммитит
            await _rebuilder.Rebuild(_db, dates);
            Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
            var response = new { success = rendered, failure = _planner.Queue.Values.Sum() };
            return Ok(response);
        }
    }
}
