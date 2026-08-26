using Formax.Application.DTOs.Matches;
using Formax.Domain.Entities;
using System.Linq;

namespace Formax.Application.Interfaces
{
    public interface IMatchReadRepository
    {
        IQueryable<Match> Query();

        Match? GetById(int id);

        List<Match> GetUpcomingMatches(DateTime from, DateTime to);

        Task<IReadOnlyList<MatchListItemDto>> GetMatchListAsync();

        /// <summary>
        /// Maçlar EKRANI için "şimdi" merkezli liste: dünden itibaren yakın pencere,
        /// kickoff'a göre ARTAN sıra. Böylece biten + canlı + yaklaşan maçlar aynı
        /// listede görünür. GetMatchListAsync'in sırası/penceresi DEĞİŞMEDİ —
        /// Home/Radar/Sapma tüketicileri etkilenmez.
        /// </summary>
        Task<IReadOnlyList<MatchListItemDto>> GetScreenMatchListAsync(int take = 250);

        Task<IReadOnlyList<MatchListItemDto>> GetMatchListByIdsAsync(IEnumerable<int> ids);

        // 🔥 NEW
        /// <param name="finishedOnly">
        /// true → yalnız OYNANMIŞ maçlar (Status = Finished). Tarih filtresi tek başına
        /// yetmiyordu: geçmiş tarihli ama oynanmamış/iptal/canlı kayıtlar da geliyordu ve
        /// skorları 0-0 olduğu için "beraberlik" gibi görünüyorlardı (ölçüldü: Fenerbahçe'nin
        /// son maçlarında 29.07.2026 Gornik Zabrze, Status=NotStarted, 0-0 → "D").
        /// Varsayılan false — mevcut çağıranların davranışı DEĞİŞMEZ.
        /// </param>
        /// <param name="leagueId">
        /// Verilirse YALNIZ o turnuvanın maçları döner. "Ligde son 5 maç" başlığının gerçekten
        /// lig maçlarını göstermesi için gerekir: filtresiz liste kupa/Avrupa maçlarını da
        /// içeriyordu (ör. Fenerbahçe'nin son 5'inde UEFA Champions League maçları vardı).
        /// Varsayılan null — mevcut çağıranların davranışı DEĞİŞMEZ.
        /// </param>
        List<Match> GetRecentMatchesForTeam(int teamId, int count = 5, bool finishedOnly = false, int? leagueId = null);

        /// <param name="finishedOnly">
        /// true → yalnız OYNANMIŞ karşılaşmalar. Tarih filtresi tek başına yetmiyordu:
        /// ölçüldü (17.08) — kapsam içi geçmiş tarihli 31 kayıt Finished DEĞİL ve skorları
        /// 0-0. Örnek: maç 15497 (Deportivo–Elche, Live) KENDİ H2H listesine 0-0 beraberlik
        /// olarak giriyordu. Varsayılan false — mevcut çağıranların davranışı değişmez.
        /// </param>

        // 🔥 NEW
        List<Match> GetHeadToHeadMatches(
            int homeTeamId,
            int awayTeamId,
            int count = 5,
            bool finishedOnly = false,
            int? excludeMatchId = null
        );
    }
}