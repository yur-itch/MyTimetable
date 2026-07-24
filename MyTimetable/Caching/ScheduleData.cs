using System.Collections;
using System.Collections.Concurrent;

namespace MyTimetable.Caching
{
    public sealed class ScheduleData
    {
        private bool _stateValid = false;
        private readonly Lock _lock = new();
        private byte[] _brotliViewResult = { };
        private byte[] _cliViewResult = { };
        public ConcurrentDictionary<DateOnly, string> PartialViewResult = new();

        public bool StateValid
        {
            get { lock (_lock) return _stateValid;  }
            set { lock (_lock) _stateValid = value; }
        }

        public byte[] ViewResult
        {
            get { lock (_lock) return _brotliViewResult; }
            set { lock (_lock) _brotliViewResult = value; }
        }

        public byte[] CliViewResult
        {
            get { lock (_lock) return _cliViewResult; }
            set { lock (_lock) _cliViewResult = value; }
        }
    }
}
