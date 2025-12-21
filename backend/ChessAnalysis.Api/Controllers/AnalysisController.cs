using Microsoft.AspNetCore.Mvc;

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

		return Ok(new
		{
			fileName = pgnFile.FileName,
			size = pgnFile.Length
		});
	}
}