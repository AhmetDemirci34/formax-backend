using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.Services.Picks;
using Formax.Domain.Constants;
using Formax.Domain.Enums;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Picks
{
    /// <summary>
    /// KULLANICI SEÇİMİ SONUÇLANDIRMA — kalıcı yazıcı.
    ///
    /// NEDEN KALICI, NEDEN OKUMA ANINDA TÜRETME DEĞİL (ölçüldü 07.09.2026): sonuç
    /// yalnız <see cref="Formax.Application.UseCases.Picks.GetUserPredictionsUseCase"/>
    /// içinde, ekran her açıldığında yeniden hesaplanıyordu. Hesabın kendisi doğruydu
    /// ama HİÇBİR YERE yazılmıyordu; yani "ne zaman sonuçlandı" bilinmiyor, seçim
    /// istatistiği (<see cref="UserPick.Status"/>) Pending'de kalıyor ve maçın skoru
    /// sonradan düzeltilirse eski karar iz bırakmadan değişiyordu.
    ///
    /// KURAL TEK YERDE KALIR: doğru/yanlış kararı yine <see cref="PickSettlement"/>
    /// saf fonksiyonundan gelir. Bu servis o kararı yalnız KALICI hâle getirir —
    /// kendi kuralını yazmaz, aksi hâlde iki yerde iki farklı sonuç doğardı.
    ///
    /// TEK YAZICI, TEK KAYNAK (11.09.2026): Tahminlerim okuma yolu artık sonucu yeniden
    /// HESAPLAMAZ; bu servisin yazdığı Status/SettledAtUtc/SettlementNote/SelectionStatus
    /// alanlarını olduğu gibi okur. Sonuçlandırma yalnız burada, bir kez olur.
    ///
    /// UYDURMA YOK: desteklenmeyen markette (<c>Unsettleable</c>) hiçbir doğru/yanlış
    /// yazılmaz. Seçim <c>Pending</c> kalır ve ekran "hesaplanamadı" der; sessizce
    /// "kaybetti" saymak kullanıcının istatistiğini bozardı.
    ///
    /// SIFIR DIŞ İSTEK: yalnız depoyu okur ve yazar. Sağlayıcıya çıkmaz.
    /// </summary>
    public sealed class UserPickSettlementService
    {
        private readonly FormaxDbContext _db;
        private readonly ILogger<UserPickSettlementService> _logger;

        public UserPickSettlementService(FormaxDbContext db, ILogger<UserPickSettlementService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>Bir turun sonucu — teşhis ve test için.</summary>
        public sealed record CycleResult(int Examined, int Settled, int Unsettleable);

        /// <summary>
        /// BİR TUR. Bitmiş maçların henüz sonuçlanmamış seçimlerini kalıcı yazar.
        /// </summary>
        /// <param name="matchId">Verilirse YALNIZ o maç işlenir (test ve elle tetik).</param>
        public async Task<CycleResult> RunCycleAsync(int? matchId = null, CancellationToken ct = default)
        {
            // ADAY: bitmiş maça bağlı, HENÜZ sonuçlanmamış seçimler.
            //
            // SettledAtUtc dolu olan tekrar işlenmez: sonuçlandırma bir kez olur ve
            // her turda aynı satırı yeniden yazmak, "ne zaman sonuçlandı" bilgisini
            // her seferinde bugüne kaydırırdı.
            var query =
                from p in _db.UserPicks
                join m in _db.Matches on p.MatchId equals m.Id
                where m.Status == MatchStatuses.Finished
                   && p.SettledAtUtc == null
                select new { Pick = p, Match = m };

            if (matchId.HasValue) query = query.Where(x => x.Match.Id == matchId.Value);

            var rows = await query.ToListAsync(ct).ConfigureAwait(false);
            if (rows.Count == 0) return new CycleResult(0, 0, 0);

            var nowUtc = DateTime.UtcNow;
            int settled = 0, unsettleable = 0;

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();

                var m = row.Match;
                var score = new PickSettlement.FinalScore(
                    m.HomeScore, m.AwayScore, m.HalfTimeHomeScore, m.HalfTimeAwayScore);

                var outcome = PickSettlement.Settle(row.Pick.MarketKey, score);

                if (outcome == PickSettlement.PickSettlementOutcome.Unsettleable)
                {
                    // HESAPLANAMIYOR. SettledAtUtc BOŞ BIRAKILIR: market desteği
                    // sonradan geldiğinde (ör. olay kaydı hattı) bu seçim yeniden
                    // aday olabilsin. Yanlış bir kapanış, gerçek sonucun önünü keserdi.
                    row.Pick.SelectionStatus = PickSelectionStatuses.Unsettleable;
                    unsettleable++;
                    continue;
                }

                row.Pick.Status = PickSettlement.ToStatus(outcome);
                row.Pick.SelectionStatus = PickSelectionStatuses.Settled;
                row.Pick.SettledAtUtc = nowUtc;
                row.Pick.SettlementNote = $"MS {m.HomeScore}-{m.AwayScore}";
                settled++;
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            if (settled > 0 || unsettleable > 0)
                _logger.LogInformation(
                    "[PICK SETTLEMENT] {Examined} secim · {Settled} sonuclandi · {Unsettleable} hesaplanamadi",
                    rows.Count, settled, unsettleable);

            return new CycleResult(rows.Count, settled, unsettleable);
        }
    }
}
