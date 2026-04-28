using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Backtest
{
    /// <summary>
    /// Mini Backfill (FAZ 1.5 sonrası)
    /// Amaç:
    /// - Bitmiş maçlar için (Finished) Source=PreMatchFinal snapshot eksikse, mevcut "maçtan önceki en son" snapshot'tan türetip yazmak.
    /// - Backtest requireSnapshot=true çıktısının 0 sample kalmamasını sağlamak.
    ///
    /// Kurallar:
    /// - Varsa PreMatchFinal yazmaz (tekillik).
    /// - "Maçtan önceki en son" snapshot yoksa dokunmaz.
    /// - Dış veri üretmez; var olan snapshot'ı kopyalar.
    /// </summary>
    public sealed class PreMatchFinalBackfillService
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IMatchOynanmaSnapshotReadRepository _snapshotReadRepository;
        private readonly IMatchOynanmaSnapshotWriter _snapshotWriter;

        public PreMatchFinalBackfillService(
            IMatchReadRepository matchReadRepository,
            IMatchOynanmaSnapshotReadRepository snapshotReadRepository,
            IMatchOynanmaSnapshotWriter snapshotWriter)
        {
            _matchReadRepository = matchReadRepository;
            _snapshotReadRepository = snapshotReadRepository;
            _snapshotWriter = snapshotWriter;
        }

        public async Task<PreMatchFinalBackfillResult> BackfillAsync(int take = 300, CancellationToken ct = default)
        {
            if (take <= 0) take = 300;

            var finished = _matchReadRepository.Query()
                .Where(m => m.Status == "Finished")
                .OrderByDescending(m => m.MatchDate)
                .Take(take)
                .ToList();

            var result = new PreMatchFinalBackfillResult
            {
                Requested = take,
                FinishedMatchesScanned = finished.Count
            };

            foreach (var m in finished)
            {
                ct.ThrowIfCancellationRequested();

                // Zaten PreMatchFinal varsa dokunma
                var existingFinal = _snapshotReadRepository.GetPreMatchFinalBeforeMatchUtc(m.Id, m.MatchDate);
                if (existingFinal != null)
                {
                    result.AlreadyHadPreMatchFinal++;
                    continue;
                }

                // Maçtan önceki en son snapshot'tan türet
                var latest = _snapshotReadRepository.GetLatestBeforeMatchUtc(m.Id, m.MatchDate);
                if (latest == null)
                {
                    result.MissingAnySnapshotBeforeMatch++;
                    continue;
                }

                // CapturedAtUtc zaten <= MatchDate filtresi ile geliyor.
                await _snapshotWriter.TryWritePreMatchFinalAsync(m.Id, latest.OynanmaSkoru, latest.CapturedAtUtc, ct);
                result.Written++;
            }

            result.Note = "Bu işlem, mevcut 'maçtan önceki en son' snapshot'tan Source=PreMatchFinal üretir. Yönlendirme/tahmin değildir.";
            return result;
        }
    }

    public sealed class PreMatchFinalBackfillResult
    {
        public int Requested { get; set; }
        public int FinishedMatchesScanned { get; set; }
        public int Written { get; set; }
        public int AlreadyHadPreMatchFinal { get; set; }
        public int MissingAnySnapshotBeforeMatch { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
