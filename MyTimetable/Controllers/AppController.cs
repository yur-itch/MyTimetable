using MyTimetable.Caching;
using MyTimetable.Entities;
using MyTimetable.Rendering;
using MyTimetable.Security;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public sealed class AppController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ScheduleData _data;
        private readonly CacheRebuilder _rebuilder;
        private readonly PlanPage _planPage;
        private readonly AuthProvider _auth;
        private readonly SessionIdProvider _sessGen;

        public AppController(ScheduleData data, AppDbContext db, CacheRebuilder rebuilder,
            PlanPage planPage, AuthProvider auth, SessionIdProvider sessGen)
        {
            _data = data;
            _db = db;
            _rebuilder = rebuilder;
            _planPage = planPage;
            _auth = auth;
            _sessGen = sessGen;
        }

        private string? SID => Request.Headers["X-Session-Id"].FirstOrDefault()
                              ?? Request.Cookies["mytimetable.session"];

        private async Task<bool> CanView() => await _auth.IsViewer(_db, SID);
        private async Task<bool> CanEdit() => await _auth.IsEditor(_db, SID);

        [HttpGet]
        [HttpGet("/")]
        public async Task<IActionResult> Get()
        {
            if (!await CanView()) return RedirectToAction("Login");
            if (!_data.StateValid)
            {
                return StatusCode(503, "Расписание временно недоступно");
            }
            bool canEdit = await CanEdit();
            Response.Headers["X-UI-Mode"] = canEdit ? "editor" : "viewer";
            Response.Headers.ContentEncoding = "gzip";
            return File(canEdit ? _data.EditorViewResult : _data.ViewerViewResult,
                "text/html; charset=utf-8");
        }

        [HttpGet("GetOne")]
        public async Task<IActionResult> GetOne(DateOnly date)
        {
            if (!await CanView()) return Unauthorized();
            return PartialFor(date);
        }

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
                await _rebuilder.Rebuild(_db, [date]);
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
                await _rebuilder.Rebuild(_db, [date]);
            }
            return PartialFor(date);
        }

        [HttpGet("Plan")]
        public async Task<IActionResult> Plan()
        {
            if (!await CanEdit()) return RedirectToAction("Login");
            Response.Headers["X-UI-Mode"] = "editor";
            return Content(_planPage.Html, "text/html; charset=utf-8");
        }

        [HttpGet("Login")]
        public IActionResult Login() => View();

        [HttpGet("Register")]
        public IActionResult Register() => View();

        public record LoginRequest(string Username, string Password);
        public record RegisterRequest(string Username, string Password);

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
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(7)
            });

            // Browser authentication uses the HttpOnly cookie. The session ID remains
            // available from /Cli/Login for non-browser clients.
            return Ok(new { username = req.Username, canEdit = await _auth.IsEditor(_db, sessionId) });
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Username and password required." });

            var user = await _auth.Register(_db, req.Username, req.Password, isViewer: true, isEditor: false);
            if (user == null)
                return Conflict(new { error = "Username already exists or password does not meet requirements." });

            // Log in automatically after registration
            string sessionId = _sessGen.Generate();
            await _auth.AddSessionFor(_db, sessionId, req.Username);
            await _db.SaveChangesAsync();

            Response.Cookies.Append("mytimetable.session", sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(7)
            });

            // Browser authentication uses the HttpOnly cookie. The session ID remains
            // available from /Cli/Register for non-browser clients.
            return Ok(new { username = req.Username, canEdit = await _auth.IsEditor(_db, sessionId) });
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
