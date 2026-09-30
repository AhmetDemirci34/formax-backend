using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using HistoricalMatch = Formax.Application.Services.Outcomes.HistoricalMatch;

namespace Formax.Tests;

/// <summary>
/// MODEL 6 (30.09.2026) — Davidson beraberlikli Elo motoru, 1X2 ölçüm/kalibrasyon yardımcıları, nested fold düzeni ve Model 6 gölge
/// kaydının zaman/duplicate/puanlama/sızıntı sözleşmeleri. Dış istek yok.
/// </summary>
public class Model6Tests
{
    private static readonly DateTime T0 = new(2025, 8, 16, 15, 0, 0, DateTimeKind.Utc);
    private const int Lg = 39;
    private static readonly CompetitionCatalog Catalog = new(new[] { Lg, 40 });

    private static HistoricalMatch M(int id, DateTime at, int h, int a, int hg, int ag, int league = Lg) => new(id, at, league, h, a, hg, ag);

    /// <summary>6 takımlı, 12 haftalık tek lig geçmişi (deterministik).</summary>
    private static List<HistoricalMatch> League(int weeks = 12, int league = Lg, int teamBase = 1, int idBase = 1)
    {
        var list = new List<HistoricalMatch>(); var rnd = new Random(7); var id = idBase;
        for (var w = 0; w < weeks; w++)
            for (var k = 0; k < 3; k++)
            {
                var h = teamBase + (w + k) % 6; var a = teamBase + (w + k + 1 + k) % 6; if (a == h) a = teamBase + (h - teamBase + 3) % 6;
                list.Add(M(id++, T0.AddDays(7 * w).AddHours(k), h, a, rnd.Next(4), rnd.Next(3), league));
            }
        return list.OrderBy(m => m.KickoffUtc).ThenBy(m => m.MatchId).ToList();
    }

    private static Dictionary<int, Probs3> Replay(IReadOnlyList<HistoricalMatch> h, DavidsonEloConfig? c = null)
    {
        var d = new Dictionary<int, Probs3>();
        DavidsonElo.Replay(h, Catalog, c ?? Model6Shadow.Davidson, DateTime.MaxValue, (m, p) => d[m.MatchId] = p);
        return d;
    }

    // ───────────────────────── zaman kuralı ─────────────────────────

    [Fact]
    public void AyniKickoffMaclari_BirbirininSonucunuGormez_MacKendiSonucunuGormez()
    {
        var h = League();
        var at = T0.AddDays(100);
        List<HistoricalMatch> With(int a, int b) => h.Concat(new[] { M(9001, at, 1, 2, a, b), M(9002, at, 3, 4, 1, 1) }).ToList();
        var x = Replay(With(5, 0)); var y = Replay(With(0, 5));
        Assert.Equal(x[9002], y[9002]); // eşzamanlı maçın sonucu sızmıyor
        Assert.Equal(x[9001], y[9001]); // maç kendi sonucunu görmüyor
    }

    [Fact]
    public void BitmemisMacinSonucu_120DakikaDolmadanKullanilmaz()
    {
        var h = League();
        var at = T0.AddDays(100);
        Dictionary<int, Probs3> Run(int hg, int ag, int laterMinutes)
            => Replay(h.Concat(new[] { M(9001, at, 1, 2, hg, ag), M(9002, at.AddMinutes(laterMinutes), 1, 3, 1, 1) }).OrderBy(m => m.KickoffUtc).ToList());
        // 60 dk sonra başlayan maç: 9001'in sonucu (henüz bitmemiş) takım 1'in gücüne girmez.
        Assert.Equal(Run(6, 0, 60)[9002], Run(0, 6, 60)[9002]);
        // 121 dk sonra: sonuç bilinir, etkiler.
        Assert.NotEqual(Run(6, 0, 121)[9002], Run(0, 6, 121)[9002]);
        // Tam 120 dk: AvailableAt < kickoff koşulu sağlanmaz → kullanılmaz.
        Assert.Equal(Run(6, 0, 120)[9002], Run(0, 6, 120)[9002]);
    }

