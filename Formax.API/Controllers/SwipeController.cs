using Formax.Application.AI.Learning;
using Formax.Application.DTOs;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/swipe")]
public class SwipeController : ControllerBase
{
    private readonly IUserTasteProfileRepository _repo;
    private readonly UserTasteEngine _engine;
    private readonly IUserActionService _userActionService;

    public SwipeController(
        IUserTasteProfileRepository repo,
        UserTasteEngine engine,
        IUserActionService userActionService)
    {
        _repo = repo;
        _engine = engine;
        _userActionService = userActionService;
    }

    [HttpPost]
    public async Task<IActionResult> Swipe([FromBody] SwipeRequest req)
    {
        var userId = 1;

        int actionType = req.Action switch
        {
            "LIKE" => 1,
            "SKIP" => -1,
            "VIEW" => 0,
            "DETAIL" => 2,
            "FOLLOW" => 3,
            _ => 0
        };

        await _userActionService.Create(new UserActionDto
        {
            UserId = userId,
            MatchId = req.MatchId,
            ActionType = actionType,
            Odds = req.Odds,

            ViewDurationMs = req.ViewDurationMs,
            OpenedDetail = req.Action == "DETAIL" || req.ViewDurationMs > 5000,
            Followed = req.Action == "FOLLOW",

            // 🔥 KRİTİK
            Team = req.Team
        });

        var profile = await _repo.GetByUserId(userId)
                      ?? new UserTasteProfile { UserId = userId };

        var updated = _engine.Update(
            profile,
            req.ConfidenceLabel,
            req.IsTrending,
            req.OddsDrop > 0,
            req.Action == "LIKE"
        );

        await _repo.Save(updated);

        return Ok();
    }
}