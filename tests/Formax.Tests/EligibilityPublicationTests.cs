using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Tests;

/// <summary>
/// KALICI UYGUNLUK YAYIN POLİTİKASI (eligibility-publication-1) — histerezisli durum makinesi, değişmez değerlendirme
/// geçmişi, dry-run / evaluate-only / publish ayrımı, bootstrap, haftalık takvim ve ürün kurallarının korunması.
/// Numaralar görev tanımındaki K maddeleridir.
/// </summary>
public class EligibilityPublicationTests
{
    private const string CH = "cfg0123456789abcdef0123456789abcd";
    private static readonly DateTime W1 = new(2026, 8, 14, 2, 0, 0, DateTimeKind.Utc);

    private static DateTime W(int i) => W1.AddDays(7 * i);

    private static CellEvaluation Ev(int league, string family, DateTime cutoff, string raw, int sample = 500, double ciLow = -0.05,
        string? model = null, string? policy = null, params string[] reasons)
        => new()
        {
            OrganizationId = league, MarketFamily = family, EvaluationCutoffUtc = cutoff, ModelVersion = model ?? OutcomeModelVersion.Current,
            ModelRunId = "elig-test", ConfigHash = CH, PolicyVersion = policy ?? EligibilityPublicationPolicy.Version,
            SampleCount = sample, ConfidenceIntervalLow = ciLow, ConfidenceIntervalHigh = raw == RawGateStatuses.Pass ? -0.01 : 0.01,
            Coverage = 1, GateStatus = raw == RawGateStatuses.Pass ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.Limited,
            RawGateStatus = raw, RawGateReasons = reasons.ToList(), EvaluatedAtUtc = cutoff
        };

    private const string P = RawGateStatuses.Pass;
    private const string F = RawGateStatuses.Fail;
    private const string H = RawGateStatuses.HardFail;

    /// <summary>Bir hücreye sırayla pencere uygular; son durumu ve geçiş gerekçelerini döner.</summary>
    private static (string Final, List<StateTransition> Steps) Walk(string initial, params string[] raws)
    {
        var states = new Dictionary<(int, string), string> { [(78, MarketFamilies.MatchResult)] = initial };
        var history = new Dictionary<(int, string), List<CellEvaluation>>();
        var steps = new List<StateTransition>();
        for (var i = 0; i < raws.Length; i++)
            steps.AddRange(EligibilityPublicationPolicy.Apply(states, history,
                new[] { Ev(78, MarketFamilies.MatchResult, W(i), raws[i], reasons: raws[i] == H ? new[] { HardFailCodes.CalibrationSafety } : Array.Empty<string>()) }));
        return (states[(78, MarketFamilies.MatchResult)], steps);
    }

    // ═══ 1–8: durum makinesi ═══

    [Fact]
    public void K01_TekPass_KapaliHucreyiAcmaz()
    {
        var (final, steps) = Walk(PublishedStates.Closed, P);
        Assert.Equal(PublishedStates.PendingOpen, final);
        Assert.False(PublishedStates.IsVisible(final));
        Assert.StartsWith("FIRST_PASS_PENDING_OPEN", steps.Single().Reason);
    }

    [Fact]
    public void K02_BesPenceredeDortPass_VeLatestPass_Acar()
    {
        var (final, _) = Walk(PublishedStates.Closed, P, P, F, P, P);
        Assert.Equal(PublishedStates.Open, final);
        var (final2, _) = Walk(PublishedStates.Closed, P, P, P, P);
        Assert.Equal(PublishedStates.Open, final2);
    }

    [Fact]
    public void K03_LatestFail_DortBesOlsaBile_Acmaz()
    {
        var hist = new[] { P, P, P, P }.Select((r, i) => Ev(78, MarketFamilies.MatchResult, W(i), r)).ToList();
        var latest = Ev(78, MarketFamilies.MatchResult, W(4), F);
        Assert.False(EligibilityPublicationPolicy.CheckOpening(hist.Append(latest).ToList()).AllMet);
        var t = EligibilityPublicationPolicy.Decide(PublishedStates.PendingOpen, hist, latest);
        Assert.Equal(PublishedStates.Closed, t.After);
        Assert.Equal(PublishedStates.Closed, EligibilityPublicationPolicy.Decide(PublishedStates.Closed, hist, latest).After);
    }

    [Fact]
    public void K04_IlkUygunSeri_Closed_PendingOpen()
    {
        var t = EligibilityPublicationPolicy.Decide(PublishedStates.Closed, Array.Empty<CellEvaluation>(), Ev(78, MarketFamilies.MatchResult, W(0), P));
        Assert.Equal(PublishedStates.Closed, t.Before);
        Assert.Equal(PublishedStates.PendingOpen, t.After);
    }

    [Fact]
    public void K05_KararlilikTamamlaninca_PendingOpen_Open()
    {
        var hist = new[] { P, P, F, P }.Select((r, i) => Ev(78, MarketFamilies.MatchResult, W(i), r)).ToList();
        var t = EligibilityPublicationPolicy.Decide(PublishedStates.PendingOpen, hist, Ev(78, MarketFamilies.MatchResult, W(4), P));
        Assert.Equal(PublishedStates.Open, t.After);
        Assert.Equal("STABILITY_CONFIRMED_OPEN", t.Reason);
        Assert.True(t.Opening!.AllMet);
    }

    [Fact]
    public void K06_OpenHucredeTekNormalFail_PendingClose()
    {
        var (final, steps) = Walk(PublishedStates.Open, F);
        Assert.Equal(PublishedStates.PendingClose, final);
        Assert.True(PublishedStates.IsVisible(final));
        Assert.Equal("FIRST_FAIL_PENDING_CLOSE", steps.Single().Reason);
    }

    [Fact]
    public void K07_PendingCloseArdindanPass_Open()
    {
        var (final, steps) = Walk(PublishedStates.Open, F, P);
        Assert.Equal(PublishedStates.Open, final);
        Assert.Equal("RECOVERED_PASS_AFTER_PENDING_CLOSE", steps.Last().Reason);
    }

    [Fact]
    public void K08_IkiArdisikNormalFail_Closed()
    {
        var (final, steps) = Walk(PublishedStates.Open, F, F);
        Assert.Equal(PublishedStates.Closed, final);
        Assert.Equal("SECOND_CONSECUTIVE_FAIL_CLOSED", steps.Last().Reason);
    }

    // ═══ 9–12: hard-fail ═══

    [Fact]
    public void K09_HardFail_AcikHucreyiHemenKapatir()
    {
        foreach (var s in new[] { PublishedStates.Open, PublishedStates.PendingClose, PublishedStates.PendingOpen })
        {
            var (final, steps) = Walk(s, H);
            Assert.Equal(PublishedStates.Closed, final);
            Assert.StartsWith("HARD_FAIL_IMMEDIATE_CLOSE", steps.Single().Reason);
        }
    }

    [Fact]
    public void K10_AnlamliKotuluk_HardFailSayilir_NoktaKotulukNormalFail()
    {
        var significant = new MarketFamilyMetrics { LeagueId = 39, Family = MarketFamilies.MatchResult, Matches = 500, Status = MarketEligibilityStatuses.WorseThanBaseline, LogLossDiff = 0.02, LogLossDiffCiLow = 0.004, LogLossDiffCiHigh = 0.03, ReasonCodes = new() { "WORSE_THAN_LEAGUE_AVERAGE" } };
        var (raw, reasons) = EligibilityPublicationPolicy.Classify(significant, EvaluationIntegrity.Clean);
        Assert.Equal(H, raw);
        Assert.Contains(HardFailCodes.SignificantlyWorse, reasons);

        var noisy = new MarketFamilyMetrics { LeagueId = 39, Family = MarketFamilies.MatchResult, Matches = 500, Status = MarketEligibilityStatuses.WorseThanBaseline, LogLossDiff = 0.002, LogLossDiffCiLow = -0.004, LogLossDiffCiHigh = 0.008, ReasonCodes = new() { "WORSE_THAN_LEAGUE_AVERAGE" } };
        Assert.Equal(F, EligibilityPublicationPolicy.Classify(noisy, EvaluationIntegrity.Clean).Raw);
    }

