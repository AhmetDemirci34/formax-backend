using System.Collections.Generic;
using Formax.Application.Coverage;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// GDP League Coverage Intelligence — GDP'nin kendi coverage'ını GERÇEK ingestion verisinden
    /// hesaplaması. Yeni provider/API çağrısı YOK; yalnız mevcut canonical DB okunur. Sonuçlar
    /// Discovery/Refresh yönetimi + diagnostik için kullanılır. Motor/context/factory bunu OKUMAZ.
    /// </summary>
    public interface ILeagueCoverageService
    {
        /// <summary>Tüm ligler için coverage profilleri (skor azalan). Kısa süre cache'lenir.</summary>
        IReadOnlyList<LeagueCoverage> ComputeAll();

        /// <summary>Tek lig için coverage profili (yoksa null).</summary>
        LeagueCoverage? Compute(int leagueId);

        /// <summary>GDP genel hazırlık skorları.</summary>
        GdpReadiness ComputeReadiness();

        /// <summary>Bir ligin Discovery tier'ı (Premium/Standard/Limited/Passive) — coverage skorundan.</summary>
        string TierFor(int leagueId);

        /// <summary>Lig id → genel coverage skoru (0-100) haritası (Refresh önceliklendirme için hızlı erişim).</summary>
        IReadOnlyDictionary<int, int> ScoreMap();

        /// <summary>Trend için son anlık görüntüleri döndürür (in-memory; process başından beri).</summary>
        IReadOnlyList<CoverageSnapshot> Trend();
    }
}
