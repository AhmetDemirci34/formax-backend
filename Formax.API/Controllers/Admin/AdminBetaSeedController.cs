using Formax.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// TEST-ONLY trigger for the synthetic beta dataset (Sprint 15F).
    /// Idempotent: re-running is a no-op once seeded. Not part of any schedule.
    /// </summary>
    [ApiController]
    [Route("admin/beta-seed")]
    public class AdminBetaSeedController : ControllerBase
    {
        private readonly FormaxDbContext _db;

        public AdminBetaSeedController(FormaxDbContext db)
        {
            _db = db;
        }

        [HttpPost("run")]
        public IActionResult Run()
        {
            var created = SyntheticBetaSeed.Seed(_db);
            return Ok(new { actionsCreated = created });
        }
    }
}
