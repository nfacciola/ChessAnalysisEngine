using System.Diagnostics;

public class StockfishSession : IDisposable
{
    private readonly Process _process;
    private readonly SemaphoreSlim _lock = new(1, 1); // Ensure 1 command at a time per user
    public DateTime LastUsed { get; private set; }
    public string Id { get; }

    public StockfishSession(string id, string stockfishPath)
    {
        Id = id;
        LastUsed = DateTime.UtcNow;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();

        // Initial Handshake (done once per session)
        _process.StandardInput.WriteLine("uci");
        _process.StandardInput.WriteLine("isready");
        // We might want to consume lines here to clear the buffer, 
        // but UCI is forgiving if we just start sending commands later.
    }

    public async Task<string> EvaluateAsync(string fen, int depth)
    {
        await _lock.WaitAsync();
        try
        {
            LastUsed = DateTime.UtcNow;

            // Clear any previous output (optional, but good practice if sync/async issues)
            // Ideally, we just rely on the request/response flow of UCI.

            await _process.StandardInput.WriteLineAsync($"position fen {fen}");
            await _process.StandardInput.WriteLineAsync($"go depth {depth}");

            var outputLines = new List<string>();
            string? line;

            // Read until bestmove
            while ((line = await _process.StandardOutput.ReadLineAsync()) != null)
            {
                outputLines.Add(line);
                if (line.StartsWith("bestmove"))
                {
                    break;
                }
            }

            return string.Join("\n", outputLines);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            try
            {
                _process.StandardInput.WriteLine("quit"); // Polite quit
                _process.WaitForExit(500); // Give it 500ms
                _process.Kill(); // Force kill
            }
            catch { /* Ignore errors on kill */ }
        }
        _process.Dispose();
        _lock.Dispose();
    }
}