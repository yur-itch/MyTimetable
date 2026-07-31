using System.Collections.Concurrent;

namespace MyTimetable.Caching
{
    public sealed class ScheduleData
    {
        private bool _stateValid = false;
        private readonly Lock _lock = new();
        private byte[] _compressedViewerViewResult = { };
        private byte[] _compressedEditorViewResult = { };
        private byte[] _cliViewResult = { };
        public ConcurrentDictionary<DateOnly, string> PartialViewResult = new();

        public bool StateValid
        {
            get { lock (_lock) return _stateValid; }
            set { lock (_lock) _stateValid = value; }
        }

        public byte[] ViewerViewResult
        {
            get { lock (_lock) return _compressedViewerViewResult; }
            set { lock (_lock) _compressedViewerViewResult = value; }
        }

        public byte[] EditorViewResult
        {
            get { lock (_lock) return _compressedEditorViewResult; }
            set { lock (_lock) _compressedEditorViewResult = value; }
        }

        public byte[] CliViewResult
        {
            get { lock (_lock) return _cliViewResult; }
            set { lock (_lock) _cliViewResult = value; }
        }
    }
}
