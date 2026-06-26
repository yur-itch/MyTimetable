using Microsoft.EntityFrameworkCore;
using MyTimetable.Models;
using MyTimetable.TsuInTime;
using MyTimetable.Entities;

namespace MyTimetable
{
    public sealed class CacheWorker : BackgroundService
    {
        private readonly ILogger<CacheWorker> _logger;
        private readonly TsuInTimeFetcher fetcher = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ScheduleBuilder _builder;
        private readonly CacheRebuilder _rebuilder;

        public CacheWorker(ILogger<CacheWorker> logger, IServiceProvider serviceProvider, ScheduleBuilder builder, CacheRebuilder rebuilder)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _builder = builder;
            _rebuilder = rebuilder;
        }

        // Тянет расписание из API и, если оно доступно, перезаписывает дефолтные уроки в БД.
        // БД — единственный источник правды; в случае недоступности API данные в ней остаются как были.
        private async Task RefreshDbFromApi()
        {
            List<DaySchedule> apiData;
            try
            {
                apiData = await fetcher.Get();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API unavailable, keeping existing DB data");
                return;
            }
            if (!apiData.Any())
            {
                return;
            }
            List<DateOnly> staleDates = await ComputeStaleDeactivationDates(apiData);

            DefaultLessonEntry[] defaultLessons = apiData
                .SelectMany(x => Enumerable
                    .Range(0, 6)
                    .Select(y => (Date: x.Date, LessonNumber: y + 1, Lesson: x.Cells[y].DefaultLesson))
                    .Where(t => t.Lesson != null)
                    .Select(t => new DefaultLessonEntry()
                    {
                        Date = t.Date,
                        LessonNumber = t.LessonNumber,
                        LessonType = t.Lesson!.LessonType,
                        Title = t.Lesson.Title,
                        Professor = t.Lesson.Professor.Name,
                        Room = t.Lesson.Room
                    }))
                .ToArray();

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                if (staleDates.Count > 0)
                {
                    // Скрытия в неделях с изменившимся составом пар сбрасываем здесь же — атомарно с replace уроков.
                    await db.Deactivations.Where(x => staleDates.Contains(x.Date)).ExecuteDeleteAsync();
                }
                await db.DefaultLessons.ExecuteDeleteAsync();
                await db.DefaultLessons.AddRangeAsync(defaultLessons);
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync(); // откат — данные как были
                throw;
            }
        }

        private static int WeekNumber(DateOnly date)
        {
            // Remap DayOfWeek so Monday=0 ... Sunday=6
            // (.NET default is Sunday=0 ... Saturday=6)
            int dayOfWeek = ((int)date.DayOfWeek + 6) % 7;

            return (date.DayNumber - dayOfWeek) / 7;
        }

        // assumes data is sorted, because the builder gives it sorted
        private static IEnumerable<List<DaySchedule>> IterateWeeks(List<DaySchedule> days)
        {
            if (days.Count == 0)
            {
                yield break;
            }
            var prevWeekNumber = -1;
            List<DaySchedule> piece = new();
            foreach (var day in days)
            {
                var newWeekNumber = WeekNumber(day.Date);
                if (newWeekNumber != prevWeekNumber)
                {
                    if (piece.Count > 0)
                    {
                        yield return piece;
                    }
                    prevWeekNumber = newWeekNumber;
                    piece = new();
                }
                piece.Add(day);
            }
            if (piece.Count != 0)
            {
                yield return piece;
            }
        }

        // Чистое вычисление: какие будущие даты потеряли актуальность скрытий — т.е. в их календарной
        // неделе изменился состав пар. В БД не пишет; эффект применяет вызывающий внутри транзакции
        // замены уроков, чтобы оба удаления и вставка были атомарны.
        private async Task<List<DateOnly>> ComputeStaleDeactivationDates(List<DaySchedule> newData)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var oldData = (await _builder.LoadFromDb()).Where(x => x.Date >= today).ToList();
            var dateMapping = new Dictionary<DateOnly, DaySchedule>();
            var daysToInvalidate = new List<DateOnly>();
            foreach (var day in newData)
            {
                if (day.Date < today)
                {
                    continue;
                }
                dateMapping[day.Date] = day;
            }
            foreach (var week in IterateWeeks(oldData))
            {
                List<DaySchedule> newWeek = new();
                foreach (var day in week)
                {
                    if (dateMapping.Remove(day.Date, out var newDay))
                    {
                        newWeek.Add(newDay);
                    }
                }
                if (!week.SequenceEqual(newWeek))
                {
                    foreach (var day in week)
                    {
                        daysToInvalidate.Add(day.Date);
                    }
                }
            }
            foreach (var value in dateMapping.Values)
            {
                daysToInvalidate.Add(value.Date);
            }
            return daysToInvalidate;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Прогрев из текущей БД до обращения к API: при засеянной БД кэш валиден сразу,
            // не дожидаясь (возможно медленного) запроса к API.
            try
            {
                await _rebuilder.Rebuild();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warm cache from DB on startup");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshDbFromApi();
                    if (await _rebuilder.Rebuild())
                    {
                        _logger.LogInformation("Updated TsuInTime views at {time}", DateTimeOffset.Now);
                    }
                    else
                    {
                        _logger.LogInformation("Failed to update TsuInTime views at {time}: DB is empty", DateTimeOffset.Now);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update TsuInTime data");
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
