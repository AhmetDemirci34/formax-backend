using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// ORGANİZASYON × MARKET AİLESİ UYGUNLUĞU (19.09.2026) — zayıf bir market artık bütün organizasyonu kapatmaz.
/// Yayın kararı iki katmandır: tarihsel organizasyon sınavı ve bu maçın kapıları. Kart sayısı 0–3 arasında dinamiktir
/// ve eksik yuva ZAYIF marketle doldurulmaz.
/// </summary>
public class MarketEligibilityTests
{
    private const int League = 140;

    private static OutcomeExpectation E(double lh, double la, double coverage = 1, int n = 20)
        => new(lh, la, 1.45, 1.15, n, n, coverage, n >= 4, 1.5, 1.1, 1.2, 1.3, 10, 10, null, null);

    private static MarketFamilyMetrics M(string family, string status, params string[] reasons)
        => new() { LeagueId = League, Family = family, Status = status, Matches = 500, ReasonCodes = reasons.ToList() };

    /// <summary>Verilen ailelerin uygun, kalanların kapalı olduğu bir organizasyon matrisi.</summary>
    private static OutcomeSnapshotBuilder.MarketPublication Pub(IEnumerable<string> eligible, IEnumerable<string>? matchGated = null)
        => new(MarketFamilies.All.Select(f => eligible.Contains(f)
                ? M(f, MarketEligibilityStatuses.Eligible)
                : M(f, MarketEligibilityStatuses.WorseThanBaseline, "WORSE_THAN_LEAGUE_AVERAGE")),
            matchGated);

    private static OutcomeSnapshotDto Snap(double lh, double la, OutcomeSnapshotBuilder.MarketPublication pub, double coverage = 1)
        => OutcomeSnapshotBuilder.Build(1, OutcomePredictor.Predict(E(lh, la, coverage), League, new OutcomeModelParameters()), "Ev", "Dep", pub);

    private static readonly string[] ResultOnly = { MarketFamilies.MatchResult, MarketFamilies.DoubleChance };
    private static readonly string[] Goals25Only = { MarketFamilies.TotalGoals25 };
    private static readonly string[] BttsOnly = { MarketFamilies.BothTeamsToScore };

    // ═══ 1. ORGANIZASYON × MARKET MATRİSİ ═══════════════════════════════════════════════════════════

    [Fact]
    public void T01_Matris_HerAileAyriOlculur_CifteSans1X2tenMirasAlir()
    {
        // 2.5 çizgisi tabandan kötü, 1.5 çizgisi iyi: ikisi BİRBİRİNDEN bağımsız karar alır.
        var samples = Samples(400, seed: 7);
        var metrics = MarketFamilyEvaluator.EvaluateAll(League, samples,
            s => OutcomePredictor.Predict(s.E, s.LeagueId, new OutcomeModelParameters()).Calibrated, 0, 30, seed: 11);

        Assert.Equal(MarketFamilies.All.Count, metrics.Count);
        Assert.Equal(MarketFamilies.All, metrics.Select(m => m.Family).ToList());
        var result = metrics.First(m => m.Family == MarketFamilies.MatchResult);
        var dc = metrics.First(m => m.Family == MarketFamilies.DoubleChance);
        Assert.Equal(result.Status, dc.Status);                                     // çifte şans ayrı model değildir
        Assert.All(metrics.Where(m => m.Family != MarketFamilies.DoubleChance), m => Assert.Equal(400, m.Matches));
    }

