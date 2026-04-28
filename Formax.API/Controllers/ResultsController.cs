using Microsoft.AspNetCore.Mvc;
using Formax.Engine;

namespace Formax.API.Controllers;

[ApiController]
[Route("api/results")]
public class ResultsController : ControllerBase
{
    private readonly ResultEngine _engine;

    public ResultsController()
    {
        _engine = new ResultEngine();
    }

    [HttpPost("match-result")]
    public IActionResult GetMatchResult([FromBody] ResultRequest request)
    {
        var result = _engine.Evaluate(request.MatchId, request.UserPick);
        return Ok(result);
    }
}