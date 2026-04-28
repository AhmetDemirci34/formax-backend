using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Formax.Application.DTOs;
using Formax.Application.AI.Learning;
using Formax.Infrastructure.Data;

[ApiController]
[Route("api/user/actions")]
public class UserActionsController : ControllerBase
{
    private readonly IUserActionService _service;
    private readonly UserLearningService _learning;
    private readonly FormaxDbContext _context;

    public UserActionsController(
        IUserActionService service,
        UserLearningService learning,
        FormaxDbContext context)
    {
        _service = service;
        _learning = learning;
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserActionDto dto)
    {
        // 🔒 DUPLICATE CHECK
        var exists = await _context.UserActions
            .AnyAsync(x => x.UserId == dto.UserId && x.MatchId == dto.MatchId);

        if (exists)
        {
            return Ok(new { message = "duplicate ignored" });
        }

        // 🔥 MAPPING FIX (EN KRİTİK)
        if (dto.ActionType != 0 && dto.ActionType != 1)
        {
            return BadRequest("Invalid ActionType");
        }

        if (dto.Odds <= 0)
        {
            dto.Odds = 1; // fallback (boş gelirse)
        }

        await _service.Create(dto);
        await _learning.UpdateUserProfile(dto);

        return Ok(new { message = "saved" });
    }
}