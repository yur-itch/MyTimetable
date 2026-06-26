namespace MyTimetable.Entities
{
    public class LessonEntry
    {
        public required DateOnly Date { get; set; }
        public required int LessonNumber { get; set; }
        public required string Title { get; set; }
        public required string LessonType { get; set; }
    }

    public class DefaultLessonEntry : LessonEntry
    {
        public required string Professor { get; set; }
        public required string Room { get; set; }
    }
    public class CustomLessonEntry : LessonEntry { }
}