using Microsoft.AspNetCore.Mvc;
using MyTimetable.Models;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public class AppController : Controller
    {
        private TsuInTimeFetcher fetcher = new();

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            List<DaySchedule> days = await fetcher.Get();
            return View(days);
        }

        [HttpGet("GetPartial")]
        public async Task<IActionResult> GetPartial(DateTime dateFrom, DateTime dateTo)
        {
            List<DaySchedule> days = await fetcher.Get(dateFrom, dateTo);
            return PartialView(days);
        }
    }
}
