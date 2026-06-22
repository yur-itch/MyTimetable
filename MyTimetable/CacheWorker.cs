namespace MyTimetable
{
    public class CacheWorker : BackgroundService
    {
        private readonly ILogger<CacheWorker> _logger;
        private TsuInTimeFetcher fetcher = new();
        private ScheduleData _data;

        public CacheWorker(ScheduleData data, ILogger<CacheWorker> logger)
        {
            _logger = logger;
            _data = data;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _data.Data = await fetcher.Get();
                    _logger.LogInformation("Updated TsuInTime data at {time}", DateTimeOffset.Now);
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
