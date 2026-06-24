using Microsoft.AspNetCore.Mvc;
using MyTimetable.Models;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public class AppController : Controller
    {
        private IServiceProvider _serviceProvider;
        private ScheduleData _data;
        private ScheduleBuilder _builder;
        private ViewRenderer _renderer;

        public AppController(ScheduleData data, IServiceProvider serviceProvider, ScheduleBuilder builder, ViewRenderer renderer)
        {
            _data = data;
            _serviceProvider = serviceProvider;
            _builder = builder;
            _renderer = renderer;
        }

        [HttpGet]
        [HttpGet("/")]
        public async Task<IActionResult> Get()
        {
            // Кэш валиден — отдаём заранее сжатую страницу. Иначе рендерим из БД на лету.
            if (_data.StateValid)
            {
                Response.Headers.ContentEncoding = "br";
                return File(_data.ViewResult, "text/html; charset=utf-8");
            }
            List<DaySchedule> schedule = await _builder.LoadFromDb();
            if (!schedule.Any())
            {
                return StatusCode(503, "Расписание временно недоступно");
            }
            string html = await _renderer.RenderViewToStringAsync("Get", schedule, ControllerContext);
            Response.Headers.ContentEncoding = "br";
            return File(Compression.Brotli(html), "text/html; charset=utf-8");
        }

        [HttpGet("GetOne")]
        public async Task<IActionResult> GetOne(DateOnly date)
        {
            if (_data.StateValid)
            {
                if (_data.PartialViewResult.TryGetValue(date, out string? cached))
                    return Content(cached, "text/html; charset=utf-8");
                return NotFound();
            }
            DaySchedule? day = (await _builder.LoadFromDb()).FirstOrDefault(d => d.Date == date);
            if (day == null)
            {
                return NotFound();
            }
            string html = await _renderer.RenderViewToStringAsync("GetOne", day, ControllerContext, true);
            return Content(html, "text/html; charset=utf-8");
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
            _data.StateValid = false; // кэш устарел — следующий Get/GetOne отрендерит из БД
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
            _data.StateValid = false; // кэш устарел — следующий Get/GetOne отрендерит из БД
            return Ok();
        }
    }
}
