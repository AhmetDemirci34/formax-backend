using System;
using System.Threading.Tasks;
using Formax.API.Contracts.Live;
using Formax.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/live/oynanma")]
    public sealed class LiveOynanmaController : ControllerBase
    {
        private readonly ILiveOynanmaSignalStore _store;

        public LiveOynanmaController(ILiveOynanmaSignalStore store)
        {
            _store = store;
        }

        // ✅ Test amaçlı: canlı oynanma sinyalini set et
        [HttpPost("{matchId:int}")]
        public async Task<IActionResult> Upsert(int matchId, [FromBody] UpsertLiveOynanmaRequest request)
        {
            if (request is null)
                return BadRequest();

            var side = (request.Side ?? "Denge").Trim();
            if (string.IsNullOrWhiteSpace(side))
                side = "Denge";

            // Basit validasyon
            if (request.Intensity < 0 || request.Intensity > 100)
                return BadRequest("Intensity 0-100 aralığında olmalı.");

            if (request.OddsMove < 0 || request.OddsMove > 100)
                return BadRequest("OddsMove 0-100 aralığında olmalı.");

            if (request.MediaTrend < 0 || request.MediaTrend > 100)
                return BadRequest("MediaTrend 0-100 aralığında olmalı.");

            // Side allowlist (case-insensitive)
            // FORMAX dil kimliği: None yok. Draw/None -> Denge.
            var normalized = side.Equals("home", StringComparison.OrdinalIgnoreCase) ? "Home"
                          : side.Equals("away", StringComparison.OrdinalIgnoreCase) ? "Away"
                          : side.Equals("draw", StringComparison.OrdinalIgnoreCase) ? "Denge"
                          : side.Equals("denge", StringComparison.OrdinalIgnoreCase) ? "Denge"
                          : side.Equals("none", StringComparison.OrdinalIgnoreCase) ? "Denge"
                          : "Denge";

            await _store.UpsertAsync(matchId, new OynanmaSinyalleri
            {
                Side = normalized,
                Intensity = request.Intensity,
                OddsMove = request.OddsMove,
                MediaTrend = request.MediaTrend,
                LastUpdatedAtUtc = DateTime.UtcNow
            });

            return Ok(new { ok = true, matchId, side = normalized, intensity = request.Intensity });
        }

        // 🔎 Oku (varsa)
        [HttpGet("{matchId:int}")]
        public async Task<IActionResult> Get(int matchId)
        {
            var v = await _store.GetAsync(matchId);
            if (v == null)
                return Ok(new { matchId, hasLive = false });

            return Ok(new
            {
                matchId,
                hasLive = true,
                side = v.Side,
                intensity = v.Intensity,
                oddsMove = v.OddsMove,
                mediaTrend = v.MediaTrend,
                lastUpdatedAtUtc = v.LastUpdatedAtUtc
            });
        }

        // 🧹 Sil
        [HttpDelete("{matchId:int}")]
        public async Task<IActionResult> Remove(int matchId)
        {
            var ok = await _store.RemoveAsync(matchId);
            return Ok(new { ok, matchId });
        }
    }
}
