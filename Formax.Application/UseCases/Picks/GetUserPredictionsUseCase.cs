using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.UseCases.Picks
{
    /// <summary>
    /// TAHMİNLERİM — kullanıcının seçtiği olası sonuçlar, maç kartları altında.
    ///
    /// KAYNAK: <c>UserPicks</c> tablosu + maçın GERÇEK durumu. localStorage YOKTUR;
    /// seçim hangi cihazdan yapıldıysa diğerinde de görünür.
    ///
    /// SALT OKUMA — SONUÇ BURADA HESAPLANMAZ (11.09.2026): doğru/yanlış kararı YALNIZ
    /// sonuçlandırma işi (<c>UserPickSettlementService</c>) tarafından bir kez verilir ve
    /// <see cref="UserPick.Status"/> / <see cref="UserPick.SettledAtUtc"/> /
    /// <see cref="UserPick.SettlementNote"/> alanlarına yazılır. Bu sorgu o kalıcı alanları
    /// OLDUĞU GİBİ okur. Önceden her GET'te <c>PickSettlement.Settle</c> yeniden çağrılıyordu:
    /// kalıcı kayıt hiç okunmuyor, kural değişirse tamamlanmış seçimin sonucu sessizce
    /// değişebiliyordu.
    ///
    /// Henüz sonuçlandırılmamış seçim (maç bitmiş olsa bile iş henüz geçmediyse)
    /// "Bekleyen" görünür; okuma yolu sonucu tahmin edip doldurmaz.
    /// Hesaplanamayan seçim "yanlış" SAYILMAZ; <see cref="UserPickDto.IsCorrect"/> null kalır.
    /// </summary>
    public sealed class GetUserPredictionsUseCase
    {
        private readonly IUserPickRepository _picks;
        private readonly IMatchReadRepository _matches;

        public GetUserPredictionsUseCase(IUserPickRepository picks, IMatchReadRepository matches)
        {
            _picks = picks;
            _matches = matches;
        }

        public async Task<List<UserPredictionCardDto>> ExecuteAsync(
            string userId, DateTime nowUtc, CancellationToken ct = default)
        {
            var picks = await _picks.GetByUserAsync(userId, ct).ConfigureAwait(false);
            if (picks.Count == 0) return new List<UserPredictionCardDto>();

            var cards = new List<UserPredictionCardDto>();

            foreach (var group in picks.GroupBy(p => p.MatchId))
            {
                var match = _matches.GetById(group.Key);
                if (match == null) continue;   // maç silinmişse kart uydurulmaz

                var isFinished = string.Equals(match.Status, MatchStatuses.Finished,
                    StringComparison.OrdinalIgnoreCase);
                var started = nowUtc >= match.MatchDate;

                var selections = new List<UserPickDto>();
                foreach (var p in group.OrderBy(p => p.CreatedAt))
                    selections.Add(BuildSelection(p, match, begun: started || isFinished));

                // KART "TAMAMLANDI" yalnız maç bitmiş VE her seçimin kalıcı son durumu
                // yazılmışsa (sonuçlandı ya da hesaplanamadı). İş henüz geçmediyse kart
                // "Bekleyen" kalır.
                var allClosed = selections.All(s =>
                    s.SelectionStatus == PickSelectionStatuses.Settled ||
                    s.SelectionStatus == PickSelectionStatuses.Unsettleable);

                cards.Add(new UserPredictionCardDto
                {
                    MatchId = match.Id,
                    HomeTeam = match.HomeTeam?.Name ?? string.Empty,
                    AwayTeam = match.AwayTeam?.Name ?? string.Empty,
                    HomeTeamLogoUrl = match.HomeTeam?.LogoUrl,
                    AwayTeamLogoUrl = match.AwayTeam?.LogoUrl,
                    League = match.League ?? string.Empty,
                    MatchDateUtc = match.MatchDate,
                    Status = match.Status,
                    // SKOR YALNIZ BİTMİŞ MAÇTA. Başlamamış maçın 0-0'ı skor değildir.
                    HomeScore = isFinished ? match.HomeScore : null,
                    AwayScore = isFinished ? match.AwayScore : null,
                    HalfTimeHomeScore = isFinished ? match.HalfTimeHomeScore : null,
                    HalfTimeAwayScore = isFinished ? match.HalfTimeAwayScore : null,
                    SecondHalfHomeScore = isFinished ? SecondHalf(match.HomeScore, match.HalfTimeHomeScore) : null,
                    SecondHalfAwayScore = isFinished ? SecondHalf(match.AwayScore, match.HalfTimeAwayScore) : null,
                    CardStatus = isFinished && allClosed
                        ? PickSelectionStatuses.Settled
                        : started || isFinished ? PickSelectionStatuses.Pending : PickSelectionStatuses.Active,
                    Selections = selections
                });
            }

            // Yaklaşan maçlar önce (en yakın üstte), sonra tamamlananlar (en yeni üstte).
            return cards
                .OrderBy(c => c.CardStatus == PickSelectionStatuses.Settled ? 1 : 0)
                .ThenBy(c => c.CardStatus == PickSelectionStatuses.Settled
                    ? -c.MatchDateUtc.Ticks
                    : c.MatchDateUtc.Ticks)
                .ToList();
        }

        /// <summary>MS − İY; İY yoksa ya da sonuç negatifse (tutarsız kayıt) null.</summary>
        private static int? SecondHalf(int fullTime, int? halfTime)
            => halfTime is int ht && fullTime - ht >= 0 ? fullTime - ht : null;

        /// <summary>
        /// Seçimin görünümü — YALNIZ kalıcı alanlardan. Hiçbir sonuç burada hesaplanmaz.
        /// </summary>
        /// <param name="begun">Maç başladı ya da bitti (seçim artık "Aktif" değil).</param>
        private static UserPickDto BuildSelection(UserPick p, Match match, bool begun)
        {
            var dto = new UserPickDto
            {
                Id = p.Id,
                MatchId = p.MatchId,
                MarketKey = p.MarketKey ?? string.Empty,
                MarketGroup = p.MarketGroup,
                Label = p.PickLabel,
                ProbabilityPercent = p.ProbabilityPercent ?? p.Confidence,
                Odd = p.OddAtSelection,
                CreatedAtUtc = p.CreatedAt,
                MatchKickoffUtc = p.MatchKickoffUtc ?? match.MatchDate
            };

            // SONUÇLANDI: sonuçlandırma işinin yazdığı karar olduğu gibi döner.
            // Status Win/Lose dışında bir değerle SettledAtUtc dolu bir satır tutarsızdır;
            // böyle bir satıra doğru/yanlış iddiası UYDURULMAZ, "Bekleyen" kalır.
            if (p.SettledAtUtc.HasValue && p.Status is PickStatus.Win or PickStatus.Lose)
            {
                dto.SelectionStatus = PickSelectionStatuses.Settled;
                dto.IsCorrect = p.Status == PickStatus.Win;
                dto.SettlementNote = p.SettlementNote;
                dto.SettledAtUtc = p.SettledAtUtc;
                return dto;
            }

            // HESAPLANAMADI: iş bu marketi sonuçlandıramadığını kalıcı yazdı. Doğru/yanlış
            // YOK; arayüz "Sonuç hesaplanamadı" der, sessizce "yanlış" saymaz.
            if (p.SelectionStatus == PickSelectionStatuses.Unsettleable)
            {
                dto.SelectionStatus = PickSelectionStatuses.Unsettleable;
                return dto;
            }

            dto.SelectionStatus = begun ? PickSelectionStatuses.Pending : PickSelectionStatuses.Active;
            return dto;
        }
    }
}
