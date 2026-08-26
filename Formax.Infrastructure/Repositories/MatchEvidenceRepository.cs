using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.Repositories
{
    /// <summary>
    /// FORMAX Data Engine v2.1 — Evidence Store upsert + context. ContentHash ile tekil
    /// saklar; okuma sırasında Freshness uygular ve MatchIntelligenceContext üretir.
    /// </summary>
    public sealed class MatchEvidenceRepository : IMatchEvidenceRepository
    {
        private readonly FormaxDbContext _context;
        private readonly MatchIntelligenceService _intelligence;
        private readonly FreshnessPolicy _freshness;
        private readonly IConfiguration _config;

        public MatchEvidenceRepository(
            FormaxDbContext context, MatchIntelligenceService intelligence, FreshnessPolicy freshness,
            IConfiguration config)
        {
            _context = context;
            _intelligence = intelligence;
            _freshness = freshness;
            _config = config;
        }

        public async Task<int> UpsertAsync(string formaxMatchId, IEnumerable<MatchEvidence> evidence,
            CancellationToken ct = default)
        {
            var list = evidence.Where(e => !string.IsNullOrWhiteSpace(e.ContentHash))
                               .GroupBy(e => e.ContentHash).Select(g => g.First()).ToList();
            if (list.Count == 0) return 0;

            var hashes = list.Select(e => e.ContentHash).ToList();
            var existing = (await _context.MatchEvidenceRecords
                .Where(r => hashes.Contains(r.ContentHash))
                .Select(r => r.ContentHash).ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var added = 0;
            foreach (var e in list)
            {
                if (existing.Contains(e.ContentHash)) continue;
                _context.MatchEvidenceRecords.Add(new MatchEvidenceRecord
                {
                    FormaxMatchId = formaxMatchId,
                    Type = Trim(e.Type, 48),
                    Cluster = Trim(e.Cluster, 256),
                    Source = Trim(e.Source, 160),
                    SourceQuality = e.SourceQuality,
                    Confidence = e.Confidence,
                    PublishedUtc = e.PublishedUtc,
                    Headline = Trim(e.Headline, 512),
                    ContentHash = e.ContentHash,
                    CreatedAt = now
                });
                added++;
            }

            await _context.SaveChangesAsync(ct);
            return added;
        }

        public Task<MatchIntelligenceContext> GetContextAsync(string formaxMatchId,
            CancellationToken ct = default)
            => GetContextAsync(formaxMatchId, null, null, null, ct);

        public Task<MatchIntelligenceContext> GetContextAsync(
            string formaxMatchId, string? homeTeam, string? awayTeam, CancellationToken ct = default)
            => GetContextAsync(formaxMatchId, homeTeam, awayTeam, null, ct);

        public Task<MatchIntelligenceContext> GetContextAsync(
            string formaxMatchId, string? homeTeam, string? awayTeam, DateTime? kickoffUtc,
            CancellationToken ct = default)
            => GetContextAsync(formaxMatchId, homeTeam, awayTeam, kickoffUtc, null, ct);

        public async Task<MatchIntelligenceContext> GetContextAsync(
            string formaxMatchId, string? homeTeam, string? awayTeam, DateTime? kickoffUtc,
            IEnumerable<string>? knownPlayerNames,
            CancellationToken ct = default)
        {
            var rows = await _context.MatchEvidenceRecords
                .AsNoTracking()
                .Where(r => r.FormaxMatchId == formaxMatchId)
                .ToListAsync(ct);

            // HABER İÇERİĞİ KÖPRÜSÜ — kanıt kaydı özet ve kaynak listesi taşımaz, ama AYNI
            // haber MatchNewsArticles'ta ContentHash ile duruyor: gerçek snippet, yayıncı
            // listesi ve kaynak sayısı orada. Yeni tablo, yeni sütun, migration ve yeni
            // kaynak YOK — yalnız zaten yazılmış veri okunur.
            var hashes = rows.Select(r => r.ContentHash).Distinct().ToList();
            var articleByHash = (await _context.MatchNewsArticles
                    .AsNoTracking()
                    .Where(a => a.FormaxMatchId == formaxMatchId && hashes.Contains(a.ContentHash))
                    .Select(a => new { a.ContentHash, a.Headline, a.Summary, a.Sources, a.SourceCount })
                    .ToListAsync(ct))
                .GroupBy(a => a.ContentHash)
                .ToDictionary(g => g.Key, g => g.First());

            // KAPI 1 — Freshness: TTL'i geçen kanıt düşer (mevcut davranış).
            var fresh = rows
                .Where(r => _freshness.IsFresh(r.Type, r.PublishedUtc))
                .Select(r =>
                {
                    articleByHash.TryGetValue(r.ContentHash, out var art);
                    var sources = (art?.Sources ?? r.Source)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return new MatchEvidence
                    {
                        FormaxMatchId = r.FormaxMatchId,
                        Type = r.Type,
                        Cluster = r.Cluster,
                        Source = r.Source,
                        SourceQuality = r.SourceQuality,
                        Confidence = r.Confidence,
                        PublishedUtc = r.PublishedUtc,
                        Headline = r.Headline,
                        ContentHash = r.ContentHash,
                        Summary = art == null ? "" : CleanSummary(art.Summary, art.Headline),
                        Sources = sources,
                        SourceCount = Math.Max(art?.SourceCount ?? 1, Math.Max(1, sources.Count))
                    };
                })
                .ToList();

            // KAPI 2+ — Futbol triyajı, içerik türü, tarihsel içerik, kalite ve maç bağlama
            // OKUMA anında da uygulanır; ardından AYNI OLAYI anlatan kayıtlar tek olaya iner
            // (çoklu kaynak = KaynakSayısı). Depoda kapılar eklenmeden önce yazılmış kayıtlar
            // bulunur; AI'ın factual katmanına girmelerini bu adım engeller.
            var minQuality = Math.Clamp(_config.GetValue("News:MinEvidenceSourceQuality", 85), 0, 100);
            var requireBothTeams = _config.GetValue("News:RequireBothTeams", false);
            var gated = _intelligence.ApplyReadGates(
                fresh, homeTeam, awayTeam, minQuality, requireBothTeams, kickoffUtc, knownPlayerNames);

            return _intelligence.BuildContext(formaxMatchId, gated);
        }

        private static string Trim(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s[..max];
        }

        /// <summary>
        /// GERÇEK ÖZET Mİ, BAŞLIĞIN YANKISI MI? Google News RSS'in &lt;description&gt;'ı gerçek bir
        /// snippet değildir: linklenmiş başlık + yayıncı adıdır ("Başlık &amp;nbsp;&amp;nbsp; ESPN").
        /// Ölçüldü (14.08): 11.825 haberin 9.039'unda özet başlıkla başlıyor. Böyle bir metni
        /// "haber içeriği" diye taşımak, başlığı özet gibi göstermek olur — YAPILMAZ.
        /// Yankı temizlendikten sonra anlamlı bir metin kalmıyorsa boş döner.
        /// </summary>
        private static string CleanSummary(string? summary, string? headline)
        {
            if (string.IsNullOrWhiteSpace(summary)) return "";

            var s = System.Net.WebUtility.HtmlDecode(summary)
                .Replace(' ', ' ');
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ").Trim();

            var h = (headline ?? "").Trim();
            if (h.Length > 0 && s.StartsWith(h, StringComparison.OrdinalIgnoreCase))
                s = s[h.Length..].Trim(' ', '-', '–', '|', '·', ',');

            // Geriye yalnız yayıncı adı ya da birkaç kelime kaldıysa gerçek içerik yoktur.
            return s.Length < 80 ? "" : s;
        }
    }
}
