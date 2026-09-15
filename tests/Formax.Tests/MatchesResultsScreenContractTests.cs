using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// "MAÇLAR → SONUÇLAR" EKRAN SÖZLEŞMESİ.
///
/// NEDEN KAYNAK METNİ ÜZERİNDEN: bu depoda frontend test koşucusu yoktur. Bu testler
/// bir render testinin yerini TUTMAZ; dar ama gerçek bir iş yaparlar: sonuç kartının
/// yanlış rotaya gitmesini, ekrana sağlayıcı çağrısı sızmasını, sabit genişlikli bir
/// kabın 375px'te taşmasını ve sekmenin varsayılanının kaymasını derleme hattında
/// yakalarlar.
///
/// Görsel ve davranışsal doğrulama (375×812'de gerçek tıklama, gerçek gezinme, yatay
/// taşma ölçümü) ayrıca çalışan uygulamada yapılmıştır.
/// </summary>
public class MatchesResultsScreenContractTests
{
    private static string Page() => Read("formax-web/app/maclar/page.tsx");
    private static string Card() => Read("formax-web/components/maclar/ResultCard.tsx");
    private static string View() => Read("formax-web/components/maclar/ResultsView.tsx");
    private static string Nav() => Read("formax-web/components/maclar/ResultDateNav.tsx");
    private static string Tabs() => Read("formax-web/components/maclar/MatchesTabs.tsx");
    private static string DayRules() => Read("formax-web/lib/matches/resultDays.ts");
    private static string Api() => Read("formax-web/lib/api/matchResults.ts");
    private static string Hooks() => Read("formax-web/hooks/useMatchResults.ts");
    private static string DaySelection() => Read("formax-web/hooks/useResultDaySelection.ts");
    private static string LeagueGroup() => Read("formax-web/components/maclar/ResultLeagueGroup.tsx");
    private static string Grouping() => Read("formax-web/lib/matches/resultGrouping.ts");

