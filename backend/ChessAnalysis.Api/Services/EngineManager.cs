using System.Collections.Concurrent;

public class EngineManager
{
    private readonly ConcurrentDictionary<string, StockfishSession> _sessions = new();
    private readonly string _stockfishPath;

    // Configurable timeout (e.g., 10 minutes)
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(10);

    public EngineManager(IWebHostEnvironment env)
    {
        // Resolve path once
        _stockfishPath = Path.Combine(env.ContentRootPath, "Stockfish", "stockfish.exe");
    }

    public StockfishSession GetSession(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, id => new StockfishSession(id, _stockfishPath));
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.Dispose();
        }
    }

    // Call this from a BackgroundService
    public void CleanupExpiredSessions()
    {
        var now = DateTime.UtcNow;
        foreach (var session in _sessions.Values)
        {
            if (now - session.LastUsed > _sessionTimeout)
            {
                RemoveSession(session.Id);
                Console.WriteLine($"Killed idle session: {session.Id}");
            }
        }
    }
}