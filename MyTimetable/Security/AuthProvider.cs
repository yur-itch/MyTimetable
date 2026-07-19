using MyTimetable.Models;

namespace MyTimetable.Security
{
    public sealed class AuthProvider
    {
        private AppDbContext _db;

        public AuthProvider(AppDbContext db) {
            _db = db;
        }

        public bool IsViewer(string? session) {
            if (session == null) return false;
            return true;
        }

        public bool IsEditor(string? session)
        {
            if (session == null) return false;
            return true;
        }

        public bool CanLogIn(string username, string password) {
            return true;
        }
    }
}
