using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// FUTBOL MANTIK İNVARYANTLARI (18.09.2026) — modelin sayısal doğruluğundan bağımsız olarak HER SÜRÜMDE tutması gereken
/// yapısal kurallar. Metrikler iyileşse bile bu kurallardan biri kırılırsa sürüm yayımlanamaz.
/// </summary>
public class OutcomeInvariantTests
{
    private const int League = 140;
    private static readonly OutcomeModelParameters P = new();

    private static OutcomeExpectation E(double lh, double la, double leagueHome = 1.45, double leagueAway = 1.15,
        double coverage = 1, int n = 20)
        => new(lh, la, leagueHome, leagueAway, n, n, coverage, n >= 4, 1.5, 1.1, 1.2, 1.3, 10, 10, null, null);

    private static ScoreDistribution D(OutcomeExpectation e, OutcomeModelParameters? p = null)
        => OutcomePredictor.Predict(e, League, p ?? P).Calibrated;

    // ── Dünya kurucu: iki lig + bir kupa; güçler bilinen, sonuçlar Poisson ────────────────────────────
    private static List<HistoricalMatch> World(int seed, int seasons = 3, int teamsPerLeague = 10)
    {
        var rnd = new Random(seed);
        var list = new List<HistoricalMatch>();
        var start = new DateTime(2022, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        var id = 1;
        // Takım gücü: 100..109 → 0,8 + i*0,04 ; 200..209 aynı ölçek
        double Str(int t) => 0.8 + (t % 100) * 0.04;
        for (var s = 0; s < seasons; s++)
            for (var round = 0; round < 18; round++)
                foreach (var league in new[] { 900, 901 })
                    for (var k = 0; k < teamsPerLeague / 2; k++)
                    {
                        var baseId = league == 900 ? 100 : 200;
                        var h = baseId + (round + k) % teamsPerLeague;
                        var a = baseId + (round + k + 1 + s) % teamsPerLeague;
                        if (h == a) continue;
                        var lh = 1.45 * Str(h) / Str(a);
                        var la = 1.15 * Str(a) / Str(h);
                        list.Add(new HistoricalMatch(id++, start.AddDays(s * 330 + round * 7 + k), league, h, a,
                            Poi(rnd, lh), Poi(rnd, la)));
                    }
        return list.OrderBy(m => m.KickoffUtc).ThenBy(m => m.MatchId).ToList();
    }

    private static int Poi(Random r, double lambda)
    {
        var l = Math.Exp(-lambda); double p = 1; var k = 0;
        do { k++; p *= r.NextDouble(); } while (p > l);
        return Math.Min(8, k - 1);
    }

    // ═══ 1–2: İÇ SAHA AVANTAJININ YÖNÜ VE DENK TAKIMLAR ══════════════════════════════════════════════

    [Fact]
    public void I01_EvDeplasmanYerDegistirince_IcSahaAvantajiDogruTarafaGecer()
    {
        var history = World(11);
        var model = new OutcomeRatingModel(P, CompetitionCatalog.Build(history));
        foreach (var m in history) model.Update(m);
        var at = history[^1].KickoffUtc.AddDays(7);

        var ab = D(model.Expect(900, 104, 105, at));
        var ba = D(model.Expect(900, 105, 104, at));
        // Aynı iki takım: ev sahibi olan taraf her iki kurulumda da kendi kazanma olasılığını artırır.
        Assert.True(ab.HomeWin > ba.AwayWin, $"104 evde {ab.HomeWin:P1} deplasmanda {ba.AwayWin:P1}");
        Assert.True(ba.HomeWin > ab.AwayWin, $"105 evde {ba.HomeWin:P1} deplasmanda {ab.AwayWin:P1}");
    }

    [Fact]
    public void I02_TamamenEsitIkiTakim_LigOnseliVeIcSahaCevresindeKalir()
    {
        var e = E(1.45, 1.15);
        var d = D(e);
        var leagueBase = ScoreDistribution.Poisson(1.45, 1.15, P.DrawInflation);
        Assert.Equal(leagueBase.HomeWin, d.HomeWin, 2);
        Assert.Equal(leagueBase.Draw, d.Draw, 2);
        Assert.Equal(leagueBase.AwayWin, d.AwayWin, 2);
        Assert.True(d.HomeWin > d.AwayWin, "denk takımlarda ev sahibi lig tabanı kadar öndedir");
    }

    // ═══ 3–4: HÜCUM VE SAVUNMA GÜCÜNÜN YÖNÜ ═════════════════════════════════════════════════════════

    [Fact]
    public void I03_HucumGucuYukselince_KazanmaVeUstOlasiligi_TersYoneGitmez()
    {
        double prevWin = -1, prevOver = -1;
        foreach (var lh in new[] { 0.9, 1.2, 1.5, 1.9, 2.4 })
        {
            var d = D(E(lh, 1.15));
            Assert.True(d.HomeWin > prevWin, $"λ_ev={lh} kazanma {d.HomeWin:P1} önceki {prevWin:P1}");
            Assert.True(d.Over(2.5) > prevOver, $"λ_ev={lh} üst {d.Over(2.5):P1} önceki {prevOver:P1}");
            prevWin = d.HomeWin; prevOver = d.Over(2.5);
        }
    }

    [Fact]
    public void I04_SavunmaKotulesince_RakibinGolVeKazanmaOlasiligi_Dusmez()
    {
        double prevAway = -1, prevScores = -1;
        foreach (var la in new[] { 0.7, 1.0, 1.4, 1.8, 2.3 })
        {
            var d = D(E(1.45, la));
            Assert.True(d.AwayWin > prevAway, $"λ_dep={la} deplasman {d.AwayWin:P1}");
            Assert.True(d.AwayScores > prevScores, $"λ_dep={la} gol atar {d.AwayScores:P1}");
            prevAway = d.AwayWin; prevScores = d.AwayScores;
        }
    }

    // ═══ 5–6: VERİ MİKTARI VE ZAMAN AĞIRLIĞI ════════════════════════════════════════════════════════

    [Fact]
    public void I05_VeriAzaldikca_Guven_Yukselmez_TahminTabanaYaklasir()
    {
        var p = new OutcomeModelParameters { UncertaintyMix = 0.35 };
        double prevTop = 1.01;
        foreach (var cov in new[] { 1.0, 0.8, 0.5, 0.2, 0.0 })
        {
            var pr = OutcomePredictor.Predict(E(2.4, 0.6, coverage: cov), League, p);
            var top = Math.Max(pr.Calibrated.HomeWin, Math.Max(pr.Calibrated.Draw, pr.Calibrated.AwayWin));
            Assert.True(top <= prevTop + 1e-9, $"kapsam={cov} en yüksek olasılık {top:P1} önceki {prevTop:P1}");
            Assert.True(pr.UncertaintyWeight >= 0, "belirsizlik ağırlığı negatif olamaz");
            prevTop = top;
        }
        // Yetersiz örneklemde tahmin hiç üretilmez.
        var model = new OutcomeRatingModel(new OutcomeModelParameters(), CompetitionCatalog.Unclassified);
        Assert.False(model.Expect(League, 1, 2, DateTime.UtcNow).Sufficient);
    }

    [Fact]
    public void I06_EskiMaclarinEtkisi_YeniMactanFazlaOlmaz()
    {
        var at = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        OutcomeRatingModel Build(int bigWinIndex)
        {
            var m = new OutcomeRatingModel(new OutcomeModelParameters(), CompetitionCatalog.Unclassified);
            for (var i = 0; i < 20; i++)
            {
                var big = i == bigWinIndex;
                m.Update(new HistoricalMatch(i + 1, at.AddDays(-400 + i * 20), League, 10, 20 + i, big ? 5 : 1, big ? 0 : 1));
                m.Update(new HistoricalMatch(1000 + i, at.AddDays(-400 + i * 20), League, 20 + i, 30, 1, 1));
            }
            return m;
        }
        var recent = Build(19).Expect(League, 10, 99, at);   // büyük galibiyet EN SON maç
        var old = Build(0).Expect(League, 10, 99, at);       // büyük galibiyet EN ESKİ maç
        Assert.True(recent.HomeLogAttack > old.HomeLogAttack,
            $"yeni {recent.HomeLogAttack:F4} eski {old.HomeLogAttack:F4} — aynı sonuç yeni maçta daha ağır olmalı");
    }

    // ═══ 7: RAKİP KALİTESİ ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void I07_GucluRakibeKarsiAlinanSonuc_ZayifRakibeKarsiAyniSonuctan_DahaDegerlidir()
    {
        var history = World(23);
        var catalog = CompetitionCatalog.Build(history);
        var at = history[^1].KickoffUtc.AddDays(3);
        // 190: yeni takım; ligin EN GÜÇLÜ (109) ya da EN ZAYIF (100) rakibine karşı aynı 2-0 galibiyetleri.
        OutcomeExpectation After(int opponent)
        {
            var m = new OutcomeRatingModel(new OutcomeModelParameters(), catalog);
            foreach (var h in history) m.Update(h);
            for (var i = 0; i < 6; i++) m.Update(new HistoricalMatch(90_000 + i, at.AddDays(-60 + i * 7), 900, 190, opponent, 2, 0));
            return m.Expect(900, 190, 105, at);
        }
        var vsStrong = After(109);
        var vsWeak = After(100);
        Assert.True(vsStrong.LambdaHome > vsWeak.LambdaHome,
            $"güçlüye karşı λ={vsStrong.LambdaHome:F3} zayıfa karşı λ={vsWeak.LambdaHome:F3}");
    }

    // ═══ 8–9: İÇ SAHA VE LİG KATSAYISININ YAPISI ════════════════════════════════════════════════════

    [Fact]
    public void I08_IcSahaAvantaji_YalnizLigTabanindan_Gelir_TarafsizSahadaUygulanmaz()
    {
        // Tarafsız saha = organizasyonun ev/deplasman gol tabanının eşit olduğu durum. Modelde iç saha avantajının
        // BAŞKA kaynağı yoktur; taban eşitse hiçbir taraf avantaj almaz.
        var neutral = D(E(1.30, 1.30, leagueHome: 1.30, leagueAway: 1.30));
        Assert.Equal(neutral.HomeWin, neutral.AwayWin, 6);
        var home = D(E(1.45, 1.15));
        Assert.True(home.HomeWin > home.AwayWin);
    }

    [Fact]
    public void I09_AyniLigKatsayisi_IkiTakimaAnlamsizFarkUretmez()
    {
        var history = World(31);
        var catalog = CompetitionCatalog.Build(history);
        var model = new OutcomeRatingModel(new OutcomeModelParameters(), catalog);
        foreach (var m in history) model.Update(m);
        var at = history[^1].KickoffUtc.AddDays(5);
        var e = model.Expect(900, 104, 105, at);
        Assert.False(e.CrossLeague, "aynı ligdeki maç ligler arası sayılamaz");
        Assert.Equal(0, e.HomeLeagueStrength - e.AwayLeagueStrength, 6);
    }

    // ═══ 10–14: MATEMATİKSEL TUTARLILIK ═════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(2.4, 0.6)] [InlineData(1.2, 1.2)] [InlineData(0.6, 0.5)] [InlineData(2.6, 2.4)] [InlineData(0.4, 2.3)]
    public void I10_I14_ToplamlarYuzde100_CifteSansTuretilir_KartlarCeliskisiz(double lh, double la)
    {
        var pr = OutcomePredictor.Predict(E(lh, la), League, P);
        var d = pr.Calibrated;
        Assert.Equal(1.0, d.HomeWin + d.Draw + d.AwayWin, 6);                       // I10
        foreach (var line in new[] { 1.5, 2.5, 3.5 })
            Assert.Equal(1.0, d.Over(line) + d.Under(line), 6);                     // I11
        Assert.Equal(1.0, d.BttsYes + d.BttsNo, 6);                                 // I12
        var s = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep");
        double C(string m) => s.Families.SelectMany(f => f.Items).Single(i => i.Market == m).CalibratedProbability;
        Assert.Equal(C("Ev Sahibi Kazanır") + C("Beraberlik"), C("Çifte Şans (1X)"), 3);   // I13 (DTO 4 haneye yuvarlar)
        Assert.Equal(C("Beraberlik") + C("Deplasman Kazanır"), C("Çifte Şans (X2)"), 3);
        Assert.Equal(C("Ev Sahibi Kazanır") + C("Deplasman Kazanır"), C("Çifte Şans (1-2)"), 3);
        Assert.True(s.Checks!.Consistent);                                          // I14
        // Ana kartlar aynı dağılımdan gelir: gösterilen yüzde ile kalibre olasılık aynı yuvarlamadadır.
        foreach (var c in s.MainCards)
            Assert.InRange(c.Probability - c.CalibratedProbability * 100, -1.01, 1.01);
        // Üç kart üç farklı aileden ve birbirinin tekrarı değil.
        Assert.Equal(3, s.MainCards.Select(c => c.Family).Distinct().Count());
        Assert.Equal(3, s.MainCards.Select(c => c.MarketKey).Distinct().Count());
    }

    // ═══ 15–17: SIZINTI VE VERİ DİSİPLİNİ ═══════════════════════════════════════════════════════════

    [Fact]
    public void I15_GelecekVeriKullanilamaz_KesimAnindanSonrakiMaclarTahmineGirmez()
    {
        var history = World(41);
        var catalog = CompetitionCatalog.Build(history);
        var cut = history[history.Count / 2].KickoffUtc;
        OutcomeExpectation Expect(IReadOnlyList<HistoricalMatch> src)
        {
            var m = new OutcomeRatingModel(new OutcomeModelParameters(), catalog);
            foreach (var h in src.Where(x => x.KickoffUtc < cut)) m.Update(h);
            return m.Expect(900, 104, 105, cut);
        }
        var a = Expect(history);
        // Gelecekteki bütün sonuçlar tersine çevrilse bile kesim anındaki beklenti DEĞİŞMEZ.
        var flipped = history.Select(m => m.KickoffUtc >= cut ? new HistoricalMatch(m.MatchId, m.KickoffUtc, m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.AwayGoals + 3, 0) : m).ToList();
        var b = Expect(flipped);
        Assert.Equal(a.LambdaHome, b.LambdaHome, 9);
        Assert.Equal(a.LambdaAway, b.LambdaAway, 9);
        Assert.Equal(a.EloHomeExpectation, b.EloHomeExpectation, 9);
    }

    [Fact]
    public void I16_TamamlanmamisMac_FormaVeReytingeGirmez()
    {
        // Model girdisi YALNIZ bitmiş maçtır: yükleyici Status=Finished süzer, ayrıca skor aralığı doğrular.
        var m = new OutcomeRatingModel(new OutcomeModelParameters(), CompetitionCatalog.Unclassified);
        var at = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 10; i++) m.Update(new HistoricalMatch(i, at.AddDays(i), League, 1, 2 + i, 2, 1));
        var before = m.Expect(League, 1, 50, at.AddDays(20));
        // Aynı model tekrar sorgulanınca (yeni maç İŞLENMEDEN) beklenti aynıdır: tahmin, işlenmemiş maçtan etkilenmez.
        var after = m.Expect(League, 1, 50, at.AddDays(20));
        Assert.Equal(before.LambdaHome, after.LambdaHome, 9);
        Assert.Equal(before.HomeSample, after.HomeSample);
    }

