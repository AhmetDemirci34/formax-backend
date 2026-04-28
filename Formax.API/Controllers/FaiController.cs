using System.Security.Claims;
using Formax.Application.Interfaces.FaiOverview;
using Formax.Application.UseCases.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FaiController : ControllerBase
{
    private readonly IFaiOverviewService _faiOverviewService;
    private readonly ResolveHomeAIStateUseCase _resolveHomeAIStateUseCase;

    public FaiController(
        IFaiOverviewService faiOverviewService,
        ResolveHomeAIStateUseCase resolveHomeAIStateUseCase)
    {
        _faiOverviewService = faiOverviewService;
        _resolveHomeAIStateUseCase = resolveHomeAIStateUseCase;
    }

    // ✅ /api/fai/overview  (JWT zorunlu)
    [Authorize]
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var items = await _faiOverviewService.GetOverviewAsync(cancellationToken);
        return Ok(items);
    }

    // ✅ /api/fai/state
    // Token varsa isPremium claim'den okur.
    // Token yoksa Free kabul eder.
    [AllowAnonymous]
    [HttpGet("state")]
    public IActionResult GetState()
    {
        var homeState = _resolveHomeAIStateUseCase.Execute();
        var s = homeState.ToString().ToUpperInvariant();

        // Short/Extended => içerik var, Silent => içerik yok
        var hasContent = !s.Contains("SILENT");

        // UI kontratı: SILENT | ACTIVE | LOCKED
        if (!hasContent)
            return Ok(new { state = "SILENT" });

        var isPremium = ReadIsPremiumFromJwtOrFalse();

        if (isPremium)
            return Ok(new { state = "ACTIVE" });

        return Ok(new { state = "LOCKED" });
    }

    private bool ReadIsPremiumFromJwtOrFalse()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return false;

        var v = User.FindFirstValue("isPremium");
        if (string.IsNullOrWhiteSpace(v)) return false;

        return v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("1");
    }
}
