using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.PostMatch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// MAÇ SONRASI VİDEO — kimlik, embed izni, tekilleştirme ve tekrar defteri.
///
/// GERÇEK İNTERNET ÇAĞRISI YOKTUR: embed doğrulayıcı sahtelenir, keşif sağlayıcısı
/// stub'tır, depo EF InMemory'dir. Testlerin hiçbiri YouTube'a, UEFA'ya veya arama
/// motoruna çıkmaz.
///
/// Veriler depodaki GERÇEK Fenerbahçe–Lyon Şampiyonlar Ligi play-off'undan alınmıştır:
///   MatchId 71513 · 18.08.2026 19:00 UTC · Fenerbahçe (ev) 1-1 Lyon   · fikstür 1622621
///   MatchId 104237 · 26.08.2026 19:00 UTC · Lyon (ev) 1-2 Fenerbahçe  · fikstür 1622630
/// </summary>
public class PostMatchVideoTests
{
    private const int Leg1Id = 71513, Leg2Id = 104237;
    private const int Fener = 3588, Lyon = 3589;
    private static readonly DateTime Leg1 = new(2026, 8, 18, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Leg2 = new(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc);

    // GERÇEK, doğrulanmış kanal kimlikleri (02.09.2026'da kanalların canonical bağlantısından).
    private const string TrtSpor = "UCfYNqluOf8EbQkL44otydMw";
    private const string FenerbahceTv = "UCgqlho3-8a6FmDqQm7Q6gJw";

    // ── Kurulum ───────────────────────────────────────────────────────────────

    private static FormaxDbContext NewDb(string name)
        => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static FormaxDbContext SeededDb(string name)
    {
        var db = NewDb(name);
        db.Teams.AddRange(
            new Team { Id = Fener, Name = "Fenerbahçe" },
            new Team { Id = Lyon, Name = "Lyon" });
        db.Matches.AddRange(
            new Match
            {
                Id = Leg1Id, ExternalMatchId = "1622621", MatchDate = Leg1,
                Status = MatchStatuses.Finished, LeagueId = LockedCompetitions.ChampionsLeague,
                HomeTeamId = Fener, AwayTeamId = Lyon, HomeScore = 1, AwayScore = 1
            },
            new Match
            {
                Id = Leg2Id, ExternalMatchId = "1622630", MatchDate = Leg2,
                Status = MatchStatuses.Finished, LeagueId = LockedCompetitions.ChampionsLeague,
                HomeTeamId = Lyon, AwayTeamId = Fener, HomeScore = 1, AwayScore = 2
            });
        db.SaveChanges();
        return db;
    }

    /// <summary>Sahte embed doğrulayıcı — hiçbir dış istek yapmaz.</summary>
    private sealed class FakeEmbedVerifier : IVideoEmbedVerifier
    {
        private readonly bool _embeddable;
        public int Calls { get; private set; }
        public FakeEmbedVerifier(bool embeddable) => _embeddable = embeddable;

        public Task<EmbedVerification> VerifyAsync(
            OfficialVideoCandidate c, OfficialVideoSource s, CancellationToken ct = default)
        {
            Calls++;
            // Kaynak politikası "gömmeye kapalı" diyorsa gerçek doğrulayıcı da istek atmaz.
            if (!s.AllowsInAppEmbed)
                return Task.FromResult(new EmbedVerification(false, null, null, s.Why));
            return Task.FromResult(_embeddable
                ? new EmbedVerification(true, $"https://www.youtube-nocookie.com/embed/{c.ExternalVideoId}",
                    "https://i.ytimg.com/vi/x/hqdefault.jpg", "test: gömmeye açık")
                : new EmbedVerification(false, null, null, "test: yayıncı gömmeyi kapatmış"));
        }
    }

    private static MatchVideoRegistrar Registrar(FormaxDbContext db, IVideoEmbedVerifier verifier)
        => new(db, verifier, NullLogger<MatchVideoRegistrar>.Instance);

    /// <summary>TRT SPOR'un GERÇEK 1. ayak özeti (videoId, başlık, yayın anı ölçüldü).</summary>
    private static OfficialVideoCandidate Leg1Highlights() => new(
        Platform: "YouTube", SourceIdentifier: TrtSpor, ExternalVideoId: "YYZYPAkPKs0",
        Title: "Şampiyonlar Ligi Play Off 1. Maç | Fenerbahçe - Lyon (Özet) x Petrol Ofisi",
        Description: "Şampiyonlar Ligi Play Off Turu ilk maçında Fenerbahçe; Kadıköy'de Lyon'u konuk etti.",
        PublishedUtc: new DateTime(2026, 8, 18, 21, 23, 26, DateTimeKind.Utc),
        SourcePageUrl: "https://www.youtube.com/watch?v=YYZYPAkPKs0",
        ThumbnailUrl: null, DurationSeconds: 494);

    /// <summary>TRT SPOR'un GERÇEK 2. ayak özeti.</summary>
    private static OfficialVideoCandidate Leg2Highlights() => new(
        Platform: "YouTube", SourceIdentifier: TrtSpor, ExternalVideoId: "sz4AJjiol84",
        Title: "Şampiyonlar Ligi Play Off 2. Maç | Lyon - Fenerbahçe (Özet) X Petrol Ofisi",
        Description: "Fenerbahçe, UEFA Şampiyonlar Ligi play-off turunda Lyon'a konuk oldu.",
        PublishedUtc: new DateTime(2026, 8, 26, 21, 25, 30, DateTimeKind.Utc),
        SourcePageUrl: "https://www.youtube.com/watch?v=sz4AJjiol84",
        ThumbnailUrl: null, DurationSeconds: 528);

    // ── 3. Resmî + embed izinli video OYNATICI AÇAR ───────────────────────────

    [Fact]
    public async Task ResmiVeEmbedIzinliVideo_PlayerAcar()
    {
        using var db = SeededDb(nameof(ResmiVeEmbedIzinliVideo_PlayerAcar));
        var result = await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, Leg1Highlights());

        Assert.True(result.Stored);
        Assert.Equal(MatchVideoVerificationStatuses.Verified, result.Status);

        var row = db.MatchVideos.Single();
        Assert.True(row.CanPlayInApp);
        Assert.Equal("https://www.youtube-nocookie.com/embed/YYZYPAkPKs0", row.EmbedUrl);
        Assert.Equal(MatchVideoTypes.MatchHighlights, row.VideoType);
        Assert.Equal("TRT SPOR", row.OfficialPublisher);
        // Kimlik kanıtları kayda DONDURULUR: hangi fikstür, hangi yön.
        Assert.Equal("1622621", row.ExternalFixtureId);
        Assert.Equal(Fener, row.HomeTeamId);
        Assert.Equal(Lyon, row.AwayTeamId);

        // Okuma yolu oynatılabilir adresi taşır.
        var dto = new MatchVideoReader(db).GetVideos(Leg1Id).Single();
        Assert.True(dto.CanPlayInApp);
        Assert.False(string.IsNullOrWhiteSpace(dto.EmbedUrl));
    }