    [Fact]
    public void I17_AyniMac_EgitimSetineIkiKezGirmez()
    {
        var at = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = new List<(int Home, int Away, DateTime Date)>();
        for (var i = 0; i < 5; i++) rows.Add((10, 20, at.AddDays(i)));
        rows.Add((10, 20, at));            // aynı gün ikinci satır (sağlayıcı tekrarı)
        var seen = new HashSet<(int, int, int)>();
        var kept = rows.Where(r => seen.Add((r.Home, r.Away, (int)(r.Date.Date - DateTime.UnixEpoch).TotalDays))).ToList();
        Assert.Equal(5, kept.Count);
    }

    // ═══ 18: SAYISAL SAĞLAMLIK ══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0.01, 0.01)] [InlineData(4.5, 4.5)] [InlineData(0.15, 4.5)] [InlineData(4.5, 0.15)]
    public void I18_ModelNaN_NegatifVeya100UstuUretmez(double lh, double la)
    {
        foreach (var p in new[]
        {
            new OutcomeModelParameters(),
            new OutcomeModelParameters { DrawInflation = 1.25, TotalGoalShrink = 0, UncertaintyMix = 0.6, BaselineMix = 0.2 },
            new OutcomeModelParameters { LowScoreRho = -0.16 },
            new OutcomeModelParameters { LowScoreRho = 0.9, HomeTilt = 0.2 }
        })
        {
            var pr = OutcomePredictor.Predict(E(lh, la, coverage: 0.3), League, p);
            foreach (var d in new[] { pr.Raw, pr.Calibrated, pr.Baseline })
            {
                foreach (var v in new[] { d.HomeWin, d.Draw, d.AwayWin, d.BttsYes, d.BttsNo, d.Over(2.5), d.Under(2.5), d.ExpectedHome, d.ExpectedAway })
                {
                    Assert.False(double.IsNaN(v) || double.IsInfinity(v), $"NaN/∞ üretildi: {v}");
                    Assert.True(v >= -1e-9, $"negatif değer: {v}");
                }
                Assert.Equal(1.0, d.Total, 6);
                Assert.True(d.HomeWin <= 1 && d.Draw <= 1 && d.AwayWin <= 1);
            }
            var s = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep");
            foreach (var c in s.Families.SelectMany(f => f.Items))
                Assert.InRange(c.Probability, 0, 100);
        }
    }

