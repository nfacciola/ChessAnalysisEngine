using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public async Task<string> ExplainMoveAsync(CoachContext context)
    {
        if (string.IsNullOrEmpty(_apiKey)) return "Config Error: Gemini API Key missing.";

        // 1. Build the Prompt (Moved from Frontend to Backend)
        var prompt = BuildPrompt(context);

        // 2. Call API
        var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={_apiKey}";
        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(apiUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                return "The coach is currently offline (API Error).";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(responseJson);
            return node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString()
                   ?? "The coach couldn't form a thought.";
        }
        catch (Exception ex)
        {
            return $"Coach Network Error: {ex.Message}";
        }
    }

    private string BuildPrompt(CoachContext ctx)
    {
        // This is the "System Prompt" + "User Prompt" combined
        return $@"
You are a Chess Coach. The student just played '{ctx.MoveSan}'.
Evaluation Label: {ctx.Label} (Score: {ctx.ScoreAfter} cp, was {ctx.ScoreBefore}).

STRICT Factual Board Context (Do not hallucinate):
Fen: {ctx.Fen}
Best Move: {ctx.BestMoveSan}
Analysis: {JsonSerializer.Serialize(ctx.BoardContext)}

Explain WHY the move '{ctx.MoveSan}' was {ctx.Label}. 
Use the 'geometry' and 'material' facts from the context above. 
Keep it concise (2-3 sentences) and encouraging.";
    }
}

// DTO matching the 'context' object sent from App.jsx
public class CoachContext
{
    public string? Fen { get; set; }
    public string? MoveSan { get; set; }
    public string? BestMoveSan { get; set; }
    public string? Label { get; set; }
    public int ScoreBefore { get; set; }
    public int ScoreAfter { get; set; }
    public object? BoardContext { get; set; } // The JSON from BoardAnalysisService
}