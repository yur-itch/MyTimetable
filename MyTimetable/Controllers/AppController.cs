using Microsoft.AspNetCore.Mvc;

namespace MyTimetable.Controllers
{
    [Route("[controller]")]
    public class AppController : Controller
    {
        [HttpGet]
        public IActionResult Get()
        {
            return View();
        }
    }
}
