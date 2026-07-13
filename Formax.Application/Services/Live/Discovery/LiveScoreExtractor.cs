using System;
using System.Text.RegularExpressions;

namespace Formax.Application.Services.Live.Discovery
{
    /// <summary>
    /// FORMAX Live Data Engine — açık kaynak başlığından/özetinden canlı skor ve dakika
    /// çıkarır. Yalnız iki takım adı da geçen ve makul bir "N-N" örüntüsü içeren metinden
    /// skor alır; yıl/absürt değerleri (2024-2025 gibi) eler. Emin değilse çıkarmaz —
    /// uydurma yok. (Ingestion normalizasyonu; MI Analyzer'ı değildir.)
    /// </summary>
    public sealed class LiveScoreExtractor
    {
        // "2-1", "2 - 1", "2:1"
        private static readonly Regex ScorePattern =
            new(@"(\d{1,2})\s*[-:]\s*(\d{1,2})", RegexOptions.Compiled);

        // "75'", "(90')", "45+2'"
        private static readonly Regex MinutePattern =
            new(@"(\d{1,3})(?:\+\d{1,2})?\s*['’]", RegexOptions.Compiled);

        /// <summary>Metinden ev/deplasman skorunu çıkarır. Başaramazsa false.</summary>
        public bool TryExtract(string text, string home, string away, out int homeScore, out int awayScore)
        {
            homeScore = 0;
            awayScore = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var t = text.ToLowerInvariant();
            var h = (home ?? string.Empty).ToLowerInvariant().Trim();
            var a = (away ?? string.Empty).ToLowerInvariant().Trim();
            if (h.Length == 0 || a.Length == 0) return false;
            if (!t.Contains(h) || !t.Contains(a)) return false;

            var m = ScorePattern.Match(text);
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups[1].Value, out var s1) ||
                !int.TryParse(m.Groups[2].Value, out var s2)) return false;

            // Absürt/yıl değerlerini ele (futbol skoru pratikte ≤ 20).
            if (s1 > 20 || s2 > 20) return false;

            // Yönlendirme: metinde önce geçen takım adı ilk skora eşlenir.
            var hi = t.IndexOf(h, StringComparison.Ordinal);
            var ai = t.IndexOf(a, StringComparison.Ordinal);
            if (hi <= ai) { homeScore = s1; awayScore = s2; }
            else { homeScore = s2; awayScore = s1; }
            return true;
        }

        /// <summary>Metinden maç dakikasını çıkarır (varsa). 1..130 dışı → null.</summary>
        public int? TryExtractMinute(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = MinutePattern.Match(text);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var min) && min >= 1 && min <= 130)
                return min;
            return null;
        }
    }
}
