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
        var boardJson = JsonSerializer.Serialize(ctx.BoardContext);

        // FIX 1: Correct Identity Logic for 'fenBefore'
        // If FEN says " w ", it is White's turn to move. So the User (who is making the move) is White.
        var isWhiteTurn = ctx.Fen != null && ctx.Fen.Contains(" w ");
        var userColor = isWhiteTurn ? "White" : "Black"; // <--- FLIPPED THIS
        var opponentColor = isWhiteTurn ? "Black" : "White"; // <--- FLIPPED THIS

        return $@"
### ROLE
You are a Chess Coach analyzing a move for **{userColor}**.
- **Current Move:** {ctx.MoveSan} (Played by {userColor})
- **Opponent:** {opponentColor}

### BOARD CONTEXT
{boardJson}

### CRITICAL RULES
1. **Naming Convention:**
   - **BANNED:** Do NOT use pronouns like ""you"", ""your"", ""me"", ""my"", or ""I"".
   - **REQUIRED:** Refer to the student as **""{userColor}""**.
   - **REQUIRED:** Refer to the opponent as **""{opponentColor}""**.
   - Example: ""{userColor}'s move controls the center against {opponentColor}.""

2. **Friendly Fire (Logic Check):**
   - **Crucial:** A move cannot ""pressure"" or ""threaten"" friendly {userColor} pieces.
   - If {userColor} moves a piece to look at another {userColor} piece, it **""defends""** or **""connects""** them.
   - INCORRECT: ""{userColor} pressures the d4 pawn."" (If d4 is {userColor})
   - CORRECT: ""{userColor} defends the d4 pawn.""

3. **Strict Threats:**
   - **DATA IS TRUTH:** Only discuss attacks found in `Tactics.DirectAttacks`.
   - If the list says ""Bishop attacks Knight"", say exactly that.
   - **Do NOT infer pins:** Even if you think there is a Queen behind that Knight, do not say it unless the list explicitly includes ""Bishop attacks Queen"".

4. **Agency:**
   - If the move is labeled **Excellent/Best/Good**, explain how it helps **{userColor}**.
   - Do NOT say a good move supports {opponentColor}'s plans.

5. **Safety & Threats:**
   - Check `{userColor}KingSafety`. If `IsInCheck` is true, warn {userColor}!
   - If `CanCastle...` is false, do not suggest castling.
   - ONLY mention attacks explicitly listed in `Tactics.DirectAttacks`.

6. **Format:**
   - **Max 3 sentences.**
   - **No Markdown.**
   - Conversational tone.

### TASK
Explain WHY the move '{ctx.MoveSan}' was {ctx.Label}.";
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