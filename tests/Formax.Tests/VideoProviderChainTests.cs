using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.PostMatch;
using Formax.Infrastructure.PostMatch;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// ÇOK KAYNAKLI VİDEO KEŞİF ZİNCİRİ.
///
/// ÖLÇÜLEN SINIR (07.09.2026): keşfin tamamı YouTube RSS akışına bağlıydı ve o akış
/// kanalın yalnız son ~15 videosunu verir; akıştan düşen resmî özet kalıcı olarak
/// kayboluyordu. Zincir, hak sahipliği sırasına göre birden çok resmî kaynağı dener.
///
/// Bu testlerde AĞ YOKTUR: sağlayıcılar sahtedir, yalnız zincirin karar mantığı ölçülür.
/// </summary>
public class VideoProviderChainTests
{
    private static VideoFixtureIdentity Fixture() => new(
        MatchId: 71513,
        ExternalFixtureId: "1234567",
        MatchDateUtc: new DateTime(2026, 8, 18, 19, 0, 0, DateTimeKind.Utc),
        HomeTeamId: 10,
        AwayTeamId: 20,
        HomeTeamName: "Fenerbahçe",
        AwayTeamName: "Olympique Lyonnais",
        OtherLegDatesUtc: Array.Empty<DateTime>());

    /// <summary>İstenen davranışı taklit eden sahte sağlayıcı — ağ yok.</summary>
    private sealed class FakeProvider : IOfficialMatchVideoProvider
    {
        private readonly IReadOnlyList<OfficialVideoCandidate> _result;
        private readonly Exception? _throw;

        public FakeProvider(string name, int priority, string status,
            IReadOnlyList<OfficialVideoCandidate>? result = null, Exception? shouldThrow = null)
        {
            Name = name; Priority = priority; Status = status;
            _result = result ?? Array.Empty<OfficialVideoCandidate>();
            _throw = shouldThrow;
        }

        public string Name { get; }
        public int Priority { get; }
        public string Status { get; }
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<OfficialVideoCandidate>> DiscoverAsync(
            VideoFixtureIdentity fixture, CancellationToken ct = default)
        {
            WasCalled = true;
            if (_throw != null) throw _throw;
            return Task.FromResult(_result);
        }
    }

    private static OfficialVideoCandidate Candidate(string videoId, string title = "MAÇ ÖZETİ")
        => new(
            Platform: "YouTube",
            SourceIdentifier: "UCyGa1YEx9ST66rYrJTGIKOw",
            ExternalVideoId: videoId,
            Title: title,
            Description: null,
            PublishedUtc: new DateTime(2026, 8, 18, 22, 0, 0, DateTimeKind.Utc),
            SourcePageUrl: "https://www.youtube.com/watch?v=" + videoId,
            ThumbnailUrl: null,
            DurationSeconds: null);

    private static CompositeOfficialMatchVideoProvider Chain(params IOfficialMatchVideoProvider[] members)
        => new(members, NullLogger<CompositeOfficialMatchVideoProvider>.Instance);

    [Fact]
    public async Task YapilandirilmamisSaglayici_Atlanir_ZincirDevamEder()
    {
        // Anahtarı olmayan sağlayıcı bir HATA değildir: atlanır, zincir devam eder.
        var notConfigured = new FakeProvider(
            "YouTubeDataApi", OfficialVideoSourceTiers.LicensedSportsOutlet,
            VideoProviderStatuses.NotConfigured);

        var working = new FakeProvider(
            "YouTubeOfficialChannels", OfficialVideoSourceTiers.AuxiliaryDiscovery,
            VideoProviderStatuses.Configured, new[] { Candidate("vid-1") });

        var chain = Chain(notConfigured, working);
        var found = await chain.DiscoverAsync(Fixture());

        Assert.False(notConfigured.WasCalled);   // hiç çağrılmadı
        Assert.True(working.WasCalled);          // zincir DURMADI
        Assert.Single(found);

        var skipped = chain.LastOutcomes.Single(o => o.Provider == "YouTubeDataApi");
        Assert.Equal(VideoProviderStatuses.NotConfigured, skipped.Status);
    }

    [Fact]
    public async Task SaglayiciHatasi_ZinciriDurdurmaz()
    {
        var broken = new FakeProvider(
            "OfficialSiteFeeds", OfficialVideoSourceTiers.Federation,
            VideoProviderStatuses.Configured, shouldThrow: new InvalidOperationException("akis bozuk"));

        var working = new FakeProvider(
            "YouTubeOfficialChannels", OfficialVideoSourceTiers.AuxiliaryDiscovery,
            VideoProviderStatuses.Configured, new[] { Candidate("vid-2") });

        var found = await Chain(broken, working).DiscoverAsync(Fixture());

        Assert.True(working.WasCalled);
        Assert.Single(found);
    }

