namespace MyTimetable.TsuInTime
{
    // Wire shapes for the TSU intime API response (https://intime.tsu.ru/api/web/v1/schedule/group).
    // These mirror the JSON exactly; TsuInTimeFetcher normalizes them into the domain model (MyTimetable.Models).

    public sealed class RawSchedule
    {
        public List<RawDaySchedule> Grid { get; set; } = null!;
    }

    public sealed class RawDaySchedule
    {
        public DateOnly Date { get; set; }
        public List<RawLesson> Lessons { get; set; } = null!;
    }

    public sealed class RawLesson
    {
        public string Type { get; set; } = null!;  // "EMPTY" или "LESSON"
        public int Starts { get; set; }   // время в секундах
        public int Ends { get; set; }
        public int LessonNumber { get; set; }

        // Поля только для типа LESSON
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string LessonType { get; set; } = null!;  // "LECTURE", "PRACTICE", "SEMINAR"
        public List<RawGroup> Groups { get; set; } = null!;
        public RawProfessor Professor { get; set; } = null!;
        public RawAudience Audience { get; set; } = null!;
    }

    public sealed class RawGroup
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    public sealed class RawProfessor
    {
        public string Id { get; set; } = null!;
        public string FullName { get; set; } = null!;
    }

    public sealed class RawAudience
    {
        public string? Name { get; set; }       // полное, есть всегда: "332 (2) Учебная аудитория", "Онлайн"
        public string? ShortName { get; set; }   // компактное "332 (2)"; null у онлайна и части помещений
    }
}
