using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Predictions
{
    public sealed class ShadowBCycleReport
    {
        public int BaseRowsSeen { get; set; }
        public int Considered { get; set; }
        public int Inserted { get; set; }
        public int AlreadyPublished { get; set; }
        public int Adjusted { get; set; }
        public int NoEvidence { get; set; }
        public int IdentityUnresolved { get; set; }
        public int NotInsertedDueToConflict { get; set; }
        public int Failed { get; set; }
        public int Settled { get; set; }
        public long ElapsedMs { get; set; }
    }

    /// <summary>
    /// SHADOW B — "aynı model + maç öncesi kanıt düzeltmesi" deney hattı.
    ///
    /// SHADOW A'YA HİÇBİR ŞEKİLDE DOKUNMAZ. Bu servis <c>Predictions</c> tablosunu YALNIZ OKUR
    /// (AsNoTracking) ve kendi tablosuna yazar. Model, λ formülleri, TeamStrength, Poisson,
    /// Gate ve Prediction Contract V1 olduğu gibi kalır: B, A'nın YAYIMLADIĞI olasılığı taban
    /// alır ve üzerine deterministik bir düzeltme uygular.
    ///
    /// NEDEN A'NIN SATIRINDAN BAŞLIYOR: taban sayıların A ile birebir aynı olduğu ancak tek
    /// kaynaktan okunursa YAPISAL olarak garanti edilir. Motor ikinci kez çalıştırılsaydı
    /// "aynı sayı" bir dilek olurdu; burada bir gerçektir.
    ///
    /// KİMLİK KÖPRÜSÜ: A satırı üretim <c>Matches.Id</c>'sine bağlıdır; kanıt deposu
    /// FORMAX_MATCH_ID kullanır. İkisi <see cref="FormaxMatchIdFactory"/> ile bağlanır —
    /// AI katmanının kullandığı köprünün aynısı. Çözülemezse maç ATLANIR, uydurma kimlik yok.
    /// </summary>
    public sealed class ShadowBPredictionService
    {
        private readonly FormaxDbContext _db;
        private readonly FormaxMatchIdFactory _matchIdFactory;
        private readonly MatchIntelligenceService _intelligence;
        private readonly NewsAdjustOptions _options;
        private readonly ILogger<ShadowBPredictionService> _logger;

        public ShadowBPredictionService(
            FormaxDbContext db,
            FormaxMatchIdFactory matchIdFactory,
            MatchIntelligenceService intelligence,
            NewsAdjustOptions options,
            ILogger<ShadowBPredictionService> logger)
        {
            _db = db;
            _matchIdFactory = matchIdFactory;
            _intelligence = intelligence;
            _options = options;
            _logger = logger;
        }

        public async Task<ShadowBCycleReport> RunCycleAsync(int horizonDays, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var report = new ShadowBCycleReport();
            var nowUtc = DateTime.UtcNow;
            var windowEnd = nowUtc.AddDays(horizonDays);

            // ── 1. TABAN: her maç için Shadow A'nın EN GÜNCEL tahmini ────────────────────
            // "En güncel" = en büyük Sequence. Eski A satırları olduğu gibi durur; B onların
            // üzerine yazmaz, yalnız en son yayımlanmış olanı taban alır.
            var baseRows = await _db.Predictions.AsNoTracking()
                .Where(p => p.MatchDate > nowUtc && p.MatchDate < windowEnd && p.PredictionEligible)
                .GroupBy(p => p.MatchId)
                .Select(g => g.OrderByDescending(p => p.Sequence).First())
                .ToListAsync(ct);

            report.BaseRowsSeen = baseRows.Count;
            if (baseRows.Count == 0)
            {
                report.Settled = await SettleAsync(ct);
                report.ElapsedMs = sw.ElapsedMilliseconds;
                return report;
            }

            // ── 2. maç kimliği + takım adları ───────────────────────────────────────────
            var matchIds = baseRows.Select(b => b.MatchId).Distinct().ToList();
            var matches = await (
                from m in _db.Matches.AsNoTracking()
                join h in _db.Teams.AsNoTracking() on m.HomeTeamId equals h.Id
                join a in _db.Teams.AsNoTracking() on m.AwayTeamId equals a.Id
                where matchIds.Contains(m.Id)
                select new { m.Id, m.MatchDate, HomeName = h.Name, AwayName = a.Name })
                .ToListAsync(ct);
            var matchById = matches.ToDictionary(m => m.Id);

            var existingIds = new HashSet<string>(
                await _db.ShadowBPredictions.Select(p => p.PredictionId).ToListAsync(ct), StringComparer.Ordinal);

            foreach (var b in baseRows)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!matchById.TryGetValue(b.MatchId, out var m)
                        || string.IsNullOrWhiteSpace(m.HomeName) || string.IsNullOrWhiteSpace(m.AwayName))
                    { report.IdentityUnresolved++; continue; }

                    string formaxMatchId;
                    try { formaxMatchId = _matchIdFactory.Create(m.MatchDate, m.HomeName!, m.AwayName!); }
                    catch { report.IdentityUnresolved++; continue; }

                    report.Considered++;

                    var evidence = await LoadPreMatchEvidenceAsync(formaxMatchId, m.HomeName!, m.AwayName!, m.MatchDate, ct);

                    var adj = NewsEvidenceAdjuster.Apply(
                        b.HomeProbability ?? 0, b.DrawProbability ?? 0, b.AwayProbability ?? 0,
                        m.HomeName!, m.AwayName!, m.MatchDate, evidence, _options);

                    if (!adj.AdjustmentApplied) report.NoEvidence++; else report.Adjusted++;

                    // ── kanıt kesimi: A'nın kesimi ile KULLANILAN en yeni kanıttan geç olanı
                    var baseCutoff = b.EvidenceCutoff;
                    DateTime? cutoff = adj.LatestEvidenceUtc;
                    if (baseCutoff.HasValue && (!cutoff.HasValue || baseCutoff.Value > cutoff.Value))
                        cutoff = baseCutoff.Value;

                    var predictionId = BuildPredictionId(b.PredictionId, cutoff, adj);

                    if (existingIds.Contains(predictionId)) { report.AlreadyPublished++; continue; }

                    var row = new ShadowBPrediction
                    {
                        PredictionId = predictionId,
                        BasePredictionId = b.PredictionId,
                        Variant = "NEWS_ADJUSTED",
                        MatchId = b.MatchId,
                        CanonicalMatchId = b.CanonicalMatchId,
                        FormaxMatchId = formaxMatchId,
                        MatchDate = b.MatchDate,
                        PredictionTimestamp = nowUtc,
                        EvidenceCutoff = cutoff,
                        ModelVersion = b.ModelVersion,
                        TeamStrengthVersion = b.TeamStrengthVersion,
                        GateVersion = b.GateVersion,
                        CalibrationVersion = b.CalibrationVersion,
                        AdjustmentVersion = NewsAdjustOptions.Version,
                        BaseHomeProbability = b.HomeProbability,
                        BaseDrawProbability = b.DrawProbability,
                        BaseAwayProbability = b.AwayProbability,
                        HomeProbability = adj.HomeProbability,
                        DrawProbability = adj.DrawProbability,
                        AwayProbability = adj.AwayProbability,
                        // Kapı kararı A'dan DEVRALINIR — B kendi kapısını kurmaz.
                        PredictionEligible = b.PredictionEligible,
                        ConfidenceClass = b.ConfidenceClass,
                        GateStatus = b.GateStatus,
                        GateReason = b.GateReason,
                        EvidenceCount = adj.Used.Count,
                        HomeImpact = adj.HomeImpact,
                        AwayImpact = adj.AwayImpact,
                        AppliedTilt = adj.AppliedTilt,
                        AdjustmentApplied = adj.AdjustmentApplied,
                        AdjustmentReason = Trim(adj.Reason, 1000),
                        ShadowMode = true,
                        CreatedAt = nowUtc
                    };
                    row.ContentHash = BuildContentHash(row);

                    foreach (var u in adj.Used)
                    {
                        row.Evidence.Add(new ShadowBPredictionEvidence
                        {
                            PredictionId = predictionId,
                            EvidenceId = u.Evidence.EvidenceId,
                            EvidenceContentHash = u.Evidence.ContentHash,
                            EventType = u.Evidence.EventType,
                            RelatedTeam = Trim(u.Evidence.RelatedTeam, 200),
                            Side = u.Side,
                            Source = Trim(u.Evidence.Source, 200),
                            SourceQuality = u.Evidence.SourceQuality,
                            Confidence = u.Evidence.Confidence,
                            SourceCount = u.Evidence.SourceCount,
                            PublishedUtc = u.Evidence.PublishedUtc,
                            Weight = u.Weight
                        });
                    }

                    _db.ShadowBPredictions.Add(row);
                    existingIds.Add(predictionId);
                    report.Inserted++;
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    _logger.LogError(ex, "[SHADOW-B] failed for match {MatchId}", b.MatchId);
                }
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Benzersizlik ihlali = aynı tahmin zaten yayımlanmış. Yeniden YAZILMAZ.
                report.NotInsertedDueToConflict = report.Inserted;
                report.Inserted = 0;
                _logger.LogWarning(ex,
                    "[SHADOW-B] insert refused — {Count} prediction(s) were NOT written; nothing was rewritten.",
                    report.NotInsertedDueToConflict);
            }

            report.Settled = await SettleAsync(ct);
            report.ElapsedMs = sw.ElapsedMilliseconds;
            return report;
        }

        /// <summary>
        /// Maç öncesi kanıtı okur ve mevcut KURAL-TABANLI kapılardan geçirir.
        ///
        /// Yeni bir sınıflandırıcı KURULMAZ: <see cref="MatchIntelligenceService.ApplyReadGates"/>
        /// zaten futbol triyajı, içerik türü, tarihsel içerik, kalite ve maç bağlama kapılarını
        /// uygular ve her kanıta EventType / RelatedTeam / Timing yazar. Shadow B bunları okur.
        /// LLM bu yolda yoktur.
        /// </summary>
        private async Task<List<AdjustmentInput>> LoadPreMatchEvidenceAsync(
            string formaxMatchId, string homeName, string awayName, DateTime kickoffUtc, CancellationToken ct)
        {
            var rows = await _db.MatchEvidenceRecords.AsNoTracking()
                .Where(r => r.FormaxMatchId == formaxMatchId && r.PublishedUtc < kickoffUtc)
                .ToListAsync(ct);
            if (rows.Count == 0) return new List<AdjustmentInput>();

            var hashes = rows.Select(r => r.ContentHash).Distinct().ToList();
            var articles = (await _db.MatchNewsArticles.AsNoTracking()
                    .Where(a => a.FormaxMatchId == formaxMatchId && hashes.Contains(a.ContentHash))
                    .Select(a => new { a.ContentHash, a.Summary, a.Sources, a.SourceCount })
                    .ToListAsync(ct))
                .GroupBy(a => a.ContentHash)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var idByHash = rows
                .GroupBy(r => r.ContentHash, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Min(x => x.Id), StringComparer.Ordinal);

            var evidence = rows.Select(r =>
            {
                articles.TryGetValue(r.ContentHash, out var art);
                var sources = (art?.Sources ?? r.Source)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => s.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                return new MatchEvidence
                {
                    FormaxMatchId = r.FormaxMatchId,
                    Type = r.Type,
                    Cluster = r.Cluster,
                    Source = r.Source,
                    SourceQuality = r.SourceQuality,
                    Confidence = r.Confidence,
                    PublishedUtc = r.PublishedUtc,
                    Headline = r.Headline,
                    ContentHash = r.ContentHash,
                    Summary = art?.Summary ?? string.Empty,
                    Sources = sources,
                    SourceCount = Math.Max(art?.SourceCount ?? 1, Math.Max(1, sources.Count))
                };
            }).ToList();

            var gated = _intelligence.ApplyReadGates(
                evidence, homeName, awayName,
                minQuality: _options.MinSourceQuality, requireBothTeams: false, kickoffUtc: kickoffUtc);

            var result = new List<AdjustmentInput>(gated.Count);
            foreach (var e in gated)
            {
                if (!idByHash.TryGetValue(e.ContentHash, out var id)) continue;
                result.Add(new AdjustmentInput
                {
                    EvidenceId = id,
                    ContentHash = e.ContentHash,
                    EventType = e.EventType,
                    RelatedTeam = e.RelatedTeam,
                    Timing = e.Timing,
                    Source = e.Source,
                    SourceQuality = e.SourceQuality,
                    Confidence = e.Confidence,
                    SourceCount = e.SourceCount,
                    PublishedUtc = e.PublishedUtc
                });
            }
            return result;
        }

        /// <summary>
        /// Shadow B tahminlerine gerçek sonucu iliştirir.
        ///
        /// GERÇEKLİK KAYNAĞI TEK: <c>Matches.HomeScore/AwayScore</c> — Shadow A'nın settlement'ı
        /// da aynı sütunları okur. B için ayrı bir "gerçek skor" ÜRETİLMEZ; sonuç türetme kuralı
        /// da A ile birebir aynıdır.
        /// </summary>
        public async Task<int> SettleAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var pending = await (
                from p in _db.ShadowBPredictions.AsNoTracking()
                where !_db.ShadowBPredictionSettlements.Any(s => s.PredictionId == p.PredictionId)
                   && p.MatchDate < now
                orderby p.MatchDate
                select new { p.PredictionId, p.MatchId, p.MatchDate })
                .Take(500).ToListAsync(ct);

            if (pending.Count == 0) return 0;

            var matchIds = pending.Select(p => p.MatchId).Distinct().ToList();
            var results = (await _db.Matches.AsNoTracking()
                .Where(m => matchIds.Contains(m.Id) && m.Status == "Finished")
                .Select(m => new { m.Id, m.HomeScore, m.AwayScore, m.MatchDate })
                .ToListAsync(ct)).ToDictionary(r => r.Id);

            var settled = 0;
            foreach (var p in pending)
            {
                if (!results.TryGetValue(p.MatchId, out var r)) continue;
                if (r.MatchDate < p.MatchDate.Date) continue;

                _db.ShadowBPredictionSettlements.Add(new ShadowBPredictionSettlement
                {
                    PredictionId = p.PredictionId,
                    ActualHomeGoals = r.HomeScore,
                    ActualAwayGoals = r.AwayScore,
                    ActualResult = r.HomeScore > r.AwayScore ? "HomeWin"
                                 : r.HomeScore == r.AwayScore ? "Draw" : "AwayWin",
                    SettlementTimestamp = now
                });
                settled++;
            }

            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex)
            {
                settled = 0;
                _logger.LogWarning(ex, "[SHADOW-B] settlement insert refused — nothing was replaced.");
            }
            return settled;
        }

        /// <summary>
        /// FMXB + 20 hex. Girdiler: taban tahmin, varyant, düzeltme kuralı sürümü, kanıt kesimi,
        /// kanıt sayısı ve uygulanan kayma. "Aynı girdi → aynı id" ve "yeni kanıt → yeni id"
        /// kurallarının ikisi de bu bileşimden çıkar.
        /// </summary>
        internal static string BuildPredictionId(string basePredictionId, DateTime? evidenceCutoff, AdjustmentResult adj)
        {
            var canonical = string.Join('|',
                basePredictionId,
                "NEWS_ADJUSTED",
                NewsAdjustOptions.Version,
                evidenceCutoff?.ToString("O", CultureInfo.InvariantCulture) ?? "null",
                adj.Used.Count.ToString(CultureInfo.InvariantCulture),
                adj.AppliedTilt.ToString("F6", CultureInfo.InvariantCulture));
            return "FMXB" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..20];
        }

        /// <summary>Yayım anındaki parmak izi — satırın sonradan değişip değişmediği bununla ölçülür.</summary>
        internal static string BuildContentHash(ShadowBPrediction r)
        {
            var canonical = string.Join('|',
                r.PredictionId, r.BasePredictionId, r.Variant,
                r.MatchId.ToString(CultureInfo.InvariantCulture),
                r.MatchDate.ToString("O", CultureInfo.InvariantCulture),
                r.EvidenceCutoff?.ToString("O", CultureInfo.InvariantCulture) ?? "null",
                r.ModelVersion, r.TeamStrengthVersion, r.GateVersion, r.CalibrationVersion,
                r.AdjustmentVersion,
                Num(r.BaseHomeProbability), Num(r.BaseDrawProbability), Num(r.BaseAwayProbability),
                Num(r.HomeProbability), Num(r.DrawProbability), Num(r.AwayProbability),
                r.PredictionEligible ? "1" : "0", r.ConfidenceClass, r.GateStatus,
                r.EvidenceCount.ToString(CultureInfo.InvariantCulture),
                r.HomeImpact.ToString("F6", CultureInfo.InvariantCulture),
                r.AwayImpact.ToString("F6", CultureInfo.InvariantCulture),
                r.AppliedTilt.ToString("F6", CultureInfo.InvariantCulture));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..32];
        }

        private static string Num(double? v) =>
            v.HasValue ? v.Value.ToString("F10", CultureInfo.InvariantCulture) : "null";

        private static string Trim(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
