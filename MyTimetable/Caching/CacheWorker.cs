using MyTimetable.TsuInTime;
using MyTimetable.Entities;

namespace MyTimetable.Caching
{
    public sealed class CacheWorker : BackgroundService
    {
        private readonly ILogger<CacheWorker> _logger;
        private readonly TsuInTimeFetcher fetcher;
        private readonly IServiceProvider _serviceProvider;
        private readonly ScheduleBuilder _builder;
        private readonly CacheRebuilder _rebuilder;
        private readonly TimeProvider _time;

        public CacheWorker(ILogger<CacheWorker> logger, IServiceProvider serviceProvider, ScheduleBuilder builder, CacheRebuilder rebuilder, TsuInTimeFetcher fetcher, TimeProvider time)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _builder = builder;
            _rebuilder = rebuilder;
            this.fetcher = fetcher;
            _time = time;
        }

        // Тянет расписание из API и, если оно доступно, перезаписывает дефолтные уроки в БД.
        // БД — единственный источник правды; в случае недоступности API данные в ней остаются как были.
        private async Task RefreshDbFromApi()
        {
            CalendarSchedule apiData;
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
                    .Range(0, DaySchedule.DefaultSlotCount)
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

        // Чистое вычисление: какие будущие даты потеряли актуальность скрытий — т.е. в их календарной
        // неделе изменился состав пар. В БД не пишет; эффект применяет вызывающий внутри транзакции
        // замены уроков, чтобы оба удаления и вставка были атомарны.
        private async Task<List<DateOnly>> ComputeStaleDeactivationDates(CalendarSchedule newData)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
            var oldData = new CalendarSchedule((await _builder.LoadFromDb(db)).Where(x => x.Date >= today));
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
            foreach (var week in oldData.GetWeeks())
            {
                List<DaySchedule> newWeek = new();
                foreach (var day in week)
                {
                    if (dateMapping.Remove(day.Date, out var newDay))
                    {
                        newWeek.Add(newDay);
                    }
                }
                // Сравниваем СОСТАВ дефолтных уроков по дням, а не DaySchedule целиком: у DaySchedule
                // поле Cells — массив, и равенство рекорда сравнило бы его по ссылке (всегда «изменилось»).
                // DefaultLesson — рекорд с value-equality, поэтому поденное сравнение корректно по контенту.
                bool sameComposition = week.Count == newWeek.Count
                    && week.Zip(newWeek).All(p => DayDefaults(p.First).SequenceEqual(DayDefaults(p.Second)));
                if (!sameComposition)
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

        // Состав дефолтных уроков дня по слотам — для контентного сравнения недель (см. ComputeStale...).
        private static IEnumerable<DefaultLesson?> DayDefaults(DaySchedule day)
            => day.Cells.Select(c => c.DefaultLesson);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Прогрев из текущей БД до обращения к API: при засеянной БД кэш валиден сразу,
            // не дожидаясь (возможно медленного) запроса к API.
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await _rebuilder.Rebuild(db);
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
                    // Свежий контекст на каждый тик: долгоживущий DbContext копил бы identity map и отдавал
                    // устаревшие отслеживаемые сущности вместо актуальных данных из БД.
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    if (await _rebuilder.Rebuild(db))
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
