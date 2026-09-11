using Formax.Domain.Entities;

namespace Formax.Application.Interfaces;

/// <summary>
/// Persistence contract for FixtureSyncJob.
///
/// Design: batch-oriented to avoid N+1 queries.
/// All lookups accept sets of external IDs and return dictionaries
/// so the job can resolve many records with two DB round-trips total.
/// </summary>
public interface IFixtureSyncRepository
{
    // ── Batch read (2 queries per cycle total) ─────────────────────────────────

    /// <summary>
    /// Returns all teams whose ExternalTeamId is in <paramref name="externalIds"/>.
    /// Key = ExternalTeamId.
    /// </summary>
    Dictionary<string, Team> GetTeamsByExternalIds(IEnumerable<string> externalIds);

    /// <summary>
    /// Returns all matches whose ExternalMatchId is in <paramref name="externalIds"/>.
    /// Key = ExternalMatchId.
    /// </summary>
    Dictionary<string, Match> GetMatchesByExternalIds(IEnumerable<string> externalIds);

    /// <summary>
    /// SONUÇ UZLAŞTIRMA ADAYLARI — başlama saatinin üzerinden yeterli süre (<paramref
    /// name="settleMarginMinutes"/>) geçtiği hâlde depoda KESİN sonucu olmayan maçlar.
    ///
    /// Ölçüt: Status ∈ {NotStarted, Live} VE MatchDate + pay &lt; şimdi.
    /// İptal/ertelenmiş maçlar aday DEĞİLDİR (sonuçları kesinleşmiştir: oynanmadılar).
    /// <paramref name="leagueIds"/> boşsa lig kısıtı uygulanmaz.
    ///
    /// Salt-okunur; sağlayıcıya HİÇBİR istek üretmez. Yalnız "hangi günleri sormalıyım"
    /// sorusunu cevaplamak için kullanılır — istek maç başına değil GÜN başına yapılır.
    /// </summary>
    List<Match> GetStaleResultCandidates(
        DateTime nowUtc,
        int settleMarginMinutes,
        IReadOnlyCollection<int> leagueIds);

    /// <summary>
    /// DOĞRULANMIŞ SEZON METADATA'SI OLAN ligler (LeagueSeasons tablosunda kaydı olanlar).
    ///
    /// Sonuç kurtarma bütçesi sınırlıyken sıranın ölçütü budur: sezon metadata'sı olmayan
    /// bir ligin sezonu ÇÖZÜLEMEZ (SEASON_START_UNRESOLVED), dolayısıyla o ligin sonucu
    /// yayımlanan bir puan durumunu tamamlamaz. Metadata'sı olan ligler önce kapatılır ki
    /// harcanan her istek gerçekten bir tablonun "eksik" damgasını kaldırsın.
    /// </summary>
    HashSet<int> GetLeaguesWithVerifiedSeason();

    /// <summary>
    /// TEKİL FİKSTÜR İSTEĞİNİ ATOMİK REZERVE ET — restart-safe hız sınırı.
    ///
    /// HTTP'den ÖNCE çağrılır. true dönerse istek yapılabilir ve deneme kalıcı deftere
    /// YAZILMIŞTIR; false dönerse istek YAPILMAZ (soğuma dolmamış, günlük tavan dolmuş
    /// ya da başka bir süreç aynı fikstürü almış).
    ///
    /// Rezervasyon tek bir koşullu SQL yazımıdır: iki süreç aynı fikstürü aynı anda
    /// alamaz. Süreç yeniden başlaması defteri SIFIRLAMAZ — tam olarak düzeltilen hata
    /// buydu (her açılışta aynı 10 isteklik patlama tekrarlanıyordu).
    /// </summary>
    /// <param name="purpose"><see cref="Formax.Domain.Constants.FixtureRefreshPurposes"/>.</param>
    bool TryReserveFixtureAttempt(
        string externalMatchId,
        string purpose,
        TimeSpan cooldown,
        int maxPerUtcDay,
        DateTime nowUtc);

