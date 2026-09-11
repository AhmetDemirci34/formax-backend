using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Follow;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;

namespace Formax.Application.UseCases.Follow
{
    /// <summary>
    /// "TAKİP ETTİĞİM MAÇLAR" EKRANI — tek veri kaynağı.
    ///
    /// NEDEN AYRI BİR OKUMA: mevcut <c>GET /api/follows/me</c> ucu maç listesi
    /// sözleşmesini (MatchListItemDto) döndürür ve durumu SAATTEN türetir
    /// (kickoff + 105 dk → "Live"). Bu ekran için iki sorun çıkarıyordu:
    ///  • Canlı veri KAPALIYKEN (LiveMatchData:Enabled=false) saatten "Live" üretmek,
    ///    depoda olmayan bir durumu uydurmaktır (bkz. Zaman Sözleşmesi).
    ///  • Ekranın ihtiyacı olan tek şey GERÇEK durum ve GERÇEK skordur; her çağrıda
    ///    Sapma Motoru çalıştırmaya gerek yoktur.
    ///
    /// KAPSAM: YALNIZ kullanıcının kendi maç takipleri. Takım ve lig takipleri bu
    /// ekranda GÖSTERİLMEZ — kayıtları silinmez, yalnız burada okunmaz.
    ///
    /// SIRA: önce yaklaşanlar (en yakın üstte), sonra tamamlananlar (en yeni üstte).
    /// </summary>
    public sealed class GetFollowedMatchesScreenUseCase
    {
        private readonly IUserMatchFollowRepository _follows;
        private readonly IMatchReadRepository _matches;

        public GetFollowedMatchesScreenUseCase(
            IUserMatchFollowRepository follows, IMatchReadRepository matches)
        {
            _follows = follows;
            _matches = matches;
        }

        public async Task<FollowedMatchesScreenDto> ExecuteAsync(
            int userId, DateTime nowUtc, CancellationToken ct = default)
        {
            var followedIds = (await _follows.GetByUserAsync(userId).ConfigureAwait(false))
                .Select(f => f.MatchId)
                .Distinct()
                .ToList();

            var screen = new FollowedMatchesScreenDto();
            if (followedIds.Count == 0) return screen;

            foreach (var id in followedIds)
            {
                var m = _matches.GetById(id);
                if (m == null) continue;   // maç yoksa kart UYDURULMAZ

                // DURUM DEPODAN OKUNUR, saatten türetilmez.
                var isFinished = string.Equals(m.Status, MatchStatuses.Finished,
                    StringComparison.OrdinalIgnoreCase);

                var card = new FollowedMatchCardDto
                {
                    MatchId = m.Id,
                    League = m.League ?? string.Empty,
                    LeagueId = m.LeagueId,
                    MatchDateUtc = m.MatchDate,
                    HomeTeam = m.HomeTeam?.Name ?? string.Empty,
                    AwayTeam = m.AwayTeam?.Name ?? string.Empty,
                    HomeTeamLogoUrl = m.HomeTeam?.LogoUrl,
                    AwayTeamLogoUrl = m.AwayTeam?.LogoUrl,
                    Status = m.Status,
                    // SKOR YALNIZ BİTMİŞ MAÇTA. Başlamamış maçın 0-0'ı skor değildir.
                    HomeScore = isFinished ? m.HomeScore : null,
                    AwayScore = isFinished ? m.AwayScore : null,
                    HalfTimeHomeScore = isFinished ? m.HalfTimeHomeScore : null,
                    HalfTimeAwayScore = isFinished ? m.HalfTimeAwayScore : null
                };

                if (isFinished) screen.Finished.Add(card);
                else screen.Upcoming.Add(card);
            }

            // YAKLAŞAN: en yakın maç üstte. TAMAMLANAN: en yeni maç üstte.
            // Eşitlikte MatchId — aynı dakikada başlayan iki maç her açılışta AYNI sırada.
            screen.Upcoming = screen.Upcoming
                .OrderBy(c => c.MatchDateUtc).ThenBy(c => c.MatchId).ToList();
            screen.Finished = screen.Finished
                .OrderByDescending(c => c.MatchDateUtc).ThenByDescending(c => c.MatchId).ToList();

            return screen;
        }
    }
}
