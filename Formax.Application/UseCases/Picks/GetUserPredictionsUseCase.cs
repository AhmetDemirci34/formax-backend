using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Picks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;

namespace Formax.Application.UseCases.Picks
{
    /// <summary>
    /// TAHMİNLERİM — kullanıcının seçtiği olası sonuçlar, maç kartları altında.
    ///
    /// KAYNAK: <c>UserPicks</c> tablosu + maçın GERÇEK durumu. localStorage YOKTUR;
    /// seçim hangi cihazdan yapıldıysa diğerinde de görünür.
    ///
    /// SETTLEMENT DÜRÜSTLÜĞÜ: sonuç yalnız hesaplanabilen marketlerde yazılır
    /// (bkz. <see cref="PickSettlement"/>). Hesaplanamayan seçim "yanlış" SAYILMAZ;
    /// <see cref="UserPickDto.IsCorrect"/> null kalır ve arayüz sonuç göstermez.
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
                    selections.Add(BuildSelection(p, match, isFinished, started));

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
                    CardStatus = isFinished
                        ? PickSelectionStatuses.Settled
                        : started ? PickSelectionStatuses.Pending : PickSelectionStatuses.Active,
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

        private static UserPickDto BuildSelection(
            UserPick p, Match match, bool isFinished, bool started)
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

            if (!isFinished)
            {
                dto.SelectionStatus = started
                    ? PickSelectionStatuses.Pending
                    : PickSelectionStatuses.Active;
                return dto;
            }

            var score = new PickSettlement.FinalScore(
                match.HomeScore, match.AwayScore,
                match.HalfTimeHomeScore, match.HalfTimeAwayScore);

            var outcome = PickSettlement.Settle(p.MarketKey, score);
            if (outcome == PickSettlement.PickSettlementOutcome.Unsettleable)
            {
                // HESAPLANAMIYOR — doğru/yanlış YAZILMAZ. Arayüz bunu "sonuç
                // hesaplanamadı" olarak gösterir; sessizce "yanlış" saymak,
                // kullanıcının istatistiğini bozardı.
                dto.SelectionStatus = PickSelectionStatuses.Unsettleable;
                return dto;
            }

            dto.SelectionStatus = PickSelectionStatuses.Settled;
            dto.IsCorrect = outcome == PickSettlement.PickSettlementOutcome.Won;
            dto.SettlementNote = $"MS {match.HomeScore}-{match.AwayScore}";
            return dto;
        }
    }
}
