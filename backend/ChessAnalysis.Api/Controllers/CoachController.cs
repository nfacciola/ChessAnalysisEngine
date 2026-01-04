using Microsoft.AspNetCore.Mvc;
using ChessAnalysis.Api.Services;

namespace ChessAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoachController : ControllerBase
{
    private readonly GeminiCoachService _coachService;

    public CoachController(GeminiCoachService coachService)
    {
        _coachService = coachService;
    }

    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] CoachContext context)
    {
        var explanation = await _coachService.ExplainMoveAsync(context);
        return Ok(new { explanation });
    }
}