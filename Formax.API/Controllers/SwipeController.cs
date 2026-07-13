using Formax.API.Common;
using Formax.Application.AI.Learning;
using Formax.Application.DTOs;
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

    private readonly IUserPreferenceRepository _prefRepo;
    private readonly IMatchBanditRepository _banditRepo;

    public SwipeController(
        IUserTasteProfileRepository repo,
        UserTasteEngine engine,
        IUserActionService userActionService,
        IUserPreferenceRepository prefRepo,
        IMatchBanditRepository banditRepo)
    {
        _repo = repo;
        _engine = engine;
        _userActionService = userActionService;
        _prefRepo = prefRepo;
        _banditRepo = banditRepo;
    }

    [HttpPost]
    public async Task<IActionResult> Swipe([FromBody] SwipeRequest req)
    {
        var userId = User.GetUserId() ?? 1;

        int actionType = req.Action.ToLower() switch
        {
            "like" => 1,
            "skip" => -1,
            "view" => 0,
            "detail" => 2,
            "follow" => 3,
            _ => 0
        };

        // 🔹 ACTION SAVE
        await _userActionService.Create(new UserActionDto
        {
            UserId = userId,
            MatchId = req.MatchId,
            ActionType = actionType,
            Odds = req.Odds,
            ViewDurationMs = req.ViewDurationMs,
            OpenedDetail = req.Action.ToLower() == "detail" || req.ViewDurationMs > 5000,
            Followed = req.Action.ToLower() == "follow",
            Team = req.Team
        });

        // 🔥 USER WEIGHT LEARNING (STABIL FIX)
        var weights = await _prefRepo.GetOrCreate(userId);

        if (actionType == 1) // LIKE
        {
            weights.LikeWeight = Math.Min(0.5, weights.LikeWeight + 0.005);
        }

        if (actionType == -1) // SKIP
        {
            weights.SkipWeight = Math.Max(-0.5, weights.SkipWeight - 0.005);
        }

        // team etkisi yavaş artsın
        weights.TeamWeight = Math.Min(0.1, weights.TeamWeight + 0.001);

        weights.UpdatedAt = DateTime.UtcNow;

        await _prefRepo.Update(weights);

        // 🔥 BANDIT (explore/exploit)
        await _banditRepo.IncrementImpression(req.MatchId);

        if (actionType == 1)
            await _banditRepo.IncrementLike(req.MatchId);

        // 🔹 EXISTING PROFILE UPDATE (eski sistemin devamı)
        var profile = await _repo.GetByUserId(userId)
                      ?? new UserTasteProfile { UserId = userId };

        var updated = _engine.Update(
            profile,
            req.ConfidenceLabel,
            req.IsTrending,
            req.OddsDrop > 0,
            req.Action.ToLower() == "like"
        );

        await _repo.Save(updated);

        return Ok();
    }
}