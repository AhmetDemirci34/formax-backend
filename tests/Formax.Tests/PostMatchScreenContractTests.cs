using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// BİTMİŞ MAÇ ÖZETİ EKRANININ SÖZLEŞMESİ — kilit ihlalleri derleme hattında yakalansın.
///
/// NEDEN KAYNAK METNİ ÜZERİNDEN: bu depoda frontend test koşucusu yoktur ve bu görev
/// kapsamında bir tane kurmak ürün kararını korumak için gereğinden büyük bir değişiklik
/// olurdu. Buradaki testler bir render testinin yerini TUTMAZ; dar ama gerçek bir iş
/// yaparlar: kaldırılan bölümlerin geri gelmesini, ikinci bir boş durum cümlesini,
/// açılışta iframe kurulmasını, sabit genişlikli kabı ve puan durumu kararının frontend'e
/// kaçmasını engellerler.
///
/// Görsel ve davranışsal doğrulama (375×812'de gerçek tıklama, gerçek oynatma, yatay
/// taşma ölçümü) ayrıca çalışan uygulamada yapılmıştır.
/// </summary>
public class PostMatchScreenContractTests
{
    private static string Screen() => Read("formax-web/components/match-center/views/FinishedMatchSummary.tsx");
    private static string Types() => Read("formax-web/types/api.ts");
    private static string MatchPage() => Read("formax-web/app/match/[id]/page.tsx");
    private static string PredictionCard() => Read("formax-web/components/predictions/PredictionCard.tsx");

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);   // depo kökü bulunamazsa test anlamsızdır
        var path = Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"beklenen dosya yok: {relative}");
        return File.ReadAllText(path);
    }

    // ── 13. HABER / YORUM / AI HİKÂYE BÖLÜMLERİ RENDER EDİLMEZ ────────────────

    [Theory]
    [InlineData("Maç Sonrası Haberler")]
    [InlineData("Teknik Direktörlerden")]
    [InlineData("Basında ve Sosyal Medyada")]
    [InlineData("postMatchContent")]
    [InlineData("PostMatchContentDto")]
    [InlineData("CoachReaction")]
    [InlineData("PlayerReaction")]
    [InlineData("PressReaction")]
    [InlineData("SocialReaction")]
    [InlineData("aiNarrative")]
    [InlineData("matchStory")]
    public void HaberYorumVeAiHikaye_BitmisMacEkraninda_Yok(string forbidden)
    {
        Assert.DoesNotContain(forbidden, Screen(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HaberSozlesmesi_ApiTiplerinden_Kaldirildi()
    {
        var types = Types();
        Assert.DoesNotContain("PostMatchContentDto", types, StringComparison.Ordinal);
        Assert.DoesNotContain("postMatchContent", types, StringComparison.Ordinal);
    }

    // ── Ekranın ürün adı ─────────────────────────────────────────────────────

    [Fact]
    public void BitmisMacEkraninin_BasligiMacOzetidir()
    {
        // Bitmiş maçta ekran "Maç Detayı" değildir; başlık ürün adını söylemelidir.
        Assert.Contains("title=\"Maç Özeti\"", MatchPage(), StringComparison.Ordinal);
        Assert.Contains("<Panel title=\"Maç Özeti\">", Screen(), StringComparison.Ordinal);
    }

    // ── 1-2. TARİH, SAAT VE EV/DEPLASMAN YÖNÜ ────────────────────────────────

    [Fact]
    public void TarihVeTurkiyeSaati_SonucKartindaGosterilir()
    {
        var screen = Screen();

        // Tarih/saat backend'in UTC anından TEK yerde (matchClock) Europe/Istanbul'a
        // çevrilir; ekran kendi saat hesabı YAPMAZ.
        Assert.Contains("formatMatchDateTR(match.matchDate)", screen, StringComparison.Ordinal);
        Assert.Contains("{when.date} · {when.time}", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("new Date(", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void EvSahibiSolda_DeplasmanSagda_TersCevrilmez()
    {
        var screen = Screen();
        var home = screen.IndexOf("match.homeTeam?.name", StringComparison.Ordinal);
        var away = screen.IndexOf("match.awayTeam?.name", StringComparison.Ordinal);

        Assert.True(home >= 0 && away >= 0);
        Assert.True(home < away, "ev sahibi deplasmandan ÖNCE render edilmeli");
    }

    [Fact]
    public void IY_2Y_MS_BackendtenGelir_EkrandaCikarmaYapilmaz()
    {
        var screen = Screen();

        Assert.Contains("sb?.halfTime", screen, StringComparison.Ordinal);
        Assert.Contains("sb?.secondHalf", screen, StringComparison.Ordinal);
        Assert.Contains("sb?.fullTime", screen, StringComparison.Ordinal);
        // 2Y çıkarması backend'dedir; ekranda ikinci bir kural olmamalı.
        Assert.DoesNotContain("fullTime.home -", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("- halfTime", screen, StringComparison.Ordinal);
    }

    // ── 10-11-12. BOŞ BÖLÜM YOK, ANA ÖZET TEKRARLANMAZ ───────────────────────

    [Fact]
    public void AnaOzet_OnemliAnlarListesinde_Tekrarlanmaz()
    {
        var screen = Screen();

        // Liste hem tür süzgeci hem de kimlik karşılaştırmasıyla ana videoyu dışlar.
        Assert.Contains("isMoment(v.videoType) && v !== main", screen, StringComparison.Ordinal);
        // Ana kart yalnız MatchHighlights/ExtendedHighlights kabul eder.
        Assert.Contains("v.canPlayInApp && isMainHighlight(v.videoType)", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void OlayVeIstatistikBolumleri_VeriYoksaRenderEdilmez()
    {
        var screen = Screen();

        Assert.Contains("{events.length > 0 && (", screen, StringComparison.Ordinal);
        Assert.Contains("{moments.length > 0 && (", screen, StringComparison.Ordinal);
        Assert.Contains("{stats && stats.rows.length > 0 && (", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void TekBosDurum_VideoIcin_VeGenelMesajlaBirlikteGosterilmez()
    {
        var screen = Screen();

        // ÜÇ DURUM (07.09.2026): tek bir "henüz bulunmuyor" metni, denemeler sürerken
        // de bittiğinde de aynı şeyi söylüyordu. Maç biteli 40 dakika olmuşken
        // "bulunamadı" demek yanlıştır — daha hiç bakılmamıştır.
        const string searching = "Resmî maç özeti kontrol ediliyor.";
        const string exhausted =
            "Bu maç için uygulama içinde oynatılabilen resmî özet videosu bulunamadı.";
        Assert.Contains(searching, screen, StringComparison.Ordinal);
        Assert.Contains(exhausted, screen, StringComparison.Ordinal);

        // Hangi metnin çıkacağı ZAMAN SÖZLEŞMESİNDEN gelir; ekran kendi hesabını yapmaz.
        Assert.Contains("isVideoSearchWindowOver(match.matchDate)", screen, StringComparison.Ordinal);

        // <Empty …/> yalnız İKİ yerde: video boş durumu ve backend'in puan durumu metni.
        Assert.Equal(2, Regex.Matches(screen, @"<Empty\b").Count);

        // Genel "ayrıntı yok" mesajı ile video boş durumu AYNI ANDA çıkamaz: biri
        // hasAnyDetail false iken, diğeri true iken render edilir.
        Assert.Contains("{!hasAnyDetail && (", screen, StringComparison.Ordinal);
        Assert.Contains("{hasAnyDetail && (", screen, StringComparison.Ordinal);
    }

    // ── 14-15. PUAN DURUMU KARARI BACKEND'İNDİR ──────────────────────────────

    [Fact]
    public void PuanDurumu_YalnizBackendTableDerse_Gosterilir()
    {
        var screen = Screen();

        // Tek koşul: availability === "Table". UEFA eleme/play-off'ta backend Table
        // demez → tablo hiç render edilmez.
        Assert.Contains("standingsAvailability === \"Table\"", screen, StringComparison.Ordinal);
        // Frontend kendi aşama kuralını YENİDEN HESAPLAMAZ.
        Assert.DoesNotContain("LeaguePhaseTable", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("NotApplicable", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("Play-off", screen, StringComparison.Ordinal);
    }

    // ── 16-17. AÇILIŞ VE TIKLAMA DIŞ KEŞİF İSTEĞİ ÜRETMEZ ────────────────────

    [Fact]
    public void SayfaAcilisinda_IframeKurulmaz_OtomatikOynatmaYok()
    {
        var screen = Screen();

        Assert.Contains("{playing && !failed ? (", screen, StringComparison.Ordinal);
        Assert.Contains("onClick={() => setPlaying(true)}", screen, StringComparison.Ordinal);

        var iframeIndex = screen.IndexOf("<iframe", StringComparison.Ordinal);
        var playingIndex = screen.IndexOf("{playing && !failed ? (", StringComparison.Ordinal);
        Assert.True(playingIndex >= 0 && iframeIndex > playingIndex,
            "iframe, playing kontrolünden SONRA kurulmalı");
    }

    [Fact]
    public void OynatmaTiklamasi_YeniKesifIstegiUretmez()
    {
        var screen = Screen();

        // Ekran hiçbir veri çağrısı yapmaz: fetch/axios/useQuery yok. Tıklama yalnız
        // yerel state'i değiştirir; keşif ya da API-Football sorgusu BAŞLATMAZ.
        Assert.DoesNotContain("fetch(", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("axios", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("useQuery", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("api/", screen, StringComparison.Ordinal);
    }

    // ── Player başarısızlığı ve bölgesel kısıt ───────────────────────────────

    [Fact]
    public void PlayerHatasinda_SonsuzSpinnerYok_DurustMesajVar()
    {
        var screen = Screen();

        Assert.Contains("if (!video.canPlayInApp)", screen, StringComparison.Ordinal);
        Assert.Contains("Uygulama içinde oynatılamıyor", screen, StringComparison.Ordinal);
        Assert.Contains("Video şu an oynatılamıyor", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("animate-spin", screen, StringComparison.Ordinal);
    }

    [Fact]
    public void BolgeselKisit_EkrandaAcikcaSoylenir()
    {
        var screen = Screen();

        Assert.Contains("video.isRegionRestricted", screen, StringComparison.Ordinal);
        Assert.Contains("yalnız {countries.join(\", \")} bölgesinde oynatılabilir",
            screen, StringComparison.Ordinal);
    }

    [Fact]
    public void GommeAdresi_YalnizCerezsizYouTubeOlabilir()
    {
        // Ekran adresi kendisi ÜRETMEZ; backend'den geleni kullanır. Backend ise yalnız
        // youtube-nocookie üretir — kural orada çivilidir ve engel AŞILMAZ.
        var verifier = Read("Formax.Infrastructure/PostMatch/YouTubeEmbedVerifier.cs");
        Assert.Contains("youtube-nocookie.com/embed/", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Frame-Options", verifier, StringComparison.Ordinal);
        Assert.DoesNotContain("Referer", verifier, StringComparison.Ordinal);
    }

    // ── 19. MOBİLDE YATAY TAŞMA OLMASIN ──────────────────────────────────────

    [Fact]
    public void MobilKap_YatayTasmayaKapali()
    {
        var screen = Screen();

        Assert.Contains("overflow-x-hidden", screen, StringComparison.Ordinal);
        // Video 16:9 oranla ölçeklenir; sabit piksel genişlik 375px'te taşmaya yol açardı.
        Assert.Contains("aspect-video w-full max-w-full", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("min-w-[", screen, StringComparison.Ordinal);
        Assert.DoesNotContain("overflow-x-auto", screen, StringComparison.Ordinal);
        // Uzun başlıklar düzeni bozmasın.
        Assert.Contains("break-words", screen, StringComparison.Ordinal);

        // SABİT genişlikler taranır. "max-w-[…]" bir ÜST SINIRDIR, kabı büyütmez;
        // taşma riski yaratan yalnız gerçek w-[…] değerleridir.
        var fixedWidths = Regex.Matches(screen, @"(?<!max-)(?<!min-)\bw-\[(\d+)px\]")
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();
        Assert.NotEmpty(fixedWidths);   // regex bozulursa test sessizce boşa düşmesin
        Assert.All(fixedWidths, w => Assert.True(w <= 80, $"375px ekranda riskli sabit genişlik: {w}px"));
    }

    [Fact]
    public void StickyAltNavigasyon_IcerigiKapatmaz()
    {
        // Alt navigasyonun altında kalan içerik olmasın diye kapta alt boşluk bırakılır.
        Assert.Contains("pb-28", Screen(), StringComparison.Ordinal);
    }

    // ── 20. TAHMİNLERİM KARTI DOĞRU ROTAYA GİDER ─────────────────────────────

    [Fact]
    public void TahminKarti_MatchRotasinaGider_AiRotasiGeriGelmez()
    {
        var card = PredictionCard();

        Assert.Contains("router.push(`/match/${p.matchId}`)", card, StringComparison.Ordinal);
        // Eski AI rotası geri getirilmemeli.
        Assert.DoesNotContain("/ai", card, StringComparison.Ordinal);
    }

    [Fact]
    public void TahminKarti_BitmisMacta_IY_2Y_MS_Gosterir()
    {
        var card = PredictionCard();

        Assert.Contains("İY {fmtScore(p.scoreBreakdown?.halfTime)}", card, StringComparison.Ordinal);
        Assert.Contains("2Y {fmtScore(p.scoreBreakdown?.secondHalf)}", card, StringComparison.Ordinal);
        Assert.Contains("MS {fmtScore(p.scoreBreakdown?.fullTime)}", card, StringComparison.Ordinal);
        // Eksik alanda 0-0 UYDURULMAZ.
        Assert.Contains("if (!s) return \"—\";", card, StringComparison.Ordinal);
    }
}
