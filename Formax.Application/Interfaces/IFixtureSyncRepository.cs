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
