using ChessAnalysis.Api.Dto;
using ChessAnalysis.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChessAnalysis.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoachController : ControllerBase
{
    private readonly GeminiCoachService _coach;

    public CoachController(GeminiCoachService coach)
    {
        _coach = coach;
    }

    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] ExplainRequest request)
    {
        // Simple passthrough to the service
        var text = await _coach.GetExplanationAsync(request);
        return Ok(new ExplainResponse(text));
    }
}