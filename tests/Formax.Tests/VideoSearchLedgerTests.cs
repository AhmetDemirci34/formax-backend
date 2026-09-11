using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.PostMatch;
using Formax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// VİDEO ARAMA DURUMU KALICI DEFTERDEN GELİR — saatten değil.
///
/// ÖLÇÜLEN HATA (11.09.2026): ekran "maç biteli 26 saat geçti mi?" diye bakıp
/// "bulunamadı" diyordu. İş hiç çalışmamış ya da rate limit'e takılmış olsa bile.
/// Bu testler kuralı defter üzerinden sabitler.
/// </summary>
public class VideoSearchLedgerTests : IDisposable
{
    private readonly DbContextOptions<FormaxDbContext> _options;
    private readonly FormaxDbContext _db;
    private const string Fixture = "999001";

    public VideoSearchLedgerTests()
    {
        _options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"video-ledger-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FormaxDbContext(_options);
    }

    public void Dispose() => _db.Dispose();

    private static readonly DateTime Kickoff = new(2026, 9, 6, 15, 30, 0, DateTimeKind.Utc);

    /// <summary>Gerçek bir deneme: rezervasyon + sonuç — işin yaptığıyla aynı sıra.</summary>
    private void Attempt(FixtureSyncRepository repo, DateTime at, string outcome)
    {
        Assert.True(repo.TryReserveFixtureAttempt(Fixture, FixtureRefreshPurposes.PostMatchVideo,
            TimeSpan.FromHours(1), 200, at));
        repo.RecordFixtureAttemptOutcome(Fixture, FixtureRefreshPurposes.PostMatchVideo, at, outcome);
        _db.SaveChanges();
    }

    [Fact]
    public void TakvimGecmis_AmaHicDenemeYok_KontrolEdiliyor()
    {
        // Maç 5 gün önce bitti — saat "çoktan bitti" der. Defter boş: iş hiç bakmadı.
        var summary = new FixtureSyncRepository(_db)
            .GetFixtureAttemptSummary(Fixture, FixtureRefreshPurposes.PostMatchVideo);

        Assert.Equal(0, summary.Attempts);
        Assert.Equal(PostMatchVideoSearchStatus.Checking,
            PostMatchVideoSearchStatus.Resolve(hasPlayableVideo: false, summary));
    }

    [Fact]
    public void DenemeHakkiKaldiysa_KontrolEdiliyor()
    {
        var repo = new FixtureSyncRepository(_db);
        var end = MatchVideoIdentityValidator.EndOf(Kickoff);
        Attempt(repo, end.AddMinutes(60), "NoData");
        Attempt(repo, end.AddHours(3), "NoData");

        var summary = repo.GetFixtureAttemptSummary(Fixture, FixtureRefreshPurposes.PostMatchVideo);
        Assert.Equal(2, summary.Attempts);
        Assert.Equal(PostMatchVideoSearchStatus.Checking,
            PostMatchVideoSearchStatus.Resolve(false, summary));
    }

    [Fact]
    public void DortGercekDenemeTamamlandi_VideoYok_Bulunamadi()
    {
        var repo = new FixtureSyncRepository(_db);
        var end = MatchVideoIdentityValidator.EndOf(Kickoff);
        Attempt(repo, end.AddMinutes(60), "NoData");
        Attempt(repo, end.AddHours(3), "NoData");
        Attempt(repo, end.AddHours(6), "NoData");
        // Dördüncü deneme — iş hak bittiğinde "Unavailable" yazar.
        Attempt(repo, end.AddHours(24), MatchVideoVerificationStatuses.Unavailable);

        var summary = repo.GetFixtureAttemptSummary(Fixture, FixtureRefreshPurposes.PostMatchVideo);
        Assert.Equal(4, summary.Attempts);
        Assert.True(summary.ExhaustedRecorded);
        Assert.Equal(PostMatchVideoSearchStatus.NotFound, PostMatchVideoSearchStatus.Resolve(false, summary));
    }

    [Fact]
    public void DortDenemeSayildi_AmaSonDenemeKaydiYok_BulunamadiDenmez()
    {
        // Sayaç 4 ama "hak bitti" kaydı yok (ör. son deneme yarıda kaldı) → kanıt eksik.
        var summary = new FixtureAttemptSummary(4, DateTime.UtcNow, "Reserved", ExhaustedRecorded: false);
        Assert.Equal(PostMatchVideoSearchStatus.Checking, PostMatchVideoSearchStatus.Resolve(false, summary));
    }

    [Fact]
    public void VideoBulundu_Player()
    {
        var summary = new FixtureAttemptSummary(1, DateTime.UtcNow, "Applied", false);
        Assert.Equal(PostMatchVideoSearchStatus.Found, PostMatchVideoSearchStatus.Resolve(true, summary));

        // Hak bitmiş olsa bile oynatılabilir video varsa player'dır.
        var exhausted = new FixtureAttemptSummary(4, DateTime.UtcNow, "Unavailable", true);
        Assert.Equal(PostMatchVideoSearchStatus.Found, PostMatchVideoSearchStatus.Resolve(true, exhausted));
    }

    [Fact]
    public void BasariliVideodanSonra_YeniDenemeYapilmaz()
    {
        // İş, oynatılabilir video varsa maçı atlar; takvim de hak bitene dek "due" dese
        // bile arama yapılmaz. Bu, IsDue'dan bağımsız — işin kendi kontrolüdür; burada
        // takvimin hak sınırı sabitlenir.
        Assert.Equal(4, PostMatchVideoSchedule.MaxAttempts);
        Assert.False(PostMatchVideoSchedule.IsDue(Kickoff, 4, DateTime.UtcNow, DateTime.UtcNow.AddDays(10)));
    }

