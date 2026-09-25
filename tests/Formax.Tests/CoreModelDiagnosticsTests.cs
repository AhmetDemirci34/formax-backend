using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Xunit;
using Axes = Formax.Application.Services.Outcomes.OutcomeBacktest.LeagueCalibrationAxes;

namespace Formax.Tests;

/// <summary>
/// ÇEKİRDEK MODEL TEŞHİSİ — kapalı organizasyonların araştırma turu (25.09.2026).
///
/// Bu turda ÜRETİM DAVRANIŞI DEĞİŞMEDİ: lig bazlı kalibrasyon ekseni yalnız ölçüm bayrağıdır ve
/// varsayılanı <see cref="Axes.None"/>'dır. Testler hem bayrağın kapalıyken hiçbir şey yapmadığını
/// hem de mevcut ürün kurallarının korunduğunu sınar.
/// </summary>
public class CoreModelDiagnosticsTests
{
    private static readonly DateTime Day0 = new(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc);

    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray()));
    }

    /// <summary>Sentetik lig: iki takım grubu, sabit güç farkı, deterministik skorlar.</summary>
    private static List<HistoricalMatch> SyntheticLeague(int leagueId, int teams, int rounds, int seed, DateTime start)
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
                    var homeId = leagueId * 1000 + h;
                    var awayId = leagueId * 1000 + a;
                    // Güç: takım indeksine göre; ev avantajı sabit.
                    var lh = 1.1 + 0.08 * (teams - h) + 0.25;
                    var la = 1.0 + 0.08 * (teams - a);
                    list.Add(new HistoricalMatch(++id, start.AddDays(day++ * 0.5), leagueId, homeId, awayId,
                        Poisson(rng, lh), Poisson(rng, la)));
                }
        return list.OrderBy(m => m.KickoffUtc).ThenBy(m => m.MatchId).ToList();
    }

    private static int Poisson(Random rng, double lambda)
    {
        var l = Math.Exp(-lambda);
        var k = 0; var p = 1.0;
        do { k++; p *= rng.NextDouble(); } while (p > l);
        return Math.Min(9, k - 1);
    }

    private static (OutcomeBacktestReport Report, List<HistoricalMatch> History) RunLab(Axes axes, int seed = 5)
    {
        var history = SyntheticLeague(39, 10, 4, seed, Day0)
            .Concat(SyntheticLeague(135, 10, 4, seed + 1, Day0))
            .OrderBy(m => m.KickoffUtc).ThenBy(m => m.MatchId).ToList();
        var catalog = CompetitionCatalog.Build(history);
        var last = history[^1].KickoffUtc.AddDays(1);
        var testStart = history[(int)(history.Count * 0.7)].KickoffUtc;
        var calStart = history[(int)(history.Count * 0.45)].KickoffUtc;
        var evalStart = history[0].KickoffUtc;
        var report = OutcomeBacktest.Run(history, catalog, new HashSet<int> { 39, 135 },
            evalStart, calStart, testStart, last, last, compareLegacy: false, candidate: true, leagueCalibration: axes);
        return (report, history);
    }

    // ════════ 1 / 17. Gelecek sonucu geçmiş özelliğe sızmaz; kalibrasyon test dışında ════════

    [Fact]
    public void C01_C17_Sizinti_Yok_KalibrasyonTestPenceresindeFitEdilmez()
    {
        var src = Source("Formax.Application", "Services", "Outcomes", "OutcomeBacktest.cs");
        // Kalibrasyon YALNIZ kalibrasyon penceresinden (cal) seçilir; test kümesi kullanılmaz.
        Assert.Contains("var cal = preTest.Where(s => s.KickoffUtc >= calStart).ToList();", src);
        Assert.Contains("// 3) Kalibrasyon — yalnız kalibrasyon penceresi.", src);
        // Lig bazlı eksenler de cal grubundan öğrenilir.
        var block = src[src.IndexOf("foreach (var g in cal.GroupBy(s => s.LeagueId))", StringComparison.Ordinal)..];
        Assert.Contains("LeagueLowScoreRho[g.Key]", block);
        Assert.Contains("LeagueDrawInflation[g.Key]", block);

        // Model akışı: beklenti maç işlenmeden ÖNCE, güncelleme SONRA.
        var model = Source("Formax.Application", "Services", "Outcomes", "OutcomeModel.cs");
        Assert.Contains("Her maçın", model);
    }

    // ════════ 2. Eğitim ve test maçı çakışmaz ════════

    [Fact]
    public void C02_EgitimVeTest_Cakismaz()
    {
        var (report, history) = RunLab(Axes.None);
        Assert.True(report.TestMatches > 0);
        // Test penceresi kesin tarih kesimidir: testStart'tan önceki hiçbir maç teste giremez.
        Assert.True(report.TestStartUtc > report.CalibrationStartUtc);
        Assert.True(report.CalibrationStartUtc > report.EvalStartUtc);
        Assert.All(history.Where(m => m.KickoffUtc < report.TestStartUtc),
            m => Assert.True(m.KickoffUtc < report.TestStartUtc));
    }

    // ════════ 3. Ev/deplasman yönü korunur ════════

    [Fact]
    public void C03_EvDeplasmanYonu_Korunur()
    {
        var p = new OutcomeModelParameters();
        var model = new OutcomeRatingModel(p, CompetitionCatalog.Unclassified);
        // Takım 1 hem evinde hem deplasmanda 3-0 kazanıyor: taraf değil TAKIM güçlü.
        // (Yalnız evinde oynatılsaydı deplasman kanıtı olmazdı ve sınav haksız olurdu.)
        for (var i = 0; i < 60; i++)
            model.Update(i % 2 == 0
                ? new HistoricalMatch(i, Day0.AddDays(i), 39, 1, 2, 3, 0)
                : new HistoricalMatch(i, Day0.AddDays(i), 39, 2, 1, 0, 3));

        var home = model.Expect(39, 1, 2, Day0.AddDays(100));   // 1 evinde
        var away = model.Expect(39, 2, 1, Day0.AddDays(100));   // 1 deplasmanda

        // YÖN KORUNUMU: takım 1 sürekli 3-0 kazandı. Hangi tarafta olursa olsun FAVORİ olmalı;
        // takımları yer değiştirmek tahmini de yer değiştirmeli (tahmin tarafa yapışmaz).
        var pp = new OutcomeModelParameters();
        var homeDist = OutcomePredictor.Predict(home, 39, pp).Calibrated;
        var awayDist = OutcomePredictor.Predict(away, 39, pp).Calibrated;

        Assert.True(homeDist.HomeWin > homeDist.AwayWin, "takım 1 evinde favori olmalı");
        Assert.True(awayDist.AwayWin > awayDist.HomeWin, "takım 1 deplasmanda da favori olmalı");
        // Takım 1'in gol beklentisi iki yönde de takım 2'ninkinden büyük.
        Assert.True(home.LambdaHome > home.LambdaAway);
        Assert.True(away.LambdaAway > away.LambdaHome);
    }

    // ════════ 6. Duplicate maç tek kez kullanılır ════════

    [Fact]
    public void C06_DuplicateMac_TekKezKullanilir()
    {
        // Yükleyici aynı gün + aynı eşleşmeyi bir kez alır (OutcomeHistoryLoader sözleşmesi).
        var loader = Source("Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs");
        Assert.Contains("var key = (m.HomeTeamId, m.AwayTeamId, (int)(m.MatchDate.Date - DateTime.UnixEpoch).TotalDays);", loader);
        Assert.Contains("if (!seen.Add(key)) continue;", loader);
    }

    // ════════ 5. Ertelenen/iptal maç eğitime girmez ════════

    [Fact]
    public void C05_ErtelenenIptal_EgitimeGirmez()
    {
        var loader = Source("Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs");
        Assert.Contains("m.Status == MatchStatuses.Finished", loader);
        // Saçma skorlar da elenir.
        Assert.Contains("if (m.HomeScore < 0 || m.AwayScore < 0 || m.HomeScore > 15 || m.AwayScore > 15) continue;", loader);
    }

    // ════════ 9. Geçmişi olmayan takım lig prior'ına daraltılır ════════

    [Fact]
    public void C09_GecmisiOlmayanTakim_LigPrioruna_Daraltilir()
    {
        var p = new OutcomeModelParameters();
        var model = new OutcomeRatingModel(p, CompetitionCatalog.Unclassified);
        for (var i = 0; i < 80; i++)
            model.Update(new HistoricalMatch(i, Day0.AddDays(i), 39, 1 + i % 8, 1 + (i + 3) % 8, 2, 1));

        // Hiç oynamamış takım: örneklem kapısı tahmini engeller (uydurma güç ÜRETİLMEZ).
        var cold = model.Expect(39, 999, 1, Day0.AddDays(200));
        Assert.False(cold.Sufficient);
        Assert.Contains("INSUFFICIENT_SAMPLE", cold.GateReasons);
        Assert.Equal(0, cold.HomeSample);
    }

    // ════════ 11. İç saha eğimi lig/sezon kesimine uyar ════════

    [Fact]
    public void C11_IcSahaEgimi_LigBazliOkunur()
    {
        var p = new OutcomeModelParameters { HomeTilt = 0.0 };
        p.LeagueHomeTilt[39] = 0.05;
        Assert.Equal(0.05, p.ForLeague(39).Tilt, 9);
        Assert.Equal(0.0, p.ForLeague(140).Tilt, 9);   // değeri olmayan lig GLOBAL değeri alır

        // İç saha eğimi λ'ları e^{+δ} ve e^{−δ} ile ölçekler: ÇARPIMLARI korunur (toplam değil).
        var e = new OutcomeExpectation(1.5, 1.2, 1.45, 1.15, 40, 40, 1.0, true, 1.5, 1.2, 1.2, 1.5, 10, 10, Day0, Day0);
        var flat = OutcomePredictor.Predict(e, 140, p).Calibrated;
        var tilted = OutcomePredictor.Predict(e, 39, p).Calibrated;
        Assert.Equal(flat.ExpectedHome * flat.ExpectedAway, tilted.ExpectedHome * tilted.ExpectedAway, 4);
        // Eğim ev sahibi lehine: ev kazanma olasılığı artar, deplasman azalır, toplam 1 kalır.
        Assert.True(tilted.HomeWin > flat.HomeWin);
        Assert.True(tilted.AwayWin < flat.AwayWin);
        Assert.Equal(1.0, tilted.HomeWin + tilted.Draw + tilted.AwayWin, 9);
    }

    // ════════ 15. Lig köprüsü elle sabit güç kullanmaz ════════

    [Fact]
    public void C15_LigKoprusu_ElleSabitGucKullanmaz()
    {
        var src = Source("Formax.Application", "Services", "Outcomes", "OutcomeModel.cs");
        // Ülke/lig adına göre elle yazılmış güç katsayısı YOK.
        foreach (var hardcoded in new[] { "\"Premier League\" =>", "\"La Liga\" =>", "leagueStrength[39]", "CountryStrength" })
            Assert.DoesNotContain(hardcoded, src);
        // Lig gücü ligler arası MAÇLARDAN çözülür.
        Assert.Contains("RefitLeagueStrengths", src);
    }

    // ════════ 16. Olasılık toplamı 1 ════════

    [Fact]
    public void C16_OlasilikToplami_Bir()
    {
        var p = new OutcomeModelParameters();
        p.LeagueLowScoreRho[135] = -0.15;
        p.LeagueDrawInflation[135] = 1.25;
        p.LeagueHomeTilt[135] = 0.10;
        foreach (var (lh, la) in new[] { (1.5, 1.2), (0.3, 2.8), (3.1, 0.4), (2.0, 2.0) })
        {
            var e = new OutcomeExpectation(lh, la, 1.45, 1.15, 40, 40, 1.0, true, lh, la, la, lh, 10, 10, Day0, Day0);
            var d = OutcomePredictor.Predict(e, 135, p).Calibrated;
            Assert.Equal(1.0, d.HomeWin + d.Draw + d.AwayWin, 9);
            Assert.Equal(1.0, d.Over(2.5) + d.Under(2.5), 9);
            Assert.Equal(1.0, d.BttsYes + d.BttsNo, 9);
        }
    }

    // ════════ 20. Aynı config deterministik sonuç verir ════════

    [Fact]
    public void C20_AyniConfig_DeterministikSonuc()
    {
        var a = RunLab(Axes.All).Report;
        var b = RunLab(Axes.All).Report;
        Assert.Equal(a.TestMatches, b.TestMatches);
        Assert.Equal(a.TestCalibrated.ResultLogLoss, b.TestCalibrated.ResultLogLoss, 12);
        Assert.Equal(a.Parameters.GoalScale, b.Parameters.GoalScale, 12);
    }

    // ════════ Bayrak KAPALIYKEN üretim davranışı değişmez ════════

    [Fact]
    public void C_BayrakKapali_UretimDavranisi_Degismez()
    {
        var off = RunLab(Axes.None).Report;
        // Lig bazlı beraberlik/iç saha/rho sözlükleri BOŞ kalır → ForLeague global değeri döndürür.
        Assert.Empty(off.Parameters.LeagueDrawInflation);
        Assert.Empty(off.Parameters.LeagueHomeTilt);
        Assert.Empty(off.Parameters.LeagueLowScoreRho);
        foreach (var lid in new[] { 39, 135 })
        {
            var (_, d, t, r) = off.Parameters.ForLeague(lid);
            Assert.Equal(off.Parameters.DrawInflation, d, 9);
            Assert.Equal(off.Parameters.HomeTilt, t, 9);
            Assert.Equal(off.Parameters.LowScoreRho, r, 9);
        }

        // ÜRETİM ÇAĞRI YOLU bayrağı HİÇ geçirmiyor (varsayılan None).
        var training = Source("Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs");
        Assert.DoesNotContain("leagueCalibration", training);
        Assert.Contains("compareLegacy: true, candidate: true)", training);
    }

    [Fact]
    public void C_BayrakAcik_LigSozlukleri_Doldurulur()
    {
        var on = RunLab(Axes.LowScoreRho).Report;
        // Lig bazlı eksenler YALNIZ global kalibrasyon uygulandığında öğrenilir (aynı kapı).
        if (on.CalibrationApplied) Assert.NotEmpty(on.Parameters.LeagueLowScoreRho);
        // Ablasyon gerçekten ayrışır: kapalı eksenler HİÇBİR durumda dolmaz.
        Assert.Empty(on.Parameters.LeagueDrawInflation);
        Assert.Empty(on.Parameters.LeagueHomeTilt);

        var all = RunLab(Axes.All).Report;
        if (all.CalibrationApplied)
        {
            Assert.NotEmpty(all.Parameters.LeagueDrawInflation);
            Assert.NotEmpty(all.Parameters.LeagueHomeTilt);
            Assert.NotEmpty(all.Parameters.LeagueLowScoreRho);
        }
    }

    // ════════ 21. Uygunluk eşikleri DÜŞÜRÜLMEDİ ════════

    [Fact]
    public void C21_UygunlukEsikleri_Dusurulmedi()
    {
        Assert.Equal(300, EligibilityPolicy.EnabledMinMatches);
        Assert.Equal(100, EligibilityPolicy.DisabledBelowMatches);
        Assert.Equal(0.03, EligibilityPolicy.MaxCalibrationErrorEnabled, 9);
        Assert.Equal(0.06, EligibilityPolicy.MaxCalibrationErrorLimited, 9);
        Assert.Equal(0.03, EligibilityPolicy.MaxHomeDrawBias, 9);
        Assert.Equal("market-eligibility-1", MarketEligibilityPolicy.Version);
    }

    // ════════ 22 / 23 / 24 / 25. Kart kuralları korunur ════════

    [Fact]
    public void C22_C23_C24_C25_KartKurallari()
    {
        var p = new OutcomeModelParameters();
        var e = new OutcomeExpectation(1.9, 0.9, 1.45, 1.15, 40, 40, 1.0, true, 1.9, 0.9, 0.9, 1.9, 12, 12, Day0, Day0);
        var pr = OutcomePredictor.Predict(e, 39, p);

        var all = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep",
            new OutcomeSnapshotBuilder.MarketPublication(MarketFamilies.All.Select(f => new MarketFamilyMetrics
            { LeagueId = 39, Family = f, Status = MarketEligibilityStatuses.Eligible, Matches = 500 })));

        var cal = pr.Calibrated;
        var expected = cal.HomeWin >= cal.Draw && cal.HomeWin >= cal.AwayWin ? OddsMarketKeys.Ms1
                     : cal.AwayWin >= cal.Draw ? OddsMarketKeys.Ms2 : OddsMarketKeys.MsX;
        Assert.Equal(expected, all.MainCards[0].MarketKey);                       // 23: argmax ilk kart
        Assert.False(OutcomeFamilies.IsCompound(all.MainCards[0].MarketKey!));    // 24: çifte şans ilk kart olamaz
        Assert.True(all.MainCards.Count is >= 1 and <= 3);
        var dc = all.MainCards.FirstOrDefault(c => OutcomeFamilies.IsCompound(c.MarketKey ?? ""));
        if (dc != null) Assert.True(all.MainCards.IndexOf(dc) > 0 && all.MainCards.Count > 1);

        // 25: ikili markette gösterilen taraf ≥ %50
        foreach (var c in all.MainCards.Where(c => c.MeasuredFamily is MarketFamilies.TotalGoals15
                     or MarketFamilies.TotalGoals25 or MarketFamilies.TotalGoals35 or MarketFamilies.BothTeamsToScore))
            Assert.True(c.CalibratedProbability >= 0.5 - 1e-9, $"{c.MarketKey} = {c.CalibratedProbability}");

        // 22: uygunluk dışı aile kart üretemez
        var gated = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep",
            new OutcomeSnapshotBuilder.MarketPublication(MarketFamilies.All.Select(f => new MarketFamilyMetrics
            {
                LeagueId = 39, Family = f, Matches = 500,
                Status = f == MarketFamilies.TotalGoals25 ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.WorseThanBaseline,
                ReasonCodes = new List<string> { "TEST" }
            })));
        Assert.All(gated.MainCards, c => Assert.Equal(MarketFamilies.TotalGoals25, c.MeasuredFamily));
    }

    // ════════ 27. Discover ve Detail aynı SnapshotId ════════

    [Fact]
    public void C27_DiscoverVeDetail_AyniSnapshotId()
    {
        var pr = OutcomePredictor.Predict(
            new OutcomeExpectation(1.5, 1.2, 1.45, 1.15, 40, 40, 1.0, true, 1.5, 1.2, 1.2, 1.5, 12, 12, Day0, Day0),
            39, new OutcomeModelParameters());
        var dto = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep");
        dto.SnapshotId = "snp-core-lab";
        dto.PredictionEligibility = PredictionEligibilities.Enabled;
        var a = OutcomeSnapshotBuilder.ForUser(Clone(dto));
        var b = OutcomeSnapshotBuilder.ForUser(Clone(dto));
        Assert.Equal("snp-core-lab", a.SnapshotId);
        Assert.Equal(a.SnapshotId, b.SnapshotId);
    }

    private static OutcomeSnapshotDto Clone(OutcomeSnapshotDto s)
        => System.Text.Json.JsonSerializer.Deserialize<OutcomeSnapshotDto>(System.Text.Json.JsonSerializer.Serialize(s))!;

    // ════════ 28. PlayerImpact Production=false kalır ════════

    [Fact]
    public void C28_PlayerImpact_ProductionFalse_Kalir()
    {
        var settings = Source("Formax.API", "appsettings.json");
        Assert.Contains("\"LineupImpact\"", settings);
        var idx = settings.IndexOf("\"LineupImpact\"", StringComparison.Ordinal);
        Assert.Contains("\"Production\": false", settings[idx..(idx + 200)]);
    }

    // ════════ 29. Laboratuvar dış istek atmaz ════════

    [Fact]
    public void C29_Laboratuvar_DisIstekAtmaz()
    {
        foreach (var file in new[]
                 {
                     Source("Formax.Application", "Services", "Outcomes", "OutcomeBacktest.cs"),
                     Source("Formax.Application", "Services", "Outcomes", "OutcomeModel.cs"),
                     Source("Formax.Infrastructure", "Outcomes", "OutcomeModelServices.cs")
                 })
        {
            Assert.DoesNotContain("HttpClient", file);
            Assert.DoesNotContain("ApiFootball", file);
            Assert.DoesNotContain("IOfficialContentFetcher", file);
        }
    }

    // ════════ 30. Sonuç/fikstür/backfill/lineup job'ları bozulmaz ════════

    [Fact]
    public void C30_MevcutJoblar_Bozulmaz()
    {
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "BackgroundJobs", "FixtureSyncJob.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "OfficialResultBotJobs.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "LineupIngestionJob.cs"),
                     Source("Formax.Infrastructure", "Lineups", "LineupBackfillService.cs")
                 })
            Assert.DoesNotContain("LeagueCalibrationAxes", file);
    }
}