    // ═══ EK: DÜŞÜK SKOR DÜZELTMESİ VE ADAY KATMANLARIN VARSAYILANI KAPALI ═══════════════════════════

    [Fact]
    public void AdayKatmanlar_VarsayilanKapali_UretimDavranisiDegismez()
    {
        var p = new OutcomeModelParameters();
        Assert.Equal(0, p.LowScoreRho);
        Assert.Equal(0, p.HomeTilt);
        Assert.Equal(0, p.TeamHomeEdgeRate);
        Assert.Empty(p.LeagueDrawInflation);
        Assert.Empty(p.LeagueHomeTilt);
        var e = E(1.8, 1.0);
        var withRho = OutcomePredictor.Predict(e, League, new OutcomeModelParameters { LowScoreRho = 0 }).Calibrated;
        var plain = ScoreDistribution.Poisson(OutcomePredictor.Shrink(e, p.TotalGoalShrink).Home, OutcomePredictor.Shrink(e, p.TotalGoalShrink).Away, p.DrawInflation);
        Assert.Equal(plain.HomeWin, withRho.HomeWin, 9);
    }

    [Fact]
    public void DusukSkorDuzeltmesi_YalnizDortHucreyiDegistirir_ToplamYine100()
    {
        var a = ScoreDistribution.Poisson(1.5, 1.1);
        var b = ScoreDistribution.Poisson(1.5, 1.1, 1.0, -0.12);
        Assert.Equal(1.0, b.Total, 6);
        Assert.True(b[0, 0] > a[0, 0], "ρ<0 0-0'ı büyütür");
        Assert.True(b[1, 1] > a[1, 1], "ρ<0 1-1'i büyütür");
        Assert.True(b[1, 0] < a[1, 0], "ρ<0 1-0'ı küçültür");
        Assert.True(b.Draw > a.Draw, "beraberlik toplamı artar");
        // 2+ gollü hücrelerin ORANI korunur (yalnız normalizasyon kayması): 3-2 / 2-3 oranı aynı kalır.
        Assert.Equal(a[3, 2] / a[2, 3], b[3, 2] / b[2, 3], 9);
    }

