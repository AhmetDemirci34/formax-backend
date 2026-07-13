using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — Search Query Builder. Maç metadata'sından (ev/deplasman/
    /// lig) DİNAMİK arama sorguları üretir: eşleşme, sakatlık, kadro, teknik direktör,
    /// transfer, basın toplantısı, form. Sorgular öncelik sırasıyla döner.
    /// </summary>
    public sealed class MatchNewsSearchQueryBuilder
    {
        // Takım başına üretilen açı ekleri (EN — global haber kapsamı için).
        private static readonly string[] Angles =
            { "injury", "lineup", "team news", "coach", "transfer", "press conference", "form" };

        public NewsQuery Build(string formaxMatchId, string home, string away,
            string league, string country, DateTime kickoffUtc)
        {
            var q = new List<string>();

            // 1) Eşleşme sorguları (en yüksek öncelik).
            if (!string.IsNullOrWhiteSpace(home) && !string.IsNullOrWhiteSpace(away))
            {
                q.Add($"{home} vs {away}");
                q.Add($"{home} {away}");
                q.Add($"{away} {home}");
                if (!string.IsNullOrWhiteSpace(league))
                    q.Add($"{league} {home} {away}");
            }

            // 2) Takım-açı sorguları.
            foreach (var team in new[] { home, away }.Where(t => !string.IsNullOrWhiteSpace(t)))
                foreach (var angle in Angles)
                    q.Add($"{team} {angle}");

            // Tekille + sırayı koru.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = q.Where(x => seen.Add(x)).ToList();

            return new NewsQuery
            {
                FormaxMatchId = formaxMatchId,
                HomeTeam = home,
                AwayTeam = away,
                League = league,
                Country = country,
                KickoffUtc = kickoffUtc,
                Queries = ordered
            };
        }
    }
}
