namespace MyTimetable.Entities
{
    public class User
    {
        public required string Username { get; set;  }
        public required string Password { get; set; }
        public required bool IsEditor { get; set; }
        public required bool IsViewer { get; set; }
        public required DateTime CreatedAt { get; set; }
    }
}