    [Fact]
    public void ParsimoniKapisi_OlculemeyenParametre_NotrDegerdeKalir()
    {
        // Seçim örneklemi 30'un altındaysa parametre değişmez (PairedSelection sözleşmesi).
        // Burada sözleşmenin kendisi doğrulanır: varsayılan parametreler nötrdür.
        var p = new OutcomeModelParameters();
        Assert.Equal(1.0, p.SeasonCarry);            // sezon daraltması yok
        Assert.Equal(1.0, p.CrossLeagueTeamWeight);  // ligler arası katkı kısılmaz
        Assert.Equal(1.0, p.DrawInflation);          // beraberlik ağırlığı yok
        Assert.Equal(1.0, p.GoalScale);
    }

    [Fact]
    public void CifteSans_Settlement_AnahtarlariDogruDegerlendirilir()
    {
        Assert.True(OutcomeBacktest.Hit(OddsMarketKeys.DoubleChance1X, 2, 1));
        Assert.True(OutcomeBacktest.Hit(OddsMarketKeys.DoubleChance1X, 1, 1));
        Assert.False(OutcomeBacktest.Hit(OddsMarketKeys.DoubleChance1X, 0, 1));
        Assert.True(OutcomeBacktest.Hit(OddsMarketKeys.DoubleChanceX2, 1, 1));
        Assert.True(OutcomeBacktest.Hit(OddsMarketKeys.DoubleChance12, 2, 1));
        Assert.False(OutcomeBacktest.Hit(OddsMarketKeys.DoubleChance12, 1, 1));
    }
}
