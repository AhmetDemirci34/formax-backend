using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Matches;
using Formax.Application.Services.Seasons;
using Formax.Application.Services.Standings;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// AI FORM KAPISI — ligin ilgisiz eksik sonucu, iki takımın gerçek formunu GİZLEYEMEZ.
///
/// ÖLÇÜLEN HATA (06.09.2026): Süper Lig'de sonucu kesinleşmemiş TEK maç
/// (Başakşehir–Galatasaray, 04.09) yüzünden Trabzonspor ve Gençlerbirliği'nin 4'er
/// maçlık TAM formu kapanıyor, yerine "Sezon verileri henüz tamamlanmadı
/// (1 lig maçı bekliyor)" teknik uyarısı basılıyordu. Aynı gün 11 kilitli ligin
/// 8'inde en az bir eksik sonuç vardı — yani hata neredeyse tüm maçlardaydı.
///
/// Bu testler gerçek internet KULLANMAZ; bellekte kurulan fikstürlerle çalışır.
/// </summary>
public class TeamFormSampleQualityTests
{
    private const int Trabzon = 10;
    private const int Genclerbirligi = 20;
    private const int Basaksehir = 30;
    private const int Galatasaray = 40;
    private const int SuperLig = 203;

    private static LeagueSeasonScope Scope() => new(
        LeagueId: SuperLig,
        SeasonYear: 2026,
        StartUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndUtc: new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        Source: "SeasonMetadata");

    /// <summary>Trabzonspor'un 4 tamamlanmış lig maçı — hepsi kesin sonuçlu.</summary>
    private static List<Match> TrabzonSettled() => new()
    {
        TestData.Finished(101, 2, 0, TestData.PastDue(24 * 20), SuperLig, Trabzon, 50),
        TestData.Finished(102, 1, 1, TestData.PastDue(24 * 14), SuperLig, 60, Trabzon),
        TestData.Finished(103, 0, 2, TestData.PastDue(24 * 8),  SuperLig, 70, Trabzon),
        TestData.Finished(104, 3, 1, TestData.PastDue(24 * 3),  SuperLig, Trabzon, 80)
    };

    /// <summary>Ligin TÜM fikstürleri: Trabzon'un 4 maçı + İLGİSİZ eksik sonuç.</summary>
    private static List<Match> LeagueFixturesWithUnrelatedGap()
    {
        var all = TrabzonSettled();
        // Sonucu kesinleşmemiş, iki takımla da İLGİSİZ maç (Başakşehir–Galatasaray).
        all.Add(TestData.Fixture(999, MatchStatuses.NotStarted, TestData.PastDue(48),
            leagueId: SuperLig, homeTeamId: Basaksehir, awayTeamId: Galatasaray));
        return all;
    }

    // ── 25. İlgisiz lig eksikliği takım formunu KAPATMAZ ────────────────────────
    [Fact]
    public void IlgisizLigEksikligi_TakimFormunuKapatmaz()
    {
        var league = LeagueFixturesWithUnrelatedGap();
        var completeness = SeasonDataCompleteness.Evaluate(league, TestData.Now);

        // Ön koşul: lig GERÇEKTEN eksik sayılıyor (kök nedenin kendisi).
        Assert.False(completeness.IsComplete);
        Assert.Equal(1, completeness.Missing);

        var teamMissing = TeamFormSampleQuality.MissingResultMatchIdsFor(
            Trabzon, league, TestData.Now);

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now,
            TrabzonSettled(), completeness, teamMissing);

