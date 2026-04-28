using Formax.Application.Services.Intelligence;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/interaction")]
public class InteractionController : ControllerBase
{
    private readonly RealtimeLearningOrchestrator _orchestrator;

    public InteractionController(RealtimeLearningOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost]
    public async Task<IActionResult> Track(int userId, string action)
    {
        await _orchestrator.ProcessInteraction(userId, action);
        return Ok();
    }
}