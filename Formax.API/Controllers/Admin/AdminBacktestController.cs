using Microsoft.AspNetCore.Mvc;
using Formax.Application.Services.Backtest;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/backtest")]
    public class AdminBacktestController : ControllerBase
    {
        private readonly BacktestService _backtestService;
        private readonly PreMatchFinalBackfillService _backfillService;

        public AdminBacktestController(BacktestService backtestService, PreMatchFinalBackfillService backfillService)
        {
            _backtestService = backtestService;
            _backfillService = backfillService;
        }

        /// <summary>
        /// Backtest çalıştırır.
        /// Örnek:
        /// GET /api/admin/backtest/run?sampleSize=200&requireSnapshot=false&minSapma=0
        /// GET /api/admin/backtest/run?sampleSize=300&requireSnapshot=true&minSapma=75
        /// </summary>
        [HttpGet("run")]
        public IActionResult Run([FromQuery] int sampleSize = 200, [FromQuery] bool requireSnapshot = false, [FromQuery] int minSapma = 0)
        {
            var result = _backtestService.Run(new BacktestRunRequest
            {
                SampleSize = sampleSize,
                RequireSnapshot = requireSnapshot,
                MinSapma = minSapma
            });

            return Ok(result);
        }

        /// <summary>
        /// Mini Backfill:
        /// Finished maçlarda Source=PreMatchFinal snapshot eksikse, "maçtan önceki en son" snapshot'tan türetip yazar.
        /// Backtest requireSnapshot=true için sample oluşmasını sağlar.
        ///
        /// Örnek:
        /// POST /api/admin/backtest/backfill-prematchfinal?take=300
        /// </summary>
        [HttpPost("backfill-prematchfinal")]
        public async Task<IActionResult> BackfillPreMatchFinal([FromQuery] int take = 300, CancellationToken ct = default)
        {
            var result = await _backfillService.BackfillAsync(take, ct);
            return Ok(result);
        }
    }
}
