using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.News.Discovery;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Match Intelligence orchestrator.
    /// Tekilleştirilmiş haberleri (v2) → Signal Extraction + Source Quality + Freshness
    /// uygulayarak Evidence'a dönüştürür ve Reasoning için MatchIntelligenceContext üretir.
    /// Mevcut Reasoning/LLM'e dokunmaz; yalnız sindirilmiş sinyalleri hazırlar.
    /// </summary>
    public sealed class MatchIntelligenceService
    {
        private readonly SignalExtractor _signals;
        private readonly SourceQualityResolver _quality;
        private readonly FreshnessPolicy _freshness;

        public MatchIntelligenceService(
            SignalExtractor signals, SourceQualityResolver quality, FreshnessPolicy freshness)
        {
            _signals = signals;
            _quality = quality;
            _freshness = freshness;
        }

        /// <summary>Tekilleştirilmiş haberlerden taze, kalite-ağırlıklı Evidence üretir.</summary>
        public List<MatchEvidence> BuildEvidence(string formaxMatchId, IEnumerable<DedupedNewsItem> items)
        {
            var evidence = new List<MatchEvidence>();

            foreach (var i in items)
            {
                var signals = _signals.Extract(i.Headline, i.Summary);
                var primary = _signals.Primary(signals);

                // Freshness: sinyal kategorisinin TTL'ini geçen kanıt düşer.
                if (!_freshness.IsFresh(primary, i.PublishedUtc)) continue;

                var quality = _quality.BestQuality(i.Sources);
                // Confidence: haber güveni (tekrar/tazelik) + kaynak kalitesi harmanı.
                var confidence = (int)Math.Round(i.Confidence * 0.6 + quality * 0.4);

                evidence.Add(new MatchEvidence
                {
                    FormaxMatchId = formaxMatchId,
                    Type = primary,
                    Cluster = string.Join(",", signals),
                    Source = i.Sources.FirstOrDefault() ?? "",
                    SourceQuality = quality,
                    Confidence = Math.Clamp(confidence, 0, 99),
                    PublishedUtc = i.PublishedUtc,
                    Headline = i.Headline,
                    ContentHash = i.ContentHash
                });
            }

            return evidence;
        }

        /// <summary>Evidence listesinden Reasoning'in tüketeceği context'i kurar.</summary>
        public MatchIntelligenceContext BuildContext(string formaxMatchId, List<MatchEvidence> evidence)
        {
            var signals = Histogram(evidence.Select(e => e.Type));
            var clusters = Histogram(evidence.SelectMany(e => e.Cluster.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim())));

            return new MatchIntelligenceContext
            {
                FormaxMatchId = formaxMatchId,
                TotalEvidence = evidence.Count,
                TotalProviders = evidence.Select(e => e.Source)
                                         .Where(s => !string.IsNullOrWhiteSpace(s))
                                         .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Signals = signals,
                Clusters = clusters,
                Confidence = evidence.Count == 0 ? 0 : (int)Math.Round(evidence.Average(e => e.Confidence)),
                TopHeadlines = evidence.OrderByDescending(e => e.Confidence).Take(5).Select(e => e.Headline).ToList(),
                LatestHeadlines = evidence.OrderByDescending(e => e.PublishedUtc).Take(5).Select(e => e.Headline).ToList(),
                TopEvidence = evidence.OrderByDescending(e => e.Confidence).Take(8).ToList()
            };
        }

        private static Dictionary<string, int> Histogram(IEnumerable<string> values)
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in values)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                d[v] = d.TryGetValue(v, out var n) ? n + 1 : 1;
            }
            return d;
        }
    }
}