    // ── 4. Resmî ama embed YASAK video PLAYER AÇMAZ ───────────────────────────

    [Fact]
    public async Task ResmiFakatEmbedYasakVideo_PlayerAcmaz()
    {
        using var db = SeededDb(nameof(ResmiFakatEmbedYasakVideo_PlayerAcmaz));
        var result = await Registrar(db, new FakeEmbedVerifier(false)).RegisterAsync(Leg1Id, Leg1Highlights());

        Assert.True(result.Stored);                       // kayıt saklanır (kaynak gerçek)
        Assert.Equal(MatchVideoVerificationStatuses.EmbedBlocked, result.Status);

        var row = db.MatchVideos.Single();
        Assert.False(row.CanPlayInApp);
        Assert.Null(row.EmbedUrl);                        // gömme adresi YAZILMAZ

        // Ekrana giden DTO'da da adres YOKTUR: "belki denerim" ihtimali bırakılmaz.
        var dto = new MatchVideoReader(db).GetVideos(Leg1Id).Single();
        Assert.False(dto.CanPlayInApp);
        Assert.Null(dto.EmbedUrl);
    }

    /// <summary>
    /// UEFA'nın resmî özeti gerçektir ama uefa.com gömmeyi CSP ile reddeder. İzin
    /// listesi bunu bildiği için doğrulayıcıya HİÇ gidilmez ve kayıt oynatılmaz.
    /// </summary>
    [Fact]
    public async Task GommeyeKapaliKaynak_DogrulayiciyaCikmadanEngellenir()
    {
        using var db = SeededDb(nameof(GommeyeKapaliKaynak_DogrulayiciyaCikmadanEngellenir));
        var verifier = new FakeEmbedVerifier(true);

        var uefa = new OfficialVideoCandidate(
            Platform: "Web", SourceIdentifier: OfficialVideoSources.UefaWeb,
            ExternalVideoId: "02a8-216161421805-43e8554af1cf-1000",
            Title: "Champions League play-off first leg highlights: Fenerbahçe 1-1 Lyon",
            Description: null,
            PublishedUtc: new DateTime(2026, 8, 18, 21, 30, 0, DateTimeKind.Utc),
            SourcePageUrl: "https://www.uefa.com/uefachampionsleague/video/highlights/02a8-216161421805-43e8554af1cf-1000--x/",
            ThumbnailUrl: null, DurationSeconds: null);

        var result = await Registrar(db, verifier).RegisterAsync(Leg1Id, uefa);

        Assert.True(result.Stored);
        Assert.Equal(MatchVideoVerificationStatuses.EmbedBlocked, result.Status);
        Assert.False(db.MatchVideos.Single().CanPlayInApp);
    }

