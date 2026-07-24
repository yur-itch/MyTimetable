
namespace MyTimetable.Entities
{
    public class Session
    {
        [Key]
        public string Id { get; set; } = null!;
        public required string Username { get; set; }
        public required DateTime StartedAt { get; set; }
        public required DateTime ExpiresAt { get; set; }
    }
}
