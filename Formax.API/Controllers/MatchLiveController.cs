using Formax.API.Common;
using Formax.API.Contracts.Live;
using Formax.Application.AI.Contexts;
using Formax.Application.Interfaces;
using Formax.Application.Services;
using Formax.Application.UseCases.Live;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/live")]
    public class MatchLiveController : ControllerBase
    {
        private readonly GetLiveMatchReadingUseCase _getLiveMatchReadingUseCase;
        private readonly GetLiveMatchTimelineUseCase _getLiveMatchTimelineUseCase;
        private readonly GetLiveMatchScreenUseCase _getLiveMatchScreenUseCase;
        private readonly GetLiveMatchAiAnalysisUseCase _getLiveMatchAiAnalysisUseCase;
        private readonly UserExperienceContextFactory _contextFactory;
        private readonly EmitMatchEventUseCase _emitMatchEventUseCase;
        private readonly IUserInterestQueryService _interestQueryService;

        public MatchLiveController(
            GetLiveMatchReadingUseCase getLiveMatchReadingUseCase,
            GetLiveMatchTimelineUseCase getLiveMatchTimelineUseCase,
            GetLiveMatchScreenUseCase getLiveMatchScreenUseCase,
            GetLiveMatchAiAnalysisUseCase getLiveMatchAiAnalysisUseCase,
            UserExperienceContextFactory contextFactory,
            EmitMatchEventUseCase emitMatchEventUseCase,
            IUserInterestQueryService interestQueryService)
        {
            _getLiveMatchReadingUseCase = getLiveMatchReadingUseCase;
            _getLiveMatchTimelineUseCase = getLiveMatchTimelineUseCase;
            _getLiveMatchScreenUseCase = getLiveMatchScreenUseCase;
            _getLiveMatchAiAnalysisUseCase = getLiveMatchAiAnalysisUseCase;
            _contextFactory = contextFactory;
            _emitMatchEventUseCase = emitMatchEventUseCase;
            _interestQueryService = interestQueryService;
        }

        // 🔴 Canlı okuma
        [HttpGet("{matchId:int}")]
        public async Task<IActionResult> GetLive(int matchId)
        {
            var result = await _getLiveMatchReadingUseCase.ExecuteAsync(matchId);
            return Ok(result);
        }

        // 🧱 Timeline
        [HttpGet("{matchId:int}/events")]
        public async Task<IActionResult> GetMatchEvents(int matchId)
        {
            var events = await _getLiveMatchTimelineUseCase.ExecuteAsync(matchId);
            return Ok(events);
        }

        // 🧾 Event emit (FAZ-2 / ADIM-1)
        [HttpPost("{matchId:int}/events")]
        public async Task<IActionResult> EmitMatchEvent(int matchId, [FromBody] EmitMatchEventRequest request)
        {
            if (request is null)
                return BadRequest();

            if (string.IsNullOrWhiteSpace(request.EventType))
                return BadRequest("EventType zorunlu.");

            if (string.IsNullOrWhiteSpace(request.TeamName))
                return BadRequest("TeamName zorunlu.");

            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest("Description zorunlu.");

            if (request.Minute < 0)
                return BadRequest("Minute negatif olamaz.");

            var userId = User.GetUserId();

            await _emitMatchEventUseCase.ExecuteAsync(
                matchId,
                request.EventType.Trim(),
                request.Minute,
                request.TeamName.Trim(),
                string.IsNullOrWhiteSpace(request.PlayerName) ? null : request.PlayerName.Trim(),
                request.Description.Trim(),
                userId
            );

            return Ok(new { ok = true });
        }

        // 📱 Canlı ekran
        [HttpGet("{matchId:int}/screen")]
        public async Task<IActionResult> GetLiveMatchScreen(int matchId)
        {
            var screen = await _getLiveMatchScreenUseCase.ExecuteAsync(matchId);
            return Ok(screen);
        }

        // 🤖 CANLI MAÇ – AI ANALİZ
        [HttpGet("{matchId:int}/ai-analysis")]
        public async Task<IActionResult> GetLiveMatchAiAnalysis(
            int matchId,
            [FromHeader(Name = "X-Experience-Matches")] int matchesWithAi = 0,
            [FromHeader(Name = "X-Experience-AiCount")] int aiContextCount = 0,
            [FromHeader(Name = "X-Experience-RegisterHint")] bool hasSeenHint = false,
            [FromHeader(Name = "X-Experience-LastType")] string? lastType = null)
        {
            UserExperienceContext ctx;

            var userId = User.GetUserId();

            if (userId.HasValue)
            {
                ctx = _contextFactory.CreateForUser(userId.Value);
            }
            else
            {
                ctx = _contextFactory.CreateForAnon(
                    matchesWithAi,
                    aiContextCount,
                    hasSeenHint,
                    lastType);
            }

            var result = await _getLiveMatchAiAnalysisUseCase.ExecuteAsync(matchId, ctx);
            return Ok(result);
        }

        // ⭐ Interest Top (FAZ-2 / ADIM-5)
        [HttpGet("interest/top/{layer}")]
        public async Task<IActionResult> GetTopInterest(string layer)
        {
            var userId = User.GetUserId();
            if (!userId.HasValue)
                return Unauthorized();

            var result = await _interestQueryService.GetTopAsync(
                userId.Value,
                layer,
                5);

            return Ok(result);
        }
    }
}