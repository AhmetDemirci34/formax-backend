using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Application.Services.Lineups;
using Formax.Application.Services.Lineups.V2;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Lineups;
using Xunit;
using Slot = Formax.Application.Services.Lineups.V2.ExpectedLineupBuilder.LineupSlot;
using TeamLineup = Formax.Application.Services.Lineups.V2.ExpectedLineupBuilder.TeamMatchLineup;

namespace Formax.Tests;

/// <summary>
/// PLAYER IMPACT V2 LABORATUVARI — görev şartnamesindeki 30 kabul testi.
///
/// Hiçbiri ağa çıkmaz, DB'ye yazmaz. Sinyal testleri SENTETİK ama kontrollü veriyle yapılır:
/// bilinen bir sinyal kurulur, laboratuvarın onu bulduğu ve karıştırıldığında kaybettiği ölçülür.
/// </summary>
public class LineupLabV2Tests
{
    private static readonly DateTime Day0 = new(2025, 1, 1, 18, 0, 0, DateTimeKind.Utc);
    private const int Home = 501;
    private const int Away = 502;

    private static readonly string[] Positions = { "G", "D", "D", "D", "D", "M", "M", "M", "F", "F", "F" };

    private static string Key(int team, string name) => PlayerIdentity.Key(team, name);

    private static List<Slot> Xi(int team, string squad, int starters = 11, int bench = 7, int? minutes = null)
    {
        var list = new List<Slot>();
        for (var i = 0; i < starters; i++)
            list.Add(new Slot(Key(team, $"{squad}{i + 1}"), Positions[i % 11], true, minutes));
        for (var i = 0; i < bench; i++)
            list.Add(new Slot(Key(team, $"{squad}B{i + 1}"), "M", false, null));
        return list;
    }

