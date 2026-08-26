using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Contract.Models;
using Formax.Contract.Services;
using Formax.DixonColes.Config;
using Formax.Infrastructure.Data;
using Formax.ModelValidation.Config;
using Formax.ModelValidation.Models;
using Formax.ModelValidation.Services;
using Formax.Prediction.Config;
using Formax.Prediction.Services;
using Formax.TeamStrength.Config;
using Formax.TeamStrength.Models;
using Formax.TeamStrength.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Predictions
{
    public sealed class ShadowCycleReport
    {
        public int UpcomingMatchesSeen { get; set; }
        public int OutOfScope { get; set; }
        public int Predicted { get; set; }
        public int Accepted { get; set; }
        public int Rejected { get; set; }
        public int Inserted { get; set; }
        public int AlreadyPublished { get; set; }
        public int ImmutabilityRefused { get; set; }
        /// <summary>Yazilmak uzere hazirlanmis ama catisma nedeniyle YAZILMAMIS satir sayisi.</summary>
        public int NotInsertedDueToConflict { get; set; }
        public int Failed { get; set; }
        public long ElapsedMs { get; set; }
        public List<double> PerMatchLatencyMs { get; } = new();
        public string? ModelFingerprint { get; set; }
        public DateOnly? RatingEvidenceThrough { get; set; }
    }

    /// <summary>
    /// SHADOW MODE — gerçek yaklaşan maçlar için, KİLİTLİ motorla tahmin üretir ve yazar.
    ///
    /// Motor araştırmadaki kodun ta kendisidir (ProjectReference ile), kopyası değil: §19'un
    /// "sapma 0" şartı ancak tek kaynak varsa yapısal olarak doğrudur.
    ///
    /// ZAMAN SÖZLEŞMESİ. Bir maç için tahmin, YALNIZ o maçtan kesinlikle önce oynanmış maçlardan
    /// üretilir. Uygulaması: her yaklaşan maç TARİHİ için ayrı bir replay yapılır ve o replay'e
    /// yalnız (a) bitmiş maçlar ve (b) o tarihteki yaklaşan maçlar girer. Aynı gün oynanan maçlar
    /// motorun kuralı gereği birbirini beslemez, sonraki günlerin maçları ise listede hiç yoktur.
    /// Böylece 25 Ağustos'taki bir maç, 23 Ağustos'ta oynanacak (henüz oynanmamış) bir maçtan
    /// bilgi alamaz.
    ///
    /// Kullanıcıya hiçbir şey gösterilmez: satırlar ShadowMode=1 ile yazılır.
    /// </summary>
    public sealed class ShadowPredictionService
    {
        private readonly FormaxDbContext _db;
        private readonly ILogger<ShadowPredictionService> _logger;
        private readonly PredictionEngineOptions _options;

        public ShadowPredictionService(FormaxDbContext db, ILogger<ShadowPredictionService> logger,
            PredictionEngineOptions options)
        {
            _db = db;
            _logger = logger;
            _options = options;
        }

        public async Task<ShadowCycleReport> RunCycleAsync(int horizonDays, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var report = new ShadowCycleReport();

            var ts = TeamStrengthConfig.Load(_options.ValidatedTeamStrengthConfigPath);
            var dc = DixonColesConfig.Load(_options.DixonColesConfigPath);
            var split = SplitConfig.Load(_options.SplitConfigPath);
            var gateCfg = GateConfig.Load(_options.GateConfigPath);
            var contractService = new ContractService(gateCfg, ContractVersions.Current,
                _options.ValidatedTeamStrengthConfigPath);
            report.ModelFingerprint = contractService.ModelFingerprint;

            var identity = new CanonicalIdentityResolver(_options.HistoricalTeamsCsvPath);

            // ── 1. tarihsel kanıt: araştırma veri seti + veri setinden sonra biten üretim maçları
            var historical = MatchCsvReader.Read(_options.HistoricalDatasetPath, ts).Matches;
            var historicalEnd = historical.Count == 0 ? DateOnly.MinValue : historical.Max(m => m.Date);

            var nowUtc = DateTime.UtcNow;
            var finishedProduction = await LoadProductionMatchesAsync(
                identity, historicalEnd.ToDateTime(TimeOnly.MinValue), nowUtc, finishedOnly: true, ct);

            var evidence = new List<MatchRecord>(historical.Count + finishedProduction.Count);
            evidence.AddRange(historical);
            evidence.AddRange(finishedProduction.Select(p => p.Record));
            report.RatingEvidenceThrough = evidence.Count == 0 ? null : evidence.Max(m => m.Date);

            _logger.LogInformation(
                "[SHADOW] evidence: {Hist} historical + {Recent} recent production = {Total}, through {Through:yyyy-MM-dd}",
                historical.Count, finishedProduction.Count, evidence.Count, report.RatingEvidenceThrough);

            // ── 2. yaklaşan maçlar
            var upcoming = await LoadProductionMatchesAsync(
                identity, nowUtc, nowUtc.AddDays(horizonDays), finishedOnly: false, ct);
            report.UpcomingMatchesSeen = upcoming.Count;

            if (upcoming.Count == 0)
            {
                report.ElapsedMs = sw.ElapsedMilliseconds;
                return report;
            }

            // ── 3. maç TARİHİ başına bir replay (zaman sözleşmesi)
            var existingIds = new HashSet<string>(
                await _db.Predictions.Select(p => p.PredictionId).ToListAsync(ct), StringComparer.Ordinal);

            foreach (var dayGroup in upcoming.GroupBy(u => u.Record.Date).OrderBy(g => g.Key))
            {
                ct.ThrowIfCancellationRequested();
                var dayMatches = dayGroup.ToList();

                var replayList = new List<MatchRecord>(evidence.Count + dayMatches.Count);
                replayList.AddRange(evidence);
                replayList.AddRange(dayMatches.Select(d => d.Record));

                var built = new TeamStrengthService(ts).Build(replayList);
                var snapshots = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side));
                var rows = built.Snapshots.ToDictionary(s => (s.MatchId, s.Side), StrengthPipeline.ToRow);
                var coverage = CompetitionCoverage.Build(replayList);

                var predictions = new Dictionary<string, MatchPrediction>(StringComparer.Ordinal);
                var wanted = new HashSet<string>(dayMatches.Select(d => d.Record.MatchId), StringComparer.Ordinal);
                new BacktestV2(dc, new ModelParameters { Rho = 0.0, BivariateC = 0.0 }, split)
                    .Run(replayList, rows, ModelMask.IndependentPoisson, p =>
                    {
                        if (wanted.Contains(p.MatchId)) predictions[p.MatchId] = p;
                    });

                foreach (var item in dayMatches)
                {
                    ct.ThrowIfCancellationRequested();
                    var matchSw = Stopwatch.StartNew();
                    try
                    {
                        if (!predictions.TryGetValue(item.Record.MatchId, out var raw))
                        {
                            report.Failed++;
                            _logger.LogWarning("[SHADOW] no raw prediction produced for match {MatchId}", item.ProductionMatchId);
                            continue;
                        }

                        snapshots.TryGetValue((raw.MatchId, "HOME"), out var home);
                        snapshots.TryGetValue((raw.MatchId, "AWAY"), out var away);

                        var contract = contractService.Build(raw, home, away,
                            item.Record.IdentityConfidence, item.Record.IdentityConfidence,
                            coverage.For(raw.MatchId));

                        report.Predicted++;
                        if (contract.PredictionEligible) report.Accepted++; else report.Rejected++;

                        // §8 — aynı girdi, aynı PredictionId: ikinci kez yazılmaz.
                        if (existingIds.Contains(contract.PredictionId))
                        {
                            report.AlreadyPublished++;
                        }
                        else
                        {
                            _db.Predictions.Add(ToEntity(contract, item));
                            existingIds.Add(contract.PredictionId);
                            report.Inserted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // §11 failure isolation: bir maçın hatası döngüyü durdurmaz.
                        report.Failed++;
                        _logger.LogError(ex, "[SHADOW] prediction failed for match {MatchId}", item.ProductionMatchId);
                    }
                    finally
                    {
                        matchSw.Stop();
                        report.PerMatchLatencyMs.Add(matchSw.Elapsed.TotalMilliseconds);
                    }
                }
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Benzersiz PredictionId ihlali = başka bir örnek aynı tahmini yazmış. Yeniden
                // yazmaya ÇALIŞILMAZ: yayımlanmış tahmin değişmez.
                report.ImmutabilityRefused++;

                // Inserted sayacı SaveChanges'ten ÖNCE artırılıyor. Kayıt reddedildiyse hiçbir
                // satır yazılmamıştır; sayacı düzeltmezsek log gerçekte olmayan bir yazımı
                // raporlar. Eşzamanlı iki örnek denendiğinde tam olarak bu görüldü: kaybeden
                // örnek "inserted 10" diyordu, oysa 0 satır yazmıştı.
                report.NotInsertedDueToConflict = report.Inserted;
                report.Inserted = 0;

                _logger.LogWarning(ex,
                    "[SHADOW] insert refused by the database — {Count} prediction(s) were NOT written; " +
                    "another instance had already published them. No prediction was rewritten.",
                    report.NotInsertedDueToConflict);
            }

            report.ElapsedMs = sw.ElapsedMilliseconds;
            return report;
        }

        private static Formax.Domain.Entities.Prediction ToEntity(PredictionContract c, UpcomingMatch item) => new()
        {
            PredictionId = c.PredictionId,
            MatchId = item.ProductionMatchId,
            CanonicalMatchId = c.MatchId,
            MatchDate = item.KickoffUtc,
            PredictionTimestamp = c.PredictionTimestamp,
            EvidenceCutoff = c.EvidenceCutoff?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ModelVersion = c.Versions.ModelVersion,
            TeamStrengthVersion = c.Versions.TeamStrengthVersion,
            GateVersion = c.Versions.GateVersion,
            CalibrationVersion = c.Versions.CalibrationVersion,
            HomeProbability = c.HomeProbability,
            DrawProbability = c.DrawProbability,
            AwayProbability = c.AwayProbability,
            PredictionEligible = c.PredictionEligible,
            ConfidenceClass = c.ConfidenceClass.ToString().ToUpperInvariant(),
            GateStatus = c.GateStatus.ToString().ToUpperInvariant(),
            GateReason = c.GateReason,
            ContentHash = c.ContentHash,
            ShadowMode = true,
            CreatedAt = DateTime.UtcNow
        };

        public sealed class UpcomingMatch
        {
            public required int ProductionMatchId { get; init; }
            public required DateTime KickoffUtc { get; init; }
            public required MatchRecord Record { get; init; }
        }

        /// <summary>
        /// Üretim maçlarını motorun anladığı biçime çevirir. Canonical kimliğe eşlenemeyen veya
        /// kilitli müsabaka kapsamı dışında kalan maç ATLANIR - uydurma kimlik üretilmez.
        /// </summary>
        private async Task<List<UpcomingMatch>> LoadProductionMatchesAsync(
            CanonicalIdentityResolver identity, DateTime fromUtc, DateTime toUtc, bool finishedOnly,
            CancellationToken ct)
        {
            var rows = await (
                from m in _db.Matches.AsNoTracking()
                join h in _db.Teams.AsNoTracking() on m.HomeTeamId equals h.Id
                join a in _db.Teams.AsNoTracking() on m.AwayTeamId equals a.Id
                where m.MatchDate > fromUtc && m.MatchDate < toUtc
                   && (finishedOnly ? m.Status == "Finished" : m.Status == "NotStarted")
                select new
                {
                    m.Id, m.MatchDate, m.LeagueId, m.Round, m.HomeScore, m.AwayScore,
                    HomeExt = h.ExternalTeamId, AwayExt = a.ExternalTeamId,
                    HomeName = h.Name, AwayName = a.Name
                }).ToListAsync(ct);

            var result = new List<UpcomingMatch>(rows.Count);
            foreach (var r in rows)
            {
                var competition = CanonicalIdentityResolver.ResolveCompetition(r.LeagueId);
                if (competition is null) continue;

                var homeCanonical = identity.ResolveTeam(r.HomeExt);
                var awayCanonical = identity.ResolveTeam(r.AwayExt);
                if (homeCanonical is null || awayCanonical is null) continue;

                result.Add(new UpcomingMatch
                {
                    ProductionMatchId = r.Id,
                    KickoffUtc = r.MatchDate,
                    Record = new MatchRecord
                    {
                        // Üretim maçı için canonical kimlik: üretim id'sinden türetilen kararlı bir anahtar.
                        MatchId = $"PRODM{r.Id.ToString(CultureInfo.InvariantCulture)}",
                        Date = DateOnly.FromDateTime(r.MatchDate),
                        Season = CanonicalIdentityResolver.ResolveSeason(r.MatchDate),
                        Competition = competition,
                        CompetitionType = CanonicalIdentityResolver.ResolveCompetitionType(competition, r.Round),
                        HomeTeamId = homeCanonical,
                        AwayTeamId = awayCanonical,
                        HomeTeamName = identity.TeamName(homeCanonical),
                        AwayTeamName = identity.TeamName(awayCanonical),
                        // Yaklaşan maçta skor henüz yok; motor bu satırın sonucunu KENDİ snapshot'ında
                        // asla kullanmaz ve listede ondan sonraki gün bulunmaz.
                        HomeGoals = finishedOnly ? r.HomeScore : 0,
                        AwayGoals = finishedOnly ? r.AwayScore : 0,
                        MatchStatus = "FT",
                        IdentityConfidence = "CONFIRMED"
                    }
                });
            }
            return result;
        }
    }

    /// <summary>Motor dosyalarının yolu. Hiçbir MODEL PARAMETRESİ içermez - yalnız nerede olduğunu söyler.</summary>
    public sealed class PredictionEngineOptions
    {
        public string EngineRoot { get; set; } = string.Empty;

        /// <summary>Job basladiktan sonra ilk cycle icin beklenen sure. Uretimde host acilisini rahatlatir; testte kisaltilir.</summary>
        public int StartupDelaySeconds { get; set; } = 120;

        /// <summary>Cycle araligi (saat).</summary>
        public double LoopHours { get; set; } = 6;

        /// <summary>Kac gun ileriye tahmin uretilecegi.</summary>
        public int HorizonDays { get; set; } = 8;

        public string HistoricalDatasetPath => Path.Combine(EngineRoot, "FORMAX_HISTORICAL_MASTER", "FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv");
        public string HistoricalTeamsCsvPath => Path.Combine(EngineRoot, "FORMAX_HISTORICAL_MASTER", "FORMAX_HISTORICAL_TEAMS.csv");
        public string ValidatedTeamStrengthConfigPath => Path.Combine(EngineRoot, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "validated_teamstrength_config.json");
        public string DixonColesConfigPath => Path.Combine(EngineRoot, "FORMAX_PROBABILITY_ENGINE", "dixon_coles", "dixoncoles.config.json");
        public string SplitConfigPath => Path.Combine(EngineRoot, "FORMAX_PROBABILITY_ENGINE", "model_validation_v2", "split.config.json");
        public string GateConfigPath => Path.Combine(EngineRoot, "FORMAX_PROBABILITY_ENGINE", "prediction_gate_v1", "gate.config.json");

        public bool IsUsable() => File.Exists(HistoricalDatasetPath) && File.Exists(ValidatedTeamStrengthConfigPath);
    }
}
