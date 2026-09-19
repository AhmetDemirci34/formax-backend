using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// ANA KART SEMANTİĞİ (19.09.2026) — kullanıcı tek kartı FORMAX'ın ANA TAHMİNİ olarak okur. 1X2 uygunsa birinci kart
/// her zaman modelin en olası maç sonucudur; daha düşük olasılıklı bir sonuç "bilgi değeri yüksek" diye ana kart yapılamaz.
/// Çifte şans yasak değildir ama ana kartın yerine geçemez ve tek kart olamaz.
/// </summary>
public class MainCardSemanticsTests
{
    private const int League = 140;

    private static OutcomeExpectation E(double lh, double la)
        => new(lh, la, 1.45, 1.15, 20, 20, 1, true, 1.5, 1.1, 1.2, 1.3, 10, 10, null, null);

    private static OutcomeSnapshotBuilder.MarketPublication Pub(params string[] eligible)
        => new(MarketFamilies.All.Select(f => new MarketFamilyMetrics
        {
            LeagueId = League, Family = f, Matches = 500,
            Status = eligible.Contains(f) ? MarketEligibilityStatuses.Eligible : MarketEligibilityStatuses.WorseThanBaseline,
            ReasonCodes = eligible.Contains(f) ? new List<string>() : new List<string> { "WORSE_THAN_LEAGUE_AVERAGE" }
        }));

    private static readonly string[] ResultOnly = { MarketFamilies.MatchResult, MarketFamilies.DoubleChance };

    private static OutcomeSnapshotDto Snap(double lh, double la, OutcomeSnapshotBuilder.MarketPublication pub)
        => OutcomeSnapshotBuilder.Build(1, OutcomePredictor.Predict(E(lh, la), League, new OutcomeModelParameters()), "Ev", "Dep", pub);

    private static double P(OutcomeSnapshotDto s, string key)
        => s.Families.SelectMany(f => f.Items).Single(i => i.MarketKey == key).CalibratedProbability;

    /// <summary>1X2 olasılıkları verilen hedefe yakın olan bir λ çifti bulur (gerçek dağılımdan, uydurma yüzde yok).</summary>
    private static (double Lh, double La) FindLambdas(double home, double draw, double away)
    {
        var best = (Lh: 1.4, La: 1.2); var bestErr = double.MaxValue;
        for (var lh = 0.30; lh <= 3.0; lh += 0.01)
            for (var la = 0.30; la <= 3.0; la += 0.01)
            {
                var d = OutcomePredictor.Predict(E(lh, la), League, new OutcomeModelParameters()).Calibrated;
                var err = Math.Abs(d.HomeWin - home) + Math.Abs(d.Draw - draw) + Math.Abs(d.AwayWin - away);
                if (err < bestErr) { bestErr = err; best = (lh, la); }
            }
        return best;
    }

    // ═══ 1. %45 Ev / %29 X / %26 Deplasman → ana kart EV SAHİBİ ══════════════════════════════════════

    [Fact]
    public void Z01_EvSahibiEnOlasi_BeraberlikAnaKartOlamaz()
    {
        var (lh, la) = FindLambdas(0.45, 0.29, 0.26);
        var s = Snap(lh, la, Pub(ResultOnly));
        var res = s.Families.Single(f => f.Family == OutcomeFamilies.Result).Items;
        var argmax = res.OrderByDescending(c => c.CalibratedProbability).First();
        Assert.Equal(OddsMarketKeys.Ms1, argmax.MarketKey);

        Assert.NotEmpty(s.MainCards);
        Assert.Equal(OddsMarketKeys.Ms1, s.MainCards[0].MarketKey);
        Assert.DoesNotContain(s.MainCards, c => c.MarketKey == OddsMarketKeys.MsX);
        // Beraberlik bilgi değeri daha yüksek olsa bile ana kart olamaz.
        var draw = res.Single(c => c.MarketKey == OddsMarketKeys.MsX);
        Assert.True(OutcomeSnapshotBuilder.InformationValue(draw) > OutcomeSnapshotBuilder.InformationValue(argmax),
            "senaryo anlamlı olsun diye beraberliğin bilgi değeri daha yüksek olmalı");
    }

