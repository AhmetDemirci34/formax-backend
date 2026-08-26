using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// Phase 7 — Social Discovery yönetimi: doğrulanmış resmi hesap registry seeding + manuel
    /// discovery tetiği. Üretim tetikleyicisi SocialDiscoveryJob 5 dk döngüsüdür. Hesaplar
    /// body ile eklenir (kodda hardcode liste YOK); yalnız verified resmi hesaplar girilmelidir.
    /// </summary>
    [ApiController]
    [Route("admin/social")]
    public class AdminSocialController : ControllerBase
    {
        private readonly IOfficialSocialAccountRepository _accounts;
        private readonly SocialDiscoveryJob _job;

        public AdminSocialController(IOfficialSocialAccountRepository accounts, SocialDiscoveryJob job)
        {
            _accounts = accounts;
            _job = job;
        }

        public sealed class AccountRequest
        {
            public string ScopeType { get; set; } = "Team";
            public string? ExternalTeamId { get; set; }
            public int LeagueId { get; set; }
            public string Platform { get; set; } = "YouTube";
            public string Handle { get; set; } = "";
            public string FeedUrl { get; set; } = "";
            public string AccountName { get; set; } = "";
            public bool Verified { get; set; } = true;
        }

        [HttpPost("accounts")]
        public async Task<IActionResult> UpsertAccount([FromBody] AccountRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Handle))
                return BadRequest(new { error = "Handle required" });

            await _accounts.UpsertAsync(new OfficialSocialAccount
            {
                ScopeType = req.ScopeType,
                ExternalTeamId = req.ExternalTeamId,
                LeagueId = req.LeagueId,
                Platform = req.Platform,
                Handle = req.Handle.Trim(),
                FeedUrl = req.FeedUrl?.Trim() ?? "",
                AccountName = req.AccountName?.Trim() ?? "",
                Verified = req.Verified,
                Active = true
            }, ct);
            await _accounts.SaveChangesAsync(ct);

            return Ok(new { message = "official account upserted", req.Platform, req.Handle });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            var added = await _job.RunCycleAsync(ct);
            return Ok(new { message = "social discovery cycle ran", added });
        }
    }
}