    [Fact]
    public void YukselenTakimOnseli_GelecekVeriyiGormez_Deterministik()
    {
        // Lig 40'tan takım 21, lig 39'a geçiyor. Önseli yalnız geçiş anından önceki güçlerden gelir.
        // Alt ligde takım 21 her maçı kazanıyor (lig ortalamasının üstünde); iki lig bağlantısız → ortalamalar ~1500.
        var lower = League(10, 40, 22, 500).Concat(Enumerable.Range(0, 10).Select(i => M(700 + i, T0.AddDays(7 * i).AddHours(5), 21, 22 + i % 6, 3, 0, 40))).ToList();
        var upper = League(10, Lg, 1, 1);
        var at = T0.AddDays(120);
        var entry = M(9100, at, 21, 1, 0, 0);
        var future = new[] { M(9101, at.AddDays(7), 21, 2, 5, 0), M(9102, at.AddDays(14), 3, 21, 0, 4) };
        var basis = lower.Concat(upper).Append(entry).OrderBy(m => m.KickoffUtc).ThenBy(m => m.MatchId).ToList();
        var a = Replay(basis);
        var b = Replay(basis.Concat(future).ToList());
        Assert.Equal(a[9100], b[9100]);
        Assert.Equal(a[9100], Replay(basis)[9100]);
        // Önsel açıkken güçlü (ya da eşit) lige geçen takım yeni lig ortalaması − 60'ın üstünde başlayamaz → önselsizden daha zayıf.
        var off = Replay(basis, Model6Shadow.Davidson with { PromotionPrior = 0 });
        Assert.True(a[9100].H < off[9100].H);
    }

    [Fact]
    public void SezonGecisi_Deterministik_VeLigOrtalamasinaCeker()
    {
        var h = League();
        var gap = T0.AddDays(84 + 70); // 70 gün ara (> 50)
        var after = h.Append(M(9200, gap, 1, 2, 1, 0)).ToList();
        // İç saha avantajı kapalı: x = R_ev − R_dep; daraltma bu farkı küçültür.
        var c = Model6Shadow.Davidson with { SeasonCarry = 0.5, HomeAdvantage = false };
        Assert.Equal(Replay(after, c)[9200], Replay(after, c)[9200]);
        var noCarry = Replay(after, c with { SeasonCarry = 1.0 })[9200];
        var carry = Replay(after, c)[9200];
        // Daraltma iki takımı ortalamaya çeker: ev/deplasman farkı küçülür (ya da eşit kalır).
        Assert.True(Math.Abs(carry.H - carry.A) < Math.Abs(noCarry.H - noCarry.A));
    }

    // ───────────────────────── olasılık sözleşmesi ─────────────────────────

    [Theory]
    [InlineData(0.0, 0.7)]
    [InlineData(1.2, 0.5)]
    [InlineData(-3.0, 1.1)]
    [InlineData(80.0, 0.7)]
    [InlineData(-80.0, 1e-9)]
    public void Davidson_Toplam1_NaNInfNegatifYok_BeraberlikFormulu(double x, double nu)
    {
        var p = DavidsonElo.Davidson(x, nu);
        Assert.True(p.IsValid);
        var xc = Math.Clamp(x, -30, 30);
        Assert.Equal(nu / (Math.Exp(xc / 2) + Math.Exp(-xc / 2) + nu), p.D, 12);
        if (x == 0) Assert.Equal(nu / (2 + nu), p.D, 12);
        // Yön: ters işaret ev ↔ deplasman aynası
        var q = DavidsonElo.Davidson(-x, nu);
        Assert.Equal(p.H, q.A, 12); Assert.Equal(p.A, q.H, 12); Assert.Equal(p.D, q.D, 12);
    }

    [Fact]
    public void EvDeplasmanYonu_GucluEvSahibiDahaYuksekEvOlasiligi()
    {
        var h = new List<HistoricalMatch>();
        var id = 1;
        // Takım 1 herkesi yeniyor, takım 6 herkese kaybediyor.
        for (var w = 0; w < 10; w++)
        {
            h.Add(M(id++, T0.AddDays(7 * w), 1, 2 + w % 4, 3, 0));
            h.Add(M(id++, T0.AddDays(7 * w).AddHours(3), 6, 2 + (w + 1) % 4, 0, 2));
        }
        var at = T0.AddDays(80);
        var d = Replay(h.Concat(new[] { M(9300, at, 1, 6, 0, 0), M(9301, at.AddDays(3), 6, 1, 0, 0) }).ToList());
        Assert.True(d[9300].H > d[9300].A);
        Assert.True(d[9301].A > d[9301].H);
        Assert.True(d[9300].H > d[9301].A); // iç saha avantajı ev sahibine
    }

