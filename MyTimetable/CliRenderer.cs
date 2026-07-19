using MyTimetable.Models;

namespace MyTimetable
{
    public sealed class CliRenderer
    {
        private static string RenderTable(CalendarSchedule schedule) {
            return "dummy";
        }

        public string Render(int slotCount, int scrollTarget, CalendarSchedule schedule)
        {
            return "{slotCount:" + slotCount + ",scrollTarget:" + scrollTarget + ",data:" + RenderTable(schedule) + "}";
        }
    }
}
