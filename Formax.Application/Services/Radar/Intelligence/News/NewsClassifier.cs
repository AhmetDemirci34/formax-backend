using System;
using System.Collections.Generic;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.2) — default keyword classifier. Scans the item's
    /// text (raw name + canonical name + payload, lowercased) against an ordered keyword
    /// table. The first category whose keyword is found wins, giving each item exactly
    /// one category. Falls back to General. Fully deterministic; no AI, no LLM.
    /// </summary>
    public sealed class NewsClassifier : INewsClassifier
    {
        // Priority order is the array order. First hit wins.
        private static readonly (NewsCategory Category, string[] Keywords)[] Rules =
        {
            (NewsCategory.Injury,       new[] { "sakat", "injury", "ameliyat", "tedavi", "rapor" }),
            (NewsCategory.Suspension,   new[] { "cezal", "ceza ", "kırmızı kart", "men cez", "suspension", "tedbir" }),
            (NewsCategory.Transfer,     new[] { "transfer", "imza", "bonservis", "anlaşma", "ayrıl" }),
            (NewsCategory.Lineup,       new[] { "kadro", "ilk 11", "11'", "lineup", "kamp", "muhtemel 11" }),
            (NewsCategory.Coach,        new[] { "teknik direktör", "hoca", "menajer", "coach", "antrenör" }),
            (NewsCategory.MatchResult,  new[] { "kazandı", "mağlup", "berabere", "yendi", "skor", "sonucu", "geçti" }),
            (NewsCategory.MatchPreview, new[] { "öncesi", "hazırlık", "karşılaşacak", "preview", "maçı bekl", "deplasman" }),
        };

        public NewsClassificationResult Classify(StagedSourceItem newsItem)
        {
            var text = $"{newsItem.RawName} {newsItem.CanonicalName} {newsItem.Payload}".ToLowerInvariant();

            foreach (var (category, keywords) in Rules)
            {
                foreach (var kw in keywords)
                {
                    if (text.Contains(kw, StringComparison.Ordinal))
                        return NewsClassificationResult.Of(category, kw);
                }
            }

            return NewsClassificationResult.Of(NewsCategory.General, string.Empty);
        }
    }
}
