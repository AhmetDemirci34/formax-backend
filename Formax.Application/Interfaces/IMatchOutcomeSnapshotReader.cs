using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// OLASI SONUÇ SNAPSHOT OKUYUCUSU — kullanıcı yolu (Keşfet, Maç Detayı) yalnız bu arayüzü kullanır: salt DB, hesaplama YOK.
    /// </summary>
    public interface IMatchOutcomeSnapshotReader
    {
        /// <summary>Maçın güncel snapshot'ı; hiç üretilmediyse Status="Pending" olan boş yük.</summary>
        Task<OutcomeSnapshotDto> GetCurrentAsync(int matchId, CancellationToken ct = default);

        /// <summary>Birden çok maçın güncel snapshot'ları (tek sorgu). Snapshot'ı olmayan maç sözlükte yer almaz.</summary>
        Task<IReadOnlyDictionary<int, OutcomeSnapshotDto>> GetCurrentForMatchesAsync(IReadOnlyCollection<int> matchIds, CancellationToken ct = default);
    }
}
