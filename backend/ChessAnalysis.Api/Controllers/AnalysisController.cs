using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace ChessAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{

	public record EvaluateRequest(string Fen, int Depth = 10);

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
	public async Task<IActionResult> Evaluate([FromBody] EvaluateRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Fen))
		{
			return BadRequest("FEN is required");
		}

		var stockfishPath = Path.Combine(
			AppContext.BaseDirectory,
			"Stockfish",
			"stockfish.exe"
		);

		if (!System.IO.File.Exists(stockfishPath))
		{
			return StatusCode(500, $"Stockfish executable not found at path {stockfishPath}");
		}

		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = stockfishPath,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			}
		};

		process.Start();

		var input = process.StandardInput;
		var output = process.StandardOutput;

		// Handshake
		await input.WriteLineAsync("uci");
		await input.WriteLineAsync("isready");

		string? line;
		while ((line = await output.ReadLineAsync()) != null)
		{
			if (line == "readyok")
				break;
		}

		// Send position + go
		await input.WriteLineAsync($"position fen {request.Fen}");
		await input.WriteLineAsync($"go depth {request.Depth}");

		int? cp = null;
		int? mate = null;
		string? bestMove = null;
		List<string> pv = new();

		while ((line = await output.ReadLineAsync()) != null)
		{
			if (line.StartsWith("info"))
			{
				var cpMatch = Regex.Match(line, @"score cp (-?\d+)");
				if (cpMatch.Success)
					cp = int.Parse(cpMatch.Groups[1].Value);

				var mateMatch = Regex.Match(line, @"score mate (-?\d+)");
				if (mateMatch.Success)
					mate = int.Parse(mateMatch.Groups[1].Value);

				var pvIndex = line.IndexOf(" pv ");
				if (pvIndex != -1)
				{
					var pvString = line[(pvIndex + 4)..]; // everything after " pv "
					pv = pvString
						.Split(' ', StringSplitOptions.RemoveEmptyEntries)
						.Where(t => t.Length >= 4) // e2e4, g1f3, etc
						.ToList();
				}
			}

			if (line.StartsWith("bestmove"))
			{
				bestMove = line.Split(' ')[1];
				break;
			}
		}

		process.Kill();

		return Ok(new
		{
			evaluation = mate.HasValue
				? new { type = "mate", value = mate.Value }
				: new { type = "cp", value = cp ?? 0 },
			bestMove,
			pv,
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