    [Fact]
    public void PlanEngeli_DenemeSayilmaz_BulunamadiyaGotermez()
    {
        var repo = new FixtureSyncRepository(_db);
        var end = MatchVideoIdentityValidator.EndOf(Kickoff);

        // Üç gerçek deneme…
        Attempt(repo, end.AddMinutes(60), "NoData");
        Attempt(repo, end.AddHours(3), "NoData");
        Attempt(repo, end.AddHours(6), "NoData");

        // …dördüncü tur rate limit'e takıldı: rezervasyon saydı, engel GERİ ALDI.
        var blockedAt = end.AddHours(24);
        Assert.True(repo.TryReserveFixtureAttempt(Fixture, FixtureRefreshPurposes.PostMatchVideo,
            TimeSpan.FromHours(1), 200, blockedAt));
        repo.RecordFixtureAttemptBlocked(Fixture, FixtureRefreshPurposes.PostMatchVideo, blockedAt,
            PostMatchEnrichmentJob.BlockedRateLimited);
        _db.SaveChanges();

        var summary = repo.GetFixtureAttemptSummary(Fixture, FixtureRefreshPurposes.PostMatchVideo);
        Assert.Equal(3, summary.Attempts);                               // engel sayılmadı
        Assert.Equal(PostMatchEnrichmentJob.BlockedRateLimited, summary.LastOutcome);
        Assert.Equal(PostMatchVideoSearchStatus.Checking, PostMatchVideoSearchStatus.Resolve(false, summary));

        // Hak hâlâ açık: takvim dördüncü denemeyi yeniden ister.
        var matchEnd = MatchVideoIdentityValidator.EndOf(Kickoff);
        Assert.True(PostMatchVideoSchedule.IsDue(matchEnd, summary.Attempts, summary.LastAttemptUtc,
            blockedAt.AddHours(19)));
    }

    [Fact]
    public void RestartSonrasi_DurumDegismez()
    {
        var end = MatchVideoIdentityValidator.EndOf(Kickoff);
        var before = new FixtureSyncRepository(_db);
        Attempt(before, end.AddMinutes(60), "NoData");
        Attempt(before, end.AddHours(3), "NoData");
        var statusBefore = PostMatchVideoSearchStatus.Resolve(false,
            before.GetFixtureAttemptSummary(Fixture, FixtureRefreshPurposes.PostMatchVideo));

        // "Restart": süreç belleğinden hiçbir şey taşımayan YENİ bağlam + YENİ depo.
        using var fresh = new FormaxDbContext(_options);
        var after = new FixtureSyncRepository(fresh)
            .GetFixtureAttemptSummary(Fixture, FixtureRefreshPurposes.PostMatchVideo);

        Assert.Equal(2, after.Attempts);
        Assert.Equal(statusBefore, PostMatchVideoSearchStatus.Resolve(false, after));
    }

    // ── ZİNCİR: ENGEL "TAMAMLANMADI" DEMEKTİR ────────────────────────────────

    private sealed class BlockedProvider : IOfficialMatchVideoProvider
    {
        public string Name => "OfficialSiteFeeds";
        public int Priority => OfficialVideoSourceTiers.Federation;
        public string Status => VideoProviderStatuses.Configured;
        public Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(VideoFixtureIdentity f, CancellationToken ct = default)
            => throw new VideoProviderUnavailableException(Name, "429", rateLimited: true);
    }

    private sealed class EmptyProvider : IOfficialMatchVideoProvider
    {
        public string Name => "YouTubeOfficialChannels";
        public int Priority => OfficialVideoSourceTiers.AuxiliaryDiscovery;
        public string Status => VideoProviderStatuses.Configured;
        public Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(VideoFixtureIdentity f, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OfficialVideoCandidate>>(Array.Empty<OfficialVideoCandidate>());
    }

    private static VideoFixtureIdentity Identity() => new(1, Fixture, Kickoff, 1, 2, "Telstar", "Cambuur", Array.Empty<DateTime>());

    [Fact]
    public async Task TumSaglayicilarEngellendi_TurTamamlanmadi()
    {
        var chain = new CompositeOfficialMatchVideoProvider(new IOfficialMatchVideoProvider[] { new BlockedProvider() },
            NullLogger<CompositeOfficialMatchVideoProvider>.Instance);

        var found = await chain.DiscoverAsync(Identity());

        Assert.Empty(found);
        Assert.False(chain.LastRunCompleted);   // iş bunu deneme SAYMAZ
        Assert.StartsWith("rate-limit", chain.LastOutcomes[0].Note);
    }

    [Fact]
    public async Task BirSaglayiciEngelli_DigerTamamladi_TurTamamlandi()
    {
        // Biri engellendi ama diğeri aramasını hatasız bitirdi: "bu turda bakıldı" doğrudur.
        var chain = new CompositeOfficialMatchVideoProvider(
            new IOfficialMatchVideoProvider[] { new BlockedProvider(), new EmptyProvider() },
            NullLogger<CompositeOfficialMatchVideoProvider>.Instance);

        await chain.DiscoverAsync(Identity());

        Assert.True(chain.LastRunCompleted);
    }
}
