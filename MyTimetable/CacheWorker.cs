using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.EntityFrameworkCore;
using MyTimetable.Models;

namespace MyTimetable
{
    public class CacheWorker : BackgroundService
    {
        private readonly ILogger<CacheWorker> _logger;
        private readonly TsuInTimeFetcher fetcher = new();
        private readonly ViewRenderer _renderer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ScheduleBuilder _builder;
        private ScheduleData _data;

        public CacheWorker(ScheduleData data, ILogger<CacheWorker> logger, ViewRenderer renderer, IServiceProvider serviceProvider, ScheduleBuilder builder)
        {
            _logger = logger;
            _data = data;
            _renderer = renderer;
            _serviceProvider = serviceProvider;
            _builder = builder;
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

            Lesson[] lessons = apiData
                .SelectMany(x => x.Lessons)
                .OfType<Lesson>()
                .ToArray();

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                await db.Lessons.Where(x => x.isCustom == false).ExecuteDeleteAsync();
                await db.Lessons.AddRangeAsync(lessons);
                await db.SaveChangesAsync(); // один запрос, внутри транзакции
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync(); // откат — данные как были
                throw;
            }
        }

        private async Task UpdateViews(List<DaySchedule> schedule)
        {
            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
            };
            var routeData = new RouteData();
            routeData.Values["controller"] = "App";

            var actionContext = new ActionContext(
                httpContext,
                routeData,
                new ActionDescriptor()
            );

            var html = await _renderer.RenderViewToStringAsync("Get", schedule, actionContext);
            _data.ViewResult = Compression.Brotli(html);

            foreach (DaySchedule day in schedule)
            {
                _data.PartialViewResult[day.Date] = await _renderer.RenderViewToStringAsync("GetOne", day, actionContext, true);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshDbFromApi();
                    List<DaySchedule> schedule = await _builder.LoadFromDb();
                    if (schedule.Any())
                    {
                        await UpdateViews(schedule);
                        _data.StateValid = true;
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