    // ── 5. Resmî OLMAYAN video REDDEDİLİR ─────────────────────────────────────

    [Fact]
    public async Task ResmiOlmayanKanal_Reddedilir()
    {
        using var db = SeededDb(nameof(ResmiOlmayanKanal_Reddedilir));
        var verifier = new FakeEmbedVerifier(true);

        // Başlık kusursuz, süre gerçekçi, hatta "official" yazıyor — ama kanal listede yok.
        var pirate = Leg1Highlights() with
        {
            SourceIdentifier = "UCzzzzzzzzzzzzzzzzzzzzzz",
            ExternalVideoId = "pirate123",
            Title = "Fenerbahçe - Lyon 1-1 OFFICIAL Maç Özeti HD"
        };

        var result = await Registrar(db, verifier).RegisterAsync(Leg1Id, pirate);

        Assert.False(result.Stored);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains("izin listesinde değil", result.Reason);
        Assert.Empty(db.MatchVideos);
        Assert.Equal(0, verifier.Calls);   // doğrulanmamış adaya embed sorgusu ATILMAZ
    }

    // ── 6. Aynı video TEKRAR KAYDEDİLMEZ ──────────────────────────────────────

    [Fact]
    public async Task AyniVideo_IkinciKezKaydedilmez()
    {
        using var db = SeededDb(nameof(AyniVideo_IkinciKezKaydedilmez));
        var registrar = Registrar(db, new FakeEmbedVerifier(true));

        Assert.True((await registrar.RegisterAsync(Leg1Id, Leg1Highlights())).Stored);

        var second = await registrar.RegisterAsync(Leg1Id, Leg1Highlights());
        Assert.False(second.Stored);
        Assert.Equal("Duplicate", second.Status);
        Assert.Single(db.MatchVideos);
    }

    [Fact]
    public async Task AyniVideo_FarkliPaylasimAdresiyle_TekrarKaydedilmez()
    {
        using var db = SeededDb(nameof(AyniVideo_FarkliPaylasimAdresiyle_TekrarKaydedilmez));
        var registrar = Registrar(db, new FakeEmbedVerifier(true));
        await registrar.RegisterAsync(Leg1Id, Leg1Highlights());

        // Aynı video, izleme parametreli paylaşım linkiyle ve farklı kimlikle gelirse
        // kanonik adres tekilleştirmeyi yine yakalar.
        var again = Leg1Highlights() with
        {
            ExternalVideoId = "YYZYPAkPKs0-mirror",
            SourcePageUrl = "https://www.youtube.com/watch?v=YYZYPAkPKs0"
        };

        var result = await registrar.RegisterAsync(Leg1Id, again);
        Assert.False(result.Stored);
        Assert.Equal("Duplicate", result.Status);
        Assert.Single(db.MatchVideos);
    }

    // ── 7-8. AYAK SIZINTISI YOK ───────────────────────────────────────────────

    [Fact]
    public async Task BirinciAyakVideosu_IkinciAyagaBaglanmaz()
    {
        using var db = SeededDb(nameof(BirinciAyakVideosu_IkinciAyagaBaglanmaz));
        var registrar = Registrar(db, new FakeEmbedVerifier(true));

        // 18.08 21:23 — 2. ayak (26.08) daha OYNANMAMIŞ. Bu video ona ait olamaz.
        var result = await registrar.RegisterAsync(Leg2Id, Leg1Highlights());

        Assert.False(result.Stored);
        Assert.Equal("Rejected", result.Status);
        Assert.Empty(db.MatchVideos);

        // Doğru ayağa ise sorunsuz bağlanır.
        Assert.True((await registrar.RegisterAsync(Leg1Id, Leg1Highlights())).Stored);
        Assert.Empty(new MatchVideoReader(db).GetVideos(Leg2Id));
        Assert.Single(new MatchVideoReader(db).GetVideos(Leg1Id));
    }

