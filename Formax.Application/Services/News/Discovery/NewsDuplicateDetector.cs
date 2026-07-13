using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — Duplicate Detection. Aynı hikâye 10 farklı sitede
    /// olabilir; başlık benzerliği (token Jaccard) + aynı URL ile gruplar. Her grup
    /// tek habere iner. Deterministik.
    /// </summary>
    public sealed class NewsDuplicateDetector
    {
        private const double SimilarityThreshold = 0.6;

        private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
        {
            "the","a","an","of","to","in","on","for","and","or","vs","v","with","at","as",
            "is","are","was","were","be","by","from","his","her","their","this","that",
            "news","live","report","update","latest"
        };

        public IReadOnlyList<List<NewsCandidate>> Group(IEnumerable<NewsCandidate> candidates)
        {
            var groups = new List<(HashSet<string> Tokens, string Host, List<NewsCandidate> Items)>();

            foreach (var c in candidates)
            {
                var tokens = Tokenize(StripPublisher(c.Headline));
                var host = Host(c.Url);

                var match = groups.FirstOrDefault(g =>
                    (!string.IsNullOrEmpty(host) && g.Host == host && Jaccard(g.Tokens, tokens) >= 0.4) ||
                    Jaccard(g.Tokens, tokens) >= SimilarityThreshold);

                if (match.Items != null)
                    match.Items.Add(c);
                else
                    groups.Add((tokens, host, new List<NewsCandidate> { c }));
            }

            return groups.Select(g => g.Items).ToList();
        }

        private static string StripPublisher(string headline)
        {
            // Google News başlıkları "... - Publisher" biçiminde gelir.
            var idx = headline.LastIndexOf(" - ", StringComparison.Ordinal);
            return idx > 10 ? headline[..idx] : headline;
        }

        private static HashSet<string> Tokenize(string text)
        {
            var lower = text.ToLowerInvariant();
            return Regex.Matches(lower, @"[a-zğüşıöç0-9]{3,}")
                        .Select(m => m.Value)
                        .Where(t => !Stop.Contains(t))
                        .ToHashSet(StringComparer.Ordinal);
        }

        private static double Jaccard(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0;
            var inter = a.Count(b.Contains);
            var union = a.Count + b.Count - inter;
            return union == 0 ? 0 : (double)inter / union;
        }

        private static string Host(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";
        }
    }
}
