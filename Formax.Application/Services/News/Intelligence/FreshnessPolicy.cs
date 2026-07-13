using System;
using System.Collections.Generic;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Freshness / TTL. Her sinyal kategorisinin geçerlilik
    /// süresi farklıdır (Lineup 24s, Injury 7g, Transfer 30g…). Süresi geçen kanıt
    /// otomatik düşer → Reasoning'e bayat sinyal gitmez.
    /// </summary>
    public sealed class FreshnessPolicy
    {
        // Sinyal → TTL (saat).
        private static readonly Dictionary<string, int> TtlHours = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Lineup"] = 24,
            ["Weather"] = 48,
            ["Travel"] = 72,
            ["Press Conference"] = 120,   // 5 gün
            ["Injury"] = 168,             // 7 gün
            ["Suspension"] = 168,
            ["Referee"] = 72,
            ["Coach"] = 336,              // 14 gün
            ["Form"] = 168,
            ["Pressure"] = 168,
            ["Derby"] = 336,
            ["Fan Interest"] = 168,
            ["Club Statement"] = 336,
            ["Transfer"] = 720,           // 30 gün
            ["Schedule"] = 720,
            ["General"] = 168,
        };

        private const int DefaultTtlHours = 168;

        public int Ttl(string signal) =>
            TtlHours.TryGetValue(signal ?? "", out var h) ? h : DefaultTtlHours;

        public bool IsFresh(string signal, DateTime publishedUtc) =>
            (DateTime.UtcNow - publishedUtc).TotalHours <= Ttl(signal);
    }
}
