using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.PostMatch;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// OYNATICI HATA BİLDİRİMİ — gerçek YouTube oynatıcısı 100/101/150/152 (kaldırılmış / gömme / bölge engeli) bildirdiğinde
    /// ekran bunu backend'e yazar: kayıt SourceBlocked olur, maç kalıcı arama kuyruğuna döner. Bu uç keşif YAPMAZ ve dış
    /// isteğe çıkmaz; yalnız o an oynatılabilir bir kaydı kapatabilir. İstemci başına dakikada 20 bildirim sınırı vardır.
    /// </summary>
    [ApiController]
    [Route("api/matches")]
    public sealed class MatchVideoPlaybackController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, (DateTime Window, int Count)> Rate = new();

        public sealed record PlaybackErrorRequest(string? VideoId, string? EmbedUrl, int Code);

        [HttpPost("{matchId:int}/videos/playback-error")]
        public async Task<IActionResult> PlaybackError(int matchId, [FromBody] PlaybackErrorRequest body,
            [FromServices] MatchVideoPlaybackReportService service, CancellationToken ct)
        {
            var client = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;
            var entry = Rate.AddOrUpdate(client, _ => (now, 1),
                (_, e) => now - e.Window > TimeSpan.FromMinutes(1) ? (now, 1) : (e.Window, e.Count + 1));
            if (entry.Count > 20) return StatusCode(429, new { error = "too many playback reports" });

            var result = await service.ReportAsync(matchId, body.VideoId ?? body.EmbedUrl, body.Code, now, ct);
            return Ok(new
            {
                accepted = result.Accepted,
                outcome = result.Outcome,
                queueState = result.QueueState,
                nextAttemptUtc = result.NextAttemptUtc
            });
        }
    }
}
