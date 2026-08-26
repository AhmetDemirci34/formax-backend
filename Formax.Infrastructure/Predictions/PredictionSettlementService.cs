using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Predictions
{
    public sealed class SettlementReport
    {
        public int Candidates { get; set; }
        public int Settled { get; set; }
        public int AlreadySettled { get; set; }
        public int NoResultYet { get; set; }
        public int RejectedAsEarly { get; set; }
        public int Failed { get; set; }
        public long ElapsedMs { get; set; }
    }

    /// <summary>
    /// Maç bittikten sonra tahmine gerçek sonucu iliştirir.
    ///
    /// Settlement tahmin satırına DOKUNMAZ - dokunamaz: sonuç ayrı tabloda durur ve
    /// <c>Predictions</c> üzerinde UPDATE veritabanı trigger'ı tarafından geri alınır. Bu servis
    /// yalnız INSERT yapar.
    ///
    /// Reddedilmiş tahmin de settle edilir (kayıt için) ama skorlanamaz: olasılığı olmadığı için
    /// metrik hesabına giremez.
    /// </summary>
    public sealed class PredictionSettlementService
    {
        private readonly FormaxDbContext _db;
        private readonly ILogger<PredictionSettlementService> _logger;

        public PredictionSettlementService(FormaxDbContext db, ILogger<PredictionSettlementService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<SettlementReport> SettleAsync(int maxRows, CancellationToken ct = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = new SettlementReport();
            var now = DateTime.UtcNow;

            // Henüz settle edilmemiş, maçı geçmişte kalmış tahminler
            var pending = await (
                from p in _db.Predictions.AsNoTracking()
                where !_db.PredictionSettlements.Any(s => s.PredictionId == p.PredictionId)
                   && p.MatchDate < now
                orderby p.MatchDate
                select new { p.PredictionId, p.MatchId, p.MatchDate, p.PredictionTimestamp })
                .Take(maxRows).ToListAsync(ct);

            report.Candidates = pending.Count;
            if (pending.Count == 0) { report.ElapsedMs = sw.ElapsedMilliseconds; return report; }

            var matchIds = pending.Select(p => p.MatchId).Distinct().ToList();
            var results = await _db.Matches.AsNoTracking()
                .Where(m => matchIds.Contains(m.Id) && m.Status == "Finished")
                .Select(m => new { m.Id, m.HomeScore, m.AwayScore, m.MatchDate })
                .ToListAsync(ct);
            var byMatch = results.ToDictionary(r => r.Id);

            foreach (var p in pending)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!byMatch.TryGetValue(p.MatchId, out var r)) { report.NoResultYet++; continue; }

                    // Sonuç, tahminden önceki bir tarihte olamaz.
                    if (r.MatchDate.Date < p.PredictionTimestamp.Date) { report.RejectedAsEarly++; continue; }

                    _db.PredictionSettlements.Add(new Domain.Entities.PredictionSettlement
                    {
                        PredictionId = p.PredictionId,
                        ActualHomeGoals = r.HomeScore,
                        ActualAwayGoals = r.AwayScore,
                        ActualResult = r.HomeScore > r.AwayScore ? "HomeWin"
                                     : r.HomeScore == r.AwayScore ? "Draw" : "AwayWin",
                        SettlementTimestamp = now
                    });
                    report.Settled++;
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    _logger.LogError(ex, "[SETTLEMENT] failed for prediction {PredictionId}", p.PredictionId);
                }
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // PK ihlali = başka bir örnek aynı tahmini settle etmiş. İkinci sonuç YAZILMAZ.
                report.AlreadySettled += report.Settled;
                report.Settled = 0;
                _logger.LogWarning(ex, "[SETTLEMENT] insert refused - a settled result was not replaced");
            }

            report.ElapsedMs = sw.ElapsedMilliseconds;
            return report;
        }
    }
}
