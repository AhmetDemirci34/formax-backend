using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Home
{
    /// <summary>
    /// FAZ 6.1 — League Baseline.
    /// Aynı ligdeki güncel sapma ve sessizlik oranına bakarak radar için lig tabanı üretir.
    /// </summary>
    public sealed class LeagueBaselineService
    {
        public IReadOnlyDictionary<string, int> Build(
            IReadOnlyList<MatchListItemDto> matches,
            IReadOnlyList<MatchSapmaSnapshot> snapshots,
            DateTime utcNow)
        {
            var snapMap = snapshots.ToDictionary(x => x.MatchId, x => x);

            var rows = matches
                .Where(x => !string.IsNullOrWhiteSpace(x.League) && snapMap.ContainsKey(x.MatchId))
                .Select(x => new
                {
                    League = x.League,
                    Snapshot = snapMap[x.MatchId],
                    IsLive = IsLive(x.Status),
                    IsNear = (x.StartTime - utcNow).TotalHours <= 6
                })
                .GroupBy(x => x.League, StringComparer.OrdinalIgnoreCase);

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in rows)
            {
                var avgSapma = group.Any() ? group.Average(x => x.Snapshot.Sapma) : 0;
                var quietRatio = group.Any() ? group.Count(x => x.Snapshot.SessizMi) / (double)group.Count() : 1.0;
                var liveRatio = group.Any() ? group.Count(x => x.IsLive || x.IsNear) / (double)group.Count() : 0.0;

                var score = Clamp((int)Math.Round(
                    (avgSapma * 0.55) +
                    ((1 - quietRatio) * 25) +
                    (liveRatio * 20),
                    MidpointRounding.AwayFromZero));

                result[group.Key] = score;
            }

            return result;
        }

        private static bool IsLive(string? status)
        {
            var value = status?.ToUpperInvariant() ?? string.Empty;
            return value.Contains("LIVE") || value.Contains("INPLAY");
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
    }
}
