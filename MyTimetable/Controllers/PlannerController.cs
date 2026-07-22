using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using MyTimetable.Models;
using MyTimetable.Planning;
using System.Text.Json;
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
            [FromBody] List<StrategySpec> specifications,
            [FromQuery] bool fromCli = false)
        {
            if (!await _auth.IsEditor(_db, SID))
                return Unauthorized(new { error = "No editing rights." });
            if (!await _auth.IsViewer(_db, SID))
                return Unauthorized(new { error = "No viewing rights." });
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
            List<DateOnly> dates = _planner.Plan(factory, days, schedule);
            await _applier.Apply(_db, schedule);
            await _db.SaveChangesAsync();
            await _rebuilder.Rebuild(_db, dates);
            await _planPage.Rebuild(_planner.Queue);
            if (fromCli) {
                Response.Headers.ContentEncoding = "br";
                return File(_data.CliViewResult, "application/json; charset=utf-8");
            } else {
                Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
                var response = new { success = rendered, failure = _planner.Queue.Values.Sum() };
                return Ok(response);
            }
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

        [HttpGet]
        public IActionResult Plan() => Content(_planPage.Html, "text/html; charset=utf-8");

        [HttpGet("Conflicts")]
        public async Task<IActionResult> Conflicts()
        {
            if (!await _auth.IsViewer(_db, SID))
                return Unauthorized(new { error = "No viewing rights." });
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            List<Slot> conflicts = _planner.GetConflictingSlots(schedule).ToList();
            return Ok(conflicts);
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
            var dates = _planner.ResolveConflicts(schedule, changeset);
            await _applier.Apply(_db, changeset);
            await _db.SaveChangesAsync();
            await _rebuilder.Rebuild(_db, dates);
            await _planPage.Rebuild(_planner.Queue);
            if (fromCli) {
                Response.Headers.ContentEncoding = "br";
                return File(_data.CliViewResult, "application/json; charset=utf-8");
            } else {
                Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
                var response = new { success = rendered, failure = _planner.Queue.Values.Sum() };
                return Ok(response);
            }
        }
    }
}
