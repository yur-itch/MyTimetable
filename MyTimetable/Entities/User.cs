using System.ComponentModel.DataAnnotations;

namespace MyTimetable.Entities
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required bool IsEditor { get; set; }
        public required bool IsViewer { get; set; }
        public required DateTime CreatedAt { get; set; }
    }
}
