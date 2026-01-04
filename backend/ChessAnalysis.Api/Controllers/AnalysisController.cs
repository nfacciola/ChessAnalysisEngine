using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace ChessAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{

	public record EvaluateRequest(string Fen, int Depth = 12);
	private readonly EngineManager _engineManager;

	public AnalysisController(EngineManager engineManager)
	{
		_engineManager = engineManager;
	}

	[HttpPost("upload")]
	public async Task<IActionResult> UploadPgn(IFormFile pgnFile)
	{
		if (pgnFile == null || pgnFile.Length == 0)
		{
			return BadRequest("No pgn file uploaded");
		}

		using var reader = new StreamReader(pgnFile.OpenReadStream());
		var pgnText = await reader.ReadToEndAsync();

		var tags = ParseTags(pgnText);
		var moves = ExtractSanMoves(pgnText);

		return Ok(new
		{
			fileName = pgnFile.FileName,
			size = pgnFile.Length,
			whitePlayer = tags.TryGetValue("White", out var white) ? white : string.Empty,
			blackPlayer = tags.TryGetValue("Black", out var black) ? black : string.Empty,
			result = tags.TryGetValue("Result", out var result) ? result : string.Empty,
			sanMoves = moves
		});
	}

	[HttpPost("evaluate")]
	public async Task<IActionResult> Evaluate([FromBody] EvaluateRequest request, [FromHeader(Name = "X-Session-ID")] string sessionId)
	{
		if (string.IsNullOrWhiteSpace(request.Fen)) return BadRequest("FEN is required");
		if (string.IsNullOrEmpty(sessionId)) return BadRequest("Session ID required");

		// 1. Get the persistent process
		var session = _engineManager.GetSession(sessionId);

		// 2. Run the heavy calculation (IO bound)
		var rawOutput = await session.EvaluateAsync(request.Fen, request.Depth);

		// 3. Parse the result (CPU bound, fast)
		var result = Services.StockfishResultParser.Parse(rawOutput);

		// 4. Return DTO
		return Ok(new
		{
			evaluation = result.Mate.HasValue
				? new { type = "mate", value = result.Mate.Value }
				: new { type = "cp", value = result.Cp ?? 0 },
			bestMove = result.BestMove,
			pv = result.Pv,
			depth = request.Depth
		});
	}


	private static Dictionary<string, string> ParseTags(string pgnText)
	{
		var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var matches = Regex.Matches(pgnText, @"\[(\w+)\s+""([^""]*)""\]");

		foreach (Match match in matches)
		{
			if (match.Groups.Count == 3)
			{
				tags[match.Groups[1].Value] = match.Groups[2].Value;
			}
		}

		return tags;
	}

	private static List<string> ExtractSanMoves(string pgnText)
	{
		var nonTagLines = pgnText
			.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
			.Where(line => !line.TrimStart().StartsWith("[", StringComparison.Ordinal))
			.ToArray();

		var movesText = string.Join(" ", nonTagLines);

		// Remove comments and variations for a simple SAN list
		movesText = Regex.Replace(movesText, @"\{[^}]*\}", " ");
		movesText = Regex.Replace(movesText, @"\([^)]*\)", " ");

		// Remove move numbers like "1." or "23..."
		movesText = Regex.Replace(movesText, @"\d+\.(\.\.)?", " ");

		// Collapse whitespace
		movesText = Regex.Replace(movesText, @"\s+", " ").Trim();

		var tokens = movesText
			.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Where(token => token is not "1-0" and not "0-1" and not "1/2-1/2" and not "*")
			.ToList();

		return tokens;
	}
}