    [Fact]
    public void PoissonSkorMatrisi_1X2yeDogruCevrilir()
    {
        var s = ScoreDistribution.Poisson(1.4, 1.1);
        double h = 0, d = 0, a = 0;
        for (var i = 0; i <= ScoreDistribution.MaxGoals; i++)
            for (var j = 0; j <= ScoreDistribution.MaxGoals; j++)
                if (i > j) h += s[i, j]; else if (i == j) d += s[i, j]; else a += s[i, j];
        var p = Probs3.From(s);
        Assert.Equal(h, p.H, 9); Assert.Equal(d, p.D, 9); Assert.Equal(a, p.A, 9);
        Assert.True(p.IsValid);
        var sym = Probs3.From(ScoreDistribution.Poisson(1.3, 1.3));
        Assert.Equal(sym.H, sym.A, 9);
    }

    [Fact]
    public void KucukOrneklemShrinkage_LigNuGlobaleCekilir()
    {
        // Yeni ligde tek beraberlik: lig ν sapması küçük; aynı sonuç çok maçta tekrarlanınca sapma büyür ama sınırlı kalır.
        var one = DavidsonElo.Replay(new[] { M(1, T0, 1, 2, 1, 1, 77) }, Catalog, Model6Shadow.Davidson, T0.AddDays(1));
        var many = DavidsonElo.Replay(Enumerable.Range(0, 400).Select(i => M(i + 1, T0.AddDays(i), 1 + i % 10, 11 + i % 10, 1, 1, 77)).ToList(),
            Catalog, Model6Shadow.Davidson, T0.AddDays(401));
        var g1 = Math.Log(one.NuOf(77)) - Math.Log(one.NuOf(999)); // 999 = hiç görülmemiş lig → yalnız global
        var gm = Math.Log(many.NuOf(77)) - Math.Log(many.NuOf(999));
        Assert.InRange(g1, 0, 0.02);
        Assert.True(gm > g1);
        // Durağan üst sınır: rate/decay × max gradyan (1) = 4 log birimi
        Assert.True(gm < DavidsonEloConfig.NuLeagueRate / DavidsonEloConfig.NuLeagueDecay);
    }

    [Fact]
    public void AyniConfig_AyniSonuc_Ablasyon_Deterministik_ConfigHashSabit()
    {
        var h = League();
        var a = Replay(h); var b = Replay(h);
        Assert.Equal(a, b);
        var abl = Replay(h, Model6Shadow.Davidson with { HomeAdvantage = false });
        Assert.Equal(abl, Replay(h, Model6Shadow.Davidson with { HomeAdvantage = false }));
        Assert.NotEqual(a[h[^1].MatchId], abl[h[^1].MatchId]);
        // Dondurulmuş yapı: sabitler değişirse hash değişir (sonuç görüldükçe otomatik değişmez).
        Assert.Equal("K=16 carry=1 promo=60 form=0 rest=0", $"K={Model6Shadow.Davidson.K} carry={Model6Shadow.Davidson.SeasonCarry} promo={Model6Shadow.Davidson.PromotionPrior} form={Model6Shadow.Davidson.FormWeight} rest={Model6Shadow.Davidson.RestBeta}");
        Assert.NotEqual(Model6Shadow.Davidson.Hash, (Model6Shadow.Davidson with { K = 17 }).Hash);
        Assert.Equal(32, Model6Shadow.ConfigHash.Length);
        Assert.Equal(Model6Shadow.ConfigHash, Model6Shadow.ConfigHash);
    }

    // ───────────────────────── nested fold düzeni ─────────────────────────

    [Fact]
    public void NestedFoldlar_ZamanYonuDogru_Ayrik_FinalTestParametreSecemez()
    {
        var kick = Enumerable.Range(0, 600).Select(i => T0.AddHours(13 * i)).ToList();
        var start = T0; var end = kick[^1].AddDays(1);
        var folds = Model6Lab.OuterFolds(kick, start, end, 6);
        Assert.Equal(6, folds.Count);
        Assert.Equal(start, folds[0].From); Assert.Equal(end, folds[^1].To);
        for (var k = 0; k < folds.Count; k++)
        {
            Assert.True(folds[k].From < folds[k].To);
            if (k > 0) Assert.Equal(folds[k - 1].To, folds[k].From);
            // İç pencere: yalnız fold başından ÖNCEKİ maçlar; dış: fold içi. Kesişim yok.
            var inner = kick.Where(t => t < folds[k].From).ToHashSet();
            var outer = kick.Where(t => t >= folds[k].From && t < folds[k].To).ToHashSet();
            Assert.False(inner.Overlaps(outer));
            Assert.All(inner, t => Assert.True(t < outer.DefaultIfEmpty(DateTime.MaxValue).Min()));
        }
        // Kalibrasyon yalnız verilen (iç) örneklerle uydurulur: dış örnekler sonuca etki edemez.
        var innerPairs = Enumerable.Range(0, 300).Select(i => (Probs3.Normalized(0.5, 0.25, 0.25), i % 2 == 0 ? 0 : i % 3)).ToList();
        Assert.Equal(Model6Lab.FitTemperature(innerPairs), Model6Lab.FitTemperature(innerPairs.ToList()));
    }

