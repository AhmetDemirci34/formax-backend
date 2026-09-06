using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Matches;
using Formax.Application.Services.Seasons;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// TAKIM ARAMASI — salt DB, sağlayıcıya sıfır istek.
///
/// GERÇEK API ÇAĞRISI YOK: depo EF InMemory, sezon çözücü sahte.
///
/// NEDEN AKSAN KATLAMASI ŞART: FormaxDB collation'ı <c>Turkish_CI_AS</c> — büyük/küçük
/// harf duyarsız ama AKSAN DUYARLI. "fenerbahce" yazan kullanıcı SQL LIKE ile
/// "Fenerbahçe"yi bulamazdı.
/// </summary>
public class TeamSearchTests
{
    private const int Fener = 3588, Lyon = 3589, Besiktas = 11;
    private static readonly DateTime Now = DateTime.UtcNow;

    /// <summary>Sezon çözücü — Temmuz kırılımı (projenin mevcut sözleşmesi).</summary>
    private sealed class FakeSeasonResolver : ILeagueSeasonResolver
    {
        public LeagueSeasonResolution Resolve(int leagueId, DateTime referenceUtc)
            => throw new NotSupportedException("arama bu yolu kullanmaz");
        public int SeasonYearOf(DateTime utc) => utc.Month >= 7 ? utc.Year : utc.Year - 1;
    }

    // AD ÇAKIŞMASI TUZAĞI: InMemory deposu ADLA paylaşılır. Aynı test adı başka bir
    // sınıfta da varsa (MatchResultsTests.AyniExternalFixtureId_IkiKezDonmez) iki test
    // AYNI depoyu kullanır ve birbirinin satırlarını görür. Sınıf öneki bunu keser.
    private static FormaxDbContext NewDb(string name)
        => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase("teamsearch-" + name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static MatchResultsReader Reader(FormaxDbContext db) => TestReaders.Results(db);

    private static Match M(int id, string ext, DateTime kickoff, string status,
        int home, int away, int leagueId = LockedCompetitions.SuperLig, string league = "Süper Lig")
        => new()
        {
            Id = id, ExternalMatchId = ext, MatchDate = kickoff, Status = status,
            LeagueId = leagueId, League = league,
            HomeTeamId = home, AwayTeamId = away, HomeScore = 1, AwayScore = 0
        };

    /// <summary>Mevcut sezonun içinde kalan, geçmiş ve gelecek maçlar.</summary>
    private static FormaxDbContext SeededDb(string name)
    {
        var db = NewDb(name);
        db.Teams.AddRange(
            new Team { Id = Fener, Name = "Fenerbahçe" },
            new Team { Id = Lyon, Name = "Lyon" },
            new Team { Id = Besiktas, Name = "Beşiktaş" });

        db.Matches.AddRange(
            // Geçmiş (bitmiş) — sezon içinde.
            M(1001, "fx-1001", Now.AddDays(-40), MatchStatuses.Finished, Fener, Besiktas),
            M(1002, "fx-1002", Now.AddDays(-5), MatchStatuses.Finished, Besiktas, Fener),
            // Gelecek (başlamamış) — sezon içinde.
            M(1003, "fx-1003", Now.AddDays(3), MatchStatuses.NotStarted, Fener, Lyon),
            M(1004, "fx-1004", Now.AddDays(20), MatchStatuses.NotStarted, Lyon, Fener),
            // Gösterilmemesi gerekenler.
            M(1005, "fx-1005", Now.AddDays(1), MatchStatuses.Live, Fener, Besiktas),
            M(1006, "fx-1006", Now.AddDays(2), MatchStatuses.Postponed, Fener, Lyon),
            M(1007, "fx-1007", Now.AddDays(4), MatchStatuses.Cancelled, Fener, Lyon),
            // Kapsam dışı lig ve LeagueId=0 artığı.
            M(1008, "fx-1008", Now.AddDays(5), MatchStatuses.NotStarted, Fener, Lyon, 94, "Primeira Liga"),
            M(1009, "fx-1009", Now.AddDays(6), MatchStatuses.NotStarted, Fener, Lyon, 0, ""));
        db.SaveChanges();
        return db;
    }

    // ── 2-3. KAPSAM: yaklaşan yalnız NotStarted, sonuç yalnız Finished ────────

    [Fact]
    public async Task YaklasanArama_YalnizNotStartedDondurur()
    {
        using var db = SeededDb(nameof(YaklasanArama_YalnizNotStartedDondurur));
        var r = await Reader(db).SearchByTeamAsync("Fenerbahçe", "upcoming");

        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.Equal(MatchStatuses.NotStarted, x.Status));
        // Canlı, ertelenmiş ve iptal maçlar GİRMEZ.
        Assert.DoesNotContain(r, x => x.MatchId is 1005 or 1006 or 1007);
        // Bitmiş maçlar da girmez.
        Assert.DoesNotContain(r, x => x.MatchId is 1001 or 1002);
    }

