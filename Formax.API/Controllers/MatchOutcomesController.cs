using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// AI OLASI SONUÇLAR — maçın güncel tahmin snapshot'ı. Keşfet ve Maç Detayı AYNI yükü okur (aynı SnapshotId, ModelVersion,
    /// yüzdeler, gerekçe kodları, hesaplama zamanı). Salt DB: olasılık hesaplanmaz, dış istek ya da LLM çağrısı yapılmaz.
    /// </summary>
    [ApiController]
    [Route("api/matches")]
    public sealed class MatchOutcomesController : ControllerBase
    {
        private readonly IMatchOutcomeSnapshotReader _reader;
        public MatchOutcomesController(IMatchOutcomeSnapshotReader reader) => _reader = reader;

        [HttpGet("{matchId:int}/outcomes")]
        public async Task<IActionResult> Get(int matchId, CancellationToken ct)
            => Ok(await _reader.GetCurrentAsync(matchId, ct));
    }
}