    [Fact]
    public void T02_HerReasonCode_KendiKosulundaUretilir()
    {
        var few = new MarketFamilyMetrics { Family = MarketFamilies.TotalGoals25, Matches = 50 };
        MarketEligibilityPolicy.Decide(few);
        Assert.Equal(MarketEligibilityStatuses.InsufficientSample, few.Status);
        Assert.Contains("SAMPLE_BELOW_100", few.ReasonCodes);

        var worse = new MarketFamilyMetrics { Family = MarketFamilies.BothTeamsToScore, Matches = 500, LogLossDiff = 0.004 };
        MarketEligibilityPolicy.Decide(worse);
        Assert.Equal(MarketEligibilityStatuses.WorseThanBaseline, worse.Status);
        Assert.Contains("WORSE_THAN_LEAGUE_AVERAGE", worse.ReasonCodes);

        var broken = new MarketFamilyMetrics { Family = MarketFamilies.MatchResult, Matches = 500, LogLossDiff = -0.05, CalibrationError = 0.08 };
        MarketEligibilityPolicy.Decide(broken);
        Assert.Equal(MarketEligibilityStatuses.CalibrationFailed, broken.Status);

        var limitedSample = new MarketFamilyMetrics { Family = MarketFamilies.MatchResult, Matches = 200, LogLossDiff = -0.05, LogLossDiffCiHigh = -0.01, SignificantlyBetter = true, FinishedLast60Days = 30 };
        MarketEligibilityPolicy.Decide(limitedSample);
        Assert.Equal(MarketEligibilityStatuses.Limited, limitedSample.Status);
        Assert.Contains("SAMPLE_BELOW_300", limitedSample.ReasonCodes);

        var notSignificant = new MarketFamilyMetrics { Family = MarketFamilies.MatchResult, Matches = 500, LogLossDiff = -0.001, SignificantlyBetter = false, FinishedLast60Days = 30 };
        MarketEligibilityPolicy.Decide(notSignificant);
        Assert.Contains("NOT_SIGNIFICANTLY_BETTER_THAN_BASELINE", notSignificant.ReasonCodes);

        var biased = new MarketFamilyMetrics { Family = MarketFamilies.MatchResult, Matches = 500, LogLossDiff = -0.05, SignificantlyBetter = true, MaxBias = 0.06, FinishedLast60Days = 30 };
        MarketEligibilityPolicy.Decide(biased);
        Assert.Contains("SEGMENT_BIAS", biased.ReasonCodes);

        var stale = new MarketFamilyMetrics { Family = MarketFamilies.MatchResult, Matches = 500, LogLossDiff = -0.05, SignificantlyBetter = true, FinishedLast60Days = 1 };
        MarketEligibilityPolicy.Decide(stale);
        Assert.Contains("STALE_LEAGUE_DATA", stale.ReasonCodes);

        var ok = new MarketFamilyMetrics { Family = MarketFamilies.MatchResult, Matches = 500, LogLossDiff = -0.05, SignificantlyBetter = true, CalibrationError = 0.02, MaxBias = 0.01, FinishedLast60Days = 30 };
        MarketEligibilityPolicy.Decide(ok);
        Assert.Equal(MarketEligibilityStatuses.Eligible, ok.Status);
        Assert.Empty(ok.ReasonCodes);
    }

    [Fact]
    public void T03_EsiklerOrganizasyonPolitikasiylaAyni_Gevsetilmedi()
    {
        Assert.Equal(300, EligibilityPolicy.EnabledMinMatches);
        Assert.Equal(100, EligibilityPolicy.DisabledBelowMatches);
        Assert.Equal(0.03, EligibilityPolicy.MaxCalibrationErrorEnabled);
        Assert.Equal(0.06, EligibilityPolicy.MaxCalibrationErrorLimited);
        Assert.Equal(0.03, EligibilityPolicy.MaxHomeDrawBias);
        Assert.Equal("market-eligibility-1", MarketEligibilityPolicy.Version);
    }

    // ═══ 2. ZAYIF MARKET GÜÇLÜ MARKETİ KAPATMAZ ═════════════════════════════════════════════════════

    [Fact]
    public void T05_ZayifKG_1X2yiKapatmaz()
    {
        var s = Snap(1.9, 1.0, Pub(ResultOnly));
        Assert.Equal(OutcomeOverallStatuses.Partial, s.OverallStatus);
        Assert.Equal(1, s.PublishedCardCount);
        Assert.Equal(OutcomeFamilies.Result, s.MainCards.Single().Family);
        Assert.DoesNotContain(s.MainCards, c => c.MeasuredFamily == MarketFamilies.BothTeamsToScore);
        Assert.False(s.Markets.Single(m => m.Family == MarketFamilies.BothTeamsToScore).Published);
    }

