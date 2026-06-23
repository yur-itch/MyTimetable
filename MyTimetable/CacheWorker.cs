using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
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

        private async Task UpdateData()
        {
            _data.Data = await fetcher.Get();
            _logger.LogInformation("Updated TsuInTime data at {time}", DateTimeOffset.Now);
        }

        private async Task UpdateViews(List<DaySchedule> days)
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

            var html = await _renderer.RenderViewToStringAsync("Get", days, actionContext);
            var bytes = Encoding.UTF8.GetBytes(html);
            using var output = new MemoryStream();
            using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
            {
                brotli.Write(bytes);
            }
            _data.ViewResult = output.ToArray();

            foreach (DaySchedule day in days)
            {
                _data.PartialViewResult[day.Date] = await _renderer.RenderViewToStringAsync("GetOne", day, actionContext, true);
            }
            //_data.ViewResult = View(days);
        }

        private async Task UpdateCache()
        {
            await UpdateData();
            await UpdateViews(_data.Data);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateCache();
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