    [Fact]
    public void Metrikler_KarisiklikMatrisi_PrecisionRecall()
    {
        var xs = new List<(Probs3, int)>
        {
            (new Probs3(0.6, 0.2, 0.2), 0), (new Probs3(0.6, 0.2, 0.2), 1), (new Probs3(0.2, 0.2, 0.6), 2), (new Probs3(0.2, 0.5, 0.3), 1)
        };
        var m = Model6Lab.Evaluate(xs);
        Assert.Equal(4, m.N);
        Assert.Equal(0.75, m.Accuracy, 5);
        Assert.Equal(1, m.Confusion[1][1]); Assert.Equal(1, m.Confusion[1][0]);
        Assert.Equal(0.5, m.Recall[1], 5); Assert.Equal(1.0, m.Precision[1], 5);
        Assert.Equal(xs.Average(x => Model6Lab.LogLoss(x.Item1, x.Item2)), m.LogLoss, 4);
    }

    // ───────────────────────── Model 6 gölge sözleşmeleri ─────────────────────────

    [Fact]
    public void Model6_AvrupaLigindeUygulanmaz_Havuz1X2Toplami1()
    {
        var d40 = ScoreDistribution.Poisson(1.5, 1.1);
        var dav = new Probs3(0.3, 0.3, 0.4);
        Assert.Same(d40, Model6Shadow.Distribution(d40, 3, dav)); // UEL → 4.0 aynen
        var dom = Model6Shadow.Distribution(d40, 39, dav);
        var p = Probs3.From(dom);
        Assert.True(p.IsValid);
        var expected = Model6Lab.Pool(Probs3.From(d40), new[] { (dav, Model6Shadow.DomesticWeight) });
        Assert.Equal(expected.H, p.H, 9); Assert.Equal(expected.D, p.D, 9);
        Assert.Equal(Model6Shadow.UefaWeight, Model6Shadow.WeightFor(2));
        Assert.Equal(0, Model6Shadow.WeightFor(3));
    }