    [Fact]
    public async Task IkinciAyakVideosu_BirinciAyagaBaglanmaz()
    {
        using var db = SeededDb(nameof(IkinciAyakVideosu_BirinciAyagaBaglanmaz));
        var registrar = Registrar(db, new FakeEmbedVerifier(true));

        // 26.08 21:25 — 1. ayaktan da sonra, ama 2. ayağa çok daha yakın.
        var result = await registrar.RegisterAsync(Leg1Id, Leg2Highlights());

        Assert.False(result.Stored);
        Assert.Empty(db.MatchVideos);

        Assert.True((await registrar.RegisterAsync(Leg2Id, Leg2Highlights())).Stored);
        Assert.Empty(new MatchVideoReader(db).GetVideos(Leg1Id));
        Assert.Single(new MatchVideoReader(db).GetVideos(Leg2Id));
    }

    // ── 9. TARİH VE YÖN EŞLEŞMEDEN KAYIT YOK ──────────────────────────────────

    [Fact]
    public async Task MacBitmedenYayimlanmis_KabulEdilmez()
    {
        using var db = SeededDb(nameof(MacBitmedenYayimlanmis_KabulEdilmez));
        // Kickoff 19:00; 19:16'da yayımlanan içerik maç özeti olamaz.
        var early = Leg1Highlights() with { PublishedUtc = new DateTime(2026, 8, 18, 19, 16, 0, DateTimeKind.Utc) };

        var result = await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, early);