    // ═══ 2. %38 Ev / %24 X / %38 Deplasman, X2 %62 → ana kart deterministik argmax ═══════════════════

    [Fact]
    public void Z02_EsitOlasilik_DeterministikArgmax_X2AnaKartOlamaz()
    {
        var (lh, la) = FindLambdas(0.38, 0.24, 0.38);
        var pub = Pub(ResultOnly);
        var a = Snap(lh, la, pub);
        var b = Snap(lh, la, pub);
        Assert.Equal(a.MainCards[0].MarketKey, b.MainCards[0].MarketKey);      // deterministik
        Assert.False(OutcomeFamilies.IsCompound(a.MainCards[0].MarketKey!));
        var res = a.Families.Single(f => f.Family == OutcomeFamilies.Result).Items;
        var expected = res.OrderByDescending(c => c.CalibratedProbability).ThenBy(c => c.MarketKey, StringComparer.Ordinal).First();
        Assert.Equal(expected.MarketKey, a.MainCards[0].MarketKey);
        // Çifte şans varsa yalnız EK karttır.
        var dc = a.MainCards.FirstOrDefault(c => OutcomeFamilies.IsCompound(c.MarketKey ?? ""));
        if (dc != null) Assert.True(a.MainCards.IndexOf(dc) > 0);
    }

    // ═══ 3. Tek kartlık maç ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Z03_TekKart_1X2Uygunsa_YalnizArgmax_CifteSansTekKartOlamaz()
    {
        var rnd = new Random(11);
        var single = 0;
        for (var i = 0; i < 400; i++)
        {
            var s = Snap(0.4 + rnd.NextDouble() * 2.4, 0.4 + rnd.NextDouble() * 2.2, Pub(ResultOnly));
            Assert.NotEmpty(s.MainCards);
            Assert.False(OutcomeFamilies.IsCompound(s.MainCards[0].MarketKey!));
            if (s.MainCards.Count == 1)
            {
                single++;
                Assert.False(OutcomeFamilies.IsCompound(s.MainCards[0].MarketKey!), "çifte şans tek kart olamaz");
            }
        }
        Assert.True(single > 0, "tek kartlık durum örneklenmeli");
    }

    // ═══ 4–5. İki ve üç kartlık maç ═════════════════════════════════════════════════════════════════

    [Fact]
    public void Z04_Z05_IkiVeUcKart_IlkKart1X2Argmax_DigerleriFarkliAile()
    {
        var rnd = new Random(23);
        int two = 0, three = 0;
        for (var i = 0; i < 400; i++)
        {
            var s = Snap(0.4 + rnd.NextDouble() * 2.4, 0.4 + rnd.NextDouble() * 2.2, Pub(MarketFamilies.All.ToArray()));
            var res = s.Families.Single(f => f.Family == OutcomeFamilies.Result).Items;
            var argmax = res.OrderByDescending(c => c.CalibratedProbability).ThenBy(c => c.MarketKey, StringComparer.Ordinal).First();
            Assert.Equal(argmax.MarketKey, s.MainCards[0].MarketKey);
            Assert.InRange(s.MainCards.Count, 1, 3);
            Assert.Equal(s.MainCards.Count, s.MainCards.Select(c => c.Family).Distinct().Count());
            Assert.Equal(s.MainCards.Count, s.MainCards.Select(c => c.MarketKey).Distinct().Count());
            if (s.MainCards.Count == 2) two++;
            if (s.MainCards.Count == 3) three++;
        }
        Assert.True(two > 0 && three > 0, $"iki kart={two} üç kart={three}");
    }

