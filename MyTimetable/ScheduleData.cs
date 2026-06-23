using Microsoft.AspNetCore.Mvc;
using MyTimetable.Models;
using System.Collections;
using System.Collections.Concurrent;

namespace MyTimetable
{
    public class ScheduleData
    {
        private readonly Lock _lock = new();
        private List<DaySchedule> _data = new();
        private byte[] _brotliViewResult = { };
        public ConcurrentDictionary<DateOnly, string> PartialViewResult = new();

        public List<DaySchedule> Data
        {
            get { lock (_lock) return _data; }
            set { lock (_lock) _data = value; }
        }

        public byte[] ViewResult
        {
            get { lock (_lock) return _brotliViewResult; }
            set { lock (_lock) _brotliViewResult = value; }
        }
    }
}