    [Fact]
    public void K11_Sizinti_HardFailSayilir_GercekBacktestGirdisinde()
    {
        var m = new MarketFamilyMetrics { LeagueId = 39, Family = MarketFamilies.MatchResult, Matches = 500, Status = MarketEligibilityStatuses.Eligible };
        var (raw, reasons) = EligibilityPublicationPolicy.Classify(m, new EvaluationIntegrity { Leakage = true });
        Assert.Equal(H, raw);
        Assert.Contains(HardFailCodes.Leakage, reasons);

        // Uçtan uca: kesimden SONRA oynanmış bir maç geçmişe sızarsa kaynak bütün hücreleri HARD_LEAKAGE yapar.
        var history = Synthetic(39, 8, 6, 3, new DateTime(2024, 6, 1, 18, 0, 0, DateTimeKind.Utc));
        var cutoff = history[^1].KickoffUtc.AddDays(1);
        var leaked = history.Append(new HistoricalMatch(999999, cutoff.AddDays(2), 39, 39001, 39002, 1, 0)).ToList();
        var evals = BacktestEligibilityEvaluationSource.Evaluate(leaked, new Dictionary<int, string> { [39] = "Premier League" }, cutoff, cutoff, CH);
        Assert.All(evals.Where(e => e.OrganizationId == 39), e => Assert.Contains(HardFailCodes.Leakage, e.RawGateReasons));
        var clean = BacktestEligibilityEvaluationSource.Evaluate(history, new Dictionary<int, string> { [39] = "Premier League" }, cutoff, cutoff, CH);
        Assert.All(clean, e => Assert.DoesNotContain(HardFailCodes.Leakage, e.RawGateReasons));
    }

    [Fact]
    public void K12_VeriButunluguHatasi_HardFailSayilir()
    {
        var dq = new MarketFamilyMetrics { LeagueId = 39, Family = MarketFamilies.TotalGoals25, Matches = 0, Status = MarketEligibilityStatuses.DataQualityFailed };
        Assert.Contains(HardFailCodes.DataIntegrity, EligibilityPublicationPolicy.Classify(dq, EvaluationIntegrity.Clean).Reasons);
        var nan = new MarketFamilyMetrics { LeagueId = 39, Family = MarketFamilies.TotalGoals25, Matches = 400, Status = MarketEligibilityStatuses.Eligible, LogLoss = double.NaN };
        Assert.Equal(H, EligibilityPublicationPolicy.Classify(nan, EvaluationIntegrity.Clean).Raw);
        var homeAway = new MarketFamilyMetrics { LeagueId = 39, Family = MarketFamilies.MatchResult, Matches = 400, Status = MarketEligibilityStatuses.Eligible };
        Assert.Contains(HardFailCodes.HomeAwayIdentity, EligibilityPublicationPolicy.Classify(homeAway, new EvaluationIntegrity { HomeAwayViolation = true }).Reasons);
        Assert.Contains(HardFailCodes.ProbabilitySum, EligibilityPublicationPolicy.Classify(homeAway, new EvaluationIntegrity { ProbabilitySumViolation = true }).Reasons);
        Assert.Contains(HardFailCodes.ModelIdentity, EligibilityPublicationPolicy.Classify(homeAway, new EvaluationIntegrity { ModelIdentityViolation = true }).Reasons);
        // Mevcut Disabled sınırları hard-fail'dir: < 100 örneklem, ECE > 0,06.
        Assert.Contains(HardFailCodes.SampleSeverelyBelow, EligibilityPublicationPolicy.Classify(new MarketFamilyMetrics { Matches = 60, Status = MarketEligibilityStatuses.InsufficientSample }, EvaluationIntegrity.Clean).Reasons);
        Assert.Contains(HardFailCodes.CalibrationSafety, EligibilityPublicationPolicy.Classify(new MarketFamilyMetrics { Matches = 400, Status = MarketEligibilityStatuses.CalibrationFailed }, EvaluationIntegrity.Clean).Reasons);
    }

    // ═══ 13–17 ve 33–34: servis (InMemory) ═══

