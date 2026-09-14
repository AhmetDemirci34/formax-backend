using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Formax.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Formax.API.Middleware
{
    /// <summary>
    /// /api/matches/{id}/detail İSTEK ÖLÇÜMÜ — korelasyon kimliği + aşama süreleri.
    ///
    /// Toplam süre yanıt gövdesi yazıldıktan sonra ölçülür (serileştirme dahil). İşleyici süresi
    /// denetleyiciden <see cref="HandlerMsKey"/> ile gelir; fark serileştirme + ara katmanlardır.
    /// DB istatistiği (bağlantı açma beklemesi, komut sayısı/süresi) <see cref="DbRequestScope"/>'tan okunur.
    /// </summary>
    public sealed class DetailTimingMiddleware
    {
        public const string HandlerMsKey = "formax.detail.handlerMs";
        public const string HeaderName = "X-Correlation-Id";
        private static readonly Regex DetailPath = new(@"^/api/matches/(\d+)/detail/?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly RequestDelegate _next;
        private readonly RuntimeHealthMonitor _monitor;
        private readonly ILogger<DetailTimingMiddleware> _log;

        public DetailTimingMiddleware(RequestDelegate next, RuntimeHealthMonitor monitor, ILogger<DetailTimingMiddleware> log)
        {
            _next = next; _monitor = monitor; _log = log;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            var m = DetailPath.Match(ctx.Request.Path.Value ?? string.Empty);
            if (!m.Success) { await _next(ctx); return; }

            var cid = ctx.Request.Headers.TryGetValue(HeaderName, out var given) && given.ToString().Length is > 0 and <= 64
                ? given.ToString()
                : Guid.NewGuid().ToString("N")[..12];
            ctx.Response.Headers[HeaderName] = cid;
            ctx.TraceIdentifier = cid;

            var stats = DbRequestScope.Begin(cid);
            var sw = Stopwatch.StartNew();
            try
            {
                await _next(ctx);
            }
            finally
            {
                sw.Stop();
                DbRequestScope.End();
                var handlerMs = ctx.Items.TryGetValue(HandlerMsKey, out var h) && h is long l ? (int)l : -1;
                var total = (int)sw.ElapsedMilliseconds;
                var timing = new DetailTiming(
                    DateTime.UtcNow, cid, int.Parse(m.Groups[1].Value), ctx.Response.StatusCode, total, handlerMs,
                    handlerMs >= 0 ? Math.Max(0, total - handlerMs) : -1,
                    stats.Commands, (int)stats.CommandMs, (int)stats.MaxCommandMs,
                    stats.ConnectionOpens, (int)stats.ConnectionWaitMs, (int)stats.MaxConnectionWaitMs,
                    stats.SlowestSql, ctx.RequestAborted.IsCancellationRequested);
                _monitor.RecordDetail(timing);
                var level = total > 2000 || timing.Aborted ? LogLevel.Warning : LogLevel.Information;
                _log.Log(level,
                    "[DETAIL-HTTP] cid={Cid} match={MatchId} status={Status} total={Total}ms handler={Handler}ms serialize={Serialize}ms db={DbN}/{DbMs}ms dbMax={DbMax}ms connOpens={Opens} connWait={ConnWait}ms connWaitMax={ConnWaitMax}ms aborted={Aborted}",
                    cid, timing.MatchId, timing.Status, total, handlerMs, timing.SerializeMs, timing.DbCommands, timing.DbMs,
                    timing.DbMaxMs, timing.ConnOpens, timing.ConnWaitMs, timing.ConnWaitMaxMs, timing.Aborted);
            }
        }
    }
}
