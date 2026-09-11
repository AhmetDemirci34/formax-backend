using System;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Application.Services.Matches;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Entities;
using Formax.Infrastructure.Http;
using Formax.Infrastructure.PostMatch;
using Formax.Infrastructure.Telemetry;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// OLAY TERİMLERİ (deterministik Türkçe), RESMÎ SİTE SITEMAP'İ ve SIRSIZ İSTEK KAYDI.
/// </summary>
public class EventLabelsAndRequestLogTests
{
    // ── OLAY TERİMLERİ ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Goal", "Normal Goal", "Gol")]
    [InlineData("Goal", "Own Goal", "Kendi Kalesine Gol")]
    [InlineData("Goal", "Penalty", "Penaltı Golü")]
    [InlineData("Goal", "Missed Penalty", "Kaçan Penaltı")]
    [InlineData("subst", "Substitution 1", "Oyuncu Değişikliği")]
    [InlineData("subst", "Substitution 5", "Oyuncu Değişikliği")]
    [InlineData("Card", "Yellow Card", "Sarı Kart")]
    [InlineData("Card", "Red Card", "Kırmızı Kart")]
    [InlineData("Card", "Second Yellow card", "İkinci Sarıdan Kırmızı Kart")]
    [InlineData("Var", "Goal confirmed", "VAR İncelemesi: Gol Onaylandı")]
    [InlineData("Var", "Goal cancelled", "VAR İncelemesi: Gol İptal Edildi")]
    [InlineData("Var", "Penalty confirmed", "VAR İncelemesi: Penaltı Onaylandı")]
    [InlineData("Var", "Penalty cancelled", "VAR İncelemesi: Penaltı İptal Edildi")]
    [InlineData("Var", "", "VAR İncelemesi")]
    public void SaglayiciTerimi_TurkceyeCevrilir(string type, string detail, string expected)
        => Assert.Equal(expected, MatchEventLabels.Resolve(type, detail).Label);

    [Fact]
    public void BilinmeyenDeger_HamKodKullaniciyaGosterilmez()
    {
        var l = MatchEventLabels.Resolve("Weird", "Some Provider Code 42");
        Assert.Equal(MatchEventLabels.UnknownLabel, l.Label);
        Assert.DoesNotContain("Provider", l.Label);
    }

    [Fact]
    public void Degisiklik_GirenCikanAyrilir_AsistEtiketiTasinmaz()
    {
        // Ölçülen yön: sağlayıcının "player" = çıkan, "assist" = giren.
        var dto = MatchEventDto.FromRecords(new[]
        {
            new MatchEventRecord { Minute = 42, EventType = "subst", Detail = "Substitution 1",
                PlayerName = "I. Baouf", AssistName = "S. Bouhoudane", TeamName = "Cambuur" }
        }).Single();

        Assert.Equal("Oyuncu Değişikliği", dto.Label);
        Assert.Equal("S. Bouhoudane", dto.PlayerIn);
        Assert.Equal("I. Baouf", dto.PlayerOut);
        Assert.Null(dto.Assist);                 // değişiklikte "Asist" yazılmaz
        Assert.Equal("Substitution 1", dto.Detail); // ham değer yalnız teşhis alanında
    }

    // ── RESMÎ SİTE VİDEO SİTEMAP'İ (trtspor.com.tr biçimi) ────────────────────

    private const string TrtSitemapSample = """
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:video="http://www.google.com/schemas/sitemap-video/1.1">
          <url>
            <loc>https://www.trtspor.com.tr/videolar/ozet-or-lyon-fenerbahce-1-2-33000001</loc>
            <video:video>
              <video:thumbnail_loc>https://cdn-i.pr.trt.com.tr/trtspor/x.jpeg</video:thumbnail_loc>
              <video:title><![CDATA[ ÖZET | Lyon-Fenerbahçe: 1-2 | Şampiyonlar Ligi Play Off 2. Maç ]]></video:title>
              <video:description><![CDATA[ Fenerbahçe tur atladı. ]]></video:description>
              <video:content_loc>https://cdn-v.pr.trt.com.tr/x/master.m3u8</video:content_loc>
              <video:publication_date>2026-08-26T12:38:19.000Z</video:publication_date>
            </video:video>
          </url>
        </urlset>
        """;

