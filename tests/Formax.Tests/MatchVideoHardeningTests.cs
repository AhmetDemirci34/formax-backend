using System;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.PostMatch;
using Formax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// VİDEO SINIFLANDIRICI SERTLEŞTİRMESİ VE GERİYE DÖNÜK DENETİM.
///
/// GERÇEK İNTERNET ÇAĞRISI YOK: embed doğrulayıcı sahtelenir, depo EF InMemory'dir.
///
/// KÖK OLAY (ölçüldü 03.09.2026): TRT SPOR'un stüdyo programı
///   "Vlahovic 25 golü geçer" l Beşiktaş - Çorum FK, Amedspor - Trabzonspor | Stadyum
/// (videoId 29GROlpBfYo) başlıkta "gol" geçtiği için GOL KLİBİ sayılmış ve İKİ ayrı
/// maça (82549, 103619) birden bağlanmıştı.
/// </summary>
public class MatchVideoHardeningTests
{
    private const string TrtSpor = "UCfYNqluOf8EbQkL44otydMw";

    private const string StudioTitle =
        "\"Vlahovic 25 golü geçer\" l Beşiktaş - Çorum FK, Amedspor - Trabzonspor | Stadyum";

    // Gerçek maçlar (depodan).
    private const int Bjk = 82549, Amed = 103619, Leg1 = 71513, Leg2 = 104237;
    private static readonly DateTime BjkKickoff = new(2026, 8, 31, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Leg1Kickoff = new(2026, 8, 18, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Leg2Kickoff = new(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc);

    private static FormaxDbContext NewDb(string name)
        => new(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class FakeEmbedVerifier : IVideoEmbedVerifier
    {
        public Task<EmbedVerification> VerifyAsync(
            OfficialVideoCandidate c, OfficialVideoSource s, System.Threading.CancellationToken ct = default)
            => Task.FromResult(new EmbedVerification(true,
                $"https://www.youtube-nocookie.com/embed/{c.ExternalVideoId}", null, "test"));
    }

    private static VideoFixtureIdentity BjkFixture() => new(
        Bjk, "fx-bjk", BjkKickoff, 1, 2, "Beşiktaş", "Çorum FK", Array.Empty<DateTime>());

    private static OfficialVideoCandidate Candidate(string title, string videoId = "29GROlpBfYo")
        => new("YouTube", TrtSpor, videoId, title, null,
            BjkKickoff.AddHours(3), $"https://www.youtube.com/watch?v={videoId}", null, null);

    // ── 7. "gol" TEK BAŞINA KABUL ETTİRMEZ ────────────────────────────────────

    [Fact]
    public void GolKelimesiTekBasina_KabulUretmez()
    {
        // Başlıkta "gol" var, takımlar var, kaynak resmî — ama gerçek özet işareti yok.
        var verdict = MatchVideoIdentityValidator.Validate(
            Candidate("Beşiktaş - Çorum FK maçında 3 gol", "only-gol"), BjkFixture());

        Assert.False(verdict.Accepted);
        Assert.Equal(MatchVideoRejectionReasons.NoHighlightMarker, verdict.RejectionCode);

        // Sınıflandırıcı da "gol"ü özet işareti SAYMAZ.
        Assert.False(MatchVideoIdentityValidator.HasHighlightMarker(
            MatchVideoIdentityValidator.Fold("25 golü geçer")));
        // Gerçek işaretler ise tanınır.
        foreach (var marker in new[] { "maç özeti", "highlights", "extended highlights",
                                       "full highlights", "goals & highlights" })
            Assert.True(MatchVideoIdentityValidator.HasHighlightMarker(
                MatchVideoIdentityValidator.Fold(marker)), marker);
    }

    // ── 8. STÜDYO PROGRAMI MAÇ ÖZETİ SAYILMAZ ────────────────────────────────

    [Fact]
    public void StudyoProgrami_MatchHighlightsSayilmaz()
    {
        var verdict = MatchVideoIdentityValidator.Validate(Candidate(StudioTitle), BjkFixture());

        Assert.False(verdict.Accepted);
        Assert.Equal(MatchVideoRejectionReasons.NotMatchHighlights, verdict.RejectionCode);
    }

    [Theory]
    [InlineData("Stadyum | Haftanın panoraması")]
    [InlineData("Beşiktaş - Çorum FK maç yorumu")]
    [InlineData("Maç sonu değerlendirme programı")]
    [InlineData("Teknik direktörün basın toplantısı")]
    [InlineData("Süper Lig analiz programı")]
    [InlineData("Canlı yayın: derbi öncesi")]
    [InlineData("Haftanın röportajı")]
    [InlineData("FORMAX podcast bölüm 3")]
    [InlineData("Hafta sonu tahmin bülteni")]
    public void ProgramIcerigi_OtomatikKabulEdilmez(string title)
    {
        Assert.True(MatchVideoIdentityValidator.IsStudioContent(
            MatchVideoIdentityValidator.Fold(title)), title);
    }

    // ── 9. BİRDEN ÇOK MAÇ İÇEREN BAŞLIK OTOMATİK BAĞLANMAZ ───────────────────

    [Fact]
    public void BirdenCokKarsilasmaIcerenBaslik_TekMacaBaglanmaz()
    {
        Assert.True(MatchVideoIdentityValidator.CountsDistinctFixtures(StudioTitle) > 1);

        // Stüdyo kelimesi olmasa bile iki karşılaşma listeleyen başlık reddedilir.
        var twoFixtures = "Beşiktaş - Çorum FK, Amedspor - Trabzonspor maç özeti";
        var verdict = MatchVideoIdentityValidator.Validate(
            Candidate(twoFixtures, "two-fixtures"), BjkFixture());

        Assert.False(verdict.Accepted);
        Assert.Equal(MatchVideoRejectionReasons.MultipleMatchesInTitle, verdict.RejectionCode);

        // Tek karşılaşmalı başlık bu kapıya TAKILMAZ.
        Assert.Equal(1, MatchVideoIdentityValidator.CountsDistinctFixtures(
            "Şampiyonlar Ligi Play Off 1. Maç | Fenerbahçe - Lyon (Özet)"));
    }

    // ── 10. YALNIZ BİR TAKIM ADI GEÇEN İÇERİK OTOMATİK BAĞLANMAZ ─────────────

    [Fact]
    public void YalnizBirTakimAdiGecenIcerik_OtomatikBaglanmaz()
    {
        var verdict = MatchVideoIdentityValidator.Validate(
            Candidate("Beşiktaş maç özeti", "one-team"), BjkFixture());

        Assert.False(verdict.Accepted);
        // Mevcut kapı (iki takım kuralı) devrede kalır.
        Assert.Contains("takım", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── DOĞRU KAYIT HÂLÂ GEÇER (sertleştirme doğruyu elemiyor) ───────────────

    [Fact]
    public void GercekMacOzeti_HalaKabulEdilir()
    {
        var fixture = new VideoFixtureIdentity(
            Leg1, "1622621", Leg1Kickoff, 3588, 3589, "Fenerbahçe", "Lyon",
            new[] { Leg2Kickoff });

        var candidate = new OfficialVideoCandidate(
            "YouTube", TrtSpor, "YYZYPAkPKs0",
            "Şampiyonlar Ligi Play Off 1. Maç | Fenerbahçe - Lyon (Özet) x Petrol Ofisi",
            "Şampiyonlar Ligi Play Off Turu ilk maçında Fenerbahçe; Kadıköy'de Lyon'u konuk etti.",
            new DateTime(2026, 8, 18, 21, 23, 26, DateTimeKind.Utc),
            "https://www.youtube.com/watch?v=YYZYPAkPKs0", null, 494);

        var verdict = MatchVideoIdentityValidator.Validate(candidate, fixture);

        Assert.True(verdict.Accepted);
        Assert.Equal(MatchVideoTypes.MatchHighlights, verdict.VideoType);
        Assert.Null(verdict.RejectionCode);
    }

    // ── 11-13. "VIDEO VAR" KURALI ────────────────────────────────────────────

    private static MatchVideo Video(int matchId, string status, bool canPlay = true,
        string? reason = null, string videoId = "v1")
        => new()
        {
            MatchId = matchId, ExternalFixtureId = "fx", ExternalVideoId = videoId,
            Title = "Maç Özeti", OfficialPublisher = "TRT SPOR",
            SourcePageUrl = $"https://www.youtube.com/watch?v={videoId}",
            EmbedUrl = canPlay ? $"https://www.youtube-nocookie.com/embed/{videoId}" : null,
            VideoType = MatchVideoTypes.MatchHighlights,
            IsOfficial = true, IsEmbeddable = canPlay, CanPlayInApp = canPlay,
            VerificationStatus = status, RejectionReason = reason
        };

    [Fact]
    public void VideoVar_YalnizVerifiedResmiVeOynatilabilirKayitta()
    {
        Assert.True(MatchVideoRules.IsPlayable(
            Video(1, MatchVideoVerificationStatuses.Verified)));

        // Rejected → ASLA
        Assert.False(MatchVideoRules.IsPlayable(
            Video(1, MatchVideoVerificationStatuses.Rejected, canPlay: false,
                reason: MatchVideoRejectionReasons.NotMatchHighlights)));

        // NeedsManualReview → ASLA (bayrağı kapalı olsa bile durum tek başına yeter)
        Assert.False(MatchVideoRules.IsPlayable(
            Video(1, MatchVideoVerificationStatuses.NeedsManualReview)));

        // Gerekçesi dolu bir kayıt, durumu ne olursa olsun gösterilmez.
        var poisoned = Video(1, MatchVideoVerificationStatuses.Verified);
        poisoned.RejectionReason = MatchVideoRejectionReasons.SharedAcrossMatches;
        Assert.False(MatchVideoRules.IsPlayable(poisoned));

        // Resmî olmayan kayıt de geçmez.
        var unofficial = Video(1, MatchVideoVerificationStatuses.Verified);
        unofficial.IsOfficial = false;
        Assert.False(MatchVideoRules.IsPlayable(unofficial));

        // EmbedBlocked oynatılamaz ama GÖRÜNEBİLİR; Rejected görünemez bile.
        Assert.False(MatchVideoRules.IsPlayable(
            Video(1, MatchVideoVerificationStatuses.EmbedBlocked, canPlay: false)));
        Assert.True(MatchVideoRules.IsVisible(
            Video(1, MatchVideoVerificationStatuses.EmbedBlocked, canPlay: false)));
        Assert.False(MatchVideoRules.IsVisible(
            Video(1, MatchVideoVerificationStatuses.Rejected, canPlay: false,
                reason: MatchVideoRejectionReasons.NotMatchHighlights)));
        Assert.False(MatchVideoRules.IsVisible(
            Video(1, MatchVideoVerificationStatuses.NeedsManualReview)));
    }

    // ── 14-17. GERİYE DÖNÜK DENETİM: YANLIŞLAR KAPANIR, DOĞRULAR KORUNUR ─────

    /// <summary>Ölçülen gerçek depo durumunu birebir kurar.</summary>
    private static FormaxDbContext RealWorldDb(string name)
    {
        var db = NewDb(name);
        db.Teams.AddRange(
            new Team { Id = 3588, Name = "Fenerbahçe" }, new Team { Id = 3589, Name = "Lyon" },
            new Team { Id = 11, Name = "Beşiktaş" }, new Team { Id = 12, Name = "Çorum FK" },
            new Team { Id = 13, Name = "Amed" }, new Team { Id = 14, Name = "Trabzonspor" });
        db.Matches.AddRange(
            new Match { Id = Leg1, ExternalMatchId = "1622621", MatchDate = Leg1Kickoff,
                Status = MatchStatuses.Finished, LeagueId = LockedCompetitions.ChampionsLeague,
                League = "UEFA Champions League", HomeTeamId = 3588, AwayTeamId = 3589,
                HomeScore = 1, AwayScore = 1, HalfTimeHomeScore = 0, HalfTimeAwayScore = 1 },
            new Match { Id = Leg2, ExternalMatchId = "1622630", MatchDate = Leg2Kickoff,
                Status = MatchStatuses.Finished, LeagueId = LockedCompetitions.ChampionsLeague,
                League = "UEFA Champions League", HomeTeamId = 3589, AwayTeamId = 3588,
                HomeScore = 1, AwayScore = 2, HalfTimeHomeScore = 0, HalfTimeAwayScore = 2 },
            new Match { Id = Bjk, ExternalMatchId = "fx-bjk", MatchDate = BjkKickoff,
                Status = MatchStatuses.Finished, LeagueId = LockedCompetitions.SuperLig,
                League = "Süper Lig", HomeTeamId = 11, AwayTeamId = 12, HomeScore = 2, AwayScore = 0 },
            new Match { Id = Amed, ExternalMatchId = "fx-amed", MatchDate = BjkKickoff,
                Status = MatchStatuses.Finished, LeagueId = LockedCompetitions.SuperLig,
                League = "Süper Lig", HomeTeamId = 13, AwayTeamId = 14, HomeScore = 1, AwayScore = 1 });

        // Doğru kayıtlar (Fenerbahçe–Lyon) + eski gevşek kuralla yazılmış YANLIŞ kayıtlar.
        db.MatchVideos.AddRange(
            new MatchVideo { Id = 3, MatchId = Leg1, ExternalFixtureId = "1622621",
                ExternalVideoId = "YYZYPAkPKs0",
                Title = "Şampiyonlar Ligi Play Off 1. Maç | Fenerbahçe - Lyon (Özet) x Petrol Ofisi",
                OfficialPublisher = "TRT SPOR", SourcePageUrl = "https://www.youtube.com/watch?v=YYZYPAkPKs0",
                EmbedUrl = "https://www.youtube-nocookie.com/embed/YYZYPAkPKs0",
                VideoType = MatchVideoTypes.MatchHighlights, IsOfficial = true, IsEmbeddable = true,
                CanPlayInApp = true, VerificationStatus = MatchVideoVerificationStatuses.Verified },
            new MatchVideo { Id = 4, MatchId = Leg2, ExternalFixtureId = "1622630",
                ExternalVideoId = "sz4AJjiol84",
                Title = "Şampiyonlar Ligi Play Off 2. Maç | Lyon - Fenerbahçe (Özet) X Petrol Ofisi",
                OfficialPublisher = "TRT SPOR", SourcePageUrl = "https://www.youtube.com/watch?v=sz4AJjiol84",
                EmbedUrl = "https://www.youtube-nocookie.com/embed/sz4AJjiol84",
                VideoType = MatchVideoTypes.MatchHighlights, IsOfficial = true, IsEmbeddable = true,
                CanPlayInApp = true, VerificationStatus = MatchVideoVerificationStatuses.Verified },
            new MatchVideo { Id = 5, MatchId = Bjk, ExternalFixtureId = "fx-bjk",
                ExternalVideoId = "29GROlpBfYo", Title = StudioTitle,
                OfficialPublisher = "TRT SPOR", SourcePageUrl = "https://www.youtube.com/watch?v=29GROlpBfYo",
                EmbedUrl = "https://www.youtube-nocookie.com/embed/29GROlpBfYo",
                VideoType = MatchVideoTypes.Goal, IsOfficial = true, IsEmbeddable = true,
                CanPlayInApp = true, VerificationStatus = MatchVideoVerificationStatuses.Verified },
            new MatchVideo { Id = 6, MatchId = Amed, ExternalFixtureId = "fx-amed",
                ExternalVideoId = "29GROlpBfYo", Title = StudioTitle,
                OfficialPublisher = "TRT SPOR", SourcePageUrl = "https://www.youtube.com/watch?v=29GROlpBfYo",
                EmbedUrl = "https://www.youtube-nocookie.com/embed/29GROlpBfYo",
                VideoType = MatchVideoTypes.Goal, IsOfficial = true, IsEmbeddable = true,
                CanPlayInApp = true, VerificationStatus = MatchVideoVerificationStatuses.Verified });
        db.SaveChanges();
        return db;
    }

    private static MatchVideoAuditService Audit(FormaxDbContext db)
        => new(db, NullLogger<MatchVideoAuditService>.Instance);

    [Fact]
    public async Task Denetim_SaltOkunurModda_HicbirSatiriDegistirmez()
    {
        using var db = RealWorldDb(nameof(Denetim_SaltOkunurModda_HicbirSatiriDegistirmez));

        var report = await Audit(db).RunAsync(apply: false);

        Assert.Equal(4, report.Total);
        Assert.Equal(2, report.MultipleMatchesInTitle);
        Assert.Equal(1, report.VideoIdsSharedAcrossMatches);
        Assert.Equal(2, report.Changes.Count);
        // Salt okunur: DB'de hiçbir şey değişmedi.
        Assert.Equal(4, db.MatchVideos.Count(v => v.CanPlayInApp));
    }

    [Fact]
    public async Task Denetim_YanlisKayitlariKapatir_DogrulariKorur()
    {
        using var db = RealWorldDb(nameof(Denetim_YanlisKayitlariKapatir_DogrulariKorur));

        var report = await Audit(db).RunAsync(apply: true);

        Assert.Equal(2, report.Rejected);
        Assert.Equal(2, report.Verified);

        // 82549 ve 103619 → Rejected, oynatılamaz, gerekçesi NotMatchHighlights.
        foreach (var id in new[] { Bjk, Amed })
        {
            var v = db.MatchVideos.Single(x => x.MatchId == id);
            Assert.Equal(MatchVideoVerificationStatuses.Rejected, v.VerificationStatus);
            Assert.Equal(MatchVideoRejectionReasons.NotMatchHighlights, v.RejectionReason);
            Assert.False(v.CanPlayInApp);
            Assert.Null(v.EmbedUrl);
            Assert.False(MatchVideoRules.IsPlayable(v));
            Assert.False(MatchVideoRules.IsVisible(v));
        }

        // Fenerbahçe–Lyon kayıtlarına DOKUNULMADI.
        foreach (var id in new[] { Leg1, Leg2 })
        {
            var v = db.MatchVideos.Single(x => x.MatchId == id);
            Assert.Equal(MatchVideoVerificationStatuses.Verified, v.VerificationStatus);
            Assert.Null(v.RejectionReason);
            Assert.True(v.CanPlayInApp);
            Assert.True(MatchVideoRules.IsPlayable(v));
        }

        // HİÇBİR SATIR SİLİNMEDİ — kayıtlar denetlenebilir durumda kalır.
        Assert.Equal(4, db.MatchVideos.Count());
    }

    [Fact]
    public async Task DenetimSonrasi_YanlisVideo_HicbirEkrandaGorunmez()
    {
        using var db = RealWorldDb(nameof(DenetimSonrasi_YanlisVideo_HicbirEkrandaGorunmez));
        await Audit(db).RunAsync(apply: true);

        var reader = new MatchVideoReader(db);

        // Maç özeti ekranı: yanlış kayıt "oynatılamıyor" satırı olarak bile ÇIKMAZ.
        Assert.Empty(reader.GetVideos(Bjk));
        Assert.Empty(reader.GetVideos(Amed));

        // Doğru videolar yerinde ve oynatılabilir.
        var leg1 = Assert.Single(reader.GetVideos(Leg1));
        Assert.True(leg1.CanPlayInApp);
        Assert.Contains("YYZYPAkPKs0", leg1.EmbedUrl);
        var leg2 = Assert.Single(reader.GetVideos(Leg2));
        Assert.True(leg2.CanPlayInApp);
        Assert.Contains("sz4AJjiol84", leg2.EmbedUrl);
    }

    [Fact]
    public async Task DenetimSonrasi_SonucKartlarindaVideoVarIsaretiDuzelir()
    {
        using var db = RealWorldDb(nameof(DenetimSonrasi_SonucKartlarindaVideoVarIsaretiDuzelir));
        var results = TestReaders.Results(db);

        // ÖNCE: yanlış kayıtlar yüzünden iki maç "Video var" diyordu.
        var before = await results.GetResultsAsync(new DateOnly(2026, 8, 31));
        Assert.True(before.Single(r => r.MatchId == Bjk).HasPlayableOfficialVideo);

        await Audit(db).RunAsync(apply: true);

        // SONRA: işaret kalkar.
        var after = await results.GetResultsAsync(new DateOnly(2026, 8, 31));
        Assert.False(after.Single(r => r.MatchId == Bjk).HasPlayableOfficialVideo);
        Assert.False(after.Single(r => r.MatchId == Amed).HasPlayableOfficialVideo);

        // Doğru maçlarda işaret KORUNUR.
        Assert.True((await results.GetResultsAsync(new DateOnly(2026, 8, 18)))
            .Single(r => r.MatchId == Leg1).HasPlayableOfficialVideo);
        Assert.True((await results.GetResultsAsync(new DateOnly(2026, 8, 26)))
            .Single(r => r.MatchId == Leg2).HasPlayableOfficialVideo);
    }

    // ── Kayıt kapısı: aynı video ikinci bir maça bağlanamaz ─────────────────

    [Fact]
    public async Task AyniVideo_IkinciBirMacaBaglanamaz()
    {
        using var db = RealWorldDb(nameof(AyniVideo_IkinciBirMacaBaglanamaz));
        var registrar = new MatchVideoRegistrar(db, new FakeEmbedVerifier(),
            NullLogger<MatchVideoRegistrar>.Instance);

        // Leg1'in GERÇEK videosunu Leg2'ye bağlamayı dene — reddedilmeli.
        var stolen = new OfficialVideoCandidate(
            "YouTube", TrtSpor, "YYZYPAkPKs0",
            "Şampiyonlar Ligi Play Off 2. Maç | Lyon - Fenerbahçe (Özet)", null,
            new DateTime(2026, 8, 26, 21, 25, 30, DateTimeKind.Utc),
            "https://www.youtube.com/watch?v=YYZYPAkPKs0-x", null, null);

        var result = await registrar.RegisterAsync(Leg2, stolen);

        Assert.False(result.Stored);
        Assert.Equal(MatchVideoVerificationStatuses.Rejected, result.Status);
    }
}