    private sealed class FakeSource : IEligibilityEvaluationSource
    {
        public string ConfigHash => CH;
        public int Calls;
        public Func<DateTime, IReadOnlyList<CellEvaluation>> Fn = _ => Array.Empty<CellEvaluation>();
        public Task<IReadOnlyList<CellEvaluation>> EvaluateAsync(DateTime cutoffUtc, DateTime evaluatedAtUtc, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Fn(cutoffUtc));
        }
    }

    /// <summary>Bütün kilitli organizasyonlar × aileler; varsayılan FAIL, verilen hücreler özel.</summary>
    private static IReadOnlyList<CellEvaluation> Matrix(DateTime cutoff, Dictionary<(int, string), string>? overrides = null)
        => LockedCompetitions.All.SelectMany(l => MarketFamilies.All.Select(f =>
        {
            var raw = overrides != null && overrides.TryGetValue((l, f), out var r) ? r : F;
            return Ev(l, f, cutoff, raw, reasons: raw == H ? new[] { HardFailCodes.SignificantlyWorse } : Array.Empty<string>());
        })).ToList();

    private sealed class Store
    {
        private readonly string _name = "elig-" + Guid.NewGuid().ToString("N");
        public FormaxDbContext Db() => new(new DbContextOptionsBuilder<FormaxDbContext>().UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        /// <summary>Üretim matrisi: EPL/Ligue 1/Süper Lig 1X2+ÇŞ, La Liga 1X2+ÇŞ+1.5 (19.09 run-b33c… ile aynı 9 hücre).</summary>
        public static readonly (int, string)[] ProductionOpen =
        {
            (39, MarketFamilies.MatchResult), (39, MarketFamilies.DoubleChance), (61, MarketFamilies.MatchResult), (61, MarketFamilies.DoubleChance),
            (140, MarketFamilies.MatchResult), (140, MarketFamilies.DoubleChance), (140, MarketFamilies.TotalGoals15),
            (203, MarketFamilies.MatchResult), (203, MarketFamilies.DoubleChance)
        };

        public static readonly DateTime ProdRunCompleted = new(2026, 9, 19, 8, 37, 9, DateTimeKind.Utc);

        public Store(bool seedRun = true)
        {
            if (!seedRun) return;
            using var db = Db();
            db.PredictionModelRuns.Add(new PredictionModelRun
            {
                RunId = "run-prod", ModelVersion = OutcomeModelVersion.Current, Status = "Accepted", StartedAtUtc = ProdRunCompleted.AddSeconds(-19),
                CompletedAtUtc = ProdRunCompleted, ParametersJson = "{}", MetricsJson = "{}"
            });
            // Daha yeni bir koşu (günlük eğitim) Bundesliga'yı açıyor — bootstrap onu değil, YAYINDAKİ koşuyu almalı.
            db.PredictionModelRuns.Add(new PredictionModelRun
            {
                RunId = "run-today", ModelVersion = OutcomeModelVersion.Current, Status = "Accepted", StartedAtUtc = ProdRunCompleted.AddDays(6),
                CompletedAtUtc = ProdRunCompleted.AddDays(6), ParametersJson = "{}", MetricsJson = "{}"
            });
            foreach (var l in LockedCompetitions.All)
                foreach (var f in MarketFamilies.All)
                {
                    db.LeagueMarketEligibilities.Add(new LeagueMarketEligibility
                    {
                        RunId = "run-prod", ModelVersion = OutcomeModelVersion.Current, PolicyVersion = MarketEligibilityPolicy.Version, LeagueId = l, Family = f,
                        Status = ProductionOpen.Contains((l, f)) ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.Limited,
                        ReasonsJson = "[]", MetricsJson = "{}", TestMatches = 500, EvaluatedAtUtc = ProdRunCompleted
                    });
                    db.LeagueMarketEligibilities.Add(new LeagueMarketEligibility
                    {
                        RunId = "run-today", ModelVersion = OutcomeModelVersion.Current, PolicyVersion = MarketEligibilityPolicy.Version, LeagueId = l, Family = f,
                        Status = ProductionOpen.Contains((l, f)) || (l == 78 && f is MarketFamilies.MatchResult or MarketFamilies.DoubleChance)
                            ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.Limited,
                        ReasonsJson = "[]", MetricsJson = "{}", TestMatches = 500, EvaluatedAtUtc = ProdRunCompleted.AddDays(6)
                    });
                }
            db.MatchPredictionSnapshots.Add(new MatchPredictionSnapshot
            {
                SnapshotId = "snp-current", MatchId = 1, ModelVersion = OutcomeModelVersion.Current, CalibrationRunId = "run-prod", IsCurrent = true,
                ComputedAtUtc = ProdRunCompleted, InputsCutoffUtc = ProdRunCompleted, PayloadJson = "{}", InputHash = "h"
            });
            db.SaveChanges();
        }

        public EligibilityPublicationService Service(FormaxDbContext db, IEligibilityEvaluationSource source)
            => new(db, source, NullLogger<EligibilityPublicationService>.Instance);
    }

    private static readonly DateTime Now = new(2026, 9, 25, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task K13_AyniKesimConfig_TekrarKosu_DuplicateUretmez()
    {
        var store = new Store();
        var src = new FakeSource { Fn = c => Matrix(c) };
        using (var db = store.Db()) await store.Service(db, src).EnsureBootstrappedAsync(Now);
        using (var db = store.Db()) await store.Service(db, src).RunAsync(new[] { W(6) }, EligibilityPublicationMode.EvaluateOnly, "Manual", Now);
        using (var db = store.Db()) await store.Service(db, src).RunAsync(new[] { W(6) }, EligibilityPublicationMode.EvaluateOnly, "Manual", Now);
        using (var db = store.Db()) await store.Service(db, src).RunAsync(new[] { W(6) }, EligibilityPublicationMode.Publish, "Manual", Now);
        using (var db = store.Db())
        {
            var r = await store.Service(db, src).RunAsync(new[] { W(6) }, EligibilityPublicationMode.Publish, "Manual", Now);
            Assert.Equal("SKIPPED_ALREADY_PUBLISHED_THIS_WEEK", r.Cutoffs.Single().Result);
        }
        using (var db = store.Db())
        {
            var cells = LockedCompetitions.All.Count * MarketFamilies.All.Count;
            Assert.Equal(cells, db.MarketEligibilityEvaluations.Count(e => e.Mode == "EvaluateOnly"));
            Assert.Equal(cells, db.MarketEligibilityEvaluations.Count(e => e.Mode == "Publish"));
            // Benzersiz anahtar: (hücre, kesim, model, config, politika, mod) tekrarı yok.
            Assert.Equal(db.MarketEligibilityEvaluations.Count(), db.MarketEligibilityEvaluations.AsEnumerable()
                .Select(e => (e.OrganizationId, e.MarketFamily, e.EvaluationCutoffUtc, e.ModelVersion, e.ConfigHash, e.PolicyVersion, e.Mode)).Distinct().Count());
        }
        Assert.Equal(2, src.Calls); // ikinci evaluate-only ve ikinci publish değerlendirme bile çalıştırmadı
    }

    [Fact]
    public async Task K14_DryRun_PublishedStateDegistirmez_HicbirSeyYazmaz()
    {
        var store = new Store();
        var src = new FakeSource { Fn = c => Matrix(c, new() { [(39, MarketFamilies.MatchResult)] = H, [(78, MarketFamilies.MatchResult)] = P }) };
        using (var db = store.Db()) await store.Service(db, src).EnsureBootstrappedAsync(Now);
        string before; int evals, runs;
        using (var db = store.Db()) { before = Snapshot(db); evals = db.MarketEligibilityEvaluations.Count(); runs = db.MarketEligibilityPublicationRuns.Count(); }
        EligibilityPublicationReport rep;
        using (var db = store.Db()) rep = await store.Service(db, src).RunAsync(new[] { W(6) }, EligibilityPublicationMode.DryRun, "Manual", Now);
        Assert.Contains(rep.Cutoffs.Single().Transitions, t => t.OrganizationId == 39 && t.MarketFamily == MarketFamilies.MatchResult && t.After == PublishedStates.Closed);
        using (var db = store.Db())
        {
            Assert.Equal(before, Snapshot(db));
            Assert.Equal(evals, db.MarketEligibilityEvaluations.Count());
            Assert.Equal(runs, db.MarketEligibilityPublicationRuns.Count());
        }
    }

    [Fact]
    public async Task K15_EvaluateOnly_PublishedStateDegistirmez()
    {
        var store = new Store();
        var src = new FakeSource { Fn = c => Matrix(c, new() { [(39, MarketFamilies.MatchResult)] = H }) };
        using (var db = store.Db()) await store.Service(db, src).EnsureBootstrappedAsync(Now);
        string before;
        using (var db = store.Db()) before = Snapshot(db);
        using (var db = store.Db()) await store.Service(db, src).RunAsync(new[] { W(6) }, EligibilityPublicationMode.EvaluateOnly, "Manual", Now);
        using (var db = store.Db())
        {
            Assert.Equal(before, Snapshot(db));
            Assert.True(db.MarketEligibilityEvaluations.All(e => e.Mode == "EvaluateOnly" && e.PublishedStateAfter == null));
            Assert.Equal(PublishedStates.Open, db.MarketEligibilityStates.Single(s => s.OrganizationId == 39 && s.MarketFamily == MarketFamilies.MatchResult).PublishedState);
        }
    }

    [Fact]
    public async Task K16_Publish_YalnizYetkiliAkista_DurumDegistirir()
    {
        // Bootstrap yoksa publish reddedilir.
        var empty = new Store(seedRun: false);
        using (var db = empty.Db())
            await Assert.ThrowsAsync<InvalidOperationException>(() => empty.Service(db, new FakeSource()).RunAsync(new[] { W(6) }, EligibilityPublicationMode.Publish, "Manual", Now));
        // Admin ucu publish'i yalnız confirm=PUBLISH ile çalıştırır; dry-run/evaluate-only durum yazan koda hiç girmez.
        var ctl = Source("Formax.API", "Controllers", "Admin", "AdminEligibilityPublicationController.cs");
        Assert.Contains("confirm != \"PUBLISH\"", ctl);
        Assert.Contains("StatusCode(403", ctl);
        // Yetkili akışta durum değişir.
        var store = new Store();
        var src = new FakeSource { Fn = c => Matrix(c, new() { [(39, MarketFamilies.MatchResult)] = H }) };
        using (var db = store.Db()) await store.Service(db, src).EnsureBootstrappedAsync(Now);
        using (var db = store.Db()) await store.Service(db, src).RunAsync(new[] { W(6) }, EligibilityPublicationMode.Publish, EligibilityPublicationSources.Scheduled, Now);
        using (var db = store.Db())
        {
            var s = db.MarketEligibilityStates.Single(x => x.OrganizationId == 39 && x.MarketFamily == MarketFamilies.MatchResult);
            Assert.Equal(PublishedStates.Closed, s.PublishedState);
            Assert.Equal(2, s.StateVersion);
            var ledger = db.MarketEligibilityStateTransitions.Where(x => x.OrganizationId == 39 && x.MarketFamily == MarketFamilies.MatchResult).OrderBy(x => x.StateVersion).ToList();
            Assert.Equal(new string?[] { null, PublishedStates.Open }, ledger.Select(x => x.FromState).ToArray());
            Assert.StartsWith("HARD_FAIL_IMMEDIATE_CLOSE", ledger.Last().ReasonCode);
        }
    }

    [Fact]
    public async Task K17_GecmisDegerlendirmesiOlmayanHucre_Acilmaz()
    {
        // Koşu yoksa bootstrap hiçbir hücreyi açmaz.
        var empty = new Store(seedRun: false);
        using (var db = empty.Db()) Assert.Equal(0, (await empty.Service(db, new FakeSource()).EnsureBootstrappedAsync(Now)).Open);
        using (var db = empty.Db()) Assert.All(db.MarketEligibilityStates, s => Assert.Equal(PublishedStates.Closed, s.PublishedState));
        // Tek bir PASS, geçmiş olmadan açmaz.
        Assert.False(EligibilityPublicationPolicy.CheckOpening(new[] { Ev(78, MarketFamilies.MatchResult, W(0), P) }).AllMet);
    }

    [Fact]
    public void K18_FarkliModelSurumleri_AyniPencereyeKarismaz()
    {
        var old = new[] { P, P, P, P }.Select((r, i) => Ev(78, MarketFamilies.MatchResult, W(i), r, model: "formax-outcome-3.0")).ToList();
        var latest = Ev(78, MarketFamilies.MatchResult, W(4), P);
        var check = EligibilityPublicationPolicy.CheckOpening(old.Append(latest).ToList());
        Assert.Equal(1, check.WindowsInLast5);
        Assert.False(check.AllMet);
        var states = new Dictionary<(int, string), string> { [latest.Cell] = PublishedStates.PendingOpen };
        var history = new Dictionary<(int, string), List<CellEvaluation>> { [latest.Cell] = old };
        Assert.Equal(PublishedStates.PendingOpen, EligibilityPublicationPolicy.Apply(states, history, new[] { latest }).Single().After);
    }

    [Fact]
    public void K19_FarkliPolitikaSurumleri_Karismaz()
    {
        var old = new[] { P, P, P, P }.Select((r, i) => Ev(78, MarketFamilies.MatchResult, W(i), r, policy: "eligibility-publication-0")).ToList();
        var latest = Ev(78, MarketFamilies.MatchResult, W(4), P);
        Assert.Equal(1, EligibilityPublicationPolicy.CheckOpening(old.Append(latest).ToList()).WindowsInLast5);
        var configChanged = new[] { P, P, P, P }.Select((r, i) => { var e = Ev(78, MarketFamilies.MatchResult, W(i), r); e.ConfigHash = "other-config-000000"; return e; }).ToList();
        Assert.False(EligibilityPublicationPolicy.CheckOpening(configChanged.Append(latest).ToList()).AllMet);
    }

    // ═══ 20–22: gerçek ölçümün ham dizileri (lab 25.09.2026, run-b33c… + beş pencere) ═══

    /// <summary>Lab ölçümü: 14.08, 28.08, 11.09, 18.09, 25.09 — ham kapı sonucu (Base 4.0).</summary>
    private static readonly Dictionary<(int, string), string[]> Measured = new()
    {
        [(78, MarketFamilies.MatchResult)] = new[] { F, F, F, F, P },
        [(78, MarketFamilies.DoubleChance)] = new[] { F, F, F, F, P },
        [(135, MarketFamilies.MatchResult)] = new[] { F, P, F, P, F },
        [(135, MarketFamilies.DoubleChance)] = new[] { F, P, F, P, F },
        [(39, MarketFamilies.MatchResult)] = new[] { F, P, P, P, P },
        [(39, MarketFamilies.DoubleChance)] = new[] { F, P, P, P, P },
        [(61, MarketFamilies.MatchResult)] = new[] { F, P, P, P, P },
        [(61, MarketFamilies.DoubleChance)] = new[] { F, P, P, P, P },
        [(140, MarketFamilies.MatchResult)] = new[] { F, P, P, P, P },
        [(140, MarketFamilies.DoubleChance)] = new[] { F, P, P, P, P },
        [(140, MarketFamilies.TotalGoals15)] = new[] { F, F, F, P, P },
        [(203, MarketFamilies.MatchResult)] = new[] { F, F, P, P, P },
        [(203, MarketFamilies.DoubleChance)] = new[] { F, F, P, P, P },
    };

    private static readonly DateTime[] Replay =
    {
        new(2026, 8, 14, 2, 0, 0, DateTimeKind.Utc), new(2026, 8, 28, 2, 0, 0, DateTimeKind.Utc), new(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc),
        new(2026, 9, 18, 2, 0, 0, DateTimeKind.Utc), new(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc)
    };

    private static FakeSource MeasuredSource() => new()
    {
        Fn = c => Matrix(c, Measured.ToDictionary(k => k.Key, k => k.Value[Array.IndexOf(Replay, c)]))
    };

    private static async Task<Dictionary<(int, string), string>> ReplayMeasured(Store store, bool fromBootstrap)
    {
        var src = MeasuredSource();
        using (var db = store.Db()) await store.Service(db, src).EnsureBootstrappedAsync(Now);
        using (var db = store.Db())
        {
            var svc = store.Service(db, src);
            var anchor = fromBootstrap ? await svc.BootstrapAnchorAsync() : null;
            await svc.RunAsync(Replay, EligibilityPublicationMode.Publish, EligibilityPublicationSources.BootstrapReplay, Now, anchor);
        }
        using (var db = store.Db()) return db.MarketEligibilityStates.ToDictionary(s => (s.OrganizationId, s.MarketFamily), s => s.PublishedState);
    }

    [Fact]
    public async Task K20_Bundesliga_TarihselBirBes_KapaliKalir()
    {
        foreach (var anchored in new[] { true, false })
        {
            var s = await ReplayMeasured(new Store(), anchored);
            Assert.False(PublishedStates.IsVisible(s[(78, MarketFamilies.MatchResult)]));
            Assert.False(PublishedStates.IsVisible(s[(78, MarketFamilies.DoubleChance)]));
            Assert.Equal(PublishedStates.PendingOpen, s[(78, MarketFamilies.MatchResult)]);
        }
    }

    [Fact]
    public async Task K21_SerieA_KararsizSonuclarla_KapaliKalir_H4UretimdeYok()
    {
        foreach (var anchored in new[] { true, false })
        {
            var s = await ReplayMeasured(new Store(), anchored);
            Assert.Equal(PublishedStates.Closed, s[(135, MarketFamilies.MatchResult)]);
            Assert.Equal(PublishedStates.Closed, s[(135, MarketFamilies.DoubleChance)]);
        }
        // H4 (lig başına düşük skor ρ) üretim yoluna hiç bağlanmadı.
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "Outcomes", "EligibilityPublicationService.cs"),
                     Source("Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "OutcomeModelJobs.cs")
                 })
        {
            Assert.DoesNotContain("LowScoreRho", file);
            Assert.DoesNotContain("leagueCalibration:", file);
        }
    }

    [Fact]
    public async Task K22_MevcutDokuzHucre_BootstraptaKaybolmaz_HardFailDogruDavranir()
    {
        var store = new Store();
        using (var db = store.Db())
        {
            var r = await store.Service(db, new FakeSource()).EnsureBootstrappedAsync(Now);
            Assert.True(r.Created);
            Assert.Equal("run-prod", r.SourceRunId); // yayındaki koşu; Bundesliga'yı açan daha yeni koşu DEĞİL
            Assert.Equal(9, r.Open);
        }
        using (var db = store.Db())
        {
            Assert.Equal(Store.ProductionOpen.OrderBy(x => x).ToList(),
                db.MarketEligibilityStates.Where(s => s.PublishedState == PublishedStates.Open).AsEnumerable().Select(s => (s.OrganizationId, s.MarketFamily)).OrderBy(x => x).ToList());
            // İkinci bootstrap hiçbir şey yazmaz.
            Assert.False((await store.Service(db, new FakeSource()).EnsureBootstrappedAsync(Now.AddHours(1))).Created);
            Assert.Equal(LockedCompetitions.All.Count * MarketFamilies.All.Count, db.MarketEligibilityStates.Count());
        }
        // Çapalı tekrar oynatma: dokuz hücrenin hepsinin son penceresi PASS → hepsi açık kalır.
        var anchored = await ReplayMeasured(new Store(), fromBootstrap: true);
        Assert.All(Store.ProductionOpen, c => Assert.Equal(PublishedStates.Open, anchored[c]));
        // Çapadan sonra hard-fail gelirse açık hücre HEMEN kapanır (grandfather yok).
        var store2 = new Store();
        using (var db = store2.Db()) await store2.Service(db, new FakeSource()).EnsureBootstrappedAsync(Now);
        var hard = new FakeSource { Fn = c => Matrix(c, Store.ProductionOpen.ToDictionary(x => x, x => x == (203, MarketFamilies.MatchResult) ? H : P)) };
        using (var db = store2.Db()) await store2.Service(db, hard).RunAsync(new[] { Replay[^1] }, EligibilityPublicationMode.Publish, "Manual", Now);
        using (var db = store2.Db())
        {
            Assert.Equal(PublishedStates.Closed, db.MarketEligibilityStates.Single(s => s.OrganizationId == 203 && s.MarketFamily == MarketFamilies.MatchResult).PublishedState);
            Assert.Equal(PublishedStates.Open, db.MarketEligibilityStates.Single(s => s.OrganizationId == 39 && s.MarketFamily == MarketFamilies.MatchResult).PublishedState);
        }
    }

    [Fact]
    public async Task K22b_KronolojikMod_AgustosFaillerini_GeriyeDonukUygular()
    {
        // Karşılaştırma (raporlanır, varsayılan DEĞİL): tam kronolojik modda Süper Lig 1X2/ÇŞ ve La Liga 1.5, çapa öncesi
        // (4.0 henüz yayında değilken) ölçülen iki ardışık FAIL yüzünden kapanır.
        var s = await ReplayMeasured(new Store(), fromBootstrap: false);
        Assert.Equal(PublishedStates.PendingOpen, s[(203, MarketFamilies.MatchResult)]);
        Assert.Equal(PublishedStates.PendingOpen, s[(140, MarketFamilies.TotalGoals15)]);
        Assert.Equal(PublishedStates.Open, s[(39, MarketFamilies.MatchResult)]);
    }

    [Fact]
    public void K23_MevcutEligibilityEsikleri_Degismez()
    {
        Assert.Equal(300, EligibilityPolicy.EnabledMinMatches);
        Assert.Equal(100, EligibilityPolicy.DisabledBelowMatches);
        Assert.Equal(0.03, EligibilityPolicy.MaxCalibrationErrorEnabled, 9);
        Assert.Equal(0.06, EligibilityPolicy.MaxCalibrationErrorLimited, 9);
        Assert.Equal(0.03, EligibilityPolicy.MaxHomeDrawBias, 9);
        Assert.Equal(5, EligibilityPolicy.MinRecentFinished);
        Assert.Equal(2000, EligibilityPolicy.BootstrapSamples);
        Assert.Equal("market-eligibility-1", MarketEligibilityPolicy.Version);
        Assert.Equal("eligibility-1", EligibilityPolicy.Version);
        // PASS tanımı = mevcut kapının Eligible kararı; yayın politikası kararı değiştirmez.
        var m = new MarketFamilyMetrics { Matches = 299, LogLossDiff = -0.05, LogLossDiffCiHigh = -0.01, SignificantlyBetter = true, CalibrationError = 0.01, FinishedLast60Days = 10 };
        MarketEligibilityPolicy.Decide(m);
        Assert.Equal(MarketEligibilityStatuses.Limited, m.Status);
        Assert.Equal(F, EligibilityPublicationPolicy.Classify(m, EvaluationIntegrity.Clean).Raw);
        // Config parmak izi deterministik ve eşiklere bağlı.
        var a = EligibilityPublicationPolicy.ConfigHash(OutcomeModelVersion.Current, TimeSpan.FromDays(600), TimeSpan.FromDays(150), TimeSpan.FromDays(150), LockedCompetitions.All);
        Assert.Equal(a, BacktestEligibilityEvaluationSource.CurrentConfigHash);
        Assert.NotEqual(a, EligibilityPublicationPolicy.ConfigHash(OutcomeModelVersion.Current, TimeSpan.FromDays(599), TimeSpan.FromDays(150), TimeSpan.FromDays(150), LockedCompetitions.All));
    }

    // ═══ 24–27: kart kuralları ve okuma yolu ═══

    private static OutcomePrediction Pred()
        => OutcomePredictor.Predict(new OutcomeExpectation(1.9, 0.9, 1.45, 1.15, 40, 40, 1.0, true, 1.9, 0.9, 0.9, 1.9, 12, 12, W1, W1), 39, new OutcomeModelParameters());

    private static Dictionary<int, List<MarketFamilyMetrics>> RawAllEligible(int league)
        => new() { [league] = MarketFamilies.All.Select(f => new MarketFamilyMetrics { LeagueId = league, Family = f, Status = MarketEligibilityStatuses.Eligible, Matches = 500 }).ToList() };

    [Fact]
    public void K24_K25_K26_KartKurallari_YayinDurumuUzerinden()
    {
        // Ham koşu her şeyi açık gösterse bile yayın durumu yalnız 1X2 + ÇŞ + (PendingClose) 2.5'i açar.
        var states = new Dictionary<(int, string), string>();
        foreach (var f in MarketFamilies.All) states[(39, f)] = PublishedStates.Closed;
        states[(39, MarketFamilies.MatchResult)] = PublishedStates.Open;
        states[(39, MarketFamilies.DoubleChance)] = PublishedStates.Open;
        states[(39, MarketFamilies.TotalGoals25)] = PublishedStates.PendingClose;
        states[(39, MarketFamilies.BothTeamsToScore)] = PublishedStates.PendingOpen;
        var rows = EligibilityPublicationOverlay.Apply(RawAllEligible(39), states)[39];
        var pr = Pred();
        var dto = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep", new OutcomeSnapshotBuilder.MarketPublication(rows));
        var cal = pr.Calibrated;
        var expected = cal.HomeWin >= cal.Draw && cal.HomeWin >= cal.AwayWin ? OddsMarketKeys.Ms1 : cal.AwayWin >= cal.Draw ? OddsMarketKeys.Ms2 : OddsMarketKeys.MsX;
        Assert.Equal(expected, dto.MainCards[0].MarketKey);                                        // 24: argmax ilk kart
        Assert.False(OutcomeFamilies.IsCompound(dto.MainCards[0].MarketKey!));                     // 25: çifte şans ilk/tek kart olamaz
        Assert.DoesNotContain(dto.MainCards, c => c.MeasuredFamily is MarketFamilies.BothTeamsToScore or MarketFamilies.TotalGoals15 or MarketFamilies.TotalGoals35); // 26
        Assert.Equal(MarketEligibilityStatuses.Limited, rows.Single(r => r.Family == MarketFamilies.BothTeamsToScore).Status);
        Assert.Contains(EligibilityPublicationOverlay.StateReasonPrefix + PublishedStates.PendingOpen, rows.Single(r => r.Family == MarketFamilies.BothTeamsToScore).ReasonCodes);

        // Yalnız ÇŞ açıksa (1X2 kapalı) ÇŞ tek kart olamaz: ilk kart çifte şans değildir.
        foreach (var f in MarketFamilies.All) states[(39, f)] = PublishedStates.Closed;
        states[(39, MarketFamilies.DoubleChance)] = PublishedStates.Open;
        var only = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep", new OutcomeSnapshotBuilder.MarketPublication(EligibilityPublicationOverlay.Apply(RawAllEligible(39), states)[39]));
        Assert.True(only.MainCards.Count == 0 || !OutcomeFamilies.IsCompound(only.MainCards[0].MarketKey!));

        // Ham koşu KAPALI ama yayın durumu Open (histerezis): kart görünür; durum tablosu boşsa ham karar aynen kalır.
        var rawClosed = new Dictionary<int, List<MarketFamilyMetrics>> { [39] = MarketFamilies.All.Select(f => new MarketFamilyMetrics { LeagueId = 39, Family = f, Status = MarketEligibilityStatuses.Limited, Matches = 500 }).ToList() };
        states[(39, MarketFamilies.MatchResult)] = PublishedStates.Open;
        Assert.Equal(MarketEligibilityStatuses.Eligible, EligibilityPublicationOverlay.Apply(rawClosed, states)[39].Single(r => r.Family == MarketFamilies.MatchResult).Status);
        Assert.Same(rawClosed, EligibilityPublicationOverlay.Apply(rawClosed, new Dictionary<(int, string), string>()));
    }

    [Fact]
    public async Task K27_DiscoverVeDetail_AyniYayimlanmisSnapshotId_UctanUca()
    {
        var w = new SnapshotWorld();
        using (var db = w.Db()) await new EligibilityPublicationService(db, new FakeSource(), NullLogger<EligibilityPublicationService>.Instance).EnsureBootstrappedAsync(SnapshotWorld.Kickoff.AddDays(-2));
        using (var db = w.Db()) Assert.Equal(1, (await w.Snapshots(db).RunAsync(SnapshotWorld.Kickoff.AddHours(-3))).Written);
        using (var db = w.Db())
        {
            var reader = new MatchOutcomeSnapshotReader(db);
            var detail = await reader.GetCurrentAsync(SnapshotWorld.MatchId);
            var discover = (await reader.GetCurrentForMatchesAsync(new[] { SnapshotWorld.MatchId }))[SnapshotWorld.MatchId];
            Assert.Equal(detail.SnapshotId, discover.SnapshotId);
            Assert.Contains(detail.MainCards, c => c.MeasuredFamily == MarketFamilies.MatchResult);
        }
        // Yayın 1X2'yi hard-fail ile kapatır → yalnız bu ligin maçı kuyruğa girer, yeni snapshot'ta 1X2 kartı yoktur, eskisi silinmez.
        var hard = new FakeSource { Fn = c => Matrix(c, new() { [(135, MarketFamilies.MatchResult)] = H, [(135, MarketFamilies.DoubleChance)] = H }) };
        EligibilityPublicationReport rep;
        using (var db = w.Db()) rep = await new EligibilityPublicationService(db, hard, NullLogger<EligibilityPublicationService>.Instance)
            .RunAsync(new[] { SnapshotWorld.Kickoff.AddDays(-1) }, EligibilityPublicationMode.Publish, "Manual", SnapshotWorld.Kickoff.AddHours(-3));
        Assert.Equal(1, rep.RecomputeRequestsEnqueued);
        using (var db = w.Db()) Assert.Equal(1, (await w.Snapshots(db).RunAsync(SnapshotWorld.Kickoff.AddHours(-2))).Written);
        using (var db = w.Db())
        {
            var reader = new MatchOutcomeSnapshotReader(db);
            var detail = await reader.GetCurrentAsync(SnapshotWorld.MatchId);
            var discover = (await reader.GetCurrentForMatchesAsync(new[] { SnapshotWorld.MatchId }))[SnapshotWorld.MatchId];
            Assert.Equal(detail.SnapshotId, discover.SnapshotId);
            Assert.DoesNotContain(detail.MainCards, c => c.MeasuredFamily is MarketFamilies.MatchResult or MarketFamilies.DoubleChance);
            Assert.Equal(2, db.MatchPredictionSnapshots.Count(s => s.MatchId == SnapshotWorld.MatchId));
            // Aynı girdi ikinci turda yeni satır üretmez.
        }
        using (var db = w.Db()) Assert.Equal(0, (await w.Snapshots(db).RunAsync(SnapshotWorld.Kickoff.AddHours(-1))).Written);
    }

    private sealed class SnapshotWorld
    {
        private readonly string _name = "elig-snap-" + Guid.NewGuid().ToString("N");
        public static readonly DateTime Kickoff = new(2026, 9, 27, 18, 45, 0, DateTimeKind.Utc);
        public const int MatchId = 777001;

        public FormaxDbContext Db() => new(new DbContextOptionsBuilder<FormaxDbContext>().UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        public SnapshotWorld()
        {
            using var db = Db();
            var names = new[] { "Venezia", "Fiorentina", "Monza", "Sassuolo", "Lecce", "Parma", "Empoli", "Cagliari" };
            for (var i = 0; i < names.Length; i++) db.Teams.Add(new Team { Id = i + 1, Name = names[i] });
            var rnd = new Random(4);
            var id = 1;
            for (var d = 220; d >= 3; d -= 2)
                for (var k = 0; k < 2; k++)
                {
                    var h = rnd.Next(1, 9); var a = rnd.Next(1, 9); if (a == h) a = h % 8 + 1;
                    db.Matches.Add(new Match { Id = id++, LeagueId = 135, League = "Serie A", MatchDate = Kickoff.AddDays(-d).AddHours(k), Status = MatchStatuses.Finished, HomeTeamId = h, AwayTeamId = a, HomeScore = rnd.Next(4), AwayScore = rnd.Next(3) });
                }
            db.Matches.Add(new Match { Id = MatchId, LeagueId = 135, League = "Serie A", MatchDate = Kickoff, Status = MatchStatuses.NotStarted, HomeTeamId = 1, AwayTeamId = 2 });
            var p = new OutcomeModelParameters { MaxSupportedProbability = 0.99, EloConflictThreshold = 1.0 };
            db.PredictionModelRuns.Add(new PredictionModelRun
            {
                RunId = "run-snap", ModelVersion = OutcomeModelVersion.Current, Status = "Accepted", StartedAtUtc = Kickoff.AddDays(-4), CompletedAtUtc = Kickoff.AddDays(-4),
                ParametersJson = JsonSerializer.Serialize(p), MetricsJson = "{}", TestMatches = 400
            });
            db.LeaguePredictionEligibilities.Add(new LeaguePredictionEligibility
            {
                RunId = "run-snap", ModelVersion = OutcomeModelVersion.Current, PolicyVersion = EligibilityPolicy.Version, LeagueId = 135, Status = PredictionEligibilities.Enabled,
                ReasonsJson = "[]", MetricsJson = "{}", EvaluatedAtUtc = Kickoff.AddDays(-4)
            });
            foreach (var family in MarketFamilies.All)
                db.LeagueMarketEligibilities.Add(new LeagueMarketEligibility
                {
                    RunId = "run-snap", ModelVersion = OutcomeModelVersion.Current, PolicyVersion = MarketEligibilityPolicy.Version, LeagueId = 135, Family = family, TestMatches = 400,
                    Status = MarketEligibilityStatuses.Eligible, ReasonsJson = "[]", MetricsJson = "{}", EvaluatedAtUtc = Kickoff.AddDays(-4)
                });
            db.SaveChanges();
        }

        public MatchPredictionSnapshotService Snapshots(FormaxDbContext db)
        {
            var loader = new OutcomeHistoryLoader(db);
            return new MatchPredictionSnapshotService(db, loader, new OutcomeModelTrainingService(db, loader, NullLogger<OutcomeModelTrainingService>.Instance),
                NullLogger<MatchPredictionSnapshotService>.Instance);
        }
    }

    // ═══ 28–32: güvenlik sözleşmeleri ═══

    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray()));
    }

    [Fact]
    public void K28_PlayerImpact_ProductionFalse_Kalir()
    {
        var settings = Source("Formax.API", "appsettings.json");
        var idx = settings.IndexOf("\"LineupImpact\": {", StringComparison.Ordinal);
        Assert.True(idx > 0);
        Assert.Contains("\"Production\": false", settings[idx..(idx + 200)]);
    }

    [Fact]
    public void K29_LeagueCalibrationAxes_None_Kalir()
    {
        var src = Source("Formax.Infrastructure", "Outcomes", "EligibilityPublicationService.cs");
        Assert.DoesNotContain("leagueCalibration", src);
        Assert.Contains("compareLegacy: false, candidate: true);", src);
        Assert.DoesNotContain("leagueCalibration", Source("Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs"));
    }

    [Fact]
    public void K30_SonucFiksturKadroBackfillJoblari_Etkilenmez()
    {
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "BackgroundJobs", "FixtureSyncJob.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "OfficialResultBotJobs.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "LineupIngestionJob.cs"),
                     Source("Formax.Infrastructure", "Lineups", "LineupBackfillService.cs")
                 })
        {
            Assert.DoesNotContain("EligibilityPublication", file);
            Assert.DoesNotContain("MarketEligibilityState", file);
        }
    }

    [Fact]
    public async Task K31_LaboratuvarVeAdmin_KullaniciYolunuBloklamaz()
    {
        // Admin ve haftalık iş kilidi BEKLEMEZ (TryWait): meşgulse 409 / BUSY.
        var ctl = Source("Formax.API", "Controllers", "Admin", "AdminEligibilityPublicationController.cs");
        Assert.Contains("Gate.WaitAsync(TimeSpan.Zero", ctl);
        Assert.Contains("StatusCode(409", ctl);
        // Kullanıcı uçları ve okuyucu yayın servisine bağlı değil (salt DB okur).
        foreach (var f in new[] { Source("Formax.API", "Controllers", "MatchOutcomesController.cs") })
            Assert.DoesNotContain("EligibilityPublicationService", f);
        var reader = Source("Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs");
        var readerPart = reader[reader.IndexOf("class MatchOutcomeSnapshotReader", StringComparison.Ordinal)..];
        Assert.DoesNotContain("EligibilityPublication", readerPart);

        var job = JobWorld(out var sp);
        Assert.Equal("NOT_DUE", await job.RunDueAsync(Now, CancellationToken.None)); // bootstrap 25.09
        await EligibilityPublicationService.Gate.WaitAsync();
        try { Assert.Equal("BUSY", await job.RunDueAsync(new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Utc), CancellationToken.None)); }
        finally { EligibilityPublicationService.Gate.Release(); }
    }

    [Fact]
    public void K32_ModelVeEligibilityCaller_ApiFootballKullanmaz()
    {
        foreach (var file in new[]
                 {
                     Source("Formax.Application", "Services", "Outcomes", "EligibilityPublication.cs"),
                     Source("Formax.Infrastructure", "Outcomes", "EligibilityPublicationService.cs"),
                     Source("Formax.API", "Controllers", "Admin", "AdminEligibilityPublicationController.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "OutcomeModelJobs.cs")
                 })
        {
            Assert.DoesNotContain("HttpClient", file);
            Assert.DoesNotContain("ApiFootball", file);
            Assert.DoesNotContain("IOfficialContentFetcher", file);
            Assert.DoesNotContain("IHttpClientFactory", file);
        }
    }

    [Fact]
    public async Task K33_RestartSonrasi_PublishedVePendingDurumlarKorunur()
    {
        var store = new Store();
        var src = MeasuredSource();
        using (var db = store.Db()) await store.Service(db, src).EnsureBootstrappedAsync(Now);
        using (var db = store.Db())
        {
            var svc = store.Service(db, src);
            await svc.RunAsync(Replay, EligibilityPublicationMode.Publish, EligibilityPublicationSources.BootstrapReplay, Now, await svc.BootstrapAnchorAsync());
        }
        // "Restart": yeni DbContext + yeni servis örneği + yeni okuma yolu.
        using (var db = store.Db())
        {
            Assert.Equal(PublishedStates.PendingOpen, db.MarketEligibilityStates.Single(s => s.OrganizationId == 78 && s.MarketFamily == MarketFamilies.MatchResult).PublishedState);
            var loader = new OutcomeHistoryLoader(db);
            var rows = await new OutcomeModelTrainingService(db, loader, NullLogger<OutcomeModelTrainingService>.Instance).MarketEligibilityAsync("run-today");
            Assert.False(rows[78].Single(r => r.Family == MarketFamilies.MatchResult).IsEligible); // ham koşu açsa bile
            Assert.True(rows[39].Single(r => r.Family == MarketFamilies.MatchResult).IsEligible);
        }
        // Sonraki hafta PASS'lar geçmişi kaldığı yerden sürdürür: Bundesliga 2/5 → hâlâ PendingOpen.
        var next = new FakeSource { Fn = c => Matrix(c, new() { [(78, MarketFamilies.MatchResult)] = P }) };
        using (var db = store.Db()) await store.Service(db, next).RunAsync(new[] { new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc) }, EligibilityPublicationMode.Publish, EligibilityPublicationSources.Scheduled, Now.AddDays(3));
        using (var db = store.Db())
            Assert.Equal(PublishedStates.PendingOpen, db.MarketEligibilityStates.Single(s => s.OrganizationId == 78 && s.MarketFamily == MarketFamilies.MatchResult).PublishedState);
    }

    private static EligibilityPublicationJob JobWorld(out ServiceProvider sp, FakeSource? src = null)
    {
        var name = "elig-job-" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<FormaxDbContext>(o => o.UseInMemoryDatabase(name).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        var source = src ?? new FakeSource { Fn = c => Matrix(c) };
        services.AddScoped<IEligibilityEvaluationSource>(_ => source);
        services.AddScoped<EligibilityPublicationService>();
        sp = services.BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        return new EligibilityPublicationJob(sp.GetRequiredService<IServiceScopeFactory>(), config, NullLogger<EligibilityPublicationJob>.Instance);
    }

    [Fact]
    public async Task K34_HaftalikJob_AyniHafta_IkinciYayinUretmez()
    {
        var src = new FakeSource { Fn = c => Matrix(c) };
        var job = JobWorld(out var sp, src);
        // Bootstrap 25.09 17:00 UTC; ilk planlı an Pazartesi 28.09 05:00 İstanbul = 02:00 UTC.
        Assert.Equal("NOT_DUE", await job.RunDueAsync(Now, CancellationToken.None));
        Assert.Equal("NOT_DUE", await job.RunDueAsync(new DateTime(2026, 9, 28, 1, 59, 0, DateTimeKind.Utc), CancellationToken.None));
        Assert.Equal("PUBLISHED", await job.RunDueAsync(new DateTime(2026, 9, 28, 2, 0, 30, DateTimeKind.Utc), CancellationToken.None));
        Assert.Equal("SKIPPED_ALREADY_PUBLISHED_THIS_WEEK", await job.RunDueAsync(new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc), CancellationToken.None));
        Assert.Equal("SKIPPED_ALREADY_PUBLISHED_THIS_WEEK", await job.RunDueAsync(new DateTime(2026, 10, 4, 20, 0, 0, DateTimeKind.Utc), CancellationToken.None));
        Assert.Equal(1, src.Calls);
        // Süreç Pazartesi kapalıysa: Salı açılışında kaçırılan anın KENDİ kesimiyle bir kez yayımlanır.
        Assert.Equal("PUBLISHED", await job.RunDueAsync(new DateTime(2026, 10, 6, 11, 0, 0, DateTimeKind.Utc), CancellationToken.None));
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FormaxDbContext>();
        Assert.Equal(new[] { new DateTime(2026, 9, 28, 2, 0, 0), new DateTime(2026, 10, 5, 2, 0, 0) },
            db.MarketEligibilityPublicationRuns.Where(r => r.Mode == "Publish").OrderBy(r => r.EvaluationCutoffUtc).Select(r => r.EvaluationCutoffUtc!.Value).ToArray());
    }

    [Fact]
    public void K35_SaatDilimi_EuropeIstanbul_DogruHesaplanir()
    {
        // Türkiye 2016'dan beri sabit UTC+3: AB yaz saati geçişinden (29.03 / 25.10.2026) etkilenmez.
        Assert.Equal(new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc), EligibilityEvaluationSchedule.LatestSlotAtOrBefore(new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 9, 21, 2, 0, 0, DateTimeKind.Utc), EligibilityEvaluationSchedule.LatestSlotAtOrBefore(new DateTime(2026, 9, 28, 1, 59, 59, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 3, 30, 2, 0, 0, DateTimeKind.Utc), EligibilityEvaluationSchedule.LatestSlotAtOrBefore(new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 10, 26, 2, 0, 0, DateTimeKind.Utc), EligibilityEvaluationSchedule.NextSlotAfter(new DateTime(2026, 10, 25, 3, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 1, 5, 2, 0, 0, DateTimeKind.Utc), EligibilityEvaluationSchedule.LatestSlotAtOrBefore(new DateTime(2026, 1, 5, 2, 0, 0, DateTimeKind.Utc)));
        // Pazar 23:30 UTC = Pazartesi 02:30 İstanbul → henüz 05:00 olmadı → bir önceki Pazartesi.
        Assert.Equal(new DateTime(2026, 9, 21, 2, 0, 0, DateTimeKind.Utc), EligibilityEvaluationSchedule.LatestSlotAtOrBefore(new DateTime(2026, 9, 27, 23, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("2026-W40", EligibilityEvaluationSchedule.WeekKey(new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("2026-W39", EligibilityEvaluationSchedule.WeekKey(new DateTime(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(TimeSpan.FromHours(3), EligibilityEvaluationSchedule.Istanbul.GetUtcOffset(new DateTime(2026, 7, 1)));
        Assert.Equal(TimeSpan.FromHours(3), EligibilityEvaluationSchedule.Istanbul.GetUtcOffset(new DateTime(2026, 12, 1)));
    }

    [Fact]
    public async Task Rollback_SonYayiniGeriAlir_DegerlendirmeKayitlariniSilmez()
    {
        var store = new Store();
        var hard = new FakeSource { Fn = c => Matrix(c, new() { [(39, MarketFamilies.MatchResult)] = H }) };
        using (var db = store.Db()) await store.Service(db, hard).EnsureBootstrappedAsync(Now);
        using (var db = store.Db()) await store.Service(db, hard).RunAsync(new[] { W(6) }, EligibilityPublicationMode.Publish, "Manual", Now);
        var key = EligibilityPublicationService.PublishRunKey(CH, W(6));
        using (var db = store.Db()) Assert.Equal("ROLLED_BACK", (await store.Service(db, hard).RollbackAsync(key, Now)).Result);
        using (var db = store.Db())
        {
            Assert.Equal(PublishedStates.Open, db.MarketEligibilityStates.Single(s => s.OrganizationId == 39 && s.MarketFamily == MarketFamilies.MatchResult).PublishedState);
            Assert.True(db.MarketEligibilityEvaluations.Any(e => e.Mode == "Publish"));
            Assert.Equal("ALREADY_ROLLED_BACK", (await store.Service(db, hard).RollbackAsync(key, Now)).Result);
        }
    }

    [Fact]
    public void CalibrationFit_KusursuzKalibrasyonda_EgimBir_KesisimSifir()
    {
        var rnd = new Random(3);
        var xs = Enumerable.Range(0, 20000).Select(_ => { var p = 0.1 + 0.8 * rnd.NextDouble(); return (p, rnd.NextDouble() < p); }).ToList();
        var (slope, intercept) = MarketFamilyEvaluator.CalibrationFit(xs);
        Assert.InRange(slope!.Value, 0.9, 1.1);
        Assert.InRange(intercept!.Value, -0.1, 0.1);
        Assert.Equal((null, null), MarketFamilyEvaluator.CalibrationFit(xs.Take(5).ToList()));
    }

    private static string Snapshot(FormaxDbContext db)
        => string.Join(";", db.MarketEligibilityStates.OrderBy(s => s.OrganizationId).ThenBy(s => s.MarketFamily).AsEnumerable()
            .Select(s => $"{s.OrganizationId}:{s.MarketFamily}={s.PublishedState}@{s.StateVersion}"));

    /// <summary>Sentetik lig (deterministik).</summary>
    private static List<HistoricalMatch> Synthetic(int leagueId, int teams, int rounds, int seed, DateTime start)
    {
        var rng = new Random(seed);
        var list = new List<HistoricalMatch>();
        var id = leagueId * 100000;
        var day = 0;
        for (var r = 0; r < rounds; r++)
            for (var h = 0; h < teams; h++)
                for (var a = 0; a < teams; a++)
                {
                    if (h == a) continue;
                    list.Add(new HistoricalMatch(++id, start.AddDays(day++ * 0.5), leagueId, leagueId * 1000 + h, leagueId * 1000 + a,
                        rng.Next(0, 4), rng.Next(0, 3)));
                }
        return list;
    }
}
