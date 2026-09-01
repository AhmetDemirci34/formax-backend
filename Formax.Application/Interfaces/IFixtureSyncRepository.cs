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

    // ── Write (tracked entities — EF handles the rest) ─────────────────────────

    void AddTeam(Team team);

    void AddMatch(Match match);

    Task SaveChangesAsync(CancellationToken ct = default);
}
