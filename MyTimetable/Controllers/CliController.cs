using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyTimetable.Entities;
using MyTimetable.Models;
using MyTimetable.Security;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public sealed class CliController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ScheduleData _data;
        private readonly CacheRebuilder _rebuilder;
        private readonly SessionIdProvider _sessGen;
        private readonly AuthProvider _auth;

        public CliController(ScheduleData data, AppDbContext db, CacheRebuilder rebuilder, SessionIdProvider sessionIdProvider, AuthProvider authProvider)
        {
            _data = data;
            _db = db;
            _rebuilder = rebuilder;
            _sessGen = sessionIdProvider;
            _auth = authProvider;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string? sessionId)
        {
            if (!await _auth.IsViewer(_db, sessionId))
            {
                return Unauthorized("No viewing rights for this page");
            }
            // Кэш всегда тёплый (прогрев на старте + пересборка на каждый Hide/Unhide).
            // Невалиден только если показывать нечего: пустая БД и API не засеял.
            if (!_data.StateValid)
            {
                return StatusCode(503, "Расписание временно недоступно");
            }
            Response.Headers.ContentEncoding = "br";
            return File(_data.CliViewResult, "application/json; charset=utf-8");
        }

        public record LoginRequest(string Username, string Password);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (!await _auth.CanLogIn(_db, req.Username, req.Password)) {
                return Unauthorized("No user with this data");
            }
            string sessionID = _sessGen.Generate();
            await _auth.AddSessionFor(_db, sessionID, req.Username);
            await _db.SaveChangesAsync();

            return Ok(sessionID);
        }

        public record RegisterRequest(string Username, string Password);

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username))
                return BadRequest("Username is required.");
            if (string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Password is required.");

            User? user = await _auth.Register(_db, req.Username, req.Password);
            if (user == null)
                return Conflict("Username already exists or password does not meet requirements.");

            string sessionID = _sessGen.Generate();
            await _auth.AddSessionFor(_db, sessionID, req.Username);
            await _db.SaveChangesAsync();

            return Ok(sessionID);
        }

        // [HttpPatch("Hide")]
        // public async Task<IActionResult> Hide(DateOnly date, int lessonNumber)
        // {
        //     DefaultLessonEntry? lesson = await _db.DefaultLessons.FindAsync(new object[] { date, lessonNumber });
        //     if (lesson == null)
        //     {
        //         return NotFound();
        //     }

        //     LessonDeactivation? deactivation = await _db.Deactivations.FindAsync(new object[] { date, lessonNumber });
        //     if (deactivation == null)
        //     {
        //         LessonDeactivation newDeactivation = new LessonDeactivation()
        //         {
        //             Date = date,
        //             Number = lessonNumber
        //         };
        //         await _db.Deactivations.AddAsync(newDeactivation);
        //         await _db.SaveChangesAsync();
        //         await _rebuilder.Rebuild(_db, [date]); // держим кэш всегда тёплым — пересобираем сразу
        //     }
        //     return PartialFor(date);
        // }

        // [HttpPatch("Unhide")]
        // public async Task<IActionResult> Unhide(DateOnly date, int lessonNumber)
        // {
        //     DefaultLessonEntry? lesson = await _db.DefaultLessons.FindAsync(new object[] { date, lessonNumber });
        //     if (lesson == null)
        //     {
        //         return NotFound();
        //     }

        //     LessonDeactivation? deactivation = await _db.Deactivations.FindAsync(new object[] { date, lessonNumber });
        //     if (deactivation != null)
        //     {
        //         _db.Deactivations.Remove(deactivation);
        //         await _db.SaveChangesAsync();
        //         await _rebuilder.Rebuild(_db, [date]); // держим кэш всегда тёплым — пересобираем сразу
        //     }
        //     return PartialFor(date);
        // }

        // // Сброс для отладки: сносит всё, что наставил планировщик/пользователь (кастомные уроки и скрытия),
        // // и сразу пересобирает кэш из очищенной БД. Дефолтные уроки не трогаем — это данные API.
        // // Только в Development: в проде эндпоинта нет (404), чтобы не снести данные случайным запросом.
        // [HttpPost("Reset")]
        // public async Task<IActionResult> Reset([FromServices] IHostEnvironment env)
        // {
        //     if (!env.IsDevelopment())
        //     {
        //         return NotFound();
        //     }
        //     int custom = await _db.CustomLessons.ExecuteDeleteAsync();
        //     int deactivations = await _db.Deactivations.ExecuteDeleteAsync();
        //     await _rebuilder.Rebuild(_db); // полная пересборка из очищенного состояния
        //     return Ok(new { cleared = new { custom, deactivations } });
        // }

    }
}
