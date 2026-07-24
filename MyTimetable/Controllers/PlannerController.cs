using MyTimetable.Caching;
using MyTimetable.Planning;
using MyTimetable.Rendering;
using MyTimetable.Security;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public sealed class PlannerController : Controller
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

        public PlannerController(ScheduleData data, AppDbContext db, CacheRebuilder rebuilder, Planner planner,
            ScheduleBuilder scheduleBuilder, ChangesetApplier applier, TimeProvider time,
            IReadOnlyDictionary<string, IPlanningSelectorFactory> strategies,
            IReadOnlyDictionary<string, IPickerFactory> pickers,
            IReadOnlyDictionary<string, ISlotterFactory> slotters,
            PlanPage planPage, AuthProvider auth)
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
        }

        private string? SID => Request.Headers["X-Session-Id"].FirstOrDefault()
                              ?? Request.Cookies["mytimetable.session"];

        [HttpPatch]
        public async Task<IActionResult> Plan(
            [FromQuery] Dictionary<string, int> titles,
            [FromBody] List<SelectorSpec> specifications,
            [FromQuery] bool fromCli = false)
        {
            if (!await _auth.IsEditor(_db, SID))
                return Unauthorized(new { error = "No editing rights." });
            if (!await _auth.IsViewer(_db, SID))
                return Unauthorized(new { error = "No viewing rights." });
            if (specifications == null || specifications.Count == 0)
                return BadRequest(new { error = "No strategies specified." });
            CalendarSchedule days = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            ScheduleChangeset schedule = new();
            List<IPlanningSelectorFactory> factories = new();
            foreach (var spec in specifications)
            {
                if (!TryResolveSpec(spec, out var sf, out var error))
                    return BadRequest(error);
                factories.Add(sf);
            }
            PlanningSelectorFactory composite = new((dict, slots) => new CompositePlanningSelector(dict, slots, factories));
            PlanResult planResult = _planner.Plan(titles, composite, days, schedule);
            await _applier.Apply(_db, schedule);
            await _db.SaveChangesAsync();
            await _rebuilder.Rebuild(_db, planResult.Dates);
            await _planPage.Rebuild(planResult.RemainingQueue);
            if (fromCli)
            {
                Response.Headers.ContentEncoding = "gzip";
                return File(_data.CliViewResult, "application/octet-stream");
            }
            else
            {
                Dictionary<DateOnly, string> rendered = planResult.Dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
                var response = new { success = rendered, failure = planResult.RemainingQueue.Values.Sum(), queue = planResult.RemainingQueue };
                return Ok(response);
            }
        }

        private bool TryResolveSpec(SelectorSpec spec, out IPlanningSelectorFactory factory, out string? error)
        {
            switch (spec)
            {
                case SelectorSpec.Prebuilt p:
                    if (!_strategies.TryGetValue(p.Name, out var sf))
                    {
                        factory = null!; error = $"strategy '{p.Name}' does not exist"; return false;
                    }
                    factory = sf; error = null; return true;
                case SelectorSpec.Composed c:
                    if (!_pickers.TryGetValue(c.Picker, out var pf))
                    {
                        factory = null!; error = $"picker '{c.Picker}' does not exist"; return false;
                    }
                    if (!_slotters.TryGetValue(c.Slotter, out var slf))
                    {
                        factory = null!; error = $"slotter '{c.Slotter}' does not exist"; return false;
                    }
                    factory = new PlanningSelectorFactory(
                        (q, s) => new DualSelector(q, s, pf, slf));
                    error = null; return true;
                default:
                    factory = null!; error = "unknown strategy spec type"; return false;
            }
        }

        [HttpGet("Strategies")]
        public IActionResult Strategies()
        {
            var prebuilt = _strategies.Keys.OrderBy(k => k).ToList();
            var pickers = _pickers.Keys.OrderBy(k => k).ToList();
            var slotters = _slotters.Keys.OrderBy(k => k).ToList();
            // Binary: 3 blocks [2B count][2B len][UTF-8 name]...
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            WriteBlock(w, prebuilt);
            WriteBlock(w, pickers);
            WriteBlock(w, slotters);
            return File(ms.ToArray(), "application/octet-stream");
        }

        private static void WriteBlock(BinaryWriter w, List<string> items)
        {
            w.Write((ushort)items.Count);
            foreach (var item in items)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(item);
                w.Write((ushort)bytes.Length);
                w.Write(bytes);
            }
        }

        [HttpGet]
        public IActionResult Plan() => Content(_planPage.Html, "text/html; charset=utf-8");

        [HttpGet("Conflicts")]
        public async Task<IActionResult> Conflicts()
        {
            if (!await _auth.IsViewer(_db, SID))
                return Unauthorized(new { error = "No viewing rights." });
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            List<Slot> conflicts = _planner.GetConflictingSlots(schedule).ToList();
            // Binary: [2B count][10B date "yyyy-MM-dd"][1B number]...
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((ushort)conflicts.Count);
            foreach (var c in conflicts)
            {
                byte[] dateBytes = System.Text.Encoding.UTF8.GetBytes(c.Date.ToString("yyyy-MM-dd"));
                w.Write(dateBytes); // always 10 bytes
                w.Write((byte)c.Number);
            }
            return File(ms.ToArray(), "application/octet-stream");
        }

        [HttpPatch("ResolveConflicts")]
        public async Task<IActionResult> ResolveConflicts([FromQuery] bool fromCli = false)
        {
            if (!await _auth.IsEditor(_db, SID))
                return Unauthorized(new { error = "No editing rights." });
            if (!await _auth.IsViewer(_db, SID))
                return Unauthorized(new { error = "No viewing rights." });
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            ScheduleChangeset changeset = new();
            ResolveResult result = _planner.ResolveConflicts(schedule, changeset);
            await _applier.Apply(_db, changeset);
            await _db.SaveChangesAsync();
            await _rebuilder.Rebuild(_db, result.Dates);
            await _planPage.Rebuild(result.FreedQueue);
            if (fromCli)
            {
                Response.Headers.ContentEncoding = "gzip";
                return File(_data.CliViewResult, "application/octet-stream");
            }
            else
            {
                Dictionary<DateOnly, string> rendered = result.Dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
                var response = new { success = rendered, failure = result.FreedQueue.Values.Sum(), queue = result.FreedQueue };
                return Ok(response);
            }
        }

        [HttpPost("Reset")]
        public async Task<IActionResult> Reset([FromServices] IHostEnvironment env)
        {
            if (!await _auth.IsEditor(_db, SID))
                return Unauthorized(new { error = "No editing rights." });
            if (!env.IsDevelopment())
                return NotFound();
            int custom = await _db.CustomLessons.ExecuteDeleteAsync();
            int deactivations = await _db.Deactivations.ExecuteDeleteAsync();
            await _rebuilder.Rebuild(_db);
            return Ok(new { cleared = new { custom, deactivations } });
        }
    }
}