    [Fact]
    public void T06_ZayifGolCizgileri_1X2yiKapatmaz_VeHerCizgiKendiKarariniAlir()
    {
        // 2.5 kapalı, 1.5 açık: gol yuvası 1.5 çizgisinden dolar.
        var s = Snap(1.6, 1.2, Pub(new[] { MarketFamilies.MatchResult, MarketFamilies.DoubleChance, MarketFamilies.TotalGoals15 }));
        Assert.Equal(2, s.PublishedCardCount);
        var goal = s.MainCards.Single(c => c.Family == OutcomeFamilies.Goals);
        Assert.StartsWith("1.5", goal.Market);
        Assert.True(s.Markets.Single(m => m.Family == MarketFamilies.TotalGoals15).Published);
        Assert.False(s.Markets.Single(m => m.Family == MarketFamilies.TotalGoals25).Published);
    }

    [Fact]
    public void T07_Zayif1X2_UygunGolMarketiniKapatmaz()
    {
        var s = Snap(1.7, 1.1, Pub(Goals25Only));
        Assert.Equal(1, s.PublishedCardCount);
        var card = s.MainCards.Single();
        Assert.Equal(OutcomeFamilies.Goals, card.Family);
        Assert.Contains("2.5", card.Market);
        Assert.DoesNotContain(s.MainCards, c => c.Family == OutcomeFamilies.Result);
    }

    // ═══ 3. KART SAYISI 0–3 DİNAMİK ═════════════════════════════════════════════════════════════════

    [Fact]
    public void T04_SifirBirIkiUcKart_DTOdaDogruTasinir()
    {
        var none = Snap(1.5, 1.2, Pub(Array.Empty<string>()));
        Assert.Equal(0, none.PublishedCardCount);
        Assert.Equal(OutcomeOverallStatuses.NotEligible, none.OverallStatus);
        Assert.Empty(none.MainCards);

        var one = Snap(1.5, 1.2, Pub(BttsOnly));
        Assert.Equal(1, one.PublishedCardCount);
        Assert.Equal(OutcomeOverallStatuses.Partial, one.OverallStatus);

        var two = Snap(1.5, 1.2, Pub(new[] { MarketFamilies.MatchResult, MarketFamilies.BothTeamsToScore }));
        Assert.Equal(2, two.PublishedCardCount);
        Assert.Equal(OutcomeOverallStatuses.Partial, two.OverallStatus);

        var three = Snap(1.5, 1.2, Pub(MarketFamilies.All));
        Assert.Equal(3, three.PublishedCardCount);
        Assert.Equal(OutcomeOverallStatuses.Full, three.OverallStatus);
        Assert.Equal(3, three.MainCards.Select(c => c.Family).Distinct().Count());
        // Kart sayısı ne olursa olsun BİRİNCİ kart 1X2'nin en olası sonucudur.
        foreach (var s in new[] { two, three })
            Assert.False(OutcomeFamilies.IsCompound(s.MainCards[0].MarketKey!));
    }

    [Fact]
    public void T04b_EksikYuva_ZayifMarketleDoldurulmaz()
    {
        // Yalnız KG uygun: gol ve sonuç yuvası BOŞ kalır, başka market ile doldurulmaz.
        var s = Snap(2.2, 0.7, Pub(BttsOnly));
        Assert.Single(s.MainCards);
        Assert.Equal(MarketFamilies.BothTeamsToScore, s.MainCards[0].MeasuredFamily);
        Assert.All(s.MainCards, c => Assert.True(s.Markets.Single(m => m.Family == c.MeasuredFamily).Published));
    }

