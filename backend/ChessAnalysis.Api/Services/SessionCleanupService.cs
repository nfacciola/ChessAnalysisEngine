public class SessionCleanupService : BackgroundService
{
    private readonly EngineManager _manager;

    public SessionCleanupService(EngineManager manager)
    {
        _manager = manager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _manager.CleanupExpiredSessions();
        }
    }
}