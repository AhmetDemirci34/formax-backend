using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Infrastructure.Outcomes
{
    /// <summary>Bir kesim tarihinde bütün organizasyon × market hücrelerini ölçen kaynak (üretimde: mevcut zamansal backtest).</summary>
    public interface IEligibilityEvaluationSource
    {
        string ConfigHash { get; }
        Task<IReadOnlyList<CellEvaluation>> EvaluateAsync(DateTime cutoffUtc, DateTime evaluatedAtUtc, CancellationToken ct = default);
    }

    /// <summary>
    /// ÜRETİM DEĞERLENDİRME KAYNAĞI — model eğitim işinin AYNI backtest'i (4.0, parsimoni kapılı seçim, lig kalibrasyon ekseni
    /// None), kesim tarihi <c>nowUtc</c> yerine verilen tarih. Yalnız DB okur; dış istek, API-Football, model parametresi yazımı
    /// YOK. Tarihsel maçlar bir kez yüklenir, her kesim için süzülür.
    /// </summary>
    public sealed class BacktestEligibilityEvaluationSource : IEligibilityEvaluationSource
    {
        private readonly OutcomeHistoryLoader _history;
        private List<HistoricalMatch>? _loaded;
        private DateTime _loadedUntil;
        private Dictionary<int, string>? _names;
        private Dictionary<int, DateTime?>? _resultUpdated;

        public BacktestEligibilityEvaluationSource(OutcomeHistoryLoader history) => _history = history;

        public static string CurrentConfigHash { get; } = EligibilityPublicationPolicy.ConfigHash(OutcomeModelVersion.Current,
            OutcomeModelTrainingService.TestWindow, OutcomeModelTrainingService.CalibrationWindow, OutcomeModelTrainingService.TrainWindow,
            LockedCompetitions.All);

        public string ConfigHash => CurrentConfigHash;

        /// <summary>Tekrar oynatma için geçmişi en geç kesime kadar bir kez yükler.</summary>
        public async Task PreloadAsync(DateTime maxCutoffUtc, CancellationToken ct = default)
        {
            if (_loaded != null && _loadedUntil >= maxCutoffUtc) return;
            _loaded = await _history.LoadAsync(maxCutoffUtc, ct).ConfigureAwait(false);
            _loadedUntil = maxCutoffUtc;
            _names ??= await _history.LoadCompetitionNamesAsync(ct).ConfigureAwait(false);
            _resultUpdated = await _history.LoadResultUpdatedAsync(LockedCompetitions.All, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<CellEvaluation>> EvaluateAsync(DateTime cutoffUtc, DateTime evaluatedAtUtc, CancellationToken ct = default)
        {
            await PreloadAsync(cutoffUtc, ct).ConfigureAwait(false);
            var history = _loaded!.Where(m => m.KickoffUtc < cutoffUtc).ToList();
            var names = _names!;
            var updated = _resultUpdated;
            return await Task.Run(() => Evaluate(history, names, cutoffUtc, evaluatedAtUtc, ConfigHash, updated), ct).ConfigureAwait(false);
        }

        /// <summary>Saf hesap (test edilebilir): backtest + organizasyon düzeyi bütünlük kontrolleri + ham kapı sınıflaması + kanıt manifestosu.</summary>
        public static IReadOnlyList<CellEvaluation> Evaluate(IReadOnlyList<HistoricalMatch> history, IReadOnlyDictionary<int, string> names,
            DateTime cutoffUtc, DateTime evaluatedAtUtc, string configHash, IReadOnlyDictionary<int, DateTime?>? resultUpdated = null)
        {
            var testStart = cutoffUtc - OutcomeModelTrainingService.TestWindow;
            var calStart = testStart - OutcomeModelTrainingService.CalibrationWindow;
            var evalStart = calStart - OutcomeModelTrainingService.TrainWindow;
            var catalog = CompetitionCatalog.Build(history, names.ToDictionary(k => k.Key, k => k.Value));
            var locked = LockedCompetitions.All.ToHashSet();
            var report = OutcomeBacktest.Run(history, catalog, locked, evalStart, calStart, testStart, cutoffUtc, cutoffUtc,
                compareLegacy: false, candidate: true);
            var artifacts = OutcomeBacktest.LastArtifacts;
            var runId = EligibilityPublicationPolicy.EvaluationRunId(cutoffUtc, configHash);

            // ── Organizasyon düzeyi bütünlük: sızıntı, ev/deplasman kimliği, olasılık toplamı, tekil maç, model kimliği ──
            var integrity = new Dictionary<int, EvaluationIntegrity>();
            EvaluationIntegrity For(int league) => integrity.TryGetValue(league, out var i) ? i : integrity[league] = new EvaluationIntegrity();
            var modelIdentityBroken = report.ModelVersion != OutcomeModelVersion.Current
                                      || report.MarketEligibilityPolicyVersion != MarketEligibilityPolicy.Version
                                      || string.IsNullOrWhiteSpace(configHash);
            foreach (var m in history.Where(m => locked.Contains(m.LeagueId)))
            {
                if (m.KickoffUtc >= cutoffUtc) { var i = For(m.LeagueId); i.Leakage = true; i.Details.Add($"HISTORY_AFTER_CUTOFF:{m.MatchId}"); }
                if (m.HomeTeamId == m.AwayTeamId) { var i = For(m.LeagueId); i.HomeAwayViolation = true; i.Details.Add($"SAME_TEAM:{m.MatchId}"); }
            }
            if (artifacts != null)
            {
                foreach (var g in artifacts.LockedTestSamples.GroupBy(s => s.LeagueId))
                {
                    var i = For(g.Key);
                    if (g.Any(s => s.KickoffUtc >= cutoffUtc || s.KickoffUtc < testStart)) { i.Leakage = true; i.Details.Add("TEST_SAMPLE_OUTSIDE_WINDOW"); }
                    if (g.Select(s => s.MatchId).Distinct().Count() != g.Count()) { i.DataIntegrityViolation = true; i.Details.Add("DUPLICATE_TEST_MATCH"); }
                    foreach (var s in g)
                    {
                        var d = OutcomePredictor.Predict(s.E, s.LeagueId, artifacts.Chosen).Calibrated;
                        var sum = d.HomeWin + d.Draw + d.AwayWin;
                        if (!double.IsFinite(sum) || Math.Abs(sum - 1) > 1e-6 || d.HomeWin < 0 || d.Draw < 0 || d.AwayWin < 0
                            || d.Over(2.5) is < 0 or > 1 || d.BttsYes is < 0 or > 1)
                        {
                            i.ProbabilitySumViolation = true; i.Details.Add($"PROBABILITY_SUM:{s.MatchId}");
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var l in locked) { var i = For(l); i.DataIntegrityViolation = true; i.Details.Add("NO_BACKTEST_ARTIFACTS"); }
            }
            if (modelIdentityBroken) foreach (var l in locked) { var i = For(l); i.ModelIdentityViolation = true; i.Details.Add("MODEL_OR_GATE_VERSION_MISMATCH"); }

            var list = new List<CellEvaluation>();
            foreach (var league in report.MarketEligibility.Where(m => m.LeagueId != null).GroupBy(m => m.LeagueId!.Value))
            {
                var result = league.FirstOrDefault(m => m.Family == MarketFamilies.MatchResult);
                var integ = integrity.TryGetValue(league.Key, out var ig) ? ig : EvaluationIntegrity.Clean;
                var manifest = BuildManifest(league.Key, artifacts?.LockedTestSamples, resultUpdated);
                foreach (var m in league)
                {
                    var e = EligibilityPublicationPolicy.ToEvaluation(m, result, integ, cutoffUtc, runId, configHash, evaluatedAtUtc, report.ModelVersion);
                    e.Evidence = manifest;
                    e.EvidenceFingerprint = EvidenceIdentity.Fingerprint(e.MarketFamily, manifest, e.ModelVersion, e.ConfigHash, e.GatePolicyVersion, e.PolicyVersion);
                    list.Add(e);
                }
            }
            return list;
        }

        /// <summary>Organizasyonun test penceresinde kullanılan tamamlanmış maçlar (MatchId → skor) + maks. başlama + maks. sonuç güncelleme.</summary>
        public static EvidenceManifest BuildManifest(int league, IReadOnlyList<EvalSample>? samples, IReadOnlyDictionary<int, DateTime?>? resultUpdated)
        {
            var m = new EvidenceManifest { OrganizationId = league };
            foreach (var s in (samples ?? Array.Empty<EvalSample>()).Where(s => s.LeagueId == league))
            {
                m.Results[s.MatchId] = (s.HomeGoals, s.AwayGoals);
                var u = resultUpdated != null && resultUpdated.TryGetValue(s.MatchId, out var v) ? v : null;
                m.UpdatedAt[s.MatchId] = u;
                if (m.MaxKickoffUtc == null || s.KickoffUtc > m.MaxKickoffUtc) m.MaxKickoffUtc = s.KickoffUtc;
                if (u != null && (m.MaxResultUpdatedUtc == null || u > m.MaxResultUpdatedUtc)) m.MaxResultUpdatedUtc = DateTime.SpecifyKind(u.Value, DateTimeKind.Utc);
            }
            return m;
        }
    }

    public enum EligibilityPublicationMode { DryRun, EvaluateOnly, Publish }

    public static class EligibilityPublicationSources
    {
        public const string Scheduled = "Scheduled";
        public const string Manual = "Manual";
        public const string BootstrapReplay = "BootstrapReplay";
    }

    public sealed class EligibilityCutoffOutcome
    {
        public DateTime CutoffUtc { get; set; }
        public string WeekKey { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string? RunKey { get; set; }
        public long DurationMs { get; set; }
        public List<StateTransition> Transitions { get; set; } = new();
        public List<CellEvaluation> Evaluations { get; set; } = new();
    }

    public sealed class EligibilityPublicationReport
    {
        public string Mode { get; set; } = string.Empty;
        public string PolicyVersion { get; set; } = EligibilityPublicationPolicy.Version;
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string ConfigHash { get; set; } = string.Empty;
        public List<EligibilityCutoffOutcome> Cutoffs { get; set; } = new();
        public Dictionary<string, string> FinalStates { get; set; } = new();
        public long TotalMs { get; set; }
        public long PeakWorkingSetBytes { get; set; }
        public int RecomputeRequestsEnqueued { get; set; }
    }

    /// <summary>
    /// YAYIN SERVİSİ — dry-run (hiçbir şey yazmaz), evaluate-only (yalnız değerlendirme kaydı; yayın durumu DEĞİŞMEZ) ve publish
    /// (durum makinesi + defter + hedefli yenileme). Aynı anda tek tur (süreç içi kilit + tekil RunKey). Yalnız DB.
    /// </summary>
    public sealed class EligibilityPublicationService
    {
        /// <summary>Süreç içi tek tur kilidi — haftalık iş ile admin çalıştırması üst üste binmez.</summary>
        public static readonly SemaphoreSlim Gate = new(1, 1);

        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
        private readonly FormaxDbContext _db;
        private readonly IEligibilityEvaluationSource _source;
        private readonly ILogger<EligibilityPublicationService> _log;

        public EligibilityPublicationService(FormaxDbContext db, IEligibilityEvaluationSource source, ILogger<EligibilityPublicationService> log)
        {
            _db = db; _source = source; _log = log;
        }

        public static string BootstrapRunKey => "bootstrap|" + EligibilityPublicationPolicy.Version;

        public static string PublishRunKey(string configHash, DateTime cutoffUtc)
            => string.Join("|", "publish", EligibilityPublicationPolicy.Version, OutcomeModelVersion.Current, configHash, EligibilityEvaluationSchedule.WeekKey(cutoffUtc));

        public static string EvaluateRunKey(string configHash, DateTime cutoffUtc)
            => string.Join("|", "evaluate", EligibilityPublicationPolicy.Version, OutcomeModelVersion.Current, configHash,
                cutoffUtc.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));

        // ═══════════════════════════ BOOTSTRAP ═══════════════════════════

        /// <summary>
        /// TEK SEFERLİK BAŞLANGIÇ AKTARIMI — kullanıcıya ŞU AN gösterilen matris içeri alınır: güncel yayımlanmış snapshot'ların
        /// kullandığı koşu (en sık CalibrationRunId) sabitlenir; o koşuda Eligible olan hücre Open, diğerleri Closed. Böylece
        /// başlangıçta oluşan yeni günlük koşunun ham matrisi yayına SIZAMAZ. Açık hücreler ayrıcalıklı değildir: sonraki her
        /// pencere onları da aynı kurallarla ölçer. İdempotent: ikinci çağrı hiçbir şey yazmaz.
        /// </summary>
        public async Task<(bool Created, string? SourceRunId, int Open, int Closed)> EnsureBootstrappedAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var existing = await _db.MarketEligibilityPublicationRuns.AsNoTracking().Where(r => r.RunKey == BootstrapRunKey)
                .Select(r => new { r.SourceModelRunId }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (existing != null) return (false, existing.SourceModelRunId, 0, 0);
            if (await _db.MarketEligibilityStates.AnyAsync(ct).ConfigureAwait(false)) return (false, null, 0, 0);

            var started = nowUtc;
            var sw = Stopwatch.StartNew();
            var pinned = await (from s in _db.MatchPredictionSnapshots.AsNoTracking()
                                where s.IsCurrent && s.ModelVersion == OutcomeModelVersion.Current && s.CalibrationRunId != null
                                group s by s.CalibrationRunId into g
                                orderby g.Count() descending, g.Key
                                select g.Key).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            pinned ??= await _db.PredictionModelRuns.AsNoTracking()
                .Where(r => r.Status == "Accepted" && r.ModelVersion == OutcomeModelVersion.Current)
                .OrderByDescending(r => r.CompletedAtUtc).Select(r => r.RunId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var rows = pinned == null
                ? new List<LeagueMarketEligibility>()
                : await _db.LeagueMarketEligibilities.AsNoTracking().Where(e => e.RunId == pinned).ToListAsync(ct).ConfigureAwait(false);

            int open = 0, closed = 0;
            foreach (var league in LockedCompetitions.All.OrderBy(x => x))
                foreach (var family in MarketFamilies.All)
                {
                    var row = rows.FirstOrDefault(r => r.LeagueId == league && r.Family == family);
                    var isOpen = row?.Status == MarketEligibilityStatuses.Eligible;
                    var state = isOpen ? PublishedStates.Open : PublishedStates.Closed;
                    var reason = isOpen ? "BOOTSTRAP_IMPORT_OPEN:" + pinned : "BOOTSTRAP_IMPORT_CLOSED:" + (pinned ?? "NO_RUN") + ":" + (row?.Status ?? "NOT_EVALUATED");
                    if (isOpen) open++; else closed++;
                    _db.MarketEligibilityStates.Add(new MarketEligibilityState
                    {
                        OrganizationId = league, MarketFamily = family, PublishedState = state, StateVersion = 1,
                        PolicyVersion = EligibilityPublicationPolicy.Version, ModelVersion = OutcomeModelVersion.Current,
                        LastTransitionReason = reason, LastPublicationRunKey = BootstrapRunKey, UpdatedAtUtc = nowUtc
                    });
                    _db.MarketEligibilityStateTransitions.Add(new MarketEligibilityStateTransition
                    {
                        OrganizationId = league, MarketFamily = family, FromState = null, ToState = state, StateVersion = 1,
                        ReasonCode = reason, PublicationRunKey = BootstrapRunKey, CreatedAtUtc = nowUtc
                    });
                }
            _db.MarketEligibilityPublicationRuns.Add(new MarketEligibilityPublicationRun
            {
                RunKey = BootstrapRunKey, Mode = "Bootstrap", Source = "Bootstrap", ModelVersion = OutcomeModelVersion.Current,
                PolicyVersion = EligibilityPublicationPolicy.Version, SourceModelRunId = pinned, StartedAtUtc = started,
                CompletedAtUtc = DateTime.UtcNow, DurationMs = sw.ElapsedMilliseconds, Cells = open + closed, Transitions = open + closed,
                PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
                SummaryJson = JsonSerializer.Serialize(new { open, closed, pinnedRun = pinned, rule = "IsCurrent snapshots' CalibrationRunId (mode)" }, Json)
            });
            try
            {
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                // Eşzamanlı ikinci bootstrap tekil indekse takıldı: diğeri kazandı, bu çağrı hiçbir şey yazmadı.
                _log.LogWarning(ex, "[ELIGIBILITY PUBLICATION] eşzamanlı bootstrap — diğer çağrı yazdı");
                _db.ChangeTracker.Clear();
                return (false, pinned, 0, 0);
            }
            _log.LogInformation("[ELIGIBILITY PUBLICATION] bootstrap: kaynak koşu={Run} open={Open} closed={Closed}", pinned, open, closed);
            return (true, pinned, open, closed);
        }

        // ═══════════════════════════ DEĞERLENDİRME / YAYIN ═══════════════════════════

        /// <summary>
        /// Kesim tarihlerini SIRAYLA işler. DryRun: hiçbir şey yazılmaz (durum simülasyonu bellekte). EvaluateOnly: yalnız
        /// değerlendirme satırları; yayın durumu DEĞİŞMEZ. Publish: yalnız yetkili akış (haftalık iş, onaylı admin, bootstrap
        /// tekrar oynatması) — durum makinesi + defter + hedefli yenileme.
        /// </summary>
        public async Task<EligibilityPublicationReport> RunAsync(IReadOnlyList<DateTime> cutoffsUtc, EligibilityPublicationMode mode, string source,
            DateTime nowUtc, CancellationToken ct = default)
            => await RunAsync(cutoffsUtc, mode, source, nowUtc, null, ct).ConfigureAwait(false);

        /// <summary>
        /// Bootstrap çapası — içeri alınan matrisin yayına girdiği an (kaynak koşunun tamamlanma zamanı). Bu andan önceki kesimler
        /// yalnız kanıt geçmişidir (<see cref="EligibilityPublicationPolicy.RecordHistory"/>).
        /// </summary>
        public async Task<DateTime?> BootstrapAnchorAsync(CancellationToken ct = default)
        {
            var source = await _db.MarketEligibilityPublicationRuns.AsNoTracking().Where(r => r.RunKey == BootstrapRunKey)
                .Select(r => r.SourceModelRunId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (source == null) return null;
            var at = await _db.PredictionModelRuns.AsNoTracking().Where(r => r.RunId == source)
                .Select(r => (DateTime?)r.CompletedAtUtc).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            return at == null ? null : DateTime.SpecifyKind(at.Value, DateTimeKind.Utc);
        }

        /// <param name="transitionsFromUtc">Verilirse bu andan önceki kesimler geçiş üretmez, yalnız geçmişe eklenir.</param>
        public async Task<EligibilityPublicationReport> RunAsync(IReadOnlyList<DateTime> cutoffsUtc, EligibilityPublicationMode mode, string source,
            DateTime nowUtc, DateTime? transitionsFromUtc, CancellationToken ct = default)
        {
            var total = Stopwatch.StartNew();
            var report = new EligibilityPublicationReport { Mode = mode.ToString(), ConfigHash = _source.ConfigHash };
            var cutoffs = cutoffsUtc.Select(c => DateTime.SpecifyKind(c, DateTimeKind.Utc)).Distinct().OrderBy(c => c).ToList();
            if (cutoffs.Count == 0) return report;
            // Gelecek kesim yalnız DRY-RUN'da ve en çok 8 gün ileri: planlı bir koşuyu (ör. Pazartesi) bugünkü veriyle önceden
            // simüle eder. Yalnız bitmiş maçlar okunduğu için sızıntı yoktur; hiçbir şey yazılmaz.
            var simulatedFuture = cutoffs.Any(c => c > nowUtc);
            if (simulatedFuture && (mode != EligibilityPublicationMode.DryRun || cutoffs.Any(c => c > nowUtc.AddDays(8))))
                throw new InvalidOperationException("CUTOFF_IN_FUTURE");

            var bootstrapped = await _db.MarketEligibilityPublicationRuns.AsNoTracking().AnyAsync(r => r.RunKey == BootstrapRunKey, ct).ConfigureAwait(false);
            if (mode == EligibilityPublicationMode.Publish && !bootstrapped) throw new InvalidOperationException("NOT_BOOTSTRAPPED");

            var (states, history) = await LoadPublishedAsync(ct).ConfigureAwait(false);
            if (_source is BacktestEligibilityEvaluationSource bt) await bt.PreloadAsync(cutoffs[^1], ct).ConfigureAwait(false);
            var lastPublished = await _db.MarketEligibilityEvaluations.AsNoTracking()
                .Where(e => e.Mode == "Publish" && e.PolicyVersion == EligibilityPublicationPolicy.Version)
                .Select(e => (DateTime?)e.EvaluationCutoffUtc).MaxAsync(ct).ConfigureAwait(false);
            // Son SAYILAN pencerenin kanıtı (hücre başına). Koruma öncesi pencerelerin manifestosu yoksa deterministik yeniden üretilir.
            var counted = await LoadCountedEvidenceAsync(persist: mode != EligibilityPublicationMode.DryRun, nowUtc, ct).ConfigureAwait(false);

            foreach (var cutoff in cutoffs)
            {
                ct.ThrowIfCancellationRequested();
                var sw = Stopwatch.StartNew();
                var outcome = new EligibilityCutoffOutcome { CutoffUtc = cutoff, WeekKey = EligibilityEvaluationSchedule.WeekKey(cutoff) };
                report.Cutoffs.Add(outcome);

                string? runKey = mode switch
                {
                    EligibilityPublicationMode.Publish => PublishRunKey(_source.ConfigHash, cutoff),
                    EligibilityPublicationMode.EvaluateOnly => EvaluateRunKey(_source.ConfigHash, cutoff),
                    _ => null
                };
                outcome.RunKey = runKey;
                if (runKey != null && await _db.MarketEligibilityPublicationRuns.AsNoTracking().AnyAsync(r => r.RunKey == runKey, ct).ConfigureAwait(false))
                {
                    outcome.Result = mode == EligibilityPublicationMode.Publish ? "SKIPPED_ALREADY_PUBLISHED_THIS_WEEK" : "SKIPPED_ALREADY_EVALUATED";
                    continue;
                }
                if (mode == EligibilityPublicationMode.Publish && lastPublished != null && cutoff <= lastPublished)
                {
                    outcome.Result = "SKIPPED_CUTOFF_NOT_AFTER_LAST_PUBLISHED";
                    continue;
                }

                var evals = await _source.EvaluateAsync(cutoff, nowUtc, ct).ConfigureAwait(false);
                outcome.Evaluations = evals.ToList();
                // YENİ KANIT KORUMASI — aynı veri kümesi sayaçları ilerletmez.
                foreach (var e in evals)
                {
                    var prev = counted.TryGetValue(e.Cell, out var p) ? p : default;
                    EvidenceIdentity.Classify(e, prev.Fingerprint, prev.Manifest);
                }
                if (mode == EligibilityPublicationMode.EvaluateOnly)
                {
                    // "Olsaydı" geçişi kopya üzerinde — yayın durumu ve geçmiş DEĞİŞMEZ.
                    outcome.Transitions = EligibilityPublicationPolicy.Apply(new Dictionary<(int, string), string>(states), Copy(history), evals);
                    await PersistEvaluationsAsync(evals, "EvaluateOnly", source, null, runKey!, ct).ConfigureAwait(false);
                    await PersistManifestsAsync(evals, reconstructed: false, nowUtc, ct).ConfigureAwait(false);
                    AddRun(runKey!, "EvaluateOnly", source, cutoff, outcome, evals.Count, 0, 0, sw);
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    outcome.Result = "EVALUATED";
                }
                else
                {
                    outcome.Transitions = transitionsFromUtc != null && cutoff < transitionsFromUtc.Value
                        ? EligibilityPublicationPolicy.RecordHistory(states, history, evals)
                        : EligibilityPublicationPolicy.Apply(states, history, evals);
                    foreach (var e in evals.Where(e => e.Counts)) counted[e.Cell] = (e.EvidenceFingerprint, e.Evidence?.Results);
                    if (mode == EligibilityPublicationMode.Publish)
                    {
                        await PersistPublishAsync(evals, outcome.Transitions, source, runKey!, cutoff, nowUtc, ct).ConfigureAwait(false);
                        await PersistManifestsAsync(evals, reconstructed: false, nowUtc, ct).ConfigureAwait(false);
                        AddRun(runKey!, "Publish", source, cutoff, outcome, evals.Count, outcome.Transitions.Count(t => t.Changed),
                            outcome.Transitions.Count(t => t.VisibilityChanged), sw);
                        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                        report.RecomputeRequestsEnqueued += await InvalidateAsync(outcome.Transitions.Where(t => t.VisibilityChanged).ToList(), runKey!, nowUtc, ct).ConfigureAwait(false);
                        lastPublished = cutoff;
                        outcome.Result = "PUBLISHED";
                    }
                    else outcome.Result = cutoff > nowUtc ? "DRY_RUN_SIMULATED_FUTURE_CUTOFF" : "DRY_RUN";
                }
                outcome.DurationMs = sw.ElapsedMilliseconds;
                _log.LogInformation("[ELIGIBILITY PUBLICATION] {Mode} kesim={Cutoff:O} hücre={Cells} geçiş={Changed} görünürlük={Vis} süre={Ms}ms",
                    mode, cutoff, evals.Count, outcome.Transitions.Count(t => t.Changed), outcome.Transitions.Count(t => t.VisibilityChanged), outcome.DurationMs);
            }
            report.FinalStates = states.OrderBy(k => k.Key.Item1).ThenBy(k => k.Key.Item2, StringComparer.Ordinal)
                .ToDictionary(k => k.Key.Item1 + ":" + k.Key.Item2, k => k.Value);
            report.TotalMs = total.ElapsedMilliseconds;
            report.PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
            return report;
        }

        private static Dictionary<(int, string), List<CellEvaluation>> Copy(Dictionary<(int, string), List<CellEvaluation>> h)
            => h.ToDictionary(k => k.Key, k => k.Value.ToList());

        /// <summary>Güncel durumlar + yayımlanmış değerlendirme geçmişi (yalnız Publish modu, bu politika sürümü).</summary>
        private async Task<(Dictionary<(int, string), string> States, Dictionary<(int, string), List<CellEvaluation>> History)> LoadPublishedAsync(CancellationToken ct)
        {
            var states = (await _db.MarketEligibilityStates.AsNoTracking().ToListAsync(ct).ConfigureAwait(false))
                .ToDictionary(s => (s.OrganizationId, s.MarketFamily), s => s.PublishedState);
            var rows = await _db.MarketEligibilityEvaluations.AsNoTracking()
                .Where(e => e.Mode == "Publish" && e.PolicyVersion == EligibilityPublicationPolicy.Version)
                .OrderBy(e => e.EvaluationCutoffUtc).ToListAsync(ct).ConfigureAwait(false);
            // Kararlılık geçmişi yalnız SAYILAN pencerelerdir (NO_NEW_EVIDENCE kayıtları audit'tir, sayaç ilerletmez).
            var history = rows.Select(ToCell).Where(e => e.Counts).GroupBy(e => e.Cell).ToDictionary(g => g.Key, g => g.ToList());
            return (states, history);
        }

        /// <summary>
        /// Hücre başına son SAYILAN yayın penceresinin kanıtı (parmak izi + manifest). Manifestosu olmayan (koruma öncesi) pencere
        /// aynı kesimle deterministik yeniden ölçülür; o pencerenin değerlendirme anından SONRA yazılmış sonuçlar manifestten
        /// çıkarılır (o an bilinmiyordu — aksi hâlde gerçek yeni kanıt gizlenirdi). <paramref name="persist"/> ise Reconstructed=true yazılır.
        /// </summary>
        private async Task<Dictionary<(int, string), (string? Fingerprint, SortedDictionary<int, (int, int)>? Manifest)>> LoadCountedEvidenceAsync(
            bool persist, DateTime nowUtc, CancellationToken ct)
        {
            var rows = await _db.MarketEligibilityEvaluations.AsNoTracking()
                .Where(e => e.Mode == "Publish" && e.PolicyVersion == EligibilityPublicationPolicy.Version
                            && (e.TransitionStatus == null || e.TransitionStatus != TransitionStatuses.NoNewEvidence))
                .Select(e => new { e.OrganizationId, e.MarketFamily, e.EvaluationCutoffUtc, e.EvaluatedAtUtc, e.EvidenceFingerprint, e.ModelVersion, e.ConfigHash })
                .ToListAsync(ct).ConfigureAwait(false);
            var latest = rows.GroupBy(r => (r.OrganizationId, r.MarketFamily)).Select(g => g.OrderBy(r => r.EvaluationCutoffUtc).Last()).ToList();
            var result = new Dictionary<(int, string), (string?, SortedDictionary<int, (int, int)>?)>();
            if (latest.Count == 0) return result;
            var cutoffs = latest.Select(l => l.EvaluationCutoffUtc).Distinct().ToList();
            var manifests = (await _db.MarketEligibilityEvidenceManifests.AsNoTracking()
                    .Where(m => cutoffs.Contains(m.EvaluationCutoffUtc) && m.PolicyVersion == EligibilityPublicationPolicy.Version)
                    .ToListAsync(ct).ConfigureAwait(false))
                .ToDictionary(m => (m.OrganizationId, m.EvaluationCutoffUtc, m.ModelVersion, m.ConfigHash), m => EvidenceManifest.Parse(m.Manifest));

            // Eksik manifestoları kesim başına bir kez yeniden üret.
            foreach (var group in latest.Where(l => !manifests.ContainsKey((l.OrganizationId, l.EvaluationCutoffUtc, l.ModelVersion, l.ConfigHash)))
                         .GroupBy(l => l.EvaluationCutoffUtc))
            {
                var cutoff = DateTime.SpecifyKind(group.Key, DateTimeKind.Utc);
                var knownAt = DateTime.SpecifyKind(group.Max(g => g.EvaluatedAtUtc), DateTimeKind.Utc);
                var re = await _source.EvaluateAsync(cutoff, knownAt, ct).ConfigureAwait(false);
                foreach (var org in re.Where(e => e.Evidence != null).GroupBy(e => e.OrganizationId))
                {
                    var src = org.First();
                    var known = new EvidenceManifest { OrganizationId = org.Key, MaxKickoffUtc = src.Evidence!.MaxKickoffUtc };
                    foreach (var (id, score) in src.Evidence!.Results)
                    {
                        var upd = src.Evidence.UpdatedAt.GetValueOrDefault(id);
                        if (upd != null && upd > knownAt) continue; // o değerlendirmede bilinmiyordu
                        known.Results[id] = score;
                        known.UpdatedAt[id] = upd;
                        if (upd != null && (known.MaxResultUpdatedUtc == null || upd > known.MaxResultUpdatedUtc)) known.MaxResultUpdatedUtc = upd;
                    }
                    manifests[(org.Key, group.Key, src.ModelVersion, src.ConfigHash)] = known.Results;
                    if (persist && !await _db.MarketEligibilityEvidenceManifests.AnyAsync(m => m.OrganizationId == org.Key && m.EvaluationCutoffUtc == cutoff
                            && m.ModelVersion == src.ModelVersion && m.ConfigHash == src.ConfigHash && m.PolicyVersion == EligibilityPublicationPolicy.Version, ct).ConfigureAwait(false))
                        _db.MarketEligibilityEvidenceManifests.Add(ManifestRow(known, cutoff, src, reconstructed: true, nowUtc));
                }
                if (persist) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                _log.LogInformation("[ELIGIBILITY PUBLICATION] kanıt manifestosu yeniden üretildi: kesim={Cutoff:O} bilinenAn={Known:O}", cutoff, knownAt);
            }
            foreach (var l in latest)
                result[(l.OrganizationId, l.MarketFamily)] = (l.EvidenceFingerprint,
                    manifests.TryGetValue((l.OrganizationId, l.EvaluationCutoffUtc, l.ModelVersion, l.ConfigHash), out var m) ? m : null);
            return result;
        }

        private static MarketEligibilityEvidenceManifest ManifestRow(EvidenceManifest m, DateTime cutoff, CellEvaluation lineage, bool reconstructed, DateTime nowUtc) => new()
        {
            OrganizationId = m.OrganizationId, EvaluationCutoffUtc = cutoff, ModelVersion = lineage.ModelVersion, ConfigHash = lineage.ConfigHash,
            PolicyVersion = EligibilityPublicationPolicy.Version, SampleCount = m.SampleCount,
            MaxKickoffUtc = m.MaxKickoffUtc, MaxResultUpdatedUtc = m.MaxResultUpdatedUtc,
            ManifestHash = m.Hash(), Manifest = m.Serialize(), Reconstructed = reconstructed, CreatedAtUtc = nowUtc
        };

        private async Task PersistManifestsAsync(IReadOnlyList<CellEvaluation> evals, bool reconstructed, DateTime nowUtc, CancellationToken ct)
        {
            foreach (var org in evals.Where(e => e.Evidence != null).GroupBy(e => e.OrganizationId))
            {
                var e = org.First();
                if (_db.MarketEligibilityEvidenceManifests.Local.Any(m => m.OrganizationId == org.Key && m.EvaluationCutoffUtc == e.EvaluationCutoffUtc && m.ConfigHash == e.ConfigHash && m.ModelVersion == e.ModelVersion)) continue;
                if (await _db.MarketEligibilityEvidenceManifests.AnyAsync(m => m.OrganizationId == org.Key && m.EvaluationCutoffUtc == e.EvaluationCutoffUtc
                        && m.ModelVersion == e.ModelVersion && m.ConfigHash == e.ConfigHash && m.PolicyVersion == EligibilityPublicationPolicy.Version, ct).ConfigureAwait(false)) continue;
                _db.MarketEligibilityEvidenceManifests.Add(ManifestRow(e.Evidence!, e.EvaluationCutoffUtc, e, reconstructed, nowUtc));
            }
        }

        public static CellEvaluation ToCell(MarketEligibilityEvaluation e) => new()
        {
            OrganizationId = e.OrganizationId, MarketFamily = e.MarketFamily, EvaluationCutoffUtc = DateTime.SpecifyKind(e.EvaluationCutoffUtc, DateTimeKind.Utc),
            ModelVersion = e.ModelVersion, ModelRunId = e.ModelRunId, ConfigHash = e.ConfigHash, PolicyVersion = e.PolicyVersion,
            GatePolicyVersion = e.GatePolicyVersion, SampleCount = e.SampleCount, LogLoss = e.LogLoss, Brier = e.Brier, Ece = e.Ece,
            CalibrationSlope = e.CalibrationSlope, CalibrationIntercept = e.CalibrationIntercept, BaselineLogLoss = e.BaselineLogLoss,
            DifferenceFromBaseline = e.DifferenceFromBaseline, ConfidenceIntervalLow = e.ConfidenceIntervalLow, ConfidenceIntervalHigh = e.ConfidenceIntervalHigh,
            Bias = e.Bias, Coverage = e.Coverage, GateStatus = e.GateStatus, RawGateStatus = e.RawGateStatus,
            RawGateReasons = JsonSerializer.Deserialize<List<string>>(e.RawGateReasonsJson) ?? new List<string>(),
            EvaluatedAtUtc = e.EvaluatedAtUtc, EvidenceFingerprint = e.EvidenceFingerprint, NewEvidenceCount = e.NewEvidenceCount,
            TransitionStatus = e.TransitionStatus
        };

        private static MarketEligibilityEvaluation ToRow(CellEvaluation e, string mode, string source, string runKey) => new()
        {
            OrganizationId = e.OrganizationId, MarketFamily = e.MarketFamily, EvaluationCutoffUtc = e.EvaluationCutoffUtc,
            ModelVersion = e.ModelVersion, ModelRunId = e.ModelRunId, ConfigHash = e.ConfigHash, PolicyVersion = e.PolicyVersion,
            GatePolicyVersion = e.GatePolicyVersion, Mode = mode, Source = source, SampleCount = e.SampleCount,
            LogLoss = e.LogLoss, Brier = e.Brier, Ece = e.Ece, CalibrationSlope = e.CalibrationSlope, CalibrationIntercept = e.CalibrationIntercept,
            BaselineLogLoss = e.BaselineLogLoss, DifferenceFromBaseline = e.DifferenceFromBaseline,
            ConfidenceIntervalLow = e.ConfidenceIntervalLow, ConfidenceIntervalHigh = e.ConfidenceIntervalHigh,
            Bias = e.Bias, Coverage = e.Coverage, GateStatus = e.GateStatus, RawGateStatus = e.RawGateStatus,
            RawGateReasonsJson = JsonSerializer.Serialize(e.RawGateReasons, Json), EvaluatedAtUtc = e.EvaluatedAtUtc,
            PublicationRunKey = runKey, EvidenceFingerprint = e.EvidenceFingerprint, NewEvidenceCount = e.NewEvidenceCount,
            TransitionStatus = e.TransitionStatus
        };

        private async Task PersistEvaluationsAsync(IReadOnlyList<CellEvaluation> evals, string mode, string source,
            IReadOnlyDictionary<(int, string), StateTransition>? transitions, string runKey, CancellationToken ct)
        {
            // Değişmez kayıt: aynı (hücre, kesim, soy, mod) varsa yeniden yazılmaz.
            var cutoff = evals.Select(e => e.EvaluationCutoffUtc).FirstOrDefault();
            var existing = (await _db.MarketEligibilityEvaluations.AsNoTracking()
                    .Where(e => e.EvaluationCutoffUtc == cutoff && e.Mode == mode && e.PolicyVersion == EligibilityPublicationPolicy.Version)
                    .Select(e => new { e.OrganizationId, e.MarketFamily, e.ModelVersion, e.ConfigHash })
                    .ToListAsync(ct).ConfigureAwait(false))
                .Select(e => (e.OrganizationId, e.MarketFamily, e.ModelVersion, e.ConfigHash)).ToHashSet();
            foreach (var e in evals)
            {
                if (existing.Contains((e.OrganizationId, e.MarketFamily, e.ModelVersion, e.ConfigHash))) continue;
                var row = ToRow(e, mode, source, runKey);
                if (transitions != null && transitions.TryGetValue(e.Cell, out var t))
                {
                    row.PublishedStateBefore = t.Before;
                    row.PublishedStateAfter = t.After;
                    row.TransitionReason = Trim(t.Reason, 200);
                }
                _db.MarketEligibilityEvaluations.Add(row);
            }
        }

        private async Task PersistPublishAsync(IReadOnlyList<CellEvaluation> evals, IReadOnlyList<StateTransition> transitions, string source,
            string runKey, DateTime cutoff, DateTime nowUtc, CancellationToken ct)
        {
            var byCell = transitions.ToDictionary(t => (t.OrganizationId, t.MarketFamily));
            await PersistEvaluationsAsync(evals, "Publish", source, byCell, runKey, ct).ConfigureAwait(false);
            var tracked = await _db.MarketEligibilityStates.ToListAsync(ct).ConfigureAwait(false);
            foreach (var t in transitions)
            {
                var e = evals.First(x => x.OrganizationId == t.OrganizationId && x.MarketFamily == t.MarketFamily);
                var s = tracked.FirstOrDefault(x => x.OrganizationId == t.OrganizationId && x.MarketFamily == t.MarketFamily);
                if (s == null)
                {
                    s = new MarketEligibilityState
                    {
                        OrganizationId = t.OrganizationId, MarketFamily = t.MarketFamily, PublishedState = PublishedStates.Closed,
                        StateVersion = 0, PolicyVersion = EligibilityPublicationPolicy.Version
                    };
                    _db.MarketEligibilityStates.Add(s);
                    tracked.Add(s);
                }
                if (t.Changed || s.StateVersion == 0)
                {
                    s.StateVersion++;
                    _db.MarketEligibilityStateTransitions.Add(new MarketEligibilityStateTransition
                    {
                        OrganizationId = t.OrganizationId, MarketFamily = t.MarketFamily, FromState = t.Before, ToState = t.After,
                        StateVersion = s.StateVersion, ReasonCode = Trim(t.Reason, 200), PublicationRunKey = runKey,
                        EvaluationCutoffUtc = cutoff, CreatedAtUtc = nowUtc
                    });
                }
                s.PublishedState = t.After;
                s.PolicyVersion = EligibilityPublicationPolicy.Version;
                s.ModelVersion = e.ModelVersion;
                s.ConfigHash = e.ConfigHash;
                s.LastEvaluationCutoffUtc = cutoff;
                s.LastRawGateStatus = e.RawGateStatus;
                s.LastTransitionReason = Trim(t.Reason, 200);
                s.LastPublicationRunKey = runKey;
                s.UpdatedAtUtc = nowUtc;
            }
        }

        private void AddRun(string runKey, string mode, string source, DateTime cutoff, EligibilityCutoffOutcome outcome, int cells, int changed, int visibility, Stopwatch sw)
        {
            _db.MarketEligibilityPublicationRuns.Add(new MarketEligibilityPublicationRun
            {
                RunKey = runKey, Mode = mode, Source = source, EvaluationCutoffUtc = cutoff, WeekKey = outcome.WeekKey,
                ModelVersion = OutcomeModelVersion.Current, ConfigHash = _source.ConfigHash, PolicyVersion = EligibilityPublicationPolicy.Version,
                StartedAtUtc = DateTime.UtcNow - sw.Elapsed, CompletedAtUtc = DateTime.UtcNow, DurationMs = sw.ElapsedMilliseconds,
                PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64, Cells = cells, Transitions = changed, VisibilityChanges = visibility,
                SummaryJson = JsonSerializer.Serialize(new
                {
                    pass = outcome.Evaluations.Count(e => e.IsPass),
                    fail = outcome.Evaluations.Count(e => e.RawGateStatus == RawGateStatuses.Fail),
                    hardFail = outcome.Evaluations.Count(e => e.IsHardFail),
                    changes = outcome.Transitions.Where(t => t.Changed).Select(t => $"{t.OrganizationId}:{t.MarketFamily} {t.Before}->{t.After} ({t.Reason})")
                }, Json)
            });
        }

        /// <summary>
        /// HEDEFLİ ÖNBELLEK TEMİZLİĞİ — yalnız görünürlüğü değişen hücrelerin organizasyonundaki yaklaşan maçlar yeniden hesaplama
        /// kuyruğuna girer (snapshot yeni yayın imzasıyla yeniden yazılır; diğer maçlara dokunulmaz).
        /// </summary>
        private async Task<int> InvalidateAsync(IReadOnlyList<StateTransition> changed, string runKey, DateTime nowUtc, CancellationToken ct)
        {
            if (changed.Count == 0) return 0;
            var leagues = changed.Select(t => t.OrganizationId).Distinct().ToList();
            var horizon = nowUtc + MatchPredictionSnapshotService.Horizon;
            var ids = await _db.Matches.AsNoTracking()
                .Where(m => leagues.Contains(m.LeagueId) && m.MatchDate > nowUtc && m.MatchDate <= horizon
                            && (m.Status == MatchStatuses.NotStarted || m.Status == MatchStatuses.PreMatch))
                .Select(m => m.Id).ToListAsync(ct).ConfigureAwait(false);
            var n = 0;
            // Tekillik: yayın turu (hafta) × maç — aynı turun ikinci temizliği kuyruğa ikinci istek eklemez.
            var week = runKey.Split('|').Last();
            foreach (var id in ids)
                if (await PredictionRecomputeQueue.EnqueueAsync(_db, id, "EligibilityChange", "eligibility-publication",
                        $"elig:{EligibilityPublicationPolicy.Version}:{week}:{id}", nowUtc, ct).ConfigureAwait(false)) n++;
            if (n > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return n;
        }

        // ═══════════════════════════ GERİ DÖNÜŞ ═══════════════════════════

        /// <summary>
        /// Son yayın turunu geri alır: o turun defterdeki her geçişi için hücre (hâlâ o turun bıraktığı durumdaysa) önceki
        /// duruma döner, yeni sürüm + ROLLBACK gerekçesi yazılır. Değerlendirme kayıtları SİLİNMEZ.
        /// </summary>
        public async Task<(string Result, int Reverted)> RollbackAsync(string publishRunKey, DateTime nowUtc, CancellationToken ct = default)
        {
            var target = await _db.MarketEligibilityPublicationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.RunKey == publishRunKey && r.Mode == "Publish", ct).ConfigureAwait(false);
            if (target == null) return ("RUN_NOT_FOUND", 0);
            var latest = await _db.MarketEligibilityPublicationRuns.AsNoTracking().Where(r => r.Mode == "Publish")
                .OrderByDescending(r => r.EvaluationCutoffUtc).Select(r => r.RunKey).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (latest != publishRunKey) return ("ONLY_LATEST_PUBLISH_CAN_BE_ROLLED_BACK", 0);
            var key = "rollback|" + publishRunKey;
            if (await _db.MarketEligibilityPublicationRuns.AsNoTracking().AnyAsync(r => r.RunKey == key, ct).ConfigureAwait(false)) return ("ALREADY_ROLLED_BACK", 0);
            var ledger = await _db.MarketEligibilityStateTransitions.AsNoTracking().Where(t => t.PublicationRunKey == publishRunKey).ToListAsync(ct).ConfigureAwait(false);
            var states = await _db.MarketEligibilityStates.ToListAsync(ct).ConfigureAwait(false);
            var n = 0;
            foreach (var t in ledger.Where(t => t.FromState != null))
            {
                var s = states.FirstOrDefault(x => x.OrganizationId == t.OrganizationId && x.MarketFamily == t.MarketFamily);
                if (s == null || s.PublishedState != t.ToState) continue;
                s.StateVersion++;
                s.PublishedState = t.FromState!;
                s.LastTransitionReason = "ROLLBACK:" + Trim(publishRunKey, 180);
                s.LastPublicationRunKey = key;
                s.UpdatedAtUtc = nowUtc;
                _db.MarketEligibilityStateTransitions.Add(new MarketEligibilityStateTransition
                {
                    OrganizationId = s.OrganizationId, MarketFamily = s.MarketFamily, FromState = t.ToState, ToState = t.FromState!,
                    StateVersion = s.StateVersion, ReasonCode = Trim("ROLLBACK:" + publishRunKey, 200), PublicationRunKey = key,
                    EvaluationCutoffUtc = t.EvaluationCutoffUtc, CreatedAtUtc = nowUtc
                });
                n++;
            }
            _db.MarketEligibilityPublicationRuns.Add(new MarketEligibilityPublicationRun
            {
                RunKey = key, Mode = "Rollback", Source = EligibilityPublicationSources.Manual, EvaluationCutoffUtc = target.EvaluationCutoffUtc,
                WeekKey = target.WeekKey, ModelVersion = OutcomeModelVersion.Current, ConfigHash = target.ConfigHash,
                PolicyVersion = EligibilityPublicationPolicy.Version, StartedAtUtc = nowUtc, CompletedAtUtc = DateTime.UtcNow,
                Cells = ledger.Count, Transitions = n, SummaryJson = "{}"
            });
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return ("ROLLED_BACK", n);
        }

        // ═══════════════════════════ ADMIN GÖRÜNÜMÜ ═══════════════════════════

        /// <summary>Salt okunur durum görünümü — DB okur; değerlendirme/eğitim/dış istek tetiklemez.</summary>
        public async Task<object> StatusAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var states = await _db.MarketEligibilityStates.AsNoTracking().OrderBy(s => s.OrganizationId).ThenBy(s => s.MarketFamily).ToListAsync(ct).ConfigureAwait(false);
            var (_, history) = await LoadPublishedAsync(ct).ConfigureAwait(false);
            var runs = await _db.MarketEligibilityPublicationRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(20)
                .Select(r => new { r.RunKey, r.Mode, r.Source, r.EvaluationCutoffUtc, r.WeekKey, r.StartedAtUtc, r.CompletedAtUtc, r.DurationMs, r.Cells, r.Transitions, r.VisibilityChanges, r.SourceModelRunId })
                .ToListAsync(ct).ConfigureAwait(false);
            var next = EligibilityEvaluationSchedule.NextSlotAfter(nowUtc);
            var lastEval = history.Values.SelectMany(h => h).Select(e => (DateTime?)e.EvaluatedAtUtc).DefaultIfEmpty(null).Max();
            return new
            {
                policyVersion = EligibilityPublicationPolicy.Version,
                gatePolicyVersion = MarketEligibilityPolicy.Version,
                modelVersion = OutcomeModelVersion.Current,
                configHash = _source.ConfigHash,
                thresholds = MarketEligibilityPolicy.ThresholdSummary,
                rules = new
                {
                    open = $"latest PASS; son {EligibilityPublicationPolicy.WindowCount} pencerede ≥{EligibilityPublicationPolicy.MinPassInWindow} PASS; son {EligibilityPublicationPolicy.RecentCount}'te ≥{EligibilityPublicationPolicy.MinPassInRecent} PASS; anlamlı kötülük/yapısal hard-fail yok; aynı soy; Closed→PendingOpen→Open",
                    close = "Open→(FAIL)→PendingClose→(FAIL)→Closed; PendingClose→(PASS)→Open; HARD_FAIL→Closed hemen"
                },
                bootstrapped = runs.Any(r => r.Mode == "Bootstrap") || await _db.MarketEligibilityPublicationRuns.AsNoTracking().AnyAsync(r => r.RunKey == BootstrapRunKey, ct).ConfigureAwait(false),
                lastEvaluatedAtUtc = lastEval,
                lastEvaluationCutoffUtc = states.Select(s => s.LastEvaluationCutoffUtc).DefaultIfEmpty(null).Max(),
                nextScheduledUtc = next,
                nextScheduledIstanbul = TimeZoneInfo.ConvertTimeFromUtc(next, EligibilityEvaluationSchedule.Istanbul).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " Europe/Istanbul",
                summary = new
                {
                    open = states.Count(s => s.PublishedState == PublishedStates.Open),
                    pendingClose = states.Count(s => s.PublishedState == PublishedStates.PendingClose),
                    pendingOpen = states.Count(s => s.PublishedState == PublishedStates.PendingOpen),
                    closed = states.Count(s => s.PublishedState == PublishedStates.Closed),
                    visibleToUsers = states.Count(s => PublishedStates.IsVisible(s.PublishedState))
                },
                cells = states.Select(s =>
                {
                    var h = history.TryGetValue((s.OrganizationId, s.MarketFamily), out var l) ? l.OrderBy(e => e.EvaluationCutoffUtc).ToList() : new List<CellEvaluation>();
                    var last5 = h.TakeLast(EligibilityPublicationPolicy.WindowCount).ToList();
                    var opening = EligibilityPublicationPolicy.CheckOpening(h);
                    return new
                    {
                        organizationId = s.OrganizationId, marketFamily = s.MarketFamily, publishedState = s.PublishedState,
                        visible = PublishedStates.IsVisible(s.PublishedState), s.StateVersion, rawGateStatus = s.LastRawGateStatus,
                        s.LastTransitionReason, s.LastEvaluationCutoffUtc, s.ModelVersion, s.ConfigHash, s.PolicyVersion,
                        passInLast5 = last5.Count(e => e.IsPass), failInLast5 = last5.Count(e => !e.IsPass),
                        hardFailInLast5 = last5.Count(e => e.IsHardFail),
                        pendingOpenProgress = s.PublishedState == PublishedStates.PendingOpen ? opening.Summary : null,
                        pendingCloseNote = s.PublishedState == PublishedStates.PendingClose ? "bir sonraki FAIL → Closed; PASS → Open" : null,
                        hardFailReasons = last5.Where(e => e.IsHardFail).SelectMany(e => e.RawGateReasons.Where(r => r.StartsWith("HARD_"))).Distinct(),
                        last5 = last5.Select(e => new
                        {
                            e.EvaluationCutoffUtc, e.RawGateStatus, e.GateStatus, reasons = e.RawGateReasons, e.SampleCount, e.LogLoss, e.BaselineLogLoss,
                            e.DifferenceFromBaseline, ciLow = e.ConfidenceIntervalLow, ciHigh = e.ConfidenceIntervalHigh, e.Ece, e.Bias, e.Coverage,
                            e.CalibrationSlope, e.CalibrationIntercept, e.ModelRunId
                        })
                    };
                }),
                runs
            };
        }

        private static string Trim(string s, int n) => s.Length > n ? s[..n] : s;
    }
}