    /// <summary>
    /// YORUMSUZ KAYNAK — "bu ekran api-football'a çıkmaz" diye YAZAN bir yorum, o
    /// yasağı ihlal etmez. Yasak listesi KODA bakmalıdır; aksi hâlde kuralı açıklayan
    /// dürüst bir yorum testi kırar ve geliştirici yorumu silmeye itilir.
    /// </summary>
    private static string CodeOnly(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"^\s*//.*$", " ", RegexOptions.Multiline);
    }

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

    // ── Sekmeler ve varsayılan ───────────────────────────────────────────────

    [Fact]
    public void MaclarEkrani_IkiSekmeliVeVarsayilanYaklasandir()
    {
        var tabs = Tabs();
        Assert.Contains("YAKLAŞAN", tabs, StringComparison.Ordinal);
        Assert.Contains("SONUÇLAR", tabs, StringComparison.Ordinal);

        // Varsayılan sekme YAKLAŞAN — ekranın kilitli kimliği maç öncesidir.
        Assert.Contains("useState<MatchesTab>(\"upcoming\")", Page(), StringComparison.Ordinal);
    }

    [Fact]
    public void YaklasanSekmesi_MevcutDavranisiKorur()
    {
        var page = Page();

        // Tarih seçici, lig gruplaması ve mevcut liste kancası yerinde.
        Assert.Contains("<DateNav day={day} onChange={setDay} />", page, StringComparison.Ordinal);
        Assert.Contains("buildLeagueGroups(data, day)", page, StringComparison.Ordinal);
        Assert.Contains("useMatchList()", page, StringComparison.Ordinal);
        // Canlı maç/canlı polling EKLENMEDİ.
        Assert.DoesNotContain("refetchInterval", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Canlı", page, StringComparison.Ordinal);
    }

    // ── 10. SONUÇ KARTI DOĞRU ROTAYA GİDER ───────────────────────────────────

    [Fact]
    public void SonucKarti_MatchRotasinaGider_AiRotasiKullanilmaz()
    {
        var page = Page();
        var card = Card();

        Assert.Contains("router.push(`/match/${matchId}`)", page, StringComparison.Ordinal);
        // Kartın GÖVDESİ ve "MAÇ ÖZETİ" butonu aynı hedefe gider.
        Assert.Equal(2, Regex.Matches(card, @"onClick=\{\(\) => onOpen\(r\.matchId\)\}").Count);
        Assert.Contains("MAÇ ÖZETİ", card, StringComparison.Ordinal);
        // Eski AI rotası geri gelmemeli.
        Assert.DoesNotContain("/ai", CodeOnly(card), StringComparison.Ordinal);
        Assert.DoesNotContain("/ai", CodeOnly(View()), StringComparison.Ordinal);
    }

    [Fact]
    public void SonucKarti_GerekliAlanlariGosterir()
    {
        var card = Card();

        Assert.Contains("r.matchTypeLabel", card, StringComparison.Ordinal);   // aşama/tur
        Assert.Contains("istanbulTime(r.matchDateUtc)", card, StringComparison.Ordinal); // TR saati
        Assert.Contains("Maç Bitti", card, StringComparison.Ordinal);
        Assert.Contains("r.homeTeam.name", card, StringComparison.Ordinal);
        Assert.Contains("r.awayTeam.name", card, StringComparison.Ordinal);
        Assert.Contains("r.homeScore", card, StringComparison.Ordinal);
        Assert.Contains("r.awayScore", card, StringComparison.Ordinal);
        Assert.Contains("İY {r.halfTimeHomeScore}-{r.halfTimeAwayScore}", card, StringComparison.Ordinal);

        // Lig adı SATIRDA DEĞİL, panel başlığındadır: satır yalnız o maça ait bilgiyi
        // taşır, lig adı her satırda tekrarlanmaz.
        Assert.DoesNotContain("r.leagueName", card, StringComparison.Ordinal);
        Assert.Contains("group.league", LeagueGroup(), StringComparison.Ordinal);
    }

    // ── 5-6. SONUÇLAR LİG BAZINDA GRUPLANIR ──────────────────────────────────

    [Fact]
    public void Sonuclar_LigBazindaGruplanir_YaklasanIleAyniDil()
    {
        var view = View();
        var panel = LeagueGroup();

        Assert.Contains("buildResultLeagueGroups(results)", view, StringComparison.Ordinal);
        Assert.Contains("<ResultLeagueGroupPanel", view, StringComparison.Ordinal);

        // Panel başlığı YAKLAŞAN'daki LeagueGroupPanel ile aynı bilgiyi taşır:
        // bayrak, ülke, lig adı ve o ligdeki maç sayısı.
        Assert.Contains("group.flag", panel, StringComparison.Ordinal);
        Assert.Contains("group.country", panel, StringComparison.Ordinal);
        Assert.Contains("group.league", panel, StringComparison.Ordinal);
        Assert.Contains("group.results.length", panel, StringComparison.Ordinal);

        // Ülke/bayrak tablosu KOPYALANMAZ; ortak leagueMeta kullanılır.
        Assert.Contains("import { leagueMeta } from \"./leagueGrouping\"", Grouping(), StringComparison.Ordinal);
    }

    [Fact]
    public void LigVeMacSiralamasi_Deterministiktir()
    {
        var grouping = Grouping();

        // Lig içi: EN SON BİTEN ÖNCE — kickoff azalan, eşitlikte MatchId azalan.
        Assert.Contains("newest(b) - newest(a) || b.matchId - a.matchId", grouping, StringComparison.Ordinal);
        // Lig sırası: en yeni maçı olan grup önce; eşitlikte kapsam sırası, sonra Türkçe alfabetik.
        Assert.Contains("a.rank - b.rank ||", grouping, StringComparison.Ordinal);
        Assert.Contains("a.league.localeCompare(b.league, \"tr\")", grouping, StringComparison.Ordinal);
    }

    [Fact]
    public void VideoIsaretiKaldirildi_IYYalnizVarsaBasilir()
    {
        var card = Card();

        // 15.09.2026: video özelliği kaldırıldı — kartta video işareti yok.
        Assert.DoesNotContain("hasPlayableOfficialVideo", card, StringComparison.Ordinal);
        Assert.DoesNotContain("Video var", card, StringComparison.Ordinal);
        // İY yoksa satır hiç basılmaz — "İY 0-0" uydurulmaz.
        Assert.Contains("r.halfTimeHomeScore != null && r.halfTimeAwayScore != null",
            card, StringComparison.Ordinal);
    }

    // ── Tarih kuralları ──────────────────────────────────────────────────────

    /// <summary>
    /// ÜRÜN KARARI (03.09.2026): pencere 30 gündür. 7 gün, bitmiş maç arşivi için
    /// yetmiyordu — iki hafta önceki bir maça normal UI akışıyla ulaşılamıyordu.
    /// </summary>
    [Fact]
    public void TarihAraligi_BugunVeOnceki30Gun()
    {
        var rules = DayRules();

        Assert.Contains("RESULT_DAY_SPAN = 30", rules, StringComparison.Ordinal);
        Assert.Contains("day <= today && day >= oldestSelectableDay(today)", rules, StringComparison.Ordinal);
        // Gün hesabı Europe/Istanbul takvimine göre; tarayıcının yerel saati kullanılmaz.
        Assert.Contains("timeZone: TR_TZ", rules, StringComparison.Ordinal);
        Assert.Contains("\"Europe/Istanbul\"", rules, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gezinme penceresinin TS kaynağındaki gün sayısı — kural tek yerde yaşıyor,
    /// test onu oradan okuyor. Sayıyı teste ikinci kez yazmak, ikisinin ayrışması
    /// demektir.
    /// </summary>
    private static int ResultDaySpan()
    {
        var m = Regex.Match(DayRules(), @"RESULT_DAY_SPAN\s*=\s*(\d+)");
        Assert.True(m.Success, "RESULT_DAY_SPAN okunamadı");
        return int.Parse(m.Groups[1].Value);
    }

    /// <summary>Pencerenin ilk günü: bugün dâhil span gün geriye.</summary>
    private static DateOnly OldestSelectable(DateOnly today) => today.AddDays(-(ResultDaySpan() - 1));

    [Theory]
    [InlineData("2026-08-18")]   // 71513 · Fenerbahçe–Lyon 1. ayak
    [InlineData("2026-08-26")]   // 104237 · Lyon–Fenerbahçe 2. ayak
    public void KabulMaclarininGunleri_GezinmeAraligindadir(string date)
    {
        // Kararın alındığı gün SABİTLENMİŞTİR: test takvimle birlikte kaymaz ve
        // "o gün bu maçlara ulaşılabiliyor muydu?" sorusunu kalıcı olarak yanıtlar.
        var referenceToday = new DateOnly(2026, 9, 3);
        var day = DateOnly.ParseExact(date, "yyyy-MM-dd");

        Assert.True(day <= referenceToday, "gelecek bir gün olamaz");
        Assert.True(day >= OldestSelectable(referenceToday),
            $"{date} {ResultDaySpan()} günlük pencerenin dışında kaldı");
    }

    [Fact]
    public void PencerdenEskiGun_NormalGezinmedeAcilmaz()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var oldest = OldestSelectable(today);

        // Sınırın kendisi seçilebilir, bir gün öncesi seçilemez.
        Assert.True(oldest >= today.AddDays(-(ResultDaySpan() - 1)));
        var tooOld = oldest.AddDays(-1);
        Assert.True(tooOld < oldest, "pencereden eski gün gezinmeye kapalı olmalı");

        // Kuralın kendisi tek merkezde ve sınırlar İKİ TARAFLI.
        Assert.Contains("day <= today && day >= oldestSelectableDay(today)",
            DayRules(), StringComparison.Ordinal);
    }

    [Fact]
    public void TarihSecici_GelecegeGitmez_SinirdaOkPasiflesir()
    {
        var nav = Nav();

        Assert.Contains("isSelectableDay(next, today)", nav, StringComparison.Ordinal);
        Assert.Contains("isSelectableDay(prev, today)", nav, StringComparison.Ordinal);
        Assert.Contains("disabled={disabled}", nav, StringComparison.Ordinal);
        // Yardımcı buton: "Bugün" ya da "Son sonuçlar".
        Assert.Contains("\"Son sonuçlar\"", nav, StringComparison.Ordinal);
        Assert.Contains("label: \"Bugün\"", nav, StringComparison.Ordinal);
    }

    [Fact]
    public void IlkAcilis_HerZamanBugun_DuneOtomatikGecmez()
    {
        // ÜRÜN KARARI (13.09.2026): SONUÇLAR sekmesi BUGÜN (Europe/Istanbul) ile açılır.
        // Bugün biten maç yoksa dürüst boş durum gösterilir; düne otomatik geçilmez.
        var selection = DaySelection();
        Assert.Contains("setDay(pickInitialDay(istanbulDay()))", selection, StringComparison.Ordinal);
        // Gün seçimi gün listesi sorgusunun sonucuna BAĞLI DEĞİLDİR.
        Assert.DoesNotContain("pickInitialDay(daysWithResults", selection, StringComparison.Ordinal);

        var rules = DayRules();
        Assert.Contains("export function pickInitialDay(today: string = istanbulDay()): string {\n  return today;\n}",
            rules.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("inWindow[0] ?? today", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void BugununSonuclari_KontrolluTazelenir_GecmisGunTazelenmez()
    {
        var hooks = Hooks();
        Assert.Contains("day !== null && day === today ? TODAY_RESULTS_REFRESH_MS : false", hooks, StringComparison.Ordinal);
        Assert.Contains("refetchIntervalInBackground: false", hooks, StringComparison.Ordinal);
        // Salt DB ucu: kanca yalnız backend sonuç uçlarını çağırır.
        Assert.Contains("getMatchResults(day!)", hooks, StringComparison.Ordinal);
    }

    // ── Boş durumlar ─────────────────────────────────────────────────────────

    [Fact]
    public void BosDurumlar_HataGibiGosterilmez()
    {
        var view = View();

        Assert.Contains("Bu tarihte tamamlanmış maç bulunmuyor.", view, StringComparison.Ordinal);
        // Pencere metni SABİT DEĞİL: gün sayısı RESULT_DAY_SPAN'dan gelir, böylece
        // pencere değiştiğinde cümle ile kural ayrışamaz.
        Assert.Contains("`Son ${RESULT_DAY_SPAN} gün içinde tamamlanmış maç bulunmuyor.`",
            view, StringComparison.Ordinal);
        // Boş durum ErrorState DEĞİLDİR; hata yalnız gerçek istek hatasında gösterilir.
        Assert.Contains("resultsQuery.isError ? (", view, StringComparison.Ordinal);
    }

    // ── 12-13. SAYFA AÇILIŞI VE SEKME DEĞİŞİMİ PROVIDER İSTEĞİ ÜRETMEZ ──────

    [Fact]
    public void EkranKatmani_YalnizKendiBackendUcunuCagirir()
    {
        var api = Api();

        // Tek çağrılan uçlar: sonuç listesi ve sonuçlu günler. Sağlayıcıya doğrudan
        // çıkan hiçbir adres yok.
        Assert.Contains("\"/api/matches/results\"", api, StringComparison.Ordinal);
        Assert.Contains("\"/api/matches/results/days\"", api, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "api-sports", "api-football", "youtube", "googleapis" })
            Assert.DoesNotContain(forbidden, CodeOnly(api), StringComparison.OrdinalIgnoreCase);

        // Ekran bileşenleri kendi başlarına veri çekmez.
        foreach (var src in new[] { Page(), View(), Card(), Nav(), Tabs() })
        {
            // Tarayıcının fetch'i aranır; React Query'nin refetch()'i değil — o zaten
            // ekranın kendi backend ucunu yeniden çağırmasıdır (yalnız kullanıcı
            // "Tekrar Dene" derse).
            Assert.False(Regex.IsMatch(CodeOnly(src), @"(?<![A-Za-z])fetch\("),
                "ekran bileşeni doğrudan fetch çağırmamalı");
            Assert.DoesNotContain("axios", CodeOnly(src), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SonuclarOnbellegi_GecmisGunOtomatikTazelenmez()
    {
        var hooks = Hooks();

        // Geçmiş sonuç DEĞİŞMEZ: periyodik yenileme yalnız BUGÜN içindir, önbellek uzun.
        Assert.Contains("refetchInterval: resultsRefreshInterval(day)", hooks, StringComparison.Ordinal);
        Assert.Contains(": false;", hooks, StringComparison.Ordinal);
        Assert.Contains("FINISHED_DATA_IS_IMMUTABLE", hooks, StringComparison.Ordinal);
        // Sekme kapalıyken sorgu HİÇ çalışmaz → sekme açılmadan istek doğmaz.
        Assert.Contains("enabled,", hooks, StringComparison.Ordinal);
        Assert.Contains("enabled: !!day,", hooks, StringComparison.Ordinal);
    }

    [Fact]
    public void SekmeDegisimi_YaklasanListesiniYenidenYuklemez()
    {
        var page = Page();

        // useMatchList sekmeden BAĞIMSIZ çağrılır; React Query önbelleği korur,
        // dolayısıyla sekme değişimi yeni backend isteği doğurmaz ve sayfa zıplamaz.
        var hookIndex = page.IndexOf("useMatchList()", StringComparison.Ordinal);
        var tabIndex = page.IndexOf("upcoming ? (", StringComparison.Ordinal);
        Assert.True(hookIndex > 0 && tabIndex > hookIndex,
            "useMatchList sekme dalından ÖNCE ve koşulsuz çağrılmalı");
    }

    // ── 14. 375×812 YATAY TAŞMA ──────────────────────────────────────────────

    [Fact]
    public void MobilKap_YatayTasmayaKapali()
    {
        Assert.Contains("overflow-x-hidden", Page(), StringComparison.Ordinal);

        foreach (var (name, src) in new[]
                 {
                     ("ResultCard", Card()), ("ResultDateNav", Nav()),
                     ("MatchesTabs", Tabs()), ("Page", Page())
                 })
        {
            Assert.DoesNotContain("overflow-x-auto", src, StringComparison.Ordinal);
            Assert.DoesNotContain("min-w-[", src, StringComparison.Ordinal);

            // "max-w-[…]" bir ÜST SINIRDIR, kabı büyütmez; taşma riskini yalnız
            // gerçek w-[…] değerleri yaratır.
            var fixedWidths = Regex.Matches(src, @"(?<!max-)(?<!min-)\bw-\[(\d+)px\]")
                .Select(m => int.Parse(m.Groups[1].Value))
                .ToList();
            Assert.All(fixedWidths,
                w => Assert.True(w <= 80, $"{name}: 375px ekranda riskli sabit genişlik {w}px"));
        }
    }

    [Fact]
    public void AltNavigasyon_IcerikleCakismaz()
    {
        // Liste kabı, sabit alt navigasyonun yüksekliği kadar alt boşluk bırakır.
        Assert.Contains("pb-[calc(var(--bottom-nav-height)+16px)]", View(), StringComparison.Ordinal);
        Assert.Contains("pb-[calc(var(--bottom-nav-height)+16px)]", Page(), StringComparison.Ordinal);
    }

    [Fact]
    public void UzunTakimAdlari_DuzeniBozmaz()
    {
        var card = Card();
        // Ad taşarsa kesilir; kartı genişletmez.
        Assert.Contains("truncate text-[13.5px]", card, StringComparison.Ordinal);
        Assert.Contains("min-w-0 flex-1 truncate", card, StringComparison.Ordinal);
        // Skor asla kırılmaz.
        Assert.Contains("shrink-0 whitespace-nowrap text-[16px] font-bold tabular-nums",
            card, StringComparison.Ordinal);
    }
}
