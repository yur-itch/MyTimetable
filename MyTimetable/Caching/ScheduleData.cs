using System.Collections.Concurrent;

namespace MyTimetable.Caching
{
    public sealed class ScheduleData
    {
        private bool _stateValid = false;
        private readonly Lock _lock = new();
        private byte[] _compressedViewResult = { };
        private byte[] _cliViewResult = { };
        public ConcurrentDictionary<DateOnly, string> PartialViewResult = new();

        public bool StateValid
        {
            get { lock (_lock) return _stateValid; }
            set { lock (_lock) _stateValid = value; }
        }

        public byte[] ViewResult
        {
            get { lock (_lock) return _compressedViewResult; }
            set { lock (_lock) _compressedViewResult = value; }
        }

        public byte[] CliViewResult
        {
            get { lock (_lock) return _cliViewResult; }
            set { lock (_lock) _cliViewResult = value; }
        }
    }
}
