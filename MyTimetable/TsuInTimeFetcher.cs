using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using MyTimetable.Models;

namespace MyTimetable.Models
{
    public class RawSchedule
    {
        public List<RawDaySchedule> Grid { get; set; }
    }

    public class RawDaySchedule
    {
        public DateOnly Date { get; set; }
        public List<RawLesson> Lessons { get; set; }
    }

    public class DaySchedule
    {
        public DateOnly Date { get; set; }
        public Lesson?[] Lessons { get; set; } = { null, null, null, null, null, null };
        // Параллельно Lessons: слот скрыт (есть в Deactivations) — рендерится приглушённым, не удаляется.
        public bool[] Hidden { get; set; } = { false, false, false, false, false, false };
    }

    public class RawLesson
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

    public class Lesson
    {
        public DateOnly Date { get; set; }
        public int LessonNumber { get; set; }
        public string Title { get; set; }
        public string LessonType { get; set; }  // "LECTURE", "PRACTICE", "SEMINAR"
        public string Professor { get; set; }
        public string Room { get; set; } = "";
        public bool isCustom { get; set; } = false;

        // Не хранится в БД: показывать препода в карточке, только если у этого предмета
        // за год встречается больше одного разного преподавателя. Проставляется при сборке кэша.
        [NotMapped]
        public bool ShowProfessor { get; set; }
    }

    public class RawGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class RawProfessor
    {
        public string Id { get; set; }
        public string FullName { get; set; }
    }

    public class RawAudience
    {
        public string? Name { get; set; }       // полное, есть всегда: "332 (2) Учебная аудитория", "Онлайн"
        public string? ShortName { get; set; }   // компактное "332 (2)"; null у онлайна и части помещений
    }

    public class LessonDeactivation
    {
        public DateOnly Date { get; set; }
        public int LessonNumber { get; set; }
    }
}

namespace MyTimetable
{
    public class TsuInTimeFetcher
    {
        private HttpClient client = new(new HttpClientHandler
        {
            // TSU intime uses a cert from an untrusted Russian CA
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            // Bypass local proxy (e.g. V2Ray on :10809) — intime.tsu.ru is accessible directly
            UseProxy = false
        });
        private string endpoint = "https://intime.tsu.ru/api/web/v1/schedule/group";
        private string groupID = "06696fef-39f2-11f0-9dca-6cb3110a6d8e";
        private string[] groupNames = { "972501", "972501 (1)" };

        private string BuildUrl()
        {
            int yearStart = DateTime.Now.Month >= 7 ? DateTime.Now.Year : DateTime.Now.Year - 1;
            var dateFrom = new DateTime(yearStart, 9, 1);
            var dateTo = new DateTime(yearStart + 1, 7, 1);
            return BuildUrl(dateFrom, dateTo);
        }

        private string BuildUrl(DateTime dateFrom, DateTime dateTo)
        {
            var dateFromFormatted = dateFrom.ToString("yyyy-MM-dd");
            var dateToFormatted = dateTo.ToString("yyyy-MM-dd");
            return $"{endpoint}?dateFrom={dateFromFormatted}&dateTo={dateToFormatted}&id={groupID}";
        }

        private static void Normalize(RawSchedule schedule)
        {
            foreach (var day in schedule.Grid)
            {
                NormalizeDay(day);
            }
        }

        private static void NormalizeDay(RawDaySchedule day)
        {
            foreach (var lesson in day.Lessons)
            {
                // Нормализуем только реальные пары: у EMPTY-слотов LESSON-поля (Title, Groups, ...) равны null.
                if (lesson?.Type?.Trim().ToUpperInvariant() != "LESSON") continue;
                NormalizeLesson(lesson);
            }
        }

        private static void NormalizeLesson(RawLesson lesson)
        {
            lesson.Type = "LESSON";
            lesson.Title = Capitalize(CollapseSpaces(lesson.Title));
            lesson.LessonType = CollapseSpaces(lesson.LessonType).ToUpperInvariant();

            foreach (var group in lesson.Groups)
            {
                NormalizeGroup(group);
            }
            if (lesson.Audience != null)
            {
                lesson.Audience.Name = CollapseSpaces(lesson.Audience.Name);
                lesson.Audience.ShortName = CollapseSpaces(lesson.Audience.ShortName);
            }
        }

        private static void NormalizeGroup(RawGroup group)
        {
            group.Name = CollapseSpaces(group.Name).ToUpperInvariant();
        }

        // Короткое имя аудитории, если есть, иначе длинное ("Онлайн" и т.п. идут как длинное).
        private static string RoomOf(RawAudience? a)
        {
            if (a == null) return "";
            return !string.IsNullOrEmpty(a.ShortName) ? a.ShortName : (a.Name ?? "");
        }

        private static string CollapseSpaces(string? s)
            => s == null ? "" : string.Join(" ", s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string Capitalize(string s)
            => s.Length == 0 ? "" : char.ToUpperInvariant(s[0]) + s[1..];

        private static string FormatProfessor(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "";
            var parts = fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return Capitalize(parts[0].ToLowerInvariant());
        }

        private Lesson? ConvertLessonFromRaw(RawLesson lesson, DateOnly date)
        {
            string? groupName = null;
            foreach (RawGroup group in lesson.Groups)
            {
                if (groupNames.Contains(group.Name))
                {
                    groupName = group.Name;
                    break;
                }
            }
            if (groupName == null)
            {
                return null;
            }
            Lesson res = new();
            res.Date = date;
            res.LessonNumber = lesson.LessonNumber;
            res.Title = lesson.Title;
            res.LessonType = lesson.LessonType;
            res.Professor = FormatProfessor(lesson.Professor?.FullName);
            res.Room = RoomOf(lesson.Audience);
            return res;
        }

        private DaySchedule ConvertDayFromRaw(RawDaySchedule rawDaySchedule)
        {
            DaySchedule day = new();
            day.Date = rawDaySchedule.Date;
            foreach (RawLesson rawLesson in rawDaySchedule.Lessons)
            {
                if (rawLesson.Type == "EMPTY")
                {
                    continue;
                }
                Lesson? lesson = ConvertLessonFromRaw(rawLesson, day.Date);
                if (lesson == null)
                {
                    continue;
                }
                if (lesson.LessonNumber < 1 || lesson.LessonNumber > 6)
                {
                    continue;
                }
                day.Lessons[lesson.LessonNumber - 1] = lesson;
            }
            return day;
        }

        private List<DaySchedule> ConvertFromRaw(RawSchedule rawSchedule)
        {
            return rawSchedule.Grid.Select(ConvertDayFromRaw).ToList();
        }

        public async Task<List<DaySchedule>> Get(string url)
        {
            var response = await client.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();
            var rawSchedule = JsonSerializer.Deserialize<RawSchedule>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rawSchedule != null)
            {
                Normalize(rawSchedule);
                return ConvertFromRaw(rawSchedule);
            }
            return new();
        }

        public async Task<List<DaySchedule>> Get() => await Get(BuildUrl());

        public async Task<List<DaySchedule>> Get(DateTime dateFrom, DateTime dateTo)
            => await Get(BuildUrl(dateFrom, dateTo));
    }
}