    [Fact]
    public void VideoSitemap_AdaylaraCevrilir_WebKaynakKimligiyle()
    {
        var c = OfficialSiteFeedVideoProvider.Parse(TrtSitemapSample, OfficialVideoSources.TrtSporWeb, "OfficialSiteFeeds").Single();

        Assert.Equal("Web", c.Platform);
        Assert.Equal("trtspor.com.tr", c.SourceIdentifier);
        Assert.Equal("ÖZET | Lyon-Fenerbahçe: 1-2 | Şampiyonlar Ligi Play Off 2. Maç", c.Title);
        Assert.Equal(new DateTime(2026, 8, 26, 12, 38, 19, DateTimeKind.Utc), c.PublishedUtc);
        Assert.Equal("OfficialSiteFeeds", c.ProviderName);
    }

    [Fact]
    public void MacOncesiTarihliSayfa_KimlikKapisindaReddedilir()
    {
        // Ölçülen gerçek: TRT sayfası maçtan saatler ÖNCE tarihlenir. Tarih "düzeltilmez";
        // kimlik kapısı adayı reddeder ve gerekçesini söyler.
        var c = OfficialSiteFeedVideoProvider.Parse(TrtSitemapSample, OfficialVideoSources.TrtSporWeb, "OfficialSiteFeeds").Single();
        var fixture = new VideoFixtureIdentity(104237, "1415370",
            new DateTime(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc), 2, 1, "Lyon", "Fenerbahçe",
            new[] { new DateTime(2026, 8, 18, 19, 0, 0, DateTimeKind.Utc) });

        // Ön süzgeç adayı TAŞIR (kickoff'tan 24 saat önceki pencere) ki gerekçe görünsün…
        Assert.Single(OfficialSiteFeedVideoProvider.Prefilter(new[] { c }, fixture));

        // …ve kapı reddeder.
        var verdict = MatchVideoIdentityValidator.Validate(c, fixture);
        Assert.False(verdict.Accepted);
        Assert.Contains("maç bitmeden", verdict.Reason);
    }

    [Fact]
    public void TrtSporWeb_GommeyeKapali_IzinListesinde()
    {
        var s = OfficialVideoSources.ByKey(OfficialVideoSources.TrtSporWeb)!;
        Assert.False(s.AllowsInAppEmbed);       // X-Frame-Options: SAMEORIGIN (ölçüldü)
        Assert.Equal(OfficialVideoSourceTiers.Broadcaster, s.Tier);
    }

    // ── SIRSIZ İSTEK KAYDI ───────────────────────────────────────────────────

    [Fact]
    public void ApiFootballKaydi_IzinListesiDisiParametreyiYazmaz()
    {
        var (q, fixture) = ApiFootballRequestLog.Sanitize(
            new Uri("https://v3.football.api-sports.io/fixtures/events?fixture=1415370&apikey=SECRET123&token=abc"));

        Assert.Equal("fixture=1415370", q);
        Assert.Equal("1415370", fixture);
        Assert.DoesNotContain("SECRET", q);
        Assert.DoesNotContain("token", q);
    }

    [Fact]
    public void ApiFootballKaydi_FixturesIdParametresindenFikstur()
    {
        var (_, fixture) = ApiFootballRequestLog.Sanitize(new Uri("https://v3.football.api-sports.io/fixtures?id=1570366"));
        Assert.Equal("1570366", fixture);
    }

    [Theory]
    [InlineData(true, "{\"errors\":[],\"response\":[]}", "OK")]
    [InlineData(true, "{\"errors\":{\"rateLimit\":\"Too many requests\"}}", "ProviderError:rateLimit")]
    [InlineData(false, "", "HttpError")]
    public void SaglayiciSonucu_YalnizAnahtarAdiYazilir(bool ok, string body, string expected)
    {
        var r = ApiFootballCacheHandler.ProviderResultOf(ok, body);
        Assert.Equal(expected, r);
        Assert.DoesNotContain("Too many", r);   // ileti metni kayda girmez
    }

    [Fact]
    public void VideoKaydi_SorguDizesiniAtar()
    {
        var log = new VideoDiscoveryRequestLog(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoDiscoveryRequestLog>.Instance);
        log.RecordRequest("YouTubeDataApi",
            "https://www.googleapis.com/youtube/v3/search?part=snippet&key=SECRETKEY", 1, "2", "403", 0);

        var e = log.RequestSnapshot().Single();
        Assert.Equal("www.googleapis.com", e.Host);
        Assert.Equal("/youtube/v3/search", e.Path);
        Assert.DoesNotContain("SECRET", e.Host + e.Path);
    }
}