    // ═══ 4. ÇİFTE ŞANS ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void T08_CifteSans_1X2UygunDegilse_AdayOlamaz()
    {
        // Çifte şans ailesi 1X2'den miras alır: 1X2 kapalıysa çifte şans da kapalıdır.
        var inherited = MarketEligibilityPolicy.InheritDoubleChance(
            new MarketFamilyMetrics { LeagueId = League, Family = MarketFamilies.MatchResult, Matches = 500, Status = MarketEligibilityStatuses.Limited, ReasonCodes = new List<string> { "CALIBRATION_ERROR" } });
        Assert.Equal(MarketEligibilityStatuses.Limited, inherited.Status);
        Assert.Contains("INHERITS_MatchResult1X2", inherited.ReasonCodes);

        var s = Snap(1.2, 1.3, Pub(Goals25Only));
        Assert.DoesNotContain(s.MainCards, c => OutcomeFamilies.IsCompound(c.MarketKey ?? ""));
    }

    [Fact]
    public void T09_CifteSans_FIILEN_YASAK_DEGIL_GercektenAyirtEdiciMacta_Secilir()
    {
        // Model beraberliği ve deplasmanı BİRLİKTE yukarı çekiyor, ev sahibini aşağı: tek bir sonuç ayrışmıyor.
        // Böyle bir maçta çifte şans kartı seçilir — yani yasak değildir, seçim skoru karar verir.
        var (cards, chosen) = FindCompoundCase();
        Assert.NotNull(chosen);
        Assert.True(OutcomeFamilies.IsCompound(chosen!.MarketKey!), $"seçilen {chosen.Market}");
        Assert.NotNull(chosen.Reason);
        // selection-4: çifte şans ANA KART OLAMAZ ve TEK KART OLAMAZ — yalnız ek karttır.
        Assert.True(cards.Count >= 2, "çifte şans tek kart olarak gösterilemez");
        Assert.NotSame(cards[0], chosen);
        Assert.False(OutcomeFamilies.IsCompound(cards[0].MarketKey!));
    }

    [Fact]
    public void T09b_CifteSans_TekBilesenYukseliyorsa_Secilmez_TekSonucKartiDahaCokSeySoyler()
    {
        // Yapay dağılım: yalnız deplasman tabanın üstünde. Birleşim (X2) yüksek yüzde verir ama bilgi TEK sonuçtadır.
        var result = new[]
        {
            Cand(OddsMarketKeys.Ms1, 0.30, 0.45),
            Cand(OddsMarketKeys.MsX, 0.22, 0.26),
            Cand(OddsMarketKeys.Ms2, 0.48, 0.29)
        };
        var x2 = Cand(OddsMarketKeys.DoubleChanceX2, 0.70, 0.55);
        Assert.True(OutcomeSnapshotBuilder.InformationValue(x2) > 0);
        // X bileşeni tabanın ALTINDA → çifte şans aday değil.
        Assert.False(result[1].InformationLift > 0);
    }

    [Fact]
    public void T10_MacDuzeyiKapi_LigUygunOlsaBile_IlgiliAileyiKapatir()
    {
        var gated = OutcomeSnapshotBuilder.MatchGatedFamilies(new[] { "OUTLIER_PROBABILITY" });
        Assert.Contains(MarketFamilies.MatchResult, gated);
        Assert.Contains(MarketFamilies.DoubleChance, gated);
        Assert.DoesNotContain(MarketFamilies.TotalGoals25, gated);      // aşırı 1X2 olasılığı gol dağılımını geçersiz kılmaz

        var s = Snap(1.6, 1.2, Pub(MarketFamilies.All, gated));
        Assert.DoesNotContain(s.MainCards, c => c.Family == OutcomeFamilies.Result);
        Assert.Equal(2, s.PublishedCardCount);
        Assert.False(s.Markets.Single(m => m.Family == MarketFamilies.MatchResult).Published);
        Assert.Contains("MATCH_LEVEL_GATE", s.Markets.Single(m => m.Family == MarketFamilies.MatchResult).ReasonCodes);
    }

