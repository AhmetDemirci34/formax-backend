using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Application.Services.MatchAnalysis;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.MatchAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// AI MAÇ ANALİZİ — elle üretim tetiği ve kabul raporu. Üretim uçları ARKA PLAN kapsamında
    /// çalışır (LLM sayacı "background:"); rapor ucu yalnız DB'yi ve sayacı okur.
    /// </summary>
    [ApiController]
    [Route("admin/analysis")]
    public class AdminAnalysisController : ControllerBase
    {
        private readonly FormaxDbContext _db;
        public AdminAnalysisController(FormaxDbContext db) => _db = db;

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromQuery] int matchId, [FromQuery] bool force,
            [FromServices] MatchAnalysisGenerator generator, CancellationToken ct)
        {
            using var _ = LlmCallMeter.Begin("background:AdminAnalysisGenerate");
            return Ok(await generator.GenerateAsync(matchId, force, allowLlm: true, ct));
        }

        [HttpPost("batch")]
        public async Task<IActionResult> Batch([FromServices] MatchAnalysisJob job, [FromQuery] int max = 30,
            [FromQuery] bool force = false, [FromQuery] int horizonHours = 72, CancellationToken ct = default)
            => Ok(await job.RunCycleAsync(max, ct, force, horizonHours));

        /// <summary>KABUL RAPORU — sıfır dış istek, sıfır LLM.</summary>
        [HttpGet("report")]
        public async Task<IActionResult> Report([FromQuery] int sinceHours = 72, CancellationToken ct = default)
        {
            var since = DateTime.UtcNow.AddHours(-sinceHours);
            var rows = await _db.MatchAnalysisSnapshots.AsNoTracking()
                .Where(s => s.GeneratedAtUtc >= since)
                .Join(_db.Matches.AsNoTracking(), s => s.MatchId, m => m.Id,
                    (s, m) => new { s, m.League, Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name })
                .ToListAsync(ct);

            var ready = rows.Where(r => r.s.Status == "Ready").ToList();
            var owners = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            int evidenceless = 0, sentences = 0;
            foreach (var r in ready)
            {
                var doc = JsonSerializer.Deserialize<MatchAnalysisDocument>(r.s.ContentJson);
                if (doc == null) continue;
                foreach (var sentence in doc.AllSentences())
                {
                    sentences++;
                    if (sentence.EvidenceKeys == null || sentence.EvidenceKeys.Count == 0) evidenceless++;
                    if (ReferenceEquals(sentence, doc.Uncertainty)) continue; // zorunlu örneklem uyarısı
                    if (!owners.TryGetValue(sentence.Text, out var set)) owners[sentence.Text] = set = new HashSet<int>();
                    set.Add(r.s.MatchId);
                }
            }
            var repeated = owners.Where(kv => kv.Value.Count > 1).ToList();

            double pairMax = 0; string? pair = null;
            for (var i = 0; i < ready.Count; i++)
                for (var j = i + 1; j < ready.Count; j++)
                {
                    var sim = AnalysisSimilarity.Jaccard(ready[i].s.FlatText, ready[j].s.FlatText);
                    if (sim > pairMax) { pairMax = sim; pair = $"{ready[i].s.MatchId}~{ready[j].s.MatchId}"; }
                }

            var reasons = rows.Where(r => r.s.RejectionsJson != null)
                .SelectMany(r => JsonSerializer.Deserialize<List<Dictionary<string, string>>>(r.s.RejectionsJson!) ?? new List<Dictionary<string, string>>())
                .GroupBy(x => x.GetValueOrDefault("Reason") ?? "?")
                .ToDictionary(g => g.Key, g => g.Count());

            return Ok(new
            {
                sinceUtc = since,
                generated = rows.Count,
                byStatus = rows.GroupBy(r => r.s.Status).ToDictionary(g => g.Key, g => g.Count()),
                byGenerator = rows.GroupBy(r => r.s.Generator).ToDictionary(g => g.Key, g => g.Count()),
                leagues = ready.Select(r => r.League).Distinct().OrderBy(x => x).ToList(),
                validatorRejectedSentences = rows.Sum(r => r.s.RejectedSentenceCount),
                rejectionReasons = reasons,
                maxSimilarityAtGeneration = rows.Count == 0 ? 0 : rows.Max(r => r.s.MaxSimilarity),
                maxPairSimilarityReady = Math.Round(pairMax, 4),
                maxPair = pair,
                acceptedSentences = sentences,
                evidencelessAcceptedSentences = evidenceless,
                repeatedSentenceCount = repeated.Count,
                repeatedSentences = repeated.Take(10).Select(kv => new { text = kv.Key, matches = kv.Value }),
                identicalAnalysisTexts = ready.GroupBy(r => r.s.FlatText).Count(g => g.Count() > 1),
                matchesWithTechnicalText = ready.Count(r => MatchAnalysisValidator.HasTechnicalText(r.s.FlatText)),
                matchesWithForbiddenPhrase = ready.Count(r => MatchAnalysisValidator.HasForbiddenPhrase(r.s.FlatText)),
                llmCalls = new
                {
                    sinceProcessStartUtc = LlmCallMeter.StartedAtUtc,
                    userRequestCalls = LlmCallMeter.RequestCalls,
                    byScope = LlmCallMeter.Snapshot()
                },
                analysisLlmCalls = rows.Sum(r => r.s.LlmCalls),
                matches = rows.OrderBy(r => r.s.KickoffUtc).Select(r => new
                {
                    r.s.MatchId, r.League, r.Home, r.Away, r.s.KickoffUtc, r.s.Status, r.s.Generator,
                    r.s.MaxSimilarity, r.s.MostSimilarMatchId, r.s.RejectedSentenceCount, text = r.s.FlatText
                })
            });
        }
    }
}