    /// <summary>Rezerve edilmiş denemenin sonucunu işaretler (teşhis; bütçeyi etkilemez).</summary>
    void RecordFixtureAttemptOutcome(string externalMatchId, string purpose, DateTime nowUtc, string outcome);

    /// <summary>
    /// BİR FİKSTÜR İÇİN BUGÜNE KADARKİ TOPLAM DENEME — gün sınırından BAĞIMSIZ.
    ///
    /// Slot temelli takvimlerin (kadro yoklaması, maç sonrası video/olay/istatistik)
    /// "kaçıncı denemedeyim?" sorusunu yanıtlar. Restart bu sayıyı sıfırlamaz; kayıt
    /// kalıcı defterdedir. Kayıt yoksa 0 döner.
    /// </summary>
    Task<int> GetFixtureAttemptCountAsync(
        string externalMatchId, string purpose, CancellationToken ct = default);

    /// <summary>
    /// BİR FİKSTÜR İÇİN SON DENEME ANI — "en son ne zaman bakıldı?".
    ///
    /// NEDEN DEFTERDEN, NEDEN VERİ SATIRINDAN DEĞİL (ölçüldü 07.09.2026): kadro ekranı
    /// "son kontrol" bilgisini kadro başlık satırının FetchedAt alanından okuyordu.
    /// Sağlayıcı kadroyu henüz yayımlamamışsa o satır HİÇ YAZILMAZ; yani tam da bilginin
    /// en çok gerektiği durumda (kadro yok) "son kontrol" boş kalıyordu. Deneme kaydı
    /// ise sonuçtan bağımsız olarak kalıcı deftere düşer.
    ///
    /// Hiç denenmemişse null döner — uydurma bir zaman ÜRETİLMEZ.
    /// </summary>
    DateTime? GetLastFixtureAttemptUtc(string externalMatchId, string purpose);

    /// <summary>
    /// DEFTER ÖZETİ — gerçek deneme sayısı, son an, son sonuç ve "hak bitti" kaydı.
    /// Maç detayının video arama durumu YALNIZ bundan türetilir; saatten türetilmez.
    /// Kayıt yoksa <see cref="Formax.Application.Services.PostMatch.FixtureAttemptSummary.None"/>.
    /// </summary>
    Formax.Application.Services.PostMatch.FixtureAttemptSummary GetFixtureAttemptSummary(
        string externalMatchId, string purpose);

    /// <summary>
    /// ENGELLENEN DENEME — sayılmaz.
    ///
    /// Rezervasyon denemeyi baştan sayar. Deneme sağlayıcı plan/rate limit engeli ya da
    /// erişilemeyen kaynak yüzünden GERÇEKLEŞMEDİYSE bu sayım geri alınır: engel, "aradık
    /// ve bulamadık" değildir. Son deneme anı KORUNUR (soğuma süresi işler, kaynak dövülmez)
    /// ve sonuç <paramref name="outcome"/> olarak teşhis için kayda geçer.
    /// </summary>
    void RecordFixtureAttemptBlocked(string externalMatchId, string purpose, DateTime nowUtc, string outcome);

    /// <summary>
    /// GELECEK FİKSTÜR TAKVİM DOĞRULAMA ADAYLARI — kickoff'u geçici olan, başlamamış,
    /// kilitli kapsamdaki ve UI'ın yakın penceresine düşebilecek maçlar.
    ///
    /// Güvenlik penceresi: geçici tarih YANLIŞ olabileceği için aday arama UI penceresinden
    /// birkaç gün GENİŞ tutulur — 4 Eylül'e taşınacak bir maç depoda 6 Eylül'de duruyordu.
    /// </summary>
    List<Match> GetFutureScheduleRefreshCandidates(
        DateTime nowUtc,
        DateTime horizonUtc,
        IReadOnlyCollection<int> leagueIds);

    // ── Write (tracked entities — EF handles the rest) ─────────────────────────

    void AddTeam(Team team);

    void AddMatch(Match match);

    Task SaveChangesAsync(CancellationToken ct = default);
}
