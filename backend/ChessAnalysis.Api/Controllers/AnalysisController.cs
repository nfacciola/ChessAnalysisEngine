using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace ChessAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
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
