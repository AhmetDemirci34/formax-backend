using System.Threading.Tasks;
using Formax.Application.AI.Contexts;
using Formax.Application.Interfaces;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.ReadProviders
{
    /// <summary>
    /// Radar/AI'nin okuduğu <see cref="PreMatchContext"/>'i GDP verisinden türetip doldurur.
    ///
    /// Önceden bu alanlar bilinçli boş bırakılıyordu (hep default League/Low/false) → Radar'ın
    /// maç çerçevesi işlevsizdi. Artık GDP'nin zaten çektiği lig adı (Domain Match.League) ve
    /// tur etiketi (GdpMatchLink.Round) <see cref="MatchFramingDeriver"/> ile çerçeveye çevrilir.
    ///
    /// GDP linki yoksa (maç GDP dışından geldiyse) tur bilinmez → yalnızca lig adından türetilir;
    /// yine de graceful çalışır. IsDerby doldurulmaz: GDP'nin kanonik verisinde takım-şehri/rakiplik
    /// yoktur (bilinçli kapsam dışı — bkz. kapanış raporu).
    /// </summary>
    public sealed class PreMatchReadProvider : IPreMatchReadProvider
    {
        private readonly FormaxDbContext _dbContext;

        public PreMatchReadProvider(FormaxDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PreMatchContext?> ReadAsync(int matchId)
        {
            var match = await _dbContext.Matches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == matchId);

            if (match == null)
                return null;

            // GDP metadata (tur bilgisi) — MatchId indeksli; maç GDP dışından geldiyse null olabilir.
            var round = await _dbContext.GdpMatchLinks
                .AsNoTracking()
                .Where(l => l.MatchId == matchId)
                .Select(l => l.Round)
                .FirstOrDefaultAsync();

            var framing = MatchFramingDeriver.Derive(match.League, round);

            return new PreMatchContext
            {
                MatchId = match.Id,
                CompetitionType = framing.CompetitionType,
                ImportanceLevel = framing.ImportanceLevel,
                IsElimination = framing.IsElimination,
                IsFinal = framing.IsFinal
                // IsDerby: GDP verisinden türetilemez (takım-şehri/rakiplik yok) → default false.
            };
        }
    }
}
