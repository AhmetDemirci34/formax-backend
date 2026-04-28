using System;
using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.Interfaces
{
    public interface IMatchOynanmaSnapshotWriter
    {
        Task TryWriteAsync(int matchId, int oynanmaSkoru, DateTime capturedAtUtc, string source, CancellationToken ct = default);

        /// <summary>
        /// FAZ 1.5 — PreMatchFinal Snapshot
        /// Kickoff'a yakın (≤10dk kala) tek bir "maç öncesi final" snapshot üretmek için kullanılır.
        /// Aynı maç için Source=PreMatchFinal zaten yazıldıysa tekrar yazmaz.
        /// </summary>
        Task TryWritePreMatchFinalAsync(int matchId, int oynanmaSkoru, DateTime capturedAtUtc, CancellationToken ct = default);
    }
}