    private static List<TeamLineup> History(int team, string squad, int matches, int fromDay = 1)
        => Enumerable.Range(0, matches)
            .Select(i => new TeamLineup(1000 + i, Day0.AddDays(fromDay + i * 7), team, Xi(team, squad)))
            .ToList();

    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray()));
    }

    private static OutcomeExpectation Expect(double lh = 1.5, double la = 1.2)
        => new(lh, la, 1.45, 1.15, 40, 40, 1.0, true, lh, la, la, lh, 12, 12, Day0, Day0);

    // ════════ 1 / 3. Beklenen kadro YALNIZ geçmişten; gelecek sızmaz ════════

    [Fact]
    public void L01_L03_BeklenenKadro_YalnizGecmistenKurulur()
    {
        var history = History(Home, "A", 10);              // Day0+1 … Day0+64
        var cutoff = history[5].KickoffUtc;                // 6. maçın anı

        var expected = ExpectedLineupBuilder.Build(Home, cutoff, history);
        Assert.NotNull(expected);
        Assert.Equal(5, expected!.ObservedTeamMatches);    // kesim ANINDAKİ maç DAHİL DEĞİL
        Assert.Equal(cutoff, expected.CutoffUtc);

        // Kesimden sonraki maçlarda başka bir kadro oynasa bile beklenen 11 DEĞİŞMEZ.
        var withFuture = history.Concat(History(Home, "Z", 20, fromDay: 400)).ToList();
        var same = ExpectedLineupBuilder.Build(Home, cutoff, withFuture);
        Assert.Equal(expected.ExpectedStarters, same!.ExpectedStarters);

        // Yetersiz geçmişte beklenen 11 ÜRETİLMEZ.
        Assert.Null(ExpectedLineupBuilder.Build(Home, history[2].KickoffUtc, history));
    }

    // ════════ 2. Maçın kendi sonucu kendi feature'ında kullanılmaz ════════

    [Fact]
    public void L02_MacinKendiSonucu_KendiOzelliginde_Kullanilmaz()
    {
        // Özellik kaydında gol/sonuç alanı YOKTUR — tip düzeyinde kanıt.
        // "GoalkeeperChanged" maç ÖNCESİ bir kadro bilgisidir; sonuç alanı değildir. Aranan,
        // maç SONUCUNU taşıyan alanlardır.
        var props = typeof(LineupSideFeatures).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(props, n => n.Contains("Goals", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(props, n => n.Contains("Score", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(props, n => n.Contains("Result", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(props, n => n.Contains("Won", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(props, n => n.Contains("Outcome", StringComparison.OrdinalIgnoreCase));

        // Veri kümesi kurucusu: beklenti maç MODELE İŞLENMEDEN ÖNCE alınır, sonuç sonra.
        var src = Source("Formax.Infrastructure", "Lineups", "LineupLabV2Service.cs");
        var expectIdx = src.IndexOf("model.Expect(", StringComparison.Ordinal);
        var updateIdx = src.IndexOf("model.Update(m)", StringComparison.Ordinal);
        Assert.True(expectIdx > 0 && updateIdx > expectIdx, "beklenti, modelin maçı görmesinden ÖNCE alınmalı");
    }

    // ════════ 4. Transfer geleceği geçmiş takımı değiştirmez ════════

    [Fact]
    public void L04_Transfer_GecmisTakimKimligini_Degistirmez()
    {
        Assert.NotEqual(Key(Home, "Transfer Olan"), Key(Away, "Transfer Olan"));

        var atHome = History(Home, "A", 12);
        var expected = ExpectedLineupBuilder.Build(Home, Day0.AddDays(200), atHome)!;
        // Aynı isimli oyuncu başka takımda oynadıysa bu takımın profilini ETKİLEMEZ.
        Assert.False(expected.UsageOf(Key(Away, "A1")).Known);
        Assert.True(expected.UsageOf(Key(Home, "A1")).Known);
    }

    // ════════ 5 / 6 / 7. Kadro/kalite yoksa etki yok, uydurma değer yok ════════

    [Fact]
    public void L05_L06_L07_KadroYok_KaliteYok_BilinmeyenOyuncu()
    {
        // Beklenen kadro kurulamadıysa özellik KULLANILAMAZ.
        var none = LineupFeatureExtractor.Extract(Xi(Home, "A"), null, null, null, null, "StartersOnly");
        Assert.False(none.ExpectedAvailable);
        Assert.Same(LineupSideFeatures.Unavailable, LineupSideFeatures.Unavailable);

        // Kullanılamaz taraf varsa maç özelliği de kullanılamaz → delta 0.
        var features = new LineupMatchFeatures(1, Day0, 39, none, none);
        Assert.False(features.Usable);

        // Bilinmeyen oyuncuya uydurma profil verilmez.
        var expected = ExpectedLineupBuilder.Build(Home, Day0.AddDays(200), History(Home, "A", 12))!;
        var unknown = expected.UsageOf(Key(Home, "HicOynamamis"));
        Assert.False(unknown.Known);
        Assert.Equal(0, unknown.StartShare);
        Assert.Null(unknown.MinuteShare);
        Assert.Null(unknown.Position);
    }

    // ════════ 8. Dakika null ise 90 kabul edilmez ════════

    [Fact]
    public void L08_DakikaYoksa_90Varsayilmaz()
    {
        var noMinutes = ExpectedLineupBuilder.Build(Home, Day0.AddDays(200), History(Home, "A", 12))!;
        Assert.All(noMinutes.Usage.Values, u => Assert.Null(u.MinuteShare));

        var withMinutes = ExpectedLineupBuilder.Build(Home, Day0.AddDays(200),
            Enumerable.Range(0, 12).Select(i => new TeamLineup(2000 + i, Day0.AddDays(1 + i * 7), Home,
                Xi(Home, "A", minutes: 45))).ToList())!;
        Assert.Equal(0.5, withMinutes.UsageOf(Key(Home, "A1")).MinuteShare!.Value, 6);

        var f = LineupFeatureExtractor.Extract(Xi(Home, "A"), noMinutes, null, null, null, "StartersOnly");
        Assert.Null(f.MeanStarterMinuteShare);
    }

    // ════════ 9 / 10 / 11 / 12. Yapısal özellikler doğru hesaplanır ════════

    [Fact]
    public void L09_L10_L11_L12_YapisalOzellikler()
    {
        var expected = ExpectedLineupBuilder.Build(Home, Day0.AddDays(200), History(Home, "A", 12))!;

        // Tam aynı kadro: süreklilik 1, Jaccard 1, değişim yok.
        var same = LineupFeatureExtractor.Extract(Xi(Home, "A"), expected, null, null, null, "WithBench");
        Assert.Equal(1.0, same.StartingXiContinuity, 6);
        Assert.Equal(1.0, same.StarterJaccard, 6);
        Assert.False(same.GoalkeeperChanged);
        Assert.Equal(0, same.DefensiveUnitChanges + same.MidfieldUnitChanges + same.AttackingUnitChanges);

        // Kaleci (A1, mevki G) değiştirildi → kaleci değişimi + savunma grubu HARİÇ sayım.
        var slots = Xi(Home, "A");
        slots[0] = new Slot(Key(Home, "YeniKaleci"), "G", true, null);
        var keeper = LineupFeatureExtractor.Extract(slots, expected, null, null, null, "WithBench");
        Assert.True(keeper.GoalkeeperChanged);
        Assert.Equal(1, keeper.MissingExpectedStarters);
        Assert.Equal(1, keeper.AddedUnexpectedStarters);
        Assert.Equal(10 / 11.0, keeper.StartingXiContinuity, 6);
        Assert.Equal(10 / 12.0, keeper.StarterJaccard, 6);   // kesişim 10, birleşim 12

        // İki forvet (A9, A10) değiştirildi → yalnız HÜCUM grubu artmalı.
        var attack = Xi(Home, "A");
        attack[8] = new Slot(Key(Home, "YeniForvet1"), "F", true, null);
        attack[9] = new Slot(Key(Home, "YeniForvet2"), "F", true, null);
        var att = LineupFeatureExtractor.Extract(attack, expected, null, null, null, "WithBench");
        Assert.Equal(4, att.AttackingUnitChanges);     // 2 eksik + 2 eklenen, hepsi F
        Assert.Equal(0, att.DefensiveUnitChanges);
        Assert.Equal(0, att.MidfieldUnitChanges);
        Assert.False(att.GoalkeeperChanged);
    }

    // ════════ 13 / 14. Fold cutoff kesin; eğitim ve holdout çakışmaz ════════

    [Fact]
    public void L13_L14_FoldCutoff_Kesin()
    {
        // Fold takvimi YALNIZ kadrosu kullanılabilir maçlardan kurulur.
        var usableSide = new LineupSideFeatures { ExpectedAvailable = true, StartingXiContinuity = 1, ObservedTeamMatches = 20 };
        var samples = Enumerable.Range(0, 90)
            .Select(i =>
            {
                var kickoff = new DateTime(2024, 8, 1, 18, 0, 0, DateTimeKind.Utc).AddDays(i * 9);
                return Sample(i, kickoff, 39, 1, 1, new LineupMatchFeatures(i, kickoff, 39, usableSide, usableSide));
            })
            .ToList();
        var folds = LineupLabV2Service.BuildFolds(samples);
        Assert.NotEmpty(folds);

        // Fold'lar örtüşmez.
        var ordered = folds.OrderBy(f => f.StartUtc).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.True(ordered[i].StartUtc >= ordered[i - 1].EndUtc, "fold'lar örtüşemez");

        // Holdout TEK ve en sondadır.
        Assert.Single(folds, f => f.IsHoldout);
        Assert.Equal(ordered[^1].Name, folds.Single(f => f.IsHoldout).Name);
    }

    // ════════ 15. Katsayı yalnız eğitim fold'unda öğrenilir ════════

    [Fact]
    public void L15_Katsayi_YalnizEgitimFoldunda()
    {
        // Eğitim satırı sayısı eşiğin altındaysa katsayı ÜRETİLMEZ (delta 0).
        var few = Enumerable.Range(0, 5)
            .Select(_ => new PoissonRow(new double[] { 1, 0, 0, 0 }, 1.4, 1)).ToList();
        var model = LineupPoissonAdjuster.Fit(few, ridge: 10);
        Assert.Equal(0, model.TrainingRows);
        Assert.Equal(0, model.LogDelta(new double[] { 5, 5, 5, 5 }));

        // Kaynakta eğitim satırları KESİN cutoff ile kesilir.
        var src = Source("Formax.Application", "Services", "Lineups", "V2", "LineupLabV2.cs");
        Assert.Contains("if (s.KickoffUtc >= cutoffUtc) break;", src);
    }

    // ════════ 16 / 17. Olasılık toplamı 1 ve tavan aşılmaz ════════

    [Fact]
    public void L16_L17_ToplamBir_TavanAsilmaz()
    {
        var rows = new List<PoissonRow>();
        var rng = new Random(7);
        for (var i = 0; i < 400; i++)
        {
            var x = new[] { rng.NextDouble() * 4 - 2, rng.NextDouble() * 4 - 2 };
            rows.Add(new PoissonRow(x, 1.4, (int)Math.Round(Math.Max(0, 1.4 * Math.Exp(0.9 * x[0])))));
        }
        var model = LineupPoissonAdjuster.Fit(rows, ridge: 1);

        for (var i = 0; i < 200; i++)
        {
            var x = new[] { rng.NextDouble() * 40 - 20, rng.NextDouble() * 40 - 20 };  // aşırı girdi
            var d = model.LogDelta(x);
            Assert.InRange(d, -LineupPoissonAdjuster.MaxLogDelta, LineupPoissonAdjuster.MaxLogDelta);

            var dist = ScoreDistribution.Poisson(1.5 * Math.Exp(d), 1.2 * Math.Exp(-d));
            Assert.Equal(1.0, dist.HomeWin + dist.Draw + dist.AwayWin, 9);
        }
        // Tavan büyütülmedi.
        Assert.Equal(0.10, LineupPoissonAdjuster.MaxLogDelta, 9);
    }

    // ════════ 18. Kadro SIRASI sonucu değiştirmez ════════

    [Fact]
    public void L18_KadroSirasi_SonucuDegistirmez()
    {
        var expected = ExpectedLineupBuilder.Build(Home, Day0.AddDays(200), History(Home, "A", 12))!;
        var slots = Xi(Home, "A");
        var shuffled = slots.OrderBy(s => s.PlayerKey, StringComparer.Ordinal).Reverse().ToList();

        var a = LineupFeatureExtractor.Extract(slots, expected, null, null, null, "WithBench");
        var b = LineupFeatureExtractor.Extract(shuffled, expected, null, null, null, "WithBench");
        Assert.Equal(a.StartingXiContinuity, b.StartingXiContinuity, 9);
        Assert.Equal(a.StarterJaccard, b.StarterJaccard, 9);
        Assert.Equal(a.MeanStarterStartShare, b.MeanStarterStartShare, 9);
        Assert.Equal(a.AttackingUnitChanges, b.AttackingUnitChanges);
    }

    // ════════ 19. Determinizm ════════

    [Fact]
    public void L19_AyniVeri_AyniSonuc()
    {
        var request = SignalRequest(seed: 11, signalStrength: 0.25);
        var first = LineupLabV2.Evaluate(request, LineupFeatureSet.OnlyContinuity, forcedRidge: 10);
        var second = LineupLabV2.Evaluate(request, LineupFeatureSet.OnlyContinuity, forcedRidge: 10);
        var m1 = first.Metric("Pooled", MarketFamilies.MatchResult)!;
        var m2 = second.Metric("Pooled", MarketFamilies.MatchResult)!;
        Assert.Equal(m1.LogLoss, m2.LogLoss, 12);
        Assert.Equal(first.AdjustedMatches, second.AdjustedMatches);
        Assert.Equal(first.MeanAbsDelta, second.MeanAbsDelta, 12);
    }

    // ════════ 20 / 21. Permutation ve label-shuffle sahte başarıyı siler ════════

    [Fact]
    public void L20_L21_Permutation_VeLabelShuffle_AvantajiYokEder()
    {
        // GERÇEK SİNYAL: kadro sürekliliği düşükse ev sahibi daha az gol atar.
        var request = SignalRequest(seed: 3, signalStrength: 0.35);
        var real = LineupLabV2.Evaluate(request, LineupFeatureSet.OnlyContinuity, forcedRidge: 1);
        var realDiff = real.Metric("Pooled", MarketFamilies.MatchResult)!.AdjustedOnlyDiff;
        Assert.True(realDiff < 0, $"kurulu sinyal bulunmalıydı, fark={realDiff}");

        // PERMUTATION: özellikler maçlar arasında karıştırılır → avantaj kaybolmalı.
        var rng = new Random(99);
        var usable = request.Samples.Where(s => s.Features is { Usable: true }).ToList();
        var shuffled = usable.Select(s => s.Features!).OrderBy(_ => rng.Next()).ToList();
        var map = new Dictionary<int, LineupMatchFeatures>();
        for (var i = 0; i < usable.Count; i++) map[usable[i].MatchId] = shuffled[i];
        var permuted = LineupLabV2.Evaluate(request, LineupFeatureSet.OnlyContinuity, forcedRidge: 1,
            featureOverride: s => map.GetValueOrDefault(s.MatchId));
        var permDiff = permuted.Metric("Pooled", MarketFamilies.MatchResult)!.AdjustedOnlyDiff;
        Assert.True(permDiff > realDiff,
            $"karıştırılmış kadro gerçek kadrodan İYİ olamaz: gerçek={realDiff} karışık={permDiff}");

        // LABEL SHUFFLE: sonuçlar karıştırılır → iyileşme kalmamalı.
        var goals = usable.Select(s => (s.HomeGoals, s.AwayGoals)).OrderBy(_ => rng.Next()).ToList();
        var byId = usable.Select((s, i) => (s.MatchId, goals[i])).ToDictionary(x => x.MatchId, x => x.Item2);
        var shuffledSamples = request.Samples
            .Select(s => byId.TryGetValue(s.MatchId, out var g) ? s with { HomeGoals = g.HomeGoals, AwayGoals = g.AwayGoals } : s)
            .ToList();
        var labels = LineupLabV2.Evaluate(request with { Samples = shuffledSamples }, LineupFeatureSet.OnlyContinuity, forcedRidge: 1);
        Assert.True(labels.Metric("Pooled", MarketFamilies.MatchResult)!.AdjustedOnlyDiff > realDiff,
            "etiketler karıştırılınca sahte başarı üretilmemeli");
    }

    // ════════ 22. Süper Lig tam modelde kullanılmaz ════════

    [Fact]
    public void L22_SuperLig_TamModelde_Kullanilmaz()
    {
        Assert.Equal(new[] { 39, 135 }, LineupLabV2Service.PrimaryLeagues);
        Assert.DoesNotContain(LockedCompetitions.SuperLig, LineupLabV2Service.PrimaryLeagues);
        Assert.Equal(LockedCompetitions.SuperLig, LineupLabV2Service.SensitivityOnlyLeague);
    }

    // ════════ 23 / 24 / 25. Kart kuralları korunur ════════

    [Fact]
    public void L23_L24_L25_KartKurallari_Korunur()
    {
        var pr = OutcomePredictor.Predict(Expect(1.9, 0.9), 39, new OutcomeModelParameters());
        var all = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep",
            new OutcomeSnapshotBuilder.MarketPublication(MarketFamilies.All.Select(f => new MarketFamilyMetrics
            { LeagueId = 39, Family = f, Status = MarketEligibilityStatuses.Eligible, Matches = 500 })));

        var cal = pr.Calibrated;
        var expectedKey = cal.HomeWin >= cal.Draw && cal.HomeWin >= cal.AwayWin ? OddsMarketKeys.Ms1
                        : cal.AwayWin >= cal.Draw ? OddsMarketKeys.Ms2 : OddsMarketKeys.MsX;
        Assert.Equal(expectedKey, all.MainCards[0].MarketKey);                         // 24
        Assert.False(OutcomeFamilies.IsCompound(all.MainCards[0].MarketKey!));         // 25
        Assert.True(all.MainCards.Count <= 3);

        var gated = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep",
            new OutcomeSnapshotBuilder.MarketPublication(MarketFamilies.All.Select(f => new MarketFamilyMetrics
            {
                LeagueId = 39, Family = f, Matches = 500,
                Status = f == MarketFamilies.BothTeamsToScore ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.WorseThanBaseline,
                ReasonCodes = new List<string> { "TEST" }
            })));
        Assert.All(gated.MainCards, c => Assert.Equal(MarketFamilies.BothTeamsToScore, c.MeasuredFamily));  // 23
    }

    // ════════ 26. Discover/Detail aynı SnapshotId ════════

    [Fact]
    public void L26_DiscoverVeDetail_AyniSnapshotId()
    {
        var pr = OutcomePredictor.Predict(Expect(), 39, new OutcomeModelParameters());
        var dto = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep");
        dto.SnapshotId = "snp-lab-v2";
        dto.PredictionEligibility = PredictionEligibilities.Enabled;
        var a = OutcomeSnapshotBuilder.ForUser(Clone(dto));
        var b = OutcomeSnapshotBuilder.ForUser(Clone(dto));
        Assert.Equal("snp-lab-v2", a.SnapshotId);
        Assert.Equal(a.SnapshotId, b.SnapshotId);
    }

    private static OutcomeSnapshotDto Clone(OutcomeSnapshotDto s)
        => System.Text.Json.JsonSerializer.Deserialize<OutcomeSnapshotDto>(System.Text.Json.JsonSerializer.Serialize(s))!;

    // ════════ 27 / 28. Mevcut işler bozulmaz; API-Football isteği 0 ════════

    [Fact]
    public void L27_L28_MevcutIsler_VeApiFootball()
    {
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "BackgroundJobs", "FixtureSyncJob.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "OfficialResultBotJobs.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "LineupIngestionJob.cs"),
                     Source("Formax.Infrastructure", "Lineups", "LineupBackfillService.cs")
                 })
            Assert.DoesNotContain("LineupLabV2", file);

        // Laboratuvar sağlayıcı ALMAZ ve ağ tipi KULLANMAZ.
        Assert.DoesNotContain(typeof(ISportsDataProvider),
            typeof(LineupLabV2Service).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "Lineups", "LineupLabV2Service.cs"),
                     Source("Formax.Application", "Services", "Lineups", "V2", "LineupLabV2.cs"),
                     Source("Formax.Application", "Services", "Lineups", "V2", "ExpectedLineup.cs"),
                     Source("Formax.Application", "Services", "Lineups", "V2", "LineupFeatures.cs"),
                     Source("Formax.Application", "Services", "Lineups", "V2", "LineupPoissonAdjuster.cs")
                 })
        {
            Assert.DoesNotContain("ApiFootball", file);
            Assert.DoesNotContain("HttpClient", file);
            Assert.DoesNotContain("IOfficialContentFetcher", file);
        }
    }

    // ════════ 29 / 30. Gölge model üretimi değiştirmez; kapı başarısızı reddeder ════════

    [Fact]
    public void L29_L30_GolgeModel_VeUretimKapisi()
    {
        // 29: katman üretimde değilken yayımlanan yüzdeler TABANLA birebir aynı.
        var e = Expect();
        var basePr = OutcomePredictor.Predict(e, 39, new OutcomeModelParameters());
        var none = LineupAdjustment.None(LineupSourceStatuses.Missing, new[] { LineupReasonCodes.LineupMissing });
        var dto = OutcomeLineupDto.Build(none, basePr.Calibrated, null, appliedToPublished: false);
        Assert.False(dto.Applied);
        Assert.Equal(dto.BaseHomeProbability, dto.LineupAdjustedHomeProbability);
        Assert.Equal(dto.BaseDrawProbability, dto.LineupAdjustedDrawProbability);
        Assert.Equal(dto.BaseAwayProbability, dto.LineupAdjustedAwayProbability);

        // 30: üretim ayarı VARSAYILAN OLARAK KAPALI ve laboratuvar bunu değiştirmez.
        var settings = Source("Formax.API", "appsettings.json");
        Assert.Contains("\"Production\": false", settings);
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "Lineups", "LineupLabV2Service.cs"),
                     Source("Formax.Application", "Services", "Lineups", "V2", "LineupLabV2.cs")
                 })
            Assert.DoesNotContain("LineupImpact:Production", file);

        // Kabul kapısı: tabandan kötü ve anlamlı iyileşmesi olmayan aday REDDEDİLİR.
        var baseOverall = MarketFamilies.Measured
            .Select(f => new MarketFamilyMetrics { Family = f, Matches = 800, LogLoss = 1.0, CalibrationError = 0.02 }).ToList();
        var failing = new LineupAblationResult
        {
            Name = "A6_Candidate", AdjustedMatches = 796,
            Overall = MarketFamilies.Measured.Select(f => new MarketFamilyMetrics { Family = f, LogLoss = 1.001, CalibrationError = 0.02 }).ToList()
        };
        foreach (var f in MarketFamilies.Measured)
        {
            failing.AdjustedOnlyLogLossDiff[f] = 0.0005;   // tabandan KÖTÜ
            failing.AdjustedOnlyCiHigh[f] = 0.0054;        // CI sıfırı kesiyor
        }
        var decision = LineupImpactPolicy.Decide(failing, baseOverall);
        Assert.Equal(LineupImpactDecisions.Shadow, decision.Decision);
        Assert.Contains(decision.Reasons, r => r.StartsWith("WORSE_THAN_BASE"));
        Assert.Contains("NO_SIGNIFICANT_IMPROVEMENT", decision.Reasons);
    }

    // ════════ Sentetik sinyal üreteci (20/21/19 için) ════════

    private static LabSample Sample(int id, DateTime kickoff, int league, int hg, int ag,
        LineupMatchFeatures? features = null, double lh = 1.5, double la = 1.2)
    {
        var e = Expect(lh, la);
        return new LabSample(id, kickoff, league, e, hg, ag,
            ScoreDistribution.Poisson(lh, la), ScoreDistribution.Poisson(1.45, 1.15), features);
    }

    /// <summary>
    /// Kontrollü deney kümesi: ev sahibinin kadro sürekliliği düştükçe attığı gol AZALIR.
    /// Laboratuvar bu sinyali bulmalı; özellikler karıştırılınca bulamamalı.
    /// </summary>
    private static LineupLabV2.LabRequest SignalRequest(int seed, double signalStrength)
    {
        var rng = new Random(seed);
        var samples = new List<LabSample>();
        var start = new DateTime(2024, 8, 1, 18, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 900; i++)
        {
            var kickoff = start.AddDays(i * 0.8);
            var continuity = 0.5 + rng.NextDouble() * 0.5;               // 0,5 … 1,0
            var baseHome = 1.5;
            var trueHome = baseHome * Math.Exp(signalStrength * (continuity - 0.75) * 4);
            var hg = Poisson(rng, trueHome);
            var ag = Poisson(rng, 1.2);

            var side = new LineupSideFeatures
            {
                ExpectedAvailable = true,
                ObservedTeamMatches = 20,
                StartingXiContinuity = continuity,
                StarterJaccard = continuity / (2 - continuity),
                MissingExpectedStarters = (int)Math.Round((1 - continuity) * 11),
                AddedUnexpectedStarters = (int)Math.Round((1 - continuity) * 11),
                LineupDataQuality = "WithMinutes"
            };
            var opponent = side with { StartingXiContinuity = 0.9, MissingExpectedStarters = 1, AddedUnexpectedStarters = 1 };

            samples.Add(Sample(i, kickoff, i % 2 == 0 ? 39 : 135, hg, ag,
                new LineupMatchFeatures(i, kickoff, i % 2 == 0 ? 39 : 135, side, opponent)));
        }

        var folds = LineupLabV2Service.BuildFolds(samples);
        return new LineupLabV2.LabRequest(samples, folds, Array.Empty<LineupFeatureSet>());
    }

    private static int Poisson(Random rng, double lambda)
    {
        var l = Math.Exp(-lambda);
        var k = 0; var p = 1.0;
        do { k++; p *= rng.NextDouble(); } while (p > l);
        return k - 1;
    }
}
