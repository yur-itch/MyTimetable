using Microsoft.EntityFrameworkCore;
using MyTimetable.Models;

namespace MyTimetable
{
    // Единый источник сборки модели расписания из БД: дефолтные уроки + отметка скрытых слотов
    // (Deactivations). Используется и фоновым воркером для кэша, и контроллером для рендера на лету.
    public class ScheduleBuilder
    {
        private readonly IServiceProvider _serviceProvider;

        public ScheduleBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<List<DaySchedule>> LoadFromDb()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<Lesson> defaultLessons = await db.Lessons.Where(x => x.isCustom == false).ToListAsync();
            if (!defaultLessons.Any())
            {
                return new();
            }

            var deactivated = (await db.Deactivations.ToListAsync())
                .Select(d => (d.Date, d.LessonNumber))
                .ToHashSet();

            DateOnly firstDate = defaultLessons.Min(x => x.Date);
            DateOnly lastDate = defaultLessons.Max(x => x.Date);
            int count = lastDate.DayNumber - firstDate.DayNumber + 1;
            List<DaySchedule> days = Enumerable.Range(0, count)
                .Select(i => new DaySchedule { Date = firstDate.AddDays(i) })
                .ToList();

            foreach (Lesson lesson in defaultLessons)
            {
                DaySchedule day = days[lesson.Date.DayNumber - firstDate.DayNumber];
                day.Lessons[lesson.LessonNumber - 1] = lesson;
                day.Hidden[lesson.LessonNumber - 1] = deactivated.Contains((lesson.Date, lesson.LessonNumber));
            }
            return days;
        }
    }
}
