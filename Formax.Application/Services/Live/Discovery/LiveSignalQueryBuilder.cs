using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Live.Discovery
{
    /// <summary>
    /// FORMAX Live Data Engine — maç için canlı-odaklı arama ifadeleri üretir.
    /// Açık haber/RSS kaynakları maç sırasında skoru başlıkta yayınlar
    /// ("Home 2-1 Away"); bu sorgular o başlıkları hedefler.
    /// </summary>
    public sealed class LiveSignalQueryBuilder
    {
        public LiveSignalQuery Build(
            int matchId, string home, string away, string league, string country, DateTime kickoffUtc)
        {
            home = (home ?? string.Empty).Trim();
            away = (away ?? string.Empty).Trim();

            var queries = new List<string>();
            if (home.Length > 0 && away.Length > 0)
            {
                queries.Add($"{home} vs {away} live score");
                queries.Add($"{home} {away} score");
                queries.Add($"{home} vs {away}");
            }

            return new LiveSignalQuery
            {
                MatchId = matchId,
                HomeTeam = home,
                AwayTeam = away,
                League = league ?? string.Empty,
                Country = country ?? string.Empty,
                KickoffUtc = kickoffUtc,
                Queries = queries
            };
        }
    }
}