        // Eksik maç bu takıma ait DEĞİL → sınırlama YOK, değerlendirme AÇIK.
        Assert.Empty(teamMissing);
        Assert.Equal(0, dto.TeamMissingResultCount);
        Assert.True(dto.AllowsGeneralization);
        Assert.Equal(4, dto.Played);
        Assert.Equal(TeamFormSampleQuality.Limited, dto.SampleQuality);
        Assert.Equal(string.Empty, dto.LimitationNote);
    }

    // ── 26. Takımı ilgilendiren eksik sonuç SINIRLAMA üretir ────────────────────
    [Fact]
    public void TakimiIlgilendirenEksikSonuc_SinirlamaUretir()
    {
        var league = TrabzonSettled();
        // Bu kez eksik sonuç Trabzonspor'un KENDİ maçı.
        league.Add(TestData.Fixture(998, MatchStatuses.NotStarted, TestData.PastDue(30),
            leagueId: SuperLig, homeTeamId: Trabzon, awayTeamId: Galatasaray));

        var teamMissing = TeamFormSampleQuality.MissingResultMatchIdsFor(
            Trabzon, league, TestData.Now);

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now,
            TrabzonSettled(), SeasonDataCompleteness.Evaluate(league, TestData.Now), teamMissing);

        Assert.Single(teamMissing);
        Assert.Equal(998, teamMissing[0]);
        Assert.Equal(1, dto.TeamMissingResultCount);
        Assert.Contains("henüz doğrulanmadığı için", dto.LimitationNote);
        // Sınırlama KAPATMA değildir: mevcut kesinleşmiş maçlarla değerlendirme sürer.
        Assert.True(dto.AllowsGeneralization);
    }

    [Fact]
    public void ErtelenenMac_TakimiEtkileyenEksiklikSayilmaz()
    {
        var league = TrabzonSettled();
        league.Add(TestData.Fixture(997, MatchStatuses.Postponed, TestData.PastDue(30),
            leagueId: SuperLig, homeTeamId: Trabzon, awayTeamId: Galatasaray));

        // Ertelenen maç oynanmamıştır; sonucu BEKLENMEZ.
        Assert.Empty(TeamFormSampleQuality.MissingResultMatchIdsFor(Trabzon, league, TestData.Now));
    }

    // ── 27. 0 maçta GENELLEME YOK ───────────────────────────────────────────────
    [Fact]
    public void SifirMac_GenellemeYok()
    {
        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now,
            new List<Match>(), null, null);

        Assert.Equal(0, dto.Played);
        Assert.Equal(TeamFormSampleQuality.None, dto.SampleQuality);
        Assert.False(dto.AllowsGeneralization);
        Assert.Contains("tamamlanmış lig maçı bulunmuyor", dto.Sentence);
        AssertNoGeneralization(dto.Sentence);
    }

    // ── 28. 1–2 maçta YALNIZ gerçek sayı ────────────────────────────────────────
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BirVeyaIkiMac_YalnizGercekSayi(int count)
    {
        var matches = TrabzonSettled().Take(count).ToList();

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now, matches, null, null);

        Assert.Equal(count, dto.Played);
        Assert.Equal(TeamFormSampleQuality.Minimal, dto.SampleQuality);
        Assert.False(dto.AllowsGeneralization);
        Assert.Contains($"tamamlanan {count} lig maçında", dto.Sentence);
        Assert.DoesNotContain("son 5", dto.Sentence);
        AssertNoGeneralization(dto.Sentence);
    }

    [Fact]
    public void IkiMac_GercekSayilariSoyler()
    {
        // 1 galibiyet (2-0) + 1 beraberlik (1-1) → spec örneğinin birebir karşılığı.
        var matches = TrabzonSettled().Take(2).ToList();

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now, matches, null, null);

        Assert.Equal(1, dto.Won);
        Assert.Equal(1, dto.Drawn);
        Assert.Equal(0, dto.Lost);
        Assert.Equal(
            "Trabzonspor bu sezon tamamlanan 2 lig maçında 1 galibiyet ve 1 beraberlik aldı.",
            dto.Sentence);
    }

    // ── 29. 3–4 maçta KÜÇÜK ÖRNEKLEM dili ───────────────────────────────────────
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void UcVeyaDortMac_KucukOrneklemDili(int count)
    {
        var matches = TrabzonSettled().Take(count).ToList();

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now, matches, null, null);

        Assert.Equal(TeamFormSampleQuality.Limited, dto.SampleQuality);
        Assert.True(dto.AllowsGeneralization);
        Assert.Contains("sınırlı örneklemde", dto.Sentence);
        Assert.Contains($"{count} lig maçlık", dto.Sentence);
        Assert.DoesNotContain("son 5", dto.Sentence);
        AssertNoGeneralization(dto.Sentence);
    }

    /// <summary>
    /// AD BİÇİMİNDEN BAĞIMSIZ DOĞRU DİLBİLGİSİ — ilgi hâli eki KULLANILMAZ.
    ///
    /// ÖLÇÜLDÜ (06.09.2026, gerçek /detail çıktısı): kalıp önce "Trabzonspor'un …"
    /// biçimindeydi ve ek harf kuralıyla üretiliyordu. Türkçe adlarda doğruydu, ama
    /// depo Türkçe adlardan ibaret değil — üç ayrı yanlış çıktı ölçüldü:
    ///   "Gençlerbirliği S.K.'in", "Manchester City'in", "Manchester United'in".
    /// Yabancı adlarda ek YAZILIŞA değil OKUNUŞA bağlı olduğu için harf temelli hiçbir
    /// kural güvenilir değildir; ek gerektirmeyen tek kalıba geçildi.
    /// </summary>
    [Theory]
    [InlineData("Trabzonspor")]
    [InlineData("Gençlerbirliği S.K.")]
    [InlineData("Manchester City")]
    [InlineData("Manchester United")]
    [InlineData("Çorum FK")]
    [InlineData("Fenerbahçe")]
    public void HerAdBicimi_EksizKalipKullanir(string teamName)
    {
        var dto = TeamSeasonFormService.Build(
            Trabzon, teamName, "Süper Lig", Scope(), TestData.Now,
            TrabzonSettled().Take(3).ToList(), null, null);

        // Cümle adla BAŞLAR ve hemen ardından ek DEĞİL, boşluk gelir.
        Assert.StartsWith(teamName + " bu sezon tamamlanan 3 lig maçlık sınırlı örneklemde", dto.Sentence);

        // Hiçbir ilgi hâli eki üretilmez.
        foreach (var wrong in new[] { "'in ", "'ın ", "'un ", "'ün ", "'nin ", "'nın ", "'nun ", "'nün " })
            Assert.DoesNotContain(wrong, dto.Sentence);

        AssertNoGeneralization(dto.Sentence);
    }

    // ── 30. 5 maçta SON 5 değerlendirmesi ───────────────────────────────────────
    [Fact]
    public void BesMac_Son5Degerlendirmesi()
    {
        var matches = TrabzonSettled();
        matches.Add(TestData.Finished(105, 1, 0, TestData.PastDue(24 * 26), SuperLig, Trabzon, 90));

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now, matches, null, null);

        Assert.Equal(5, dto.Played);
        Assert.Equal(TeamFormSampleQuality.Sufficient, dto.SampleQuality);
        Assert.True(dto.AllowsLastFivePhrase);
        Assert.Contains("son 5 maçında", dto.Sentence);
    }

    // ── 31. Önceki sezon / cup / friendly / live / future DAHİL EDİLMEZ ─────────
    [Fact]
    public void KapsamDisiMaclar_FormaGirmez()
    {
        // TeamSeasonFormService yalnız kendisine VERİLEN settled kümesini sayar; kapsam
        // süzgeci çağıran taraftadır. Burada süzgecin kuralı doğrulanır: aynı sezon +
        // aynı lig + Finished + kickoff'tan önce.
        var scope = Scope();
        var kickoff = TestData.Now;

        var all = new List<Match>
        {
            TestData.Finished(201, 1, 0, TestData.PastDue(24 * 5), SuperLig, Trabzon, 50),   // ✓
            TestData.Finished(202, 1, 0, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                SuperLig, Trabzon, 50),                                                      // ✗ önceki sezon
            TestData.Finished(203, 1, 0, TestData.PastDue(24 * 4), 206, Trabzon, 50),        // ✗ kupa (başka lig)
            TestData.Fixture(204, MatchStatuses.Live, TestData.PastDue(1), 1, 0,
                SuperLig, Trabzon, 50),                                                      // ✗ canlı
            TestData.Fixture(205, MatchStatuses.NotStarted, TestData.Now.AddDays(3), 0, 0,
                SuperLig, Trabzon, 50)                                                       // ✗ gelecek
        };

        var eligible = all
            .Where(m => m.LeagueId == scope.LeagueId)
            .Where(m => m.MatchDate >= scope.StartUtc && m.MatchDate < scope.EndUtc)
            .Where(m => m.MatchDate < kickoff)
            .Where(m => string.Equals(m.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(eligible);
        Assert.Equal(201, eligible[0].Id);

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", scope, kickoff, eligible, null, null);

        Assert.Equal(1, dto.Played);
        Assert.Equal(new[] { 201 }, dto.MatchIds);
    }

    // ── 32. Teknik completeness mesajı KULLANICI METNİNDE görünmez ──────────────
    [Fact]
    public void TeknikCompletenessMesaji_KullaniciMetnindeGorunmez()
    {
        var league = LeagueFixturesWithUnrelatedGap();
        var completeness = SeasonDataCompleteness.Evaluate(league, TestData.Now);
        var teamMissing = TeamFormSampleQuality.MissingResultMatchIdsFor(Trabzon, league, TestData.Now);

        var dto = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now,
            TrabzonSettled(), completeness, teamMissing);

        foreach (var forbidden in new[]
                 {
                     "lig maçı bekliyor", "IsComplete", "AllowsGeneralization",
                     "SeasonDataCompleteness", "settle window", "Sezon verileri",
                     "veritabanında"
                 })
        {
            Assert.DoesNotContain(forbidden, dto.Sentence, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, dto.LimitationNote, StringComparison.OrdinalIgnoreCase);
        }

        // Teşhis alanları DTO'da DURUR (silinmez) — yalnız kullanıcı metnine girmez.
        Assert.False(dto.IsSeasonDataComplete);
        Assert.Equal(1, dto.SeasonMissingFixtures);
    }

    // ── 33. LLM W/D/L sayılarını DEĞİŞTİREMEZ ───────────────────────────────────
    [Fact]
    public void FormSayilari_BackendDeterministik_LlmYok()
    {
        var matches = TrabzonSettled();

        var a = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now, matches, null, null);
        var b = TeamSeasonFormService.Build(
            Trabzon, "Trabzonspor", "Süper Lig", Scope(), TestData.Now, matches, null, null);

        // Aynı girdi → AYNI çıktı. Metin de sayı da modelden değil, bu hesaptan gelir.
        Assert.Equal(a.Sentence, b.Sentence);
        Assert.Equal(a.Won, b.Won);
        Assert.Equal(a.Drawn, b.Drawn);
        Assert.Equal(a.Lost, b.Lost);

        // 2-0 G · 1-1 B · 0-2 G(deplasman) · 3-1 G → 3 galibiyet 1 beraberlik
        Assert.Equal(3, a.Won);
        Assert.Equal(1, a.Drawn);
        Assert.Equal(0, a.Lost);
        Assert.Equal(8, a.GoalsFor);
        Assert.Equal(2, a.GoalsAgainst);
    }

    /// <summary>Desteklenmeyen üstünlük/genelleme sözcükleri metinde bulunmamalı.</summary>
    private static void AssertNoGeneralization(string sentence)
    {
        foreach (var word in new[] { "formda", "düşüşte", "favori", "üstün", "momentum" })
            Assert.DoesNotContain(word, sentence, StringComparison.OrdinalIgnoreCase);
    }
}
