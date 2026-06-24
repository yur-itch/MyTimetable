using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.EntityFrameworkCore;
using MyTimetable.Models;
using System.IO.Compression;
using System.Text;

namespace MyTimetable
{
    public class CacheWorker : BackgroundService
    {
        private readonly ILogger<CacheWorker> _logger;
        private readonly TsuInTimeFetcher fetcher = new();
        private readonly ViewRenderer _renderer;
        private readonly IServiceProvider _serviceProvider;
        private ScheduleData _data;

        public CacheWorker(ScheduleData data, ILogger<CacheWorker> logger, ViewRenderer renderer, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _data = data;
            _renderer = renderer;
            _serviceProvider = serviceProvider;
        }

        private async Task<bool> UpdateData()
        {
            List<DaySchedule> newData;
            try
            {
                newData = await fetcher.Get();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API unavailable, falling back to DB");
                newData = new();
            }
            bool fromApi = false;
            if (newData.Any())
            {
                _data.Data = newData;
                fromApi = true;
            } else
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var defaultLessons = db.Lessons.Where(x => x.isCustom == false).ToList();
                if (defaultLessons.Any())
                {
                    var firstDate = defaultLessons.Min(x => x.Date);
                    var lastDate = defaultLessons.Max(x => x.Date);
                    int count = lastDate.DayNumber - firstDate.DayNumber + 1;
                    List<DaySchedule> days = Enumerable.Range(0, count)
                        .Select(i => new DaySchedule { Date = firstDate.AddDays(i) })
                        .ToList();
                    foreach (Lesson lesson in defaultLessons)
                    {
                        days[lesson.Date.DayNumber - firstDate.DayNumber].Lessons[lesson.LessonNumber - 1] = lesson;
                    }
                    _data.Data = days;
                }
            }
            if (fromApi || _data.Data.Any())
            {
                _data.StateValid = true;
                _logger.LogInformation("Updated TsuInTime data at {time}", DateTimeOffset.Now);
            } else
            {
                _logger.LogInformation("Failed to update TsuInTime data at {time}: API not available and DB is empty", DateTimeOffset.Now);
            }
            return fromApi;
        }

        private async Task UpdateViews()
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

            var html = await _renderer.RenderViewToStringAsync("Get", _data.Data, actionContext);
            var bytes = Encoding.UTF8.GetBytes(html);
            using var output = new MemoryStream();
            using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
            {
                brotli.Write(bytes);
            }
            _data.ViewResult = output.ToArray();

            foreach (DaySchedule day in _data.Data)
            {
                _data.PartialViewResult[day.Date] = await _renderer.RenderViewToStringAsync("GetOne", day, actionContext, true);
            }
        }

        private async Task UpdateDb()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Lesson[] lessons = _data.Data
                .SelectMany(x => x.Lessons)
                .OfType<Lesson>()
                .ToArray();
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    bool fromApi = await UpdateData();
                    var tasks = fromApi
                        ? new[] { UpdateViews(), UpdateDb() }
                        : new[] { UpdateViews() };
                    await Task.WhenAll(tasks);
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
