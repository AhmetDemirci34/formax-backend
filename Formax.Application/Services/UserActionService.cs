using Formax.Application.DTOs;
using Formax.Application.Interfaces.Repositories;
using Formax.Domain.Entities;

namespace Formax.Application.Services;

public class UserActionService : IUserActionService
{
    private readonly IUserActionRepository _repo;

    public UserActionService(IUserActionRepository repo)
    {
        _repo = repo;
    }

    public async Task Create(UserActionDto dto)
    {
        var entity = new UserAction
        {
            UserId = dto.UserId,
            MatchId = dto.MatchId,
            ActionType = dto.ActionType,
            Odds = dto.Odds,
            CreatedAt = DateTime.UtcNow,
            Followed = dto.Followed,
            OpenedDetail = dto.OpenedDetail,
            ViewDurationMs = dto.ViewDurationMs,

            // 🔥 KRİTİK
            Team = dto.Team
        };

        await _repo.AddAsync(entity);
    }
}