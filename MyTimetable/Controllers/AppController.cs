using Microsoft.AspNetCore.Mvc;
using MyTimetable.Models;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public class AppController : Controller
    {
        private ScheduleData _data;

        public AppController(ScheduleData data)
        {
            _data = data;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<DaySchedule> days = _data.Data;
            return View(days);
        }

        [HttpGet("GetPartial")]
        public IActionResult GetPartial(DateOnly dateFrom, DateOnly dateTo)
        {
            List<DaySchedule> days = _data.Data
                .Where(x => x.Date >= dateFrom && x.Date <= dateTo).ToList();
            return PartialView(days);
        }
    }
}