    [Fact]
    public async Task SonucArama_YalnizFinishedDondurur()
    {
        using var db = SeededDb(nameof(SonucArama_YalnizFinishedDondurur));
        var r = await Reader(db).SearchByTeamAsync("Fenerbahçe", "finished");

        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.Equal(MatchStatuses.Finished, x.Status));
        Assert.DoesNotContain(r, x => x.MatchId is 1003 or 1004 or 1005);
    }

    // ── 4-6. SEÇİLİ GÜNLE SINIRLI DEĞİL, SEZON TARANIR ───────────────────────

    [Fact]
    public async Task Arama_SeciliGunleSinirliDegil_SezonuTarar()
    {
        using var db = SeededDb(nameof(Arama_SeciliGunleSinirliDegil_SezonuTarar));
        var reader = Reader(db);

        // 40 gün önceki maç, 30 günlük gezinme penceresinin DIŞINDA — ama arama bulur.
        var finished = await reader.SearchByTeamAsync("Fenerbahçe", "finished");
        Assert.Contains(finished, x => x.MatchId == 1001);

        // 20 gün sonraki maç, dört günlük yaklaşan penceresinin DIŞINDA — ama arama bulur.
        var upcoming = await reader.SearchByTeamAsync("Fenerbahçe", "upcoming");
        Assert.Contains(upcoming, x => x.MatchId == 1004);
    }

    // ── 7-8. BÜYÜK/KÜÇÜK HARF VE TÜRKÇE KARAKTER ─────────────────────────────

    [Theory]
    [InlineData("Fenerbahçe")]
    [InlineData("fenerbahçe")]
    [InlineData("FENERBAHÇE")]
    [InlineData("Fenerbahce")]   // aksansız — collation CI_AS bunu bulamazdı
    [InlineData("fenerbahce")]
    [InlineData("FENER")]
    [InlineData("  fener  ")]    // baştaki/sondaki boşluk kırpılır
    public async Task BuyukKucukVeTurkceKarakter_FarkEtmez(string term)
    {
        using var db = SeededDb(nameof(BuyukKucukVeTurkceKarakter_FarkEtmez) + term);
        var r = await Reader(db).SearchByTeamAsync(term, "upcoming");
        Assert.NotEmpty(r);
    }

    [Fact]
    public async Task BesiktasGibiTurkceAdlar_AksansizYazimlaBulunur()
    {
        using var db = SeededDb(nameof(BesiktasGibiTurkceAdlar_AksansizYazimlaBulunur));
        var r = await Reader(db).SearchByTeamAsync("besiktas", "finished");
        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.True(
            x.HomeTeam.Name.Contains("Beşiktaş") || x.AwayTeam.Name.Contains("Beşiktaş")));
    }

    [Fact]
    public void BenzerAdlar_YanlisTakimaEslesmez()
    {
        // "Trabzon" yazan kullanıcıya "Konyaspor" GELMEZ: bulanık/benzerlik eşleşmesi yok.
        Assert.True(TeamSearchTerm.Matches("Trabzonspor", TeamSearchTerm.Normalize("trabzon")));
        Assert.False(TeamSearchTerm.Matches("Konyaspor", TeamSearchTerm.Normalize("trabzon")));
        Assert.False(TeamSearchTerm.Matches("Fenerbahçe", TeamSearchTerm.Normalize("galatasaray")));
    }

    // ── 9. İKİ KARAKTERDEN KISA SORGU DB'YE GİTMEZ ───────────────────────────

    [Fact]
    public async Task IkiKarakterdenKisaSorgu_BosDoner()
    {
        using var db = SeededDb(nameof(IkiKarakterdenKisaSorgu_BosDoner));
        var reader = Reader(db);

        Assert.Empty(await reader.SearchByTeamAsync("F", "upcoming"));
        Assert.Empty(await reader.SearchByTeamAsync(" ", "upcoming"));
        Assert.Empty(await reader.SearchByTeamAsync("", "finished"));
        Assert.False(TeamSearchTerm.IsSearchable("f"));
        Assert.True(TeamSearchTerm.IsSearchable("fe"));
    }

    // ── 11-12. KAPSAM DIŞI VE KOPYA SATIRLAR ─────────────────────────────────

    [Fact]
    public async Task LeagueIdSifirVeKapsamDisi_Donmez()
    {
        using var db = SeededDb(nameof(LeagueIdSifirVeKapsamDisi_Donmez));
        var r = await Reader(db).SearchByTeamAsync("Fenerbahçe", "upcoming");

        Assert.DoesNotContain(r, x => x.LeagueId == 0);
        Assert.DoesNotContain(r, x => x.MatchId is 1008 or 1009);
        Assert.All(r, x => Assert.Contains(x.LeagueId, LockedCompetitions.All));
    }

    [Fact]
    public async Task AyniExternalFixtureId_IkiKezDonmez()
    {
        using var db = SeededDb(nameof(AyniExternalFixtureId_IkiKezDonmez));
        // Aynı fikstür ikinci bir satıra düşmüş.
        db.Matches.Add(M(1103, "fx-1003", Now.AddDays(3), MatchStatuses.NotStarted, Fener, Lyon));
        db.SaveChanges();

        var r = await Reader(db).SearchByTeamAsync("Fenerbahçe", "upcoming");

        var ids = r.Where(x => x.ExternalFixtureId == "fx-1003").ToList();
        Assert.Single(ids);
        Assert.Equal(1003, ids[0].MatchId);   // en küçük MatchId → deterministik
    }

    // ── Sıralama ve tavan ────────────────────────────────────────────────────

    [Fact]
    public async Task Siralama_YaklasandaEnYakin_BitmisteEnYeniOnce()
    {
        using var db = SeededDb(nameof(Siralama_YaklasandaEnYakin_BitmisteEnYeniOnce));
        var reader = Reader(db);

        var upcoming = await reader.SearchByTeamAsync("Fenerbahçe", "upcoming");
        Assert.True(upcoming[0].MatchDateUtc <= upcoming[^1].MatchDateUtc);
        Assert.Equal(1003, upcoming[0].MatchId);   // 3 gün sonra, 20 günden önce

        var finished = await reader.SearchByTeamAsync("Fenerbahçe", "finished");
        Assert.True(finished[0].MatchDateUtc >= finished[^1].MatchDateUtc);
        Assert.Equal(1002, finished[0].MatchId);   // 5 gün önce, 40 günden yeni
    }

    [Fact]
    public async Task SonucTavani_YuzdenFazlaDonmez()
    {
        using var db = NewDb(nameof(SonucTavani_YuzdenFazlaDonmez));
        db.Teams.AddRange(new Team { Id = Fener, Name = "Fenerbahçe" }, new Team { Id = Lyon, Name = "Lyon" });
        for (var i = 0; i < 140; i++)
            db.Matches.Add(M(2000 + i, $"fx-b{i}", Now.AddDays(-1 - i % 20).AddMinutes(i),
                MatchStatuses.Finished, Fener, Lyon));
        db.SaveChanges();

        var r = await Reader(db).SearchByTeamAsync("Fenerbahçe", "finished");
        Assert.Equal(100, r.Count);
    }

    // ── 13. ARAMA SAĞLAYICIYA ÇIKMAZ ─────────────────────────────────────────

    [Fact]
    public void AramaOkumaYolu_SaglayiciBagimliligiTasimaz()
    {
        // Sözleşme testi: okuma yolunun kurucusunda HTTP istemcisi ya da sağlayıcı
        // arayüzü YOKTUR. Böyle bir bağımlılık eklenirse test kırılır.
        var ctor = typeof(MatchResultsReader).GetConstructors().Single();
        var types = ctor.GetParameters().Select(p => p.ParameterType.Name).ToList();

        Assert.Equal(new[] { "FormaxDbContext", "ILeagueSeasonResolver", "IMemoryCache" }, types);
        Assert.DoesNotContain(types, t => t.Contains("HttpClient", StringComparison.Ordinal));
        Assert.DoesNotContain(types, t => t.Contains("Provider", StringComparison.Ordinal));
    }

    // ── 1 + 14. ARAMA KUTUSU HER İKİ SEKMEDE, SONUÇ DOĞRU ROTAYA GİDER ───────

    [Fact]
    public void AramaKutusu_HerIkiSekmedeGorunur_VeDogruRotayaGider()
    {
        var page = ReadFile("formax-web/app/maclar/page.tsx");

        // Kutu sekme dalından BAĞIMSIZ render edilir → iki sekmede de görünür.
        Assert.Contains("<TeamSearchInput", page, StringComparison.Ordinal);
        var searchIdx = page.IndexOf("<TeamSearchInput", StringComparison.Ordinal);
        var branchIdx = page.IndexOf("{search.isActive ? (", StringComparison.Ordinal);
        Assert.True(searchIdx > 0 && branchIdx > searchIdx,
            "arama kutusu sekme/sonuç dalından ÖNCE ve koşulsuz render edilmeli");

        // Scope sekmeye göre belirlenir.
        Assert.Contains("upcoming ? \"upcoming\" : \"finished\"", page, StringComparison.Ordinal);

        // Sonuç kartı doğru rotaya gider.
        Assert.Contains("router.push(`/match/${matchId}`)", page, StringComparison.Ordinal);
        var results = ReadFile("formax-web/components/maclar/SearchResults.tsx");
        Assert.Contains("onOpen(m.matchId)", results, StringComparison.Ordinal);
        Assert.DoesNotContain("/ai", results, StringComparison.Ordinal);
    }

    [Fact]
    public void AramaKancasi_DebounceVeIptalKurallari()
    {
        var hook = ReadFile("formax-web/hooks/useTeamSearch.ts");

        Assert.Contains("useDebounce(query.trim(), 300)", hook, StringComparison.Ordinal);
        Assert.Contains("debouncedQuery.length < 2", hook, StringComparison.Ordinal);
        // Önceki istek yenisi geldiğinde İPTAL edilir.
        Assert.Contains("abortRef.current?.abort();", hook, StringComparison.Ordinal);
        Assert.Contains("return () => controller.abort();", hook, StringComparison.Ordinal);
        // Unmount/iptal sonrası state YAZILMAZ.
        Assert.Contains("if (!controller.signal.aborted)", hook, StringComparison.Ordinal);
    }

    private static string ReadFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"beklenen dosya yok: {relative}");
        return File.ReadAllText(path);
    }
}
