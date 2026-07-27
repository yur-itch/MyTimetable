using System.Text.Json;

namespace MyTimetable.TsuInTime
{
    public sealed class TsuInTimeFetcher
    {
        private readonly HttpClient _client;
        private readonly string endpoint = "https://intime.tsu.ru/api/web/v1/schedule/group";
        private readonly string groupID = "06696fef-39f2-11f0-9dca-6cb3110a6d8e";
        private readonly string[] groupNames = { "972501", "972501 (1)" };
        private readonly TimeProvider _time;

        public TsuInTimeFetcher(
            TimeProvider? time = null,
            IConfiguration? configuration = null,
            IHostEnvironment? environment = null)
        {
            _time = time ?? TimeProvider.System;

            var handler = new HttpClientHandler
            {
                // The endpoint is accessible directly and should not use a local proxy.
                UseProxy = false
            };

            // The endpoint currently uses a certificate from a Russian CA that may not be
            // trusted by the local machine. This insecure workaround is available only when
            // explicitly enabled in Development configuration.
            bool allowUntrusted = environment?.IsDevelopment() == true
                && configuration?.GetValue<bool>("Schedule:AllowUntrustedCertificate") == true;
            if (allowUntrusted)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        private string BuildUrl()
        {
            var now = _time.GetLocalNow().DateTime;
            int yearStart = now.Month >= 7 ? now.Year : now.Year - 1;
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

        private DefaultLesson? ConvertLessonFromRaw(RawLesson lesson)
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
            DefaultLesson res = new DefaultLesson()
            {
                Title = lesson.Title,
                LessonType = lesson.LessonType,
                Professor = new Professor()
                {
                    Name = FormatProfessor(lesson.Professor?.FullName),
                    IsShown = true  // TODO: replace with actual value
                },
                Room = RoomOf(lesson.Audience)
            };
            return res;
        }

        private DaySchedule ConvertDayFromRaw(RawDaySchedule rawDaySchedule)
        {
            //DaySchedule day = new();
            //day.Date = rawDaySchedule.Date;
            DefaultLesson?[] lessons = new DefaultLesson?[DaySchedule.DefaultSlotCount];
            foreach (RawLesson rawLesson in rawDaySchedule.Lessons)
            {
                if (rawLesson.Type == "EMPTY")
                {
                    continue;
                }
                if (rawLesson.LessonNumber < 1 || rawLesson.LessonNumber > DaySchedule.DefaultSlotCount)
                {
                    continue;
                }
                DefaultLesson? lesson = ConvertLessonFromRaw(rawLesson);
                if (lesson == null)
                {
                    continue;
                }
                lessons[rawLesson.LessonNumber - 1] = lesson;
            }
            var cells = lessons.Select(x => new Cell()
            {
                CustomLesson = null,
                DefaultLesson = x,
                Hidden = false,
            }).ToArray();
            var day = new DaySchedule()
            {
                Date = rawDaySchedule.Date,
                Cells = cells
            };
            return day;
        }

        private CalendarSchedule ConvertFromRaw(RawSchedule rawSchedule)
        {
            return new CalendarSchedule(rawSchedule.Grid.Select(ConvertDayFromRaw));
        }

        public async Task<CalendarSchedule> Get(string url)
        {
            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            var rawSchedule = JsonSerializer.Deserialize<RawSchedule>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rawSchedule != null)
            {
                Normalize(rawSchedule);
                return ConvertFromRaw(rawSchedule);
            }
            return new CalendarSchedule([]);
        }

        public async Task<CalendarSchedule> Get() => await Get(BuildUrl());

        public async Task<CalendarSchedule> Get(DateTime dateFrom, DateTime dateTo)
            => await Get(BuildUrl(dateFrom, dateTo));
    }
}