        Assert.False(result.Stored);
        Assert.Contains("maç bitmeden", result.Reason);
        Assert.Empty(db.MatchVideos);
    }

    [Fact]
    public async Task EvDeplasmanYonuTers_KabulEdilmez()
    {
        using var db = SeededDb(nameof(EvDeplasmanYonuTers_KabulEdilmez));

        // 1. ayakta ev sahibi Fenerbahçe'dir. "Lyon - Fenerbahçe" sırası TERSTİR:
        // başlık 2. ayağın maçını tarif ediyor, zaman ise 1. ayağınkine denk geliyor.
        var reversed = Leg1Highlights() with
        {
            ExternalVideoId = "reversed1",
            Title = "Şampiyonlar Ligi Play Off | Lyon - Fenerbahçe (Özet)",
            Description = null,
            SourcePageUrl = "https://www.youtube.com/watch?v=reversed1"
        };

        var result = await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, reversed);

        Assert.False(result.Stored);
        Assert.Contains("yön", result.Reason);
        Assert.Empty(db.MatchVideos);
    }

    [Fact]
    public void YonOkuma_SiralamayiDogruCozer()
    {
        Assert.Equal(MatchVideoIdentityValidator.Direction.Match,
            MatchVideoIdentityValidator.ReadDirection("fenerbahce - lyon (ozet)", "Fenerbahçe", "Lyon"));
        Assert.Equal(MatchVideoIdentityValidator.Direction.Reversed,
            MatchVideoIdentityValidator.ReadDirection("lyon - fenerbahce (ozet)", "Fenerbahçe", "Lyon"));
        // Ayraç yoksa yön İDDİA EDİLMEMİŞTİR — "ters" sayılıp doğru video elenmez.
        Assert.Equal(MatchVideoIdentityValidator.Direction.NotAsserted,
            MatchVideoIdentityValidator.ReadDirection(
                "fenerbahce deplasmanda lyon karsisinda kazandi", "Lyon", "Fenerbahçe"));
    }

    /// <summary>
    /// Basın toplantısı resmî kanaldadır, doğru maçındır, doğru zamandadır — ama maç
    /// GÖRÜNTÜSÜ değildir. Ürün kararı gereği bu ekrana girmez.
    /// </summary>
    [Fact]
    public async Task BasinToplantisi_MacGoruntusuSayilmaz()
    {
        using var db = SeededDb(nameof(BasinToplantisi_MacGoruntusuSayilmaz));
        var presser = new OfficialVideoCandidate(
            Platform: "YouTube", SourceIdentifier: FenerbahceTv, ExternalVideoId: "93ORyb_hCqU",
            Title: "Teknik Direktörümüz İsmail Kartal'ın Maç Sonu Basın Toplantısı | Fenerbahçe 1-1 Olympique Lyon",
            Description: null,
            PublishedUtc: new DateTime(2026, 8, 18, 22, 30, 0, DateTimeKind.Utc),
            SourcePageUrl: "https://www.youtube.com/watch?v=93ORyb_hCqU",
            ThumbnailUrl: null, DurationSeconds: 181);

        var result = await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, presser);

        Assert.False(result.Stored);
        Assert.Empty(db.MatchVideos);
    }

    // ── 10. SAYFA TIKLAMASI DIŞ İSTEK ÜRETMEZ ─────────────────────────────────

    [Fact]
    public async Task OkumaYolu_HicbirDisBagimliligaSahipDegil()
    {
        // Sözleşme testi: okuma yolunun kurucusunda HTTP, keşif sağlayıcısı veya embed
        // doğrulayıcısı YOKTUR. Böyle bir bağımlılık eklenirse test kırılır — yani
        // "maç detayına tıklamak dış istek üretmesin" kuralı koda çivilenmiştir.
        var ctor = typeof(MatchVideoReader).GetConstructors().Single();
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Single(paramTypes);
        Assert.Equal(typeof(FormaxDbContext), paramTypes[0]);
        Assert.DoesNotContain(paramTypes, t =>
            typeof(IOfficialMatchVideoProvider).IsAssignableFrom(t)
            || typeof(IVideoEmbedVerifier).IsAssignableFrom(t)
            || t.Name.Contains("HttpClient", StringComparison.Ordinal));

        // Ve gerçekten çalışır: kayıt DB'den okunur.
        using var db = SeededDb(nameof(OkumaYolu_HicbirDisBagimliligaSahipDegil));
        await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, Leg1Highlights());
        Assert.Single(new MatchVideoReader(db).GetVideos(Leg1Id));
    }

    // ── 11. TEKRAR DEFTERİ RESTART SONRASI KORUNUR ────────────────────────────

    [Fact]
    public void TekrarTakvimi_UcDenemeSonrasiDurur()
    {
        var end = MatchVideoIdentityValidator.EndOf(Leg1);

        // Maç biter bitmez bakılmaz; ilk bakış 75 dk sonradır.
        Assert.False(PostMatchEnrichmentJob.IsDue(end, 0, null, end.AddMinutes(30)));
        Assert.True(PostMatchEnrichmentJob.IsDue(end, 0, null, end.AddMinutes(80)));

        // 1. deneme sonrası 6 saat, 2. deneme sonrası 24 saat beklenir.
        var first = end.AddMinutes(80);
        Assert.False(PostMatchEnrichmentJob.IsDue(end, 1, first, first.AddHours(3)));
        Assert.True(PostMatchEnrichmentJob.IsDue(end, 1, first, first.AddHours(6)));

        var second = first.AddHours(6);
        Assert.False(PostMatchEnrichmentJob.IsDue(end, 2, second, second.AddHours(12)));
        Assert.True(PostMatchEnrichmentJob.IsDue(end, 2, second, second.AddHours(24)));

        // Hak bittikten sonra bir daha ASLA denenmez — sonsuz yoklama yok.
        var third = second.AddHours(24);
        Assert.False(PostMatchEnrichmentJob.IsDue(end, 3, third, third.AddDays(30)));
        Assert.Equal(3, PostMatchEnrichmentJob.MaxAttempts);
    }

    [Fact]
    public void TekrarDefteri_RestartSonrasiKorunur()
    {
        var dbName = nameof(TekrarDefteri_RestartSonrasiKorunur);
        var end = MatchVideoIdentityValidator.EndOf(Leg1);
        var attemptAt = end.AddMinutes(80);

        // 1) "İlk açılış": iki deneme deftere yazılır.
        using (var db = NewDb(dbName))
        {
            db.FixtureRefreshAttempts.AddRange(
                new FixtureRefreshAttempt
                {
                    ExternalMatchId = "1622621", Purpose = FixtureRefreshPurposes.PostMatchVideo,
                    DayUtc = attemptAt.Date, AttemptCount = 1,
                    LastAttemptUtc = attemptAt, LastOutcome = "NoData"
                },
                // İkinci deneme ERTESİ GÜNE düşer: gün anahtarlı sayaç tek başına
                // toplamı vermez, bu yüzden okuma günler ÜSTÜNDEN toplanır.
                new FixtureRefreshAttempt
                {
                    ExternalMatchId = "1622621", Purpose = FixtureRefreshPurposes.PostMatchVideo,
                    DayUtc = attemptAt.AddHours(6).Date, AttemptCount = 1,
                    LastAttemptUtc = attemptAt.AddHours(6), LastOutcome = "NoData"
                });
            db.SaveChanges();
        }

        // 2) "Restart": yepyeni bir DbContext defteri aynen bulur.
        using (var db = NewDb(dbName))
        {
            var rows = db.FixtureRefreshAttempts.AsNoTracking()
                .Where(a => a.ExternalMatchId == "1622621"
                         && a.Purpose == FixtureRefreshPurposes.PostMatchVideo)
                .ToList();

            var attempts = rows.Sum(a => a.AttemptCount);
            var last = rows.Max(a => a.LastAttemptUtc);

            Assert.Equal(2, attempts);
            // Sayaç sıfırlanmadığı için restart hemen yeni bir arama BAŞLATMAZ.
            Assert.False(PostMatchEnrichmentJob.IsDue(end, attempts, last, last.AddHours(1)));
            Assert.True(PostMatchEnrichmentJob.IsDue(end, attempts, last, last.AddHours(24)));
        }
    }

    // ── Kapsam: yalnız bitmiş + kilitli organizasyon ───────────────────────────

    [Fact]
    public void VideoAmaci_AyriBirButceDir()
    {
        // Video araması haber/sonuç bütçesinden yemez; defterde ayrı amaç olarak durur.
        Assert.Equal("PostMatchVideo", FixtureRefreshPurposes.PostMatchVideo);
        Assert.NotEqual(FixtureRefreshPurposes.Result, FixtureRefreshPurposes.PostMatchVideo);
        Assert.NotEqual(FixtureRefreshPurposes.PostMatchContent, FixtureRefreshPurposes.PostMatchVideo);
    }

    [Fact]
    public async Task KesifKapaliSaglayici_SifirAdayDoner()
    {
        var identity = new VideoFixtureIdentity(
            Leg1Id, "1622621", Leg1, Fener, Lyon, "Fenerbahçe", "Lyon", new List<DateTime> { Leg2 });

        var candidates = await new DisabledOfficialMatchVideoProvider().DiscoverAsync(identity);
        Assert.Empty(candidates);
    }

    /// <summary>
    /// Keşif YALNIZ izin listesindeki kanalların akışını okur. Bu, sonucun tanımı gereği
    /// resmî olmasını sağlar — arama motoru sonucundan videoya terfi eden bir yol yoktur.
    /// </summary>
    [Fact]
    public void KanalAkisi_YalnizIzinListesindekiKanallardanOkunur()
    {
        var channels = OfficialVideoSources.DiscoverableYouTubeChannels();

        Assert.NotEmpty(channels);
        Assert.All(channels, c =>
        {
            Assert.Equal("YouTube", c.Platform);
            Assert.True(c.AllowsInAppEmbed);
            Assert.StartsWith("UC", c.YouTubeChannelId);
        });
        // Gömmeye kapalı olduğu ÖLÇÜLMÜŞ kaynak keşif turuna alınmaz.
        Assert.DoesNotContain(channels, c => c.Key == OfficialVideoSources.UefaWeb);
    }

    [Fact]
    public void KanalAkisi_AtomCevabiniAdaylaraCevirir()
    {
        const string xml = """
        <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015"
              xmlns:media="http://search.yahoo.com/mrss/"
              xmlns="http://www.w3.org/2005/Atom">
          <entry>
            <yt:videoId>YYZYPAkPKs0</yt:videoId>
            <title>Şampiyonlar Ligi Play Off 1. Maç | Fenerbahçe - Lyon (Özet)</title>
            <published>2026-08-18T21:23:26+00:00</published>
            <media:group>
              <media:description>Kadıköy'de 1-1</media:description>
              <media:thumbnail url="https://i.ytimg.com/vi/YYZYPAkPKs0/hqdefault.jpg"/>
            </media:group>
          </entry>
          <entry>
            <title>Kimliksiz kayit</title>
            <published>2026-08-18T21:00:00+00:00</published>
          </entry>
        </feed>
        """;

        var parsed = YouTubeChannelFeedVideoProvider.Parse(xml, TrtSpor);

        // Kimliği olmayan kayıt ATLANIR — eksik alan uydurulmaz.
        var only = Assert.Single(parsed);
        Assert.Equal("YYZYPAkPKs0", only.ExternalVideoId);
        Assert.Equal(TrtSpor, only.SourceIdentifier);
        Assert.Equal(new DateTime(2026, 8, 18, 21, 23, 26, DateTimeKind.Utc), only.PublishedUtc);
        // Akış süre vermez; "0:00" uydurulmaz.
        Assert.Null(only.DurationSeconds);
    }

    // ── 9. BÖLGESEL KISIT DTO'YA TAŞINIR ──────────────────────────────────────

    [Fact]
    public async Task BolgeselKisit_DtoyaTasinir()
    {
        using var db = SeededDb(nameof(BolgeselKisit_DtoyaTasinir));

        // ÖLÇÜLDÜ: TRT SPOR'un play-off özetleri availableCountries = ["TR"].
        var trOnly = Leg1Highlights() with { AvailableCountries = new[] { "tr" } };
        Assert.True((await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, trOnly)).Stored);

        var row = db.MatchVideos.Single();
        Assert.Equal("TR", row.AvailableCountries);       // büyük harfe normalize
        Assert.True(row.IsRegionRestricted);

        var dto = new MatchVideoReader(db).GetVideos(Leg1Id).Single();
        Assert.True(dto.IsRegionRestricted);
        Assert.Equal(new[] { "TR" }, dto.AvailableCountries);
        // Kısıt oynatılabilirliği İPTAL ETMEZ: TR'deki kullanıcı için video gerçekten oynar.
        Assert.True(dto.CanPlayInApp);
    }

    [Fact]
    public async Task UlkeListesiYoksa_HerYerdeAcikVarsayilmaz()
    {
        using var db = SeededDb(nameof(UlkeListesiYoksa_HerYerdeAcikVarsayilmaz));
        await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, Leg1Highlights());

        var dto = new MatchVideoReader(db).GetVideos(Leg1Id).Single();
        // Kaynak söylemediyse kısıt "yok" değil BİLİNMİYOR: uyarı gösterilmez ama
        // "her yerde açık" iddiası da üretilmez.
        Assert.False(dto.IsRegionRestricted);
        Assert.Empty(dto.AvailableCountries);
    }

    // ── 10. ANA ÖZET, ÖNEMLİ ANLAR LİSTESİNDE TEKRARLANMAZ ────────────────────

    [Fact]
    public void AnaOzetTurleri_OnemliAnListesineDusmez()
    {
        // Uzun özet de bir MAÇ ÖZETİDİR; ayrı bir "önemli an klibi" değildir.
        Assert.True(MatchVideoTypes.IsMainHighlight(MatchVideoTypes.MatchHighlights));
        Assert.True(MatchVideoTypes.IsMainHighlight(MatchVideoTypes.ExtendedHighlights));
        Assert.False(MatchVideoTypes.IsMoment(MatchVideoTypes.MatchHighlights));
        Assert.False(MatchVideoTypes.IsMoment(MatchVideoTypes.ExtendedHighlights));

        // Ayrı klip türleri ise ana karta çıkmaz.
        foreach (var t in new[] { MatchVideoTypes.Goal, MatchVideoTypes.Penalty,
                                  MatchVideoTypes.RedCard, MatchVideoTypes.Var,
                                  MatchVideoTypes.ImportantMoment })
        {
            Assert.True(MatchVideoTypes.IsMoment(t));
            Assert.False(MatchVideoTypes.IsMainHighlight(t));
        }
    }

    [Fact]
    public async Task TekOzetVideosu_OnemliAnlarBolumunuDoldurmaz()
    {
        using var db = SeededDb(nameof(TekOzetVideosu_OnemliAnlarBolumunuDoldurmaz));
        await Registrar(db, new FakeEmbedVerifier(true)).RegisterAsync(Leg1Id, Leg1Highlights());

        var videos = new MatchVideoReader(db).GetVideos(Leg1Id);
        // Tek kayıt vardır ve o da ana özettir → "önemli an" listesi BOŞ kalır.
        // Tam özeti sahte gol kliplerine bölmek YASAKTIR.
        Assert.Single(videos);
        Assert.Empty(videos.Where(v => MatchVideoTypes.IsMoment(v.VideoType)));
    }

    // ── 12. İSTATİSTİK: SIFIR DOLU SATIR "VERİ" DEĞİLDİR ──────────────────────

    [Fact]
    public void TumAlanlariSifirOlanIstatistikSatiri_VeriSayilmaz()
    {
        // ÖLÇÜLDÜ (02.09.2026): 71513 ve 104237 için MatchLiveStats satırı VAR ama
        // skor dışındaki bütün alanlar sıfır (canlı alım kapalı). Bunu ekrana basmak
        // kullanıcıya "%0 topa sahip olma, 0 şut" diye YANLIŞ bir maç anlatırdı.
        var empty = new MatchLiveStats { MatchId = Leg1Id, HomeScore = 1, AwayScore = 1, Phase = "FT" };
        Assert.Null(MatchStatisticsDto.From(empty));
        Assert.Null(MatchStatisticsDto.From(null));
    }

    [Fact]
    public void GercekIstatistik_SatirlaraCevrilir()
    {
        var stats = new MatchLiveStats
        {
            MatchId = Leg1Id, Phase = "FT",
            PossessionHome = 52, PossessionAway = 48,
            ShotsHome = 12, ShotsAway = 12,
            ShotsOnTargetHome = 5, ShotsOnTargetAway = 5
        };

        var dto = MatchStatisticsDto.From(stats);

        Assert.NotNull(dto);
        var possession = dto!.Rows.Single(r => r.Key == "possession");
        Assert.Equal(52, possession.Home);
        Assert.Equal(48, possession.Away);
        Assert.True(possession.IsPercentage);
        Assert.Equal(12, dto.Rows.Single(r => r.Key == "shots").Home);
    }

    // ── 1-2. TARİH VE EV/DEPLASMAN YÖNÜ ───────────────────────────────────────

    [Fact]
    public void FikstürKimligi_TarihVeYonuAynenTasir()
    {
        using var db = SeededDb(nameof(FikstürKimligi_TarihVeYonuAynenTasir));
        var registrar = Registrar(db, new FakeEmbedVerifier(true));

        var leg1 = registrar.BuildIdentityAsync(Leg1Id).Result!;
        Assert.Equal(Leg1, leg1.MatchDateUtc);
        Assert.Equal(Fener, leg1.HomeTeamId);          // 1. ayakta ev sahibi Fenerbahçe
        Assert.Equal(Lyon, leg1.AwayTeamId);
        Assert.Equal("1622621", leg1.ExternalFixtureId);

        var leg2 = registrar.BuildIdentityAsync(Leg2Id).Result!;
        Assert.Equal(Leg2, leg2.MatchDateUtc);
        Assert.Equal(Lyon, leg2.HomeTeamId);           // 2. ayakta ev sahibi Lyon — TERS DEĞİL
        Assert.Equal(Fener, leg2.AwayTeamId);
        Assert.Equal("1622630", leg2.ExternalFixtureId);

        // İki ayak birbirini "diğer ayak" olarak görür — ayrım burada mümkün olur.
        Assert.Contains(Leg2, leg1.OtherLegDatesUtc);
        Assert.Contains(Leg1, leg2.OtherLegDatesUtc);
    }

    // ── Okuma sıralaması: ana özet başa ───────────────────────────────────────

    [Fact]
    public async Task OkumaSiralamasi_OynatilabilirOzetIlkSirada()
    {
        using var db = SeededDb(nameof(OkumaSiralamasi_OynatilabilirOzetIlkSirada));
        var registrar = Registrar(db, new FakeEmbedVerifier(true));

        // Gol klibi özetten SONRA yayımlanmış olsun; yine de özet ana karta çıkmalı.
        //
        // GOL KLİBİ DOĞRUDAN DEPOYA KURULUR, kayıt kapısından geçirilmez. Nedeni:
        // 03.09.2026 sertleştirmesinden sonra "Greenwood'un golü" gibi bir başlık
        // OTOMATİK kabul edilmez (gerçek özet işareti yoktur; bkz.
        // MatchVideoHardeningTests.GolKelimesiTekBasina_KabulUretmez). Böyle bir klip
        // ancak insan doğrulamasıyla girer. Bu test kayıt POLİTİKASINI değil, okuma
        // yolunun SIRALAMASINI ölçer; ikisini birbirine bağlamak testi yanlış şeye
        // duyarlı yapardı.
        db.MatchVideos.Add(new MatchVideo
        {
            MatchId = Leg1Id, ExternalFixtureId = "1622621", ExternalVideoId = "goal-greenwood",
            Title = "Greenwood'un golü | Fenerbahçe - Lyon",
            OfficialPublisher = "TRT SPOR",
            SourcePageUrl = "https://www.youtube.com/watch?v=goal-greenwood",
            EmbedUrl = "https://www.youtube-nocookie.com/embed/goal-greenwood",
            VideoType = MatchVideoTypes.Goal,
            PublishedAtUtc = new DateTime(2026, 8, 18, 22, 0, 0, DateTimeKind.Utc),
            IsOfficial = true, IsEmbeddable = true, CanPlayInApp = true,
            VerificationStatus = MatchVideoVerificationStatuses.Verified
        });
        db.SaveChanges();

        await registrar.RegisterAsync(Leg1Id, Leg1Highlights());

        var videos = new MatchVideoReader(db).GetVideos(Leg1Id);
        Assert.Equal(2, videos.Count);
        Assert.Equal(MatchVideoTypes.MatchHighlights, videos[0].VideoType);
        Assert.Equal(MatchVideoTypes.Goal, videos[1].VideoType);
    }
}
