using ChessAnalysis.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
	// Define the request structure once
	public record EvaluateRequest(string Fen, int Depth = 17);
	private readonly EngineManager _engineManager;
	private readonly BoardAnalysisService _analysisService;


	public AnalysisController(EngineManager engineManager, BoardAnalysisService analysisService)
	{
		_engineManager = engineManager;
		_analysisService = analysisService;
	}

	// Existing Endpoint: Gets the "Truth" (Stockfish)
	[HttpPost("evaluate")]
	public async Task<IActionResult> Evaluate([FromBody] EvaluateRequest request, [FromHeader(Name = "X-Session-ID")] string sessionId)
	{
		if (string.IsNullOrWhiteSpace(request.Fen)) return BadRequest("FEN is required");
		// Note: You might want to allow empty session ID for simple one-off checks, 
		// but if your EngineManager requires it, keep this check.
		if (string.IsNullOrEmpty(sessionId)) return BadRequest("Session ID required");

		var session = _engineManager.GetSession(sessionId);
		var rawOutput = await session.EvaluateAsync(request.Fen, request.Depth);
		var result = StockfishResultParser.Parse(rawOutput);

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

	//Gets the "Context" (Holistic Features)
	[HttpPost("context")]
	public IActionResult GetContext([FromBody] EvaluateRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Fen)) return BadRequest("FEN is required");

		// This is instant, so no need for async/await usually
		var context = _analysisService.Analyze(request.Fen);

		return Ok(context);
	}
}