    [Fact]
    public async Task AyniVideo_IkiKaynaktan_TekKezTasinir_OnceligiYuksekOlanKazanir()
    {
        // Aynı video hem federasyon akışından hem yardımcı keşiften gelirse, ilk
        // bulan (hak sahipliği yüksek olan) kazanır ve kopya taşınmaz.
        var federation = new FakeProvider(
            "OfficialSiteFeeds", OfficialVideoSourceTiers.Federation,
            VideoProviderStatuses.Configured, new[] { Candidate("ayni-video") });

        var auxiliary = new FakeProvider(
            "YouTubeOfficialChannels", OfficialVideoSourceTiers.AuxiliaryDiscovery,
            VideoProviderStatuses.Configured, new[] { Candidate("ayni-video") });

        // Kasten TERS sırada verilir: sıralamayı zincir yapar, çağıran değil.
        var found = await Chain(auxiliary, federation).DiscoverAsync(Fixture());

        Assert.Single(found);
        Assert.Equal("OfficialSiteFeeds", found[0].ProviderName);
    }

    [Fact]
    public async Task ZincirSonucu_ProviderAdiniVeMacKimliginiTasir()
    {
        var provider = new FakeProvider(
            "YouTubeOfficialChannels", OfficialVideoSourceTiers.AuxiliaryDiscovery,
            VideoProviderStatuses.Configured, new[] { Candidate("vid-3") });

        var fixture = Fixture();
        var found = await Chain(provider).DiscoverAsync(fixture);

        Assert.Equal("YouTubeOfficialChannels", found[0].ProviderName);
        Assert.Equal(fixture.MatchId, found[0].MatchId);
        Assert.Equal(fixture.ExternalFixtureId, found[0].ExternalFixtureId);
    }

    [Fact]
    public async Task HicbiriYapilandirilmamissa_ZincirDurumuDurustce_NotConfigured()
    {
        var chain = Chain(
            new FakeProvider("OfficialSiteFeeds", OfficialVideoSourceTiers.Federation,
                VideoProviderStatuses.NotConfigured),
            new FakeProvider("YouTubeDataApi", OfficialVideoSourceTiers.LicensedSportsOutlet,
                VideoProviderStatuses.NotConfigured));

        Assert.Equal(VideoProviderStatuses.NotConfigured, chain.Status);
        Assert.Empty(await chain.DiscoverAsync(Fixture()));
    }

    // ── KAYNAK ÖNCELİĞİ ──────────────────────────────────────────────────────

    [Fact]
    public void EvSahibiKulup_DeplasmanKulubunun_Onunde()
    {
        var fenerbahce = OfficialVideoSources.ByKey("fenerbahce")!;
        var lyon = OfficialVideoSources.ByKey("olympique-lyonnais")!;

        // Fenerbahçe EV sahibi olduğu maçta önce gelir…
        var homeTier = OfficialVideoSources.EffectiveTier(fenerbahce, "Fenerbahçe", "Olympique Lyonnais");
        var awayTier = OfficialVideoSources.EffectiveTier(lyon, "Fenerbahçe", "Olympique Lyonnais");
        Assert.True(homeTier < awayTier);

        // …ikinci ayakta sıra TERSİNE döner.
        var homeTier2 = OfficialVideoSources.EffectiveTier(lyon, "Olympique Lyonnais", "Fenerbahçe");
        var awayTier2 = OfficialVideoSources.EffectiveTier(fenerbahce, "Olympique Lyonnais", "Fenerbahçe");
        Assert.True(homeTier2 < awayTier2);
    }

    [Fact]
    public void MacinTarafiOlmayanKulup_KulupOnceligiTasimaz()
    {
        var fenerbahce = OfficialVideoSources.ByKey("fenerbahce")!;

        var tier = OfficialVideoSources.EffectiveTier(fenerbahce, "Telstar", "Cambuur");

        Assert.Equal(OfficialVideoSourceTiers.LicensedSportsOutlet, tier);
    }

    [Fact]
    public void KesifSirasi_HakSahipligiDuzeninde()
    {
        var ordered = OfficialVideoSources.DiscoverableYouTubeChannels("Fenerbahçe", "Olympique Lyonnais");

        var tiers = ordered
            .Select(s => OfficialVideoSources.EffectiveTier(s, "Fenerbahçe", "Olympique Lyonnais"))
            .ToList();

        // Sıralı olmalı: federasyon → yayıncı → ev sahibi → deplasman.
        Assert.Equal(tiers.OrderBy(t => t).ToList(), tiers);
        Assert.Equal(OfficialVideoSourceTiers.Federation, tiers.First());
    }
}
