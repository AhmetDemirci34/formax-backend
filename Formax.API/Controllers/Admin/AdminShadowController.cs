using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Predictions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// SHADOW WORKER SAĞLIK UCU — salt-okunur.
    ///
    /// Sürekli çalışan bir sunucuda gölge işin yaşayıp yaşamadığı buradan ölçülür:
    /// startup · last successful cycle · last error · prediction count.
    ///
    /// Hiçbir tahmin ÜRETMEZ, hiçbir satır yazmaz, hiçbir model parametresine dokunmaz.
    /// Kullanıcıya açılan bir yüzey DEĞİLDİR (admin ağı); tahmin olasılığı DÖNDÜRMEZ —
    /// yalnız sayaç ve zaman damgası.
    /// </summary>
    [ApiController]
    [Route("admin/shadow")]
    public class AdminShadowController : ControllerBase
    {
        private readonly ShadowHealthState _health;
        private readonly FormaxDbContext _db;
        private readonly PredictionEngineOptions _options;

        public AdminShadowController(ShadowHealthState health, FormaxDbContext db, PredictionEngineOptions options)
        {
            _health = health;
            _db = db;
            _options = options;
        }

        /// <summary>
        /// Gölge işin canlılığı + kalıcı tahmin sayaçları.
        /// status: RUNNING | STALE | FAILING | NOT_STARTED | DISABLED
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> Health(CancellationToken ct)
        {
            var s = _health.Snapshot();
            var nowUtc = DateTime.UtcNow;

            // Kalıcı gerçek: süreç yeniden başlasa da bu sayılar DB'de durur.
            //
            // DB ERİŞİLEMEZKEN DE CEVAP VERİLİR. Bu uç, tam olarak veritabanının düştüğü anda
            // okunacaktır; o anda 500 dönmek, arızayı görmesi gereken kişiden arıza bilgisini
            // saklamak olurdu. Sayaçlar boş gelir, worker.lastError okunabilir kalır.
            bool databaseReachable = true;
            string? databaseError = null;
            int? total = null, shadowRows = null, settled = null, distinctMatches = null;
            DateTime? lastWriteUtc = null;
            try
            {
                total = await _db.Predictions.AsNoTracking().CountAsync(ct);
                shadowRows = await _db.Predictions.AsNoTracking().CountAsync(p => p.ShadowMode, ct);
                lastWriteUtc = total == 0
                    ? (DateTime?)null
                    : await _db.Predictions.AsNoTracking().MaxAsync(p => (DateTime?)p.CreatedAt, ct);
                settled = await _db.PredictionSettlements.AsNoTracking().CountAsync(ct);
                distinctMatches = await _db.Predictions.AsNoTracking().Select(p => p.MatchId).Distinct().CountAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                databaseReachable = false;
                // Yalnız tip + mesajın ilk satırı: bağlantı dizesi bu yola girmez.
                var first = (ex.Message ?? string.Empty).Split('\n')[0].Trim();
                databaseError = ex.GetType().Name + ": " + (first.Length > 300 ? first.Substring(0, 300) : first);
            }

            // ── SHADOW B sayaçları — AYRI. Shadow A'nın hiçbir alanını etkilemez. ──────
            var bs = _health.ShadowBSnapshot();
            int? shadowBTotal = null, shadowBDistinctMatches = null, shadowBSettled = null, shadowBAdjustedRows = null;
            DateTime? lastShadowBWriteUtc = null;
            if (databaseReachable)
            {
                try
                {
                    shadowBTotal = await _db.ShadowBPredictions.AsNoTracking().CountAsync(ct);
                    shadowBDistinctMatches = await _db.ShadowBPredictions.AsNoTracking()
                        .Select(p => p.MatchId).Distinct().CountAsync(ct);
                    shadowBAdjustedRows = await _db.ShadowBPredictions.AsNoTracking()
                        .CountAsync(p => p.AdjustmentApplied, ct);
                    shadowBSettled = await _db.ShadowBPredictionSettlements.AsNoTracking().CountAsync(ct);
                    lastShadowBWriteUtc = shadowBTotal == 0
                        ? null
                        : await _db.ShadowBPredictions.AsNoTracking().MaxAsync(p => (DateTime?)p.CreatedAt, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Shadow B tabloları henüz oluşmadıysa (migration uygulanmadan) sağlık ucu
                    // ÇALIŞMAYA DEVAM EDER; Shadow A'nın görünürlüğü B'ye bağlı olamaz.
                    shadowBTotal = null;
                }
            }

            var b = new
            {
                jobStartedUtc = bs.JobStartedUtc,
                lastCycleStartedUtc = bs.LastCycleStartedUtc,
                lastSuccessfulCycleUtc = bs.LastSuccessfulCycleUtc,
                lastCycleMs = bs.LastCycleMs,
                cyclesCompleted = bs.CyclesCompleted,
                shadowBFailures = bs.CyclesFailed,
                consecutiveFailures = bs.ConsecutiveFailures,
                lastError = bs.LastError,
                lastErrorUtc = bs.LastErrorUtc,
                shadowBInserted = bs.Inserted,
                shadowBAlreadyPublished = bs.AlreadyPublished,
                shadowBAdjusted = bs.Adjusted,
                shadowBNoEvidence = bs.NoEvidence,
                shadowBConflicted = bs.Conflicted,
                shadowBSettledThisProcess = bs.SettledThisProcess,
                shadowBTotal,
                shadowBDistinctMatches,
                shadowBAdjustedRows,
                shadowBSettled,
                lastShadowBWriteUtc,
                adjustmentVersion = Formax.Infrastructure.Predictions.NewsAdjustOptions.Version
            };

            // Bir cycle'ın "bayat" sayılma eşiği: beklenen aralığın 2,5 katı + 10 dk pay.
            var expectedLoop = TimeSpan.FromHours(_options.LoopHours);
            var staleAfter = TimeSpan.FromMinutes(expectedLoop.TotalMinutes * 2.5 + 10);

            string status;
            if (!databaseReachable) status = "DB_UNREACHABLE";
            else if (s.ShadowDisabledReason is not null) status = "DISABLED";
            else if (s.ShadowJobStartedUtc is null) status = "NOT_STARTED";
            else if (s.ShadowConsecutiveFailures >= 3) status = "FAILING";
            else if (s.ShadowLastSuccessfulCycleUtc is null)
                status = nowUtc - s.ShadowJobStartedUtc.Value > staleAfter ? "STALE" : "RUNNING";
            else
                status = nowUtc - s.ShadowLastSuccessfulCycleUtc.Value > staleAfter ? "STALE" : "RUNNING";

            return Ok(new
            {
                status,
                nowUtc,

                worker = new
                {
                    processStartedUtc = s.ProcessStartedUtc,
                    processUptimeMinutes = Math.Round((nowUtc - s.ProcessStartedUtc).TotalMinutes, 1),
                    shadowJobStartedUtc = s.ShadowJobStartedUtc,
                    lastCycleStartedUtc = s.ShadowLastCycleStartedUtc,
                    lastSuccessfulCycleUtc = s.ShadowLastSuccessfulCycleUtc,
                    minutesSinceLastSuccessfulCycle = s.ShadowLastSuccessfulCycleUtc is null
                        ? (double?)null
                        : Math.Round((nowUtc - s.ShadowLastSuccessfulCycleUtc.Value).TotalMinutes, 1),
                    lastCycleMs = s.ShadowLastCycleMs,
                    cyclesCompleted = s.ShadowCyclesCompleted,
                    cyclesFailed = s.ShadowCyclesFailed,
                    consecutiveFailures = s.ShadowConsecutiveFailures,
                    lastError = s.ShadowLastError,
                    lastErrorUtc = s.ShadowLastErrorUtc,
                    disabledReason = s.ShadowDisabledReason,
                    expectedLoopHours = _options.LoopHours,
                    horizonDays = _options.HorizonDays,
                    modelFingerprint = s.ShadowModelFingerprint,
                    ratingEvidenceThroughUtc = s.ShadowRatingEvidenceThroughUtc
                },

                // Bu SÜREÇ boyunca üretilen cycle sonuçları (yeniden başlatmada sıfırlanır).
                processCounters = new
                {
                    lastCycleUpcomingSeen = s.ShadowLastCycleSeen,
                    inserted = s.ShadowInserted,
                    alreadyPublished = s.ShadowAlreadyPublished,
                    conflicted = s.ShadowConflicted,
                    failedMatches = s.ShadowFailedMatches
                },

                // Kalıcı gerçek (DB). DB erişilemezse sayaçlar null gelir.
                databaseReachable,
                databaseError,
                predictions = new
                {
                    total,
                    shadow = shadowRows,
                    distinctMatches,
                    settled,
                    lastWriteUtc,
                    minutesSinceLastWrite = lastWriteUtc is null
                        ? (double?)null
                        : Math.Round((nowUtc - lastWriteUtc.Value).TotalMinutes, 1)
                },

                settlement = new
                {
                    jobStartedUtc = s.SettlementJobStartedUtc,
                    lastSuccessfulCycleUtc = s.SettlementLastSuccessfulCycleUtc,
                    cyclesCompleted = s.SettlementCyclesCompleted,
                    cyclesFailed = s.SettlementCyclesFailed,
                    settledThisProcess = s.SettlementSettled,
                    lastError = s.SettlementLastError,
                    lastErrorUtc = s.SettlementLastErrorUtc
                },

                // ── SHADOW B — AYRI SAYAÇLAR ────────────────────────────────────────
                // Yukarıdaki Shadow A alanlarının hiçbiri bu blok yüzünden değişmedi.
                shadowB = b
            });
        }

        /// <summary>
        /// A/B KARŞILAŞTIRMA — aynı gerçek maçlarda habersiz (A) ve kanıt düzeltmeli (B)
        /// modelin skorlanması. Salt-okuma; hiçbir satır yazmaz.
        ///
        /// Yalnız İKİSİ DE settle edilmiş tahminler karşılaştırılır: A'nın skorlandığı bir maçta
        /// B yoksa o maç hiçbir tarafa yazılmaz — aksi hâlde iki model farklı maç kümesinde
        /// ölçülür ve karşılaştırma anlamını yitirirdi.
        /// </summary>
        [HttpGet("ab-report")]
        public async Task<IActionResult> AbReport(CancellationToken ct)
        {
            // Her maç için A'nın ve B'nin EN SON settle edilmiş tahmini.
            var aRows = await (
                from p in _db.Predictions.AsNoTracking()
                join s in _db.PredictionSettlements.AsNoTracking() on p.PredictionId equals s.PredictionId
                where p.PredictionEligible && p.HomeProbability != null
                select new
                {
                    p.MatchId, p.Sequence, p.PredictionId,
                    H = p.HomeProbability!.Value, D = p.DrawProbability!.Value, A = p.AwayProbability!.Value,
                    s.ActualResult, s.ActualHomeGoals, s.ActualAwayGoals
                }).ToListAsync(ct);

            var bRows = await (
                from p in _db.ShadowBPredictions.AsNoTracking()
                join s in _db.ShadowBPredictionSettlements.AsNoTracking() on p.PredictionId equals s.PredictionId
                where p.PredictionEligible && p.HomeProbability != null
                select new
                {
                    p.MatchId, p.Sequence, p.PredictionId, p.BasePredictionId,
                    H = p.HomeProbability!.Value, D = p.DrawProbability!.Value, A = p.AwayProbability!.Value,
                    p.AdjustmentApplied, p.EvidenceCount, p.AppliedTilt,
                    s.ActualResult
                }).ToListAsync(ct);

            var aByMatch = aRows.GroupBy(r => r.MatchId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Sequence).First());
            var bByMatch = bRows.GroupBy(r => r.MatchId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Sequence).First());

            var paired = aByMatch.Keys.Intersect(bByMatch.Keys).OrderBy(x => x).ToList();

            var rows = new List<object>();
            double llA = 0, llB = 0, brA = 0, brB = 0, rpsA = 0, rpsB = 0;
            int top1A = 0, top1B = 0, bBetter = 0, bWorse = 0, bEqual = 0, adjusted = 0, noAdjustment = 0;

            foreach (var matchId in paired)
            {
                var a = aByMatch[matchId];
                var b = bByMatch[matchId];
                var outcome = a.ActualResult;

                var (la, ba, ra, t1a) = Score(a.H, a.D, a.A, outcome);
                var (lb, bb, rb, t1b) = Score(b.H, b.D, b.A, outcome);

                llA += la; llB += lb; brA += ba; brB += bb; rpsA += ra; rpsB += rb;
                top1A += t1a; top1B += t1b;

                if (b.AdjustmentApplied) adjusted++; else noAdjustment++;

                // "Daha iyi" = gerçekleşen sonucun log-loss'u daha düşük.
                if (Math.Abs(la - lb) < 1e-12) bEqual++;
                else if (lb < la) bBetter++;
                else bWorse++;

                rows.Add(new
                {
                    matchId,
                    a = new { a.PredictionId, home = R(a.H), draw = R(a.D), away = R(a.A), logLoss = R(la) },
                    b = new
                    {
                        b.PredictionId, b.BasePredictionId, home = R(b.H), draw = R(b.D), away = R(b.A),
                        logLoss = R(lb), b.AdjustmentApplied, b.EvidenceCount, appliedTilt = R(b.AppliedTilt)
                    },
                    actualResult = outcome,
                    actualScore = $"{a.ActualHomeGoals}-{a.ActualAwayGoals}"
                });
            }

            var n = paired.Count;
            object Metrics(double ll, double br, double rps, int t1) => n == 0
                ? new { n = 0, logLoss = (double?)null, brier = (double?)null, rps = (double?)null, top1Accuracy = (double?)null }
                : new { n, logLoss = (double?)R(ll / n), brier = (double?)R(br / n), rps = (double?)R(rps / n), top1Accuracy = (double?)R((double)t1 / n) };

            return Ok(new
            {
                comparedMatches = n,
                note = n == 0
                    ? "Henüz aynı maçta hem A hem B settle edilmiş tahmin yok — Shadow B açıldıktan sonra oynanan maçlar gerekir."
                    : "Yalnız ikisi de settle edilmiş maçlar karşılaştırıldı.",
                shadowA = Metrics(llA, brA, rpsA, top1A),
                shadowB = Metrics(llB, brB, rpsB, top1B),
                verdict = new
                {
                    bBetterMatches = bBetter,
                    bWorseMatches = bWorse,
                    bIdenticalMatches = bEqual,
                    matchesWithAdjustment = adjusted,
                    matchesWithoutAdjustment = noAdjustment
                },
                matches = rows
            });
        }

        /// <summary>LogLoss, Brier ve RPS — standart tanımlar, tek yerde.</summary>
        private static (double logLoss, double brier, double rps, int top1) Score(
            double pH, double pD, double pA, string actualResult)
        {
            var yH = actualResult == "HomeWin" ? 1.0 : 0.0;
            var yD = actualResult == "Draw" ? 1.0 : 0.0;
            var yA = actualResult == "AwayWin" ? 1.0 : 0.0;

            var pActual = Math.Max(yH * pH + yD * pD + yA * pA, 1e-15);
            var logLoss = -Math.Log(pActual);

            var brier = (pH - yH) * (pH - yH) + (pD - yD) * (pD - yD) + (pA - yA) * (pA - yA);

            // RPS — sıralı sonuç uzayı (Home, Draw, Away) üzerinde kümülatif kare fark.
            var c1 = (pH - yH);
            var c2 = (pH + pD) - (yH + yD);
            var rps = (c1 * c1 + c2 * c2) / 2.0;

            var maxP = Math.Max(pH, Math.Max(pD, pA));
            var predicted = maxP == pH ? "HomeWin" : maxP == pD ? "Draw" : "AwayWin";
            var top1 = predicted == actualResult ? 1 : 0;

            return (logLoss, brier, rps, top1);
        }

        private static double R(double v) => Math.Round(v, 6, MidpointRounding.ToEven);
    }

    /// <summary>
    /// LIVENESS — süreç ayakta mı? Hiçbir bağımlılığa dokunmaz (DB dahil), böylece bir
    /// supervisor / yeniden başlatma kuralı DB yavaşlığını "uygulama öldü" sanmaz.
    /// </summary>
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(new
        {
            status = "ok",
            utc = DateTime.UtcNow
        });
    }
}