    // ═══ 6. 1X2 uygun değil ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Z06_1X2UygunDegil_1X2VeDCSizmaz_IkiliMarketEnOlasiTarafiGosterir()
    {
        var rnd = new Random(31);
        var shown = 0;
        for (var i = 0; i < 300; i++)
        {
            var s = Snap(0.5 + rnd.NextDouble() * 2.2, 0.5 + rnd.NextDouble() * 2.0,
                Pub(MarketFamilies.TotalGoals25, MarketFamilies.BothTeamsToScore));
            foreach (var c in s.MainCards)
            {
                Assert.NotEqual(MarketFamilies.MatchResult, c.MeasuredFamily);
                Assert.NotEqual(MarketFamilies.DoubleChance, c.MeasuredFamily);
                Assert.True(c.CalibratedProbability >= 0.5, $"{c.Market} %{c.Probability} — ikili markette en olası taraf gösterilir");
                shown++;
            }
        }
        Assert.True(shown > 0);
    }

    // ═══ 7. Gerçek maç regresyonu — 104149 ve 99159 ═════════════════════════════════════════════════

    [Theory]
    // 104149: Ev %45 / X %29 / Dep %26 — eskiden tek kart "Beraberlik %29" gösteriliyordu.
    [InlineData(0.45, 0.29, 0.26, OddsMarketKeys.Ms1)]
    // 99159: Ev %38 / X %24 / Dep %38, X2 %62 — eskiden ana kart "Çifte Şans (X2)" idi.
    [InlineData(0.38, 0.24, 0.38, OddsMarketKeys.Ms1)]
    public void Z07_GercekMacRegresyonu_AnaKartEnOlasiSonuc(double h, double d, double a, string expectedKey)
    {
        var (lh, la) = FindLambdas(h, d, a);
        var s = Snap(lh, la, Pub(ResultOnly));
        Assert.Equal(expectedKey, s.MainCards[0].MarketKey);
        Assert.False(OutcomeFamilies.IsCompound(s.MainCards[0].MarketKey!));
        Assert.True(s.MainCards[0].CalibratedProbability >= P(s, OddsMarketKeys.MsX));
        Assert.True(s.MainCards[0].CalibratedProbability >= P(s, OddsMarketKeys.Ms2) - 1e-9);
    }

    // ═══ 8. Model 4.0 olasılıkları ve tutarlılık DEĞİŞMEDİ ══════════════════════════════════════════

    [Theory]
    [InlineData(2.4, 0.6)] [InlineData(1.2, 1.2)] [InlineData(0.6, 0.5)] [InlineData(2.3, 2.1)] [InlineData(0.4, 2.3)]
    public void Z08_Model40Olasiliklari_KartKuralindanBagimsiz(double lh, double la)
    {
        var pr = OutcomePredictor.Predict(E(lh, la), League, new OutcomeModelParameters());
        var wide = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep", Pub(MarketFamilies.All.ToArray()));
        var narrow = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep", Pub(ResultOnly));
        foreach (var key in new[] { OddsMarketKeys.Ms1, OddsMarketKeys.MsX, OddsMarketKeys.Ms2,
                                    OddsMarketKeys.Over25, OddsMarketKeys.Under25, OddsMarketKeys.BttsYes, OddsMarketKeys.BttsNo,
                                    OutcomeMarketKeys.Over15, OutcomeMarketKeys.Over35 })
            Assert.Equal(P(wide, key), P(narrow, key), 9);
        // Dağılımın kendisi de kart kuralından bağımsızdır (DTO 4 haneye yuvarlar).
        Assert.Equal(Math.Round(pr.Calibrated.HomeWin, 4), P(wide, OddsMarketKeys.Ms1), 9);
        Assert.Equal(Math.Round(pr.Calibrated.Over(2.5), 4), P(wide, OddsMarketKeys.Over25), 9);
        Assert.Equal(Math.Round(pr.Calibrated.BttsYes, 4), P(wide, OddsMarketKeys.BttsYes), 9);
        Assert.True(wide.Checks!.Consistent && narrow.Checks!.Consistent);
        Assert.Equal(wide.ExpectedHomeGoals, narrow.ExpectedHomeGoals);
    }
}
