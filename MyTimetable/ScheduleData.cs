using MyTimetable.Models;

namespace MyTimetable
{
    public class ScheduleData
    {
        private readonly Lock _lock = new();
        private List<DaySchedule> _data = new();

        public List<DaySchedule> Data
        {
            get { lock (_lock) return _data; }
            set { lock (_lock) _data = value; }
        }
    }
}
