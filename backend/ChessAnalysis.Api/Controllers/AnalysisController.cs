using Microsoft.AspNetCore.Mvc;

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
}
