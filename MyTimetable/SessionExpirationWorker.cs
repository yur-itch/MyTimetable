namespace MyTimetable
{
    public sealed class SessionExpirationWorker : BackgroundService
    {
        private readonly ILogger<SessionExpirationWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeProvider _time;

        public SessionExpirationWorker(ILogger<SessionExpirationWorker> logger, IServiceProvider serviceProvider, TimeProvider time)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _time = time;
        }

        private async Task Update(CancellationToken token) {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = _time.GetUtcNow();
            await db.Sessions
                .Where(x => x.ExpiresAt <= now)
                .ExecuteDeleteAsync(token);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Update(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clear stale sessions from DB");
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
