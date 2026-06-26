namespace MyTimetable.TsuInTime
{
    // Wire shapes for the TSU intime API response (https://intime.tsu.ru/api/web/v1/schedule/group).
    // These mirror the JSON exactly; TsuInTimeFetcher normalizes them into the domain model (MyTimetable.Models).

    public sealed class RawSchedule
    {
        public List<RawDaySchedule> Grid { get; set; }
    }

    public sealed class RawDaySchedule
    {
        public DateOnly Date { get; set; }
        public List<RawLesson> Lessons { get; set; }
    }

    public sealed class RawLesson
    {
        public string Type { get; set; }  // "EMPTY" или "LESSON"
        public int Starts { get; set; }   // время в секундах
        public int Ends { get; set; }
        public int LessonNumber { get; set; }

        // Поля только для типа LESSON
        public string Id { get; set; }
        public string Title { get; set; }
        public string LessonType { get; set; }  // "LECTURE", "PRACTICE", "SEMINAR"
        public List<RawGroup> Groups { get; set; }
        public RawProfessor Professor { get; set; }
        public RawAudience Audience { get; set; }
    }

    public sealed class RawGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public sealed class RawProfessor
    {
        public string Id { get; set; }
        public string FullName { get; set; }
    }

    public sealed class RawAudience
    {
        public string? Name { get; set; }       // полное, есть всегда: "332 (2) Учебная аудитория", "Онлайн"
        public string? ShortName { get; set; }   // компактное "332 (2)"; null у онлайна и части помещений
    }
}
