using Microsoft.AspNetCore.Mvc;
using MyTimetable.Models;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public class AppController : Controller
    {
        private IServiceProvider _serviceProvider;
        private ScheduleData _data;
        private CacheRebuilder _rebuilder;

        public AppController(ScheduleData data, IServiceProvider serviceProvider, CacheRebuilder rebuilder)
        {
            _data = data;
            _serviceProvider = serviceProvider;
            _rebuilder = rebuilder;
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
        public IActionResult GetOne(DateOnly date)
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
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Lesson? lesson = await db.Lessons.FindAsync(new object[] { date, lessonNumber, false });
            if (lesson == null)
            {
                return NotFound();
            }

            LessonDeactivation? deactivation = await db.Deactivations.FindAsync(new object[] { date, lessonNumber });
            if (deactivation != null)
            {
                return Ok("Урок уже скрыт");
            }

            LessonDeactivation newDeactivation = new();
            newDeactivation.LessonNumber = lessonNumber;
            newDeactivation.Date = date;
            await db.Deactivations.AddAsync(newDeactivation);

            await db.SaveChangesAsync();
            await _rebuilder.Rebuild(); // держим кэш всегда тёплым — пересобираем сразу
            return Ok();
        }

        [HttpPatch("Unhide")]
        public async Task<IActionResult> Unhide(DateOnly date, int lessonNumber)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Lesson? lesson = await db.Lessons.FindAsync(new object[] { date, lessonNumber, false });
            if (lesson == null)
            {
                return NotFound();
            }

            LessonDeactivation? deactivation = await db.Deactivations.FindAsync(new object[] { date, lessonNumber });
            if (deactivation == null)
            {
                return Ok("Урок уже показан");
            }

            db.Deactivations.Remove(deactivation);

            await db.SaveChangesAsync();
            await _rebuilder.Rebuild(); // держим кэш всегда тёплым — пересобираем сразу
            return Ok();
        }
    }
}
