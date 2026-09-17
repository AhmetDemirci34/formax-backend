using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// MAÇ DETAYI İLK AÇILIŞ GÜVENİLİRLİĞİ — kök nedene çivilenmiş sözleşme.
///
/// ÖLÇÜLEN KÖK NEDEN (03–06.09.2026, gerçek backend):
///   /api/matches/{id}/detail aşama profili — aynı istek:
///     sync=130ms context=15ms evidence=2ms news=37ms narrative(LLM)=13.100ms
///     total=13.284ms
///   Yani ekranın ihtiyaç duyduğu her şey ~184 ms'de hazırdı; kalan 13 saniye
///   yalnız BULUT LLM anlatı beklemesiydi. Soğuk LLM 5,7–13,1 sn arasında
///   dalgalanıyor, istemci zaman aşımı 10 sn → 10 sn'yi aşan açılışlarda istek
///   iptal ediliyor ve "Maç bilgileri şu an yüklenemiyor" görünüyordu. Yenilemek
///   çalışıyordu çünkü anlatı artık önbellekteydi (0,2 sn).
///
/// DÜZELTME: anlatı cevabı BLOKLAMAZ. Bitmiş maçta hiç üretilmez (kilitli özet
/// ekranı anlatı göstermez); diğer maçlarda kısa bir bütçe beklenir, dolarsa cevap
/// anlatısız döner ve üretim arka planda önbelleği ısıtır.
///
/// Bu testler kaynak sözleşmesini korur: bir gün biri "await Task.WhenAll(...)"
/// hâline geri dönerse derleme hattında yakalanır.
/// </summary>
public class MatchDetailNavigationTests
{
    private static string UseCase() => Read("Formax.Application/UseCases/GetMatchDetailAIContextUseCase.cs");
    private static string Hook() => Read("formax-web/hooks/useMatchDetail.ts");
    private static string ApiClient() => Read("formax-web/lib/api/client.ts");
    private static string Page() => Read("formax-web/app/match/[id]/page.tsx");

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"beklenen dosya yok: {relative}");
        return File.ReadAllText(path);
    }

    // ── Kök neden: cevap artık LLM'i beklemez ────────────────────────────────

    // GÜNCELLEME (13.09.2026, kilitli ürün kararı): "Maç sayfası açıldığında LLM çağırma."
    // Önceki sözleşme (3 sn anlatı bütçesi + arka planda sürdürme) daha sıkı bir kurala
    // dönüştü: detay yolu LLM'e HİÇ gitmez; AI Maç Analizi arka planda (MatchAnalysisJob)
    // üretilir ve burada yalnız DB'deki hazır kayıt okunur. Test adları korunarak aynı
    // niyet (cevap LLM beklemez, bitmiş maç LLM tetiklemez, istek token'ına bağlı arka
    // plan görevi yok) yeni sözleşmeyle sabitlenir.

    [Fact]
    public void DetayCevabi_AnlatiyiSinirsizBeklemez()
    {
        var src = UseCase();
        Assert.DoesNotContain("_narrativePipeline", src, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerateAsync(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("await Task.WhenAll(discoverTask, reportTask, inceleTask);", src, StringComparison.Ordinal);
        Assert.Contains("_analysisReader.GetAsync(matchId, ct)", src, StringComparison.Ordinal);
    }

    [Fact]
    public void BitmisMacta_LlmHicCagrilmaz()
    {
        var src = UseCase();
        Assert.Contains("var isFinished = string.Equals(detail.Status, MatchStatuses.Finished", src, StringComparison.Ordinal);
        Assert.Contains("detail.Analysis = isFinished ? null : await _analysisReader.GetAsync(matchId, ct);", src, StringComparison.Ordinal);
        Assert.DoesNotContain("RadarSurface.", src, StringComparison.Ordinal);
    }

    [Fact]
    public void ArkaPlandaSurenAnlati_IstekTokenineBaglanmaz()
    {
        var src = UseCase();
        // İstek yolunda arka plana bırakılan (ateşle-unut) anlatı görevi YOKTUR.
        Assert.DoesNotContain("ContinueWith(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("CancellationToken.None", src.Substring(src.IndexOf("public async Task<MatchDetailDto?> ExecuteAsync", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    // ── Kontrollü retry: en fazla 1, yalnız network/5xx/timeout ──────────────

    [Fact]
    public void KontrolluRetry_EnFazlaBirKez()
    {
        var hook = Hook();

        Assert.Contains("if (failureCount >= 1) return false;", hook, StringComparison.Ordinal);
        Assert.Contains("retryDelay", hook, StringComparison.Ordinal);
        // Sonsuz/agresif retry yok.
        Assert.DoesNotContain("retry: true", hook, StringComparison.Ordinal);
        Assert.DoesNotContain("retry: 3", hook, StringComparison.Ordinal);
    }

    [Fact]
    public void DortYuzHatalar_YenidenDenenmez()
    {
        var hook = Hook();

        // 4xx = istemci hatası (ör. 404). Tekrar denemek yalnız kullanıcıyı bekletir.
        Assert.Contains("if (status !== undefined && status >= 400 && status < 500) return false;",
            hook, StringComparison.Ordinal);
    }

    [Fact]
    public void ZamanAsimi_KorlemesineBuyutulmedi()
    {
        // Düzeltme "timeout'u büyüt" DEĞİLDİR: istemci sınırı 10 sn'de kaldı, sunucu
        // tarafındaki bekleme kaldırıldı. Sınır büyütülseydi kullanıcı 13 saniye
        // spinner'a bakardı.
        Assert.Contains("timeout: 10_000", ApiClient(), StringComparison.Ordinal);
    }

    // ── Duplicate istek / unmount sonrası state ──────────────────────────────

    [Fact]
    public void AyniMacIcin_TekSorguAnahtari()
    {
        var hook = Hook();

        // React Query aynı queryKey için isteği tekilleştirir: StrictMode'un çift
        // mount'u ikinci bir HTTP isteği DOĞURMAZ (ölçüldü: her açılışta tam 1
        // /detail isteği).
        Assert.Contains("matchDetailKey = (matchId: number) => [\"match\", matchId] as const", hook, StringComparison.Ordinal);
        Assert.Contains("queryKey: matchDetailKey(matchId)", hook, StringComparison.Ordinal);
        Assert.Contains("enabled: enabled && matchId > 0", hook, StringComparison.Ordinal);
    }

    [Fact]
    public void EkranManuelState_YazmazUnmountSonrasi()
    {
        var page = Page();

        // Veri yolu tamamen React Query'nindir; sayfa kendi setState'iyle istek sonucu
        // yazmaz, dolayısıyla unmount sonrası state yazma yüzeyi YOKTUR.
        Assert.Contains("const { data: match, isLoading, isError, refetch } = useMatchDetail(matchId);",
            page, StringComparison.Ordinal);
        Assert.DoesNotContain("setMatch(", page, StringComparison.Ordinal);
        Assert.DoesNotContain("useEffect(() => { getMatchDetail", page, StringComparison.Ordinal);
    }

    [Fact]
    public void RotaParametresi_IlkRenderdaHazir()
    {
        var page = Page();

        // Next 16'da params bir Promise'tir; use(params) ile ÇÖZÜLEREK okunur.
        // matchId ilk render'da hazırdır — "ilk render'da parametre yok" yarışı yoktur.
        Assert.Contains("const { id } = use(params);", page, StringComparison.Ordinal);
        Assert.Contains("const matchId = parseInt(id, 10);", page, StringComparison.Ordinal);
    }

    [Fact]
    public void EskiAiRotasi_KullanilmazVeSayfaAcilisi_SaglayiciCagirmaz()
    {
        var page = Page();

        // Sayfa açılışında yalnız /detail çağrılır; keşif/sağlayıcı ucu yoktur.
        Assert.DoesNotContain("api-football", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/video/run", page, StringComparison.Ordinal);
        Assert.DoesNotContain("router.push(`/match/${matchId}/ai`)", page, StringComparison.Ordinal);
    }
}
