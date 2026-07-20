using System.ComponentModel.DataAnnotations;

namespace MyTimetable.Entities
{
    public class Session
    {
        [Key]
        public required string Id { get; set; }
        public required string Username { get; set; }
        public required DateTime StartedAt { get; set; }
        public required DateTime ExpiresAt { get; set; }
    }
}