    private sealed class Store
    {
        private readonly string _name = "m6-" + Guid.NewGuid().ToString("N");
        public FormaxDbContext Db() => new(new DbContextOptionsBuilder<FormaxDbContext>().UseInMemoryDatabase(_name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    }

    private static readonly DateTime K = new(2026, 10, 9, 19, 0, 0, DateTimeKind.Utc);

    private static ForwardPredictionLedger.Input In(int id, DateTime kickoff, bool with6 = true, double lh = 1.6, double la = 1.0)
    {
        var d40 = ScoreDistribution.Poisson(lh, la);
        return new(id, 39, kickoff, "snp", d40, ScoreDistribution.Poisson(lh + 0.2, la), 20, 20, 1,
            with6 ? Model6Shadow.Distribution(d40, 39, new Probs3(0.3, 0.3, 0.4)) : null);
    }

    [Fact]
    public async Task Model6Golge_KickoffSonrasiYazilmaz_DuplicateYok_SonradanEklenmez_RestarttaKorunur_OtomatikPuanlanir()
    {
        var st = new Store();
        using (var db = st.Db())
        {
            db.Matches.Add(new Match { Id = 1, LeagueId = 39, MatchDate = K, Status = MatchStatuses.NotStarted, HomeTeamId = 10, AwayTeamId = 11 });
            db.Matches.Add(new Match { Id = 3, LeagueId = 39, MatchDate = K, Status = MatchStatuses.NotStarted, HomeTeamId = 12, AwayTeamId = 13 });
            db.SaveChanges();
            Assert.Equal(13, await ForwardPredictionLedger.RecordAsync(db, new[] { In(1, K) }, K.AddHours(-3)));
            // Kickoff sonrası: kayıt yok.
            Assert.Equal(0, await ForwardPredictionLedger.RecordAsync(db, new[] { In(2, K.AddMinutes(-1)) }, K));
            // 4.0 daha önce Model 6'sız kilitlendiyse Model 6 sonradan eklenmez.
            Assert.Equal(12, await ForwardPredictionLedger.RecordAsync(db, new[] { In(3, K, with6: false) }, K.AddHours(-3)));
            Assert.Equal(0, await ForwardPredictionLedger.RecordAsync(db, new[] { In(3, K) }, K.AddHours(-2)));
        }
        using (var db = st.Db()) // restart
        {
            Assert.Equal(0, await ForwardPredictionLedger.RecordAsync(db, new[] { In(1, K, lh: 3.0) }, K.AddHours(-1)));
            var r6 = db.ForwardPredictionRecords.Where(r => r.ModelVersion == Model6Shadow.Version).ToList();
            Assert.Single(r6);
            Assert.Equal(MarketFamilies.MatchResult, r6[0].Market);
            Assert.True(r6[0].PredictionLockedAtUtc < r6[0].KickoffUtc);
            Assert.Equal(db.ForwardPredictionRecords.Count(), db.ForwardPredictionRecords.Select(r => new { r.MatchId, r.ModelVersion, r.Market }).Distinct().Count());
            var m = db.Matches.Single(x => x.Id == 1); m.Status = MatchStatuses.Finished; m.HomeScore = 0; m.AwayScore = 0; db.SaveChanges();
            await ForwardPredictionLedger.ScoreAsync(db, K.AddHours(3));
        }
        using (var db = st.Db())
        {
            var r6 = db.ForwardPredictionRecords.Single(r => r.ModelVersion == Model6Shadow.Version);
            Assert.Equal("X", r6.ActualOutcome); Assert.NotNull(r6.ScoredAtUtc); Assert.NotNull(r6.LogLoss);
            var p = JsonSerializer.Deserialize<Dictionary<string, double>>(r6.ProbabilitiesJson)!;
            Assert.Equal(Math.Round(-Math.Log(p["X"]), 6), r6.LogLoss!.Value, 6);
        }
    }

    [Fact]
    public void IleriyeDonukKapi_YetersizVerideBloklar()
    {
        var few = Enumerable.Range(0, 40).Select(i => new Model6Shadow.ForwardPair(i, 39, K.AddDays(i / 10), new Probs3(0.45, 0.27, 0.28), new Probs3(0.47, 0.26, 0.27), i % 3)).ToList();
        var g = Model6Shadow.ForwardGate(few);
        Assert.Equal("BLOCKED_INSUFFICIENT_FORWARD_DATA", g.Status);
        Assert.Contains(g.Reasons, r => r.StartsWith("INSUFFICIENT_MATCHES"));
        Assert.Equal("BLOCKED_INSUFFICIENT_FORWARD_DATA", Model6Shadow.ForwardGate(Array.Empty<Model6Shadow.ForwardPair>()).Status);
    }

    [Fact]
    public void DisIstekYok_KullaniciUclariModel6yiOkumaz()
    {
        foreach (var f in new[]
                 {
                     Source("Formax.Application", "Services", "Outcomes", "Model6Shadow.cs"),
                     Source("Formax.Application", "Services", "Outcomes", "Model6Lab.cs"),
                     Source("Formax.Infrastructure", "Outcomes", "ForwardPredictionLedger.cs"),
                     Source("Formax.API", "Controllers", "Admin", "AdminForwardShadowController.cs")
                 })
        {
            Assert.DoesNotContain("HttpClient", f); Assert.DoesNotContain("ApiFootball", f); Assert.DoesNotContain("ISportsDataProvider", f);
        }
        // Admin dışındaki hiçbir uç Model 6'yı ya da gölge defterini okumaz (eligibility dışı yüzde sızıntısı yapısal olarak 0).
        var root = Path.Combine(Root(), "Formax.API", "Controllers");
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var src = File.ReadAllText(file);
            Assert.DoesNotContain("Model6Shadow", src); Assert.DoesNotContain("DavidsonElo", src); Assert.DoesNotContain("ForwardPrediction", src);
        }
        // Snapshot oluşturucu (kullanıcıya giden yük) Model 6'yı bilmez.
        Assert.DoesNotContain("Model6", Source("Formax.Application", "Services", "Outcomes", "OutcomeSnapshotBuilder.cs"));
    }

    private static string Root()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir!;
    }

    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));
}
