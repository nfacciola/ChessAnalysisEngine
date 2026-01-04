using System.Text;
using System.Text.Json;
using ChessAnalysis.Api.Dto;

namespace ChessAnalysis.Api.Services;

public class GeminiCoachService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string Model = "gemini-2.5-flash-lite";

    public GeminiCoachService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["Gemini:ApiKey"] ?? throw new Exception("Gemini API key not found");
    }

    public async Task<string> GetExplanationAsync(ExplainRequest request)
    {
        var prompt = BuildPrompt(request);

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={_apiKey}";

        try
        {
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Gemini API Error: {error}");
                return "The coach is currently offline.";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "No explanation returned.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Coach Exception: {ex.Message}");
            return "I couldn't analyze this move right now.";
        }
    }


    private string BuildPrompt(ExplainRequest req)
    {
        return $@"
You are a friendly but sharp Chess Grandmaster Coach. 
The student played the move **{req.MoveSan}** in a position.
The engine classifies this move as: **{req.Label}**.

Context:
- FEN: {req.Fen}
- Engine recommended: {req.BestMoveSan} (UCI notation)
- Evaluation shift: {req.ScoreBefore} -> {req.ScoreAfter} (Centipawns)

Task:
Explain WHY the student's move ({req.MoveSan}) is considered {req.Label} compared to the best move. 
Be concise. 
If it was a blunder, briefly mention the tactical threat they missed. 
If it was a good move, praise the strategic idea.
Speak directly to the student ('You moved...').";
    }

}