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
            Response.Headers.ContentEncoding = "br";
            return File(_data.ViewResult, "text/html; charset=utf-8");
        }

        [HttpGet("GetOne")]
        public IActionResult GetOne(DateOnly date)
        {
            if (_data.PartialViewResult.TryGetValue(date, out string? html))
                return Content(html, "text/html; charset=utf-8");
            return NotFound();
        }
    }
}
