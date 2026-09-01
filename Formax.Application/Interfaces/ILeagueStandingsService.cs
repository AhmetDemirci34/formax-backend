using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Standings;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// İÇ KAYNAKLI PUAN DURUMU SERVİSİ.
    ///
    /// MİMARİ (ürün kararı 30.08.2026):
    ///   DB'deki tamamlanmış mevcut sezon lig maçları
    ///     → HourlyStandingsProjectionJob (saatte bir)
    ///     → LeagueStandingsSnapshot (lig+sezon başına TEK satır)
    ///     → Cache
    ///     → Maç Detayı / Form Durumları
    ///
    /// Okuma yolu HESAP YAPMAZ ve DIŞ İSTEK ÜRETMEZ. Kullanıcı tıklaması ne api-football'a
    /// ne başka bir kaynağa gider; yalnız snapshot okunur. Tek istisna: o lig+sezon için
    /// HİÇ snapshot yoksa ilk okuma bir kez üretir (eşzamanlı istekler tek hesabı bekler).
    /// </summary>
    public interface ILeagueStandingsService
    {
        /// <summary>
        /// SENKRON OKUMA — yalnız cache/DB. Snapshot yoksa null döner ve HESAP TETİKLEMEZ.
        /// Maç detayı bu yolu kullanır: kullanıcı tıklaması asla projeksiyon çalıştırmaz.
        /// </summary>
        LeagueStandingsSnapshotDto? GetCached(int leagueId, int seasonId);

        /// <summary>Snapshot cache/DB den okur. Yoksa bir kez üretir (kilitli).</summary>
        Task<LeagueStandingsSnapshotDto?> GetAsync(int leagueId, int seasonId, CancellationToken ct = default);

        /// <summary>Snapshot'ı yeniden hesaplar ve kalıcılaştırır (job ve sonuç tetiklemesi kullanır).</summary>
        Task<LeagueStandingsSnapshotDto?> RefreshAsync(int leagueId, int seasonYear, CancellationToken ct = default);

        /// <summary>Kapsamdaki tüm ligleri mevcut sezon için yeniler. Döndürdüğü sayı = yenilenen lig sayısı.</summary>
        Task<int> RefreshCurrentSeasonAsync(CancellationToken ct = default);

        /// <summary>
        /// SONUÇ TETİKLEMELİ YENİLEME — bir maç sonucu DB'ye KESİN olarak yazıldıktan sonra
        /// çağrılır; saatlik turu beklemeden yalnız o lig+sezon yeniden hesaplanır.
        /// </summary>
        Task RefreshForSettledMatchAsync(int leagueId, System.DateTime matchDateUtc, CancellationToken ct = default);
    }
}
