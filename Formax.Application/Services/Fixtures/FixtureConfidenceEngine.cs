using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Data Engine v1 — Confidence Engine. Aynı FORMAX_MATCH_ID'ye çözülen
    /// adaylar (lig+gün+ev+deplasman zaten uyuşuyor) için güven üretir:
    ///   • kaç farklı kaynak doğruladı (en güçlü sinyal)
    ///   • başlama saatleri tutarlı mı
    ///   • kaynakların kendi veri güveni
    /// Tek kaynak asla yüksek güvene ulaşamaz → yanlış/teyitsiz maç oluşturulmaz.
    /// </summary>
    public sealed class FixtureConfidenceEngine
    {
        private const int SingleSourceCeiling = 70;

        public int Score(IEnumerable<FixtureCandidate> group)
        {
            var items = group.ToList();
            if (items.Count == 0) return 0;

            var sources = items.Select(i => i.Source).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            var baseScore = sources >= 3 ? 95 : sources == 2 ? 85 : 65;

            // Saat tutarlılığı (çok kaynak varsa anlamlı).
            var timePenalty = 0;
            if (sources >= 2)
            {
                var maxDiffMin = (items.Max(i => i.DateUtc) - items.Min(i => i.DateUtc)).TotalMinutes;
                if (maxDiffMin > 90) timePenalty = 10;
            }

            var avgSourceConf = (int)Math.Round(items.Average(i => i.SourceConfidence));

            var score = (int)Math.Round(baseScore * 0.8 + avgSourceConf * 0.2) - timePenalty;

            if (sources == 1) score = Math.Min(score, SingleSourceCeiling);

            return Math.Clamp(score, 0, 99);
        }
    }
}
