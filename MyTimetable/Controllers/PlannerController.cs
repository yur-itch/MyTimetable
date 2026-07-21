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
        private readonly PlanPage _planPage;
        private readonly AuthProvider _auth;

        public PlannerController(ScheduleData data, AppDbContext db, CacheRebuilder rebuilder, Planner planner,
            ScheduleBuilder scheduleBuilder, ChangesetApplier applier, TimeProvider time,
            IReadOnlyDictionary<string, IPlanningSelectorFactory> strategies, PlanPage planPage, AuthProvider auth)
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
            _auth = auth;
        }

        [HttpPatch]
        public async Task<IActionResult> Plan([FromQuery] Dictionary<string, int> titles, [FromQuery] List<string> strategies, [FromQuery] bool fromCli = false, [FromHeader(Name = "X-Session-Id")] string? sessionId = null)
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                if (!await _auth.IsEditor(_db, sessionId))
                    return Unauthorized("No editing rights for this page");
            }
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
            await _db.SaveChangesAsync();
            await _rebuilder.Rebuild(_db, dates);
            await _planPage.Rebuild(_planner.Queue);
            if (fromCli) {
                Response.Headers.Append("Content-Encoding", "br");
                return Ok(_data.CliViewResult);
            } else {
                Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
                var response = new { success = rendered, failure = _planner.Queue.Values.Sum() };
                return Ok(response);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Plan() {
            return Content(_planPage.Html, "text/html; charset=utf-8");
        }

        [HttpGet("Conflicts")]
        public async Task<IActionResult> Conflicts([FromQuery] bool fromCli = false, [FromHeader(Name = "X-Session-Id")] string? sessionId = null)
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                if (!await _auth.IsEditor(_db, sessionId))
                    return Unauthorized("No editing rights for this page");
            }
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            List<Slot> conflicts = _planner.GetConflictingSlots(schedule).ToList();
            return Ok(conflicts);
        }

        [HttpPatch("ResolveConflicts")]
        public async Task<IActionResult> ResolveConflicts([FromQuery] bool fromCli = false, [FromHeader(Name = "X-Session-Id")] string? sessionId = null)
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                if (!await _auth.IsEditor(_db, sessionId))
                    return Unauthorized("No editing rights for this page");
            }
            CalendarSchedule schedule = await _builder.LoadFromDb(_db, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _builder.YearEnd);
            ScheduleChangeset changeset = new();
            var dates = _planner.ResolveConflicts(schedule, changeset);
            await _applier.Apply(_db, changeset);
            await _db.SaveChangesAsync();
            await _rebuilder.Rebuild(_db, dates);
            await _planPage.Rebuild(_planner.Queue);
            if (fromCli) {
                Response.Headers.Append("Content-Encoding", "br");
                return Ok(_data.CliViewResult);
            } else {
                Dictionary<DateOnly, string> rendered = dates.ToDictionary(x => x, x => _data.PartialViewResult[x]);
                var response = new { success = rendered, failure = _planner.Queue.Values.Sum() };
                return Ok(response);
            }
        }
    }
}
