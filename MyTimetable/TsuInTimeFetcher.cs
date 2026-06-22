using System.Text.Json;
using MyTimetable.Models;

namespace MyTimetable.Models
{
    public class ScheduleResponse
    {
        public List<DaySchedule> Grid { get; set; }
    }

    public class DaySchedule
    {
        public string Date { get; set; }
        public List<Lesson> Lessons { get; set; }
    }

    public class Lesson
    {
        public string Type { get; set; }  // "EMPTY" или "LESSON"
        public int Starts { get; set; }   // время в секундах
        public int Ends { get; set; }
        public int LessonNumber { get; set; }

        // Поля только для типа LESSON
        public string Id { get; set; }
        public string Title { get; set; }
        public string LessonType { get; set; }  // "LECTURE", "PRACTICE", "SEMINAR"
        public List<Group> Groups { get; set; }
        public Professor Professor { get; set; }
    }

    public class Group
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsSubgroup { get; set; }
    }

    public class Professor
    {
        public string Id { get; set; }
        public string FullName { get; set; }
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
        private static string endpoint = "https://intime.tsu.ru/api/web/v1/schedule/group";
        private static string groupID = "06696fef-39f2-11f0-9dca-6cb3110a6d8e";

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

        public async Task<List<DaySchedule>> Get(string url)
        {
            var response = await client.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();
            var schedule = JsonSerializer.Deserialize<ScheduleResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return schedule?.Grid ?? new();
        }

        public async Task<List<DaySchedule>> Get() => await Get(BuildUrl());

        public async Task<List<DaySchedule>> Get(DateTime dateFrom, DateTime dateTo)
            => await Get(BuildUrl(dateFrom, dateTo));
    }
}
