using MyTimetable.Caching;
using MyTimetable.Entities;
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
        public async Task<IActionResult> Get([FromHeader(Name = "X-Session-Id")] string? sessionId)
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
            Response.Headers.ContentEncoding = "gzip";
            return File(_data.CliViewResult, "application/octet-stream");
        }

        public record LoginRequest(string Username, string Password);

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (!await _auth.CanLogIn(_db, req.Username, req.Password)) {
                return Unauthorized("No user with this data");
            }
            string sessionID = _sessGen.Generate();
            await _auth.AddSessionFor(_db, sessionID, req.Username);
            await _db.SaveChangesAsync();

            return Content(sessionID, "text/plain");
        }

        public record RegisterRequest(string Username, string Password);

        [HttpPost("Register")]
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

            return Content(sessionID, "text/plain");
        }

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout([FromHeader(Name = "X-Session-Id")] string? sessionId)
        {
            if (sessionId != null)
            {
                Session? session = await _db.Sessions.FindAsync(sessionId);
                if (session != null)
                {
                    _db.Sessions.Remove(session);
                    await _db.SaveChangesAsync();
                }
            }
            return Ok();
        }
    }
}