    [Fact]
    public void T11_UygunOlmayanMarket_KullaniciyaSizmaz()
    {
        var s = Snap(1.8, 1.0, Pub(ResultOnly));
        s.PredictionEligibility = PredictionEligibilities.Enabled;
        var user = OutcomeSnapshotBuilder.ForUser(s);

        var shown = user.Families.SelectMany(f => f.Items).ToList();
        Assert.All(shown, i => Assert.Contains(i.MeasuredFamily, new[] { MarketFamilies.MatchResult, MarketFamilies.DoubleChance }));
        Assert.DoesNotContain(shown, i => i.MeasuredFamily == MarketFamilies.BothTeamsToScore);
        Assert.DoesNotContain(shown, i => i.MeasuredFamily == MarketFamilies.TotalGoals25);
        Assert.Empty(user.Families.Where(f => f.Items.Count == 0));      // boş aile başlığı gönderilmez
        // Gol çizgisi yayımlanmıyorsa beklenen gol de taşınmaz.
        Assert.Null(user.ExpectedHomeGoals);
        Assert.NotEmpty(user.TopScores);                                 // 1X2 yayımlanıyor → en olası skorlar kalır
    }

    [Fact]
    public void T11b_HicUygunMarketYoksa_HicbirYuzdeTasinmaz()
    {
        var s = Snap(2.4, 0.6, Pub(Array.Empty<string>()));
        var (elig, reasons) = OutcomeSnapshotBuilder.Finalize(s, Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal(PredictionEligibilities.Limited, elig);
        Assert.NotEmpty(reasons);
        s.PredictionEligibility = elig;
        var user = OutcomeSnapshotBuilder.ForUser(s);
        Assert.Equal("NotEligible", user.Status);
        Assert.Empty(user.MainCards);
        Assert.Empty(user.Families);
        Assert.Empty(user.TopScores);
        Assert.Null(user.ExpectedHomeGoals);
        Assert.Equal(OutcomeSnapshotBuilder.NotEligibleNotice, user.Notice);
    }

    [Fact]
    public void T10b_LimitedOrganizasyon_UygunMarketiVarsa_PartialYayinlanir()
    {
        // Lig anahtarı Limited olsa bile market bazlı uygunluk varsa yayın yapılır (eski all-or-nothing kalktı).
        var s = Snap(1.9, 1.05, Pub(ResultOnly));
        var (elig, _) = OutcomeSnapshotBuilder.Finalize(s, Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal(PredictionEligibilities.Enabled, elig);
        Assert.Equal(OutcomeOverallStatuses.Partial, s.OverallStatus);
    }

    [Fact]
    public void T12_SertKapi_ButunMaciKapatir()
    {
        var s = Snap(1.5, 1.2, Pub(MarketFamilies.All));
        var (elig, reasons) = OutcomeSnapshotBuilder.Finalize(s, new[] { "INSUFFICIENT_SAMPLE" }, Array.Empty<string>());
        Assert.Equal(PredictionEligibilities.Disabled, elig);
        Assert.Contains("INSUFFICIENT_SAMPLE", reasons);
    }

    // ═══ 5. MATEMATİK VE DETERMİNİZM ════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(2.4, 0.6)] [InlineData(1.2, 1.2)] [InlineData(0.6, 0.5)] [InlineData(0.4, 2.3)]
    public void T15_Model40Olasiliklari_KartSecimindenEtkilenmez(double lh, double la)
    {
        var pr = OutcomePredictor.Predict(E(lh, la), League, new OutcomeModelParameters());
        var full = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep", Pub(MarketFamilies.All));
        var narrow = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep", Pub(ResultOnly));
        double P(OutcomeSnapshotDto s, string market) => s.Families.SelectMany(f => f.Items).Single(i => i.Market == market).CalibratedProbability;
        foreach (var m in new[] { "Ev Sahibi Kazanır", "Beraberlik", "Deplasman Kazanır", "2.5 Üst", "Karşılıklı Gol Var" })
            Assert.Equal(P(full, m), P(narrow, m), 9);
        Assert.Equal(full.ExpectedHomeGoals, narrow.ExpectedHomeGoals);
        Assert.True(full.Checks!.Consistent && narrow.Checks!.Consistent);
    }

    [Fact]
    public void T13_KartSecimi_Deterministik_AyniGirdiAyniCikti()
    {
        var pub = Pub(MarketFamilies.All);
        var a = Snap(1.7, 1.1, pub);
        var b = Snap(1.7, 1.1, pub);
        Assert.Equal(a.MainCards.Select(c => c.MarketKey), b.MainCards.Select(c => c.MarketKey));
        Assert.Equal(a.MainCards.Select(c => c.Probability), b.MainCards.Select(c => c.Probability));
        // Gösterim sırası sabittir: Maç Sonucu → Gol → KG.
        Assert.Equal(new[] { OutcomeFamilies.Result, OutcomeFamilies.Goals, OutcomeFamilies.Btts }, a.MainCards.Select(c => c.Family));
    }

    [Fact]
    public void T13b_UygunlukKarari_SurecOnbellegindenBagimsiz_Deterministik()
    {
        // string.GetHashCode() süreçler arası rastgeledir; tohum aile SIRASINDAN türetilmelidir.
        var samples = Samples(350, seed: 3);
        Func<EvalSample, ScoreDistribution> dist = s => OutcomePredictor.Predict(s.E, s.LeagueId, new OutcomeModelParameters()).Calibrated;
        var a = MarketFamilyEvaluator.EvaluateAll(League, samples, dist, 0, 30, seed: 5000 + League);
        var b = MarketFamilyEvaluator.EvaluateAll(League, samples, dist, 0, 30, seed: 5000 + League);
        Assert.Equal(a.Select(x => x.Status + "|" + x.LogLossDiffCiHigh), b.Select(x => x.Status + "|" + x.LogLossDiffCiHigh));
    }

    [Fact]
    public void T14_EkKartlar_BilgiTasimayanAdayiSecmez_AnaKartMuaf()
    {
        // Ana kart modelin en olası maç sonucudur (bilgi kapısına tabi değildir); EK kartlar taban neyse onu tekrar etmez.
        var pub = Pub(MarketFamilies.All);
        var s = Snap(1.45, 1.15, pub);       // tam lig ortalaması: hiçbir yön bilgi taşımaz
        Assert.All(s.MainCards.Skip(1), c => Assert.True(c.InformationLift > 0, $"{c.Market} bilgi farkı {c.InformationLift}"));
    }

    // ═══ yardımcılar ════════════════════════════════════════════════════════════════════════════════

    private static OutcomeCandidateDto Cand(string key, double p, double b) => new()
    {
        MarketKey = key, CalibratedProbability = p, BaselineProbability = b, InformationLift = Math.Round(p - b, 4)
    };

    /// <summary>Çifte şansın seçildiği gerçek bir dağılım arar (yoksa boş).</summary>
    private static (List<OutcomeCandidateDto> Cards, OutcomeCandidateDto? Compound) FindCompoundCase()
    {
        var pub = Pub(new[] { MarketFamilies.MatchResult, MarketFamilies.DoubleChance });
        for (var lh = 0.4; lh <= 2.6; lh += 0.05)
            for (var la = 0.4; la <= 2.6; la += 0.05)
            {
                var s = Snap(lh, la, pub);
                var card = s.MainCards.FirstOrDefault(c => OutcomeFamilies.IsCompound(c.MarketKey ?? ""));
                if (card != null) return (s.MainCards, card);
            }
        return (new List<OutcomeCandidateDto>(), null);
    }

    /// <summary>Sentetik değerlendirme örnekleri — gerçek dağılımdan üretilmiş sonuçlar.</summary>
    private static List<EvalSample> Samples(int n, int seed)
    {
        var rnd = new Random(seed);
        var list = new List<EvalSample>(n);
        var at = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < n; i++)
        {
            var lh = 0.7 + rnd.NextDouble() * 1.8;
            var la = 0.6 + rnd.NextDouble() * 1.5;
            int hg = Poi(rnd, lh), ag = Poi(rnd, la);
            list.Add(new EvalSample(i + 1, League, at.AddDays(i), E(lh, la), hg, ag, 0.45, 0.26, 0.29, 0.52, 0.51, 0.75, 0.30));
        }
        return list;
    }

    private static int Poi(Random r, double lambda)
    {
        var l = Math.Exp(-lambda); double p = 1; var k = 0;
        do { k++; p *= r.NextDouble(); } while (p > l);
        return Math.Min(8, k - 1);
    }
}
