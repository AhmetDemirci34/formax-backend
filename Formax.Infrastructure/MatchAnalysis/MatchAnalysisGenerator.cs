using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Application.Services.MatchAnalysis;
using Formax.Application.UseCases;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.BackgroundJobs;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.MatchAnalysis
{
    public sealed record AnalysisGenerationResult(
        int MatchId, string Outcome, string? Status, string? Generator, double MaxSimilarity, int Rejected, int LlmCalls);

    /// <summary>
    /// AI MAÇ ANALİZİ ÜRETİCİ — YALNIZ ARKA PLANDA çalışır (job ya da admin tetiği).
    ///
    /// Akış: DB'deki doğrulanmış veriden girdi → deterministik kanıt (sayılar burada) →
    /// kanıt anahtarlı cümleler → doğrulayıcı → tekrar/benzerlik engeli → (isteğe bağlı) LLM yalnız
    /// DOĞRULANMIŞ cümleleri doğal dile çevirir; çevrilen her cümle aynı doğrulayıcıdan geçer, geçmezse
    /// deterministik cümle kalır → tek satır DB'ye yazılır. Kanıt değişmediyse yeniden üretilmez.
    /// </summary>
    public sealed class MatchAnalysisGenerator
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

        private readonly FormaxDbContext _db;
        private readonly GetMatchDetailAIContextUseCase _detail;
        private readonly ILLMClient _llm;
        private readonly IConfiguration _config;
        private readonly ILogger<MatchAnalysisGenerator> _log;

        public MatchAnalysisGenerator(
            FormaxDbContext db, GetMatchDetailAIContextUseCase detail, ILLMClient llm,
            IConfiguration config, ILogger<MatchAnalysisGenerator> log)
        {
            _db = db; _detail = detail; _llm = llm; _config = config; _log = log;
        }

        private bool LlmEnabled
            => _config.GetValue("MatchAnalysis:LlmVerbalize", true)
               && !string.Equals(_config["Llm:Provider"] ?? "Mock", "Mock", StringComparison.OrdinalIgnoreCase);

        public async Task<AnalysisGenerationResult> GenerateAsync(int matchId, bool force, bool allowLlm, CancellationToken ct = default)
        {
            var match = await _db.Matches.AsNoTracking().Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId, ct);
            if (match == null) return new(matchId, "MatchNotFound", null, null, 0, 0, 0);
            if (match.Status is MatchStatuses.Finished or "Cancelled" or "Postponed" or MatchStatuses.Live)
                return new(matchId, "SkippedStatus:" + match.Status, null, null, 0, 0, 0);

            var input = await BuildInputAsync(match, ct);
            if (input == null) return new(matchId, "DetailUnavailable", null, null, 0, 0, 0);

            var evidence = MatchEvidenceBuilder.Build(input);
            var evidenceJson = JsonSerializer.Serialize(evidence, Json);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidenceJson))).ToLowerInvariant();

            var existing = await _db.MatchAnalysisSnapshots.FirstOrDefaultAsync(s => s.MatchId == matchId, ct);
            if (!force && existing != null && existing.InputHash == hash)
                return new(matchId, "Unchanged", existing.Status, existing.Generator, existing.MaxSimilarity, 0, 0);

            // Önceki analizler (başka maçlar, son 14 gün) — benzerlik ve birebir cümle tekrarı.
            var since = DateTime.UtcNow.AddDays(-14);
            var recent = await _db.MatchAnalysisSnapshots.AsNoTracking()
                .Where(s => s.MatchId != matchId && s.GeneratedAtUtc >= since && s.Status == "Ready")
                .Select(s => new { s.MatchId, s.FlatText, s.ContentJson })
                .ToListAsync(ct);
            var recentSentences = new HashSet<string>(recent.SelectMany(r => SentencesOf(r.ContentJson, includeUncertainty: false)),
                StringComparer.Ordinal);

            var rejections = new List<(string Text, string Reason)>();
            (MatchAnalysisDocument Doc, double Sim, int? SimMatch) Score(MatchAnalysisDocument d)
            {
                var flat = AnalysisSimilarity.Flatten(d);
                double best = 0; int? bestId = null;
                foreach (var r in recent)
                {
                    var j = AnalysisSimilarity.Jaccard(flat, r.FlatText);
                    if (j > best) { best = j; bestId = r.MatchId; }
                }
                return (d, Math.Round(best, 4), bestId);
            }

            MatchAnalysisDocument Prepare(int variant)
            {
                var v = MatchAnalysisValidator.Validate(MatchAnalysisComposer.Compose(input, evidence, variant),
                    evidence, input.HomeName, input.AwayName);
                rejections.AddRange(v.Rejected);
                return DropRepeated(v.Accepted, recentSentences, rejections);
            }

            string generator = "Deterministic";
            var chosen = Score(Prepare(0));
            if (chosen.Sim > AnalysisSimilarity.MaxAllowed)
            {
                generator = "DeterministicVariant";
                var alt = Score(Prepare(1));
                if (alt.Sim < chosen.Sim) chosen = alt;
            }
            if (chosen.Sim > AnalysisSimilarity.MaxAllowed)
            {
                generator = "DeterministicShort";
                chosen = Score(Short(chosen.Doc));
            }

            var llmCalls = 0;
            if (allowLlm && LlmEnabled && !chosen.Doc.IsEmpty)
            {
                llmCalls = 1;
                var polished = await MatchAnalysisVerbalizer.VerbalizeAsync(_llm, chosen.Doc, evidence, input.HomeName, input.AwayName,
                    rejections, TimeSpan.FromSeconds(_config.GetValue("MatchAnalysis:LlmTimeoutSeconds", 90)), _log, ct);
                if (polished != null)
                {
                    var scored = Score(DropRepeated(polished, recentSentences, rejections));
                    if (scored.Sim <= Math.Max(chosen.Sim, AnalysisSimilarity.MaxAllowed) && !scored.Doc.IsEmpty)
                    {
                        chosen = scored;
                        generator = "LlmVerbalized";
                    }
                }
            }

            var status = chosen.Doc.IsEmpty ? "InsufficientData"
                       : chosen.Sim > AnalysisSimilarity.MaxAllowed ? "SimilarityRejected"
                       : "Ready";

            var row = existing ?? new MatchAnalysisSnapshot { MatchId = matchId };
            row.KickoffUtc = match.MatchDate;
            row.GeneratedAtUtc = DateTime.UtcNow;
            row.InputHash = hash;
            row.Status = status;
            row.Generator = generator;
            row.ContentJson = JsonSerializer.Serialize(chosen.Doc, Json);
            row.EvidenceJson = evidenceJson;
            row.FlatText = AnalysisSimilarity.Flatten(chosen.Doc);
            row.MaxSimilarity = chosen.Sim;
            row.MostSimilarMatchId = chosen.SimMatch;
            row.RejectedSentenceCount = rejections.Count;
            row.RejectionsJson = rejections.Count == 0 ? null
                : JsonSerializer.Serialize(rejections.Select(r => new { r.Text, r.Reason }), Json);
            row.LlmCalls = llmCalls;
            if (existing == null) _db.MatchAnalysisSnapshots.Add(row);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("[ANALYSIS] {MatchId} {Home}–{Away}: {Status} ({Generator}), benzerlik {Sim}, ret {Rej}, llm {Llm}",
                matchId, input.HomeName, input.AwayName, status, generator, chosen.Sim, rejections.Count, llmCalls);
            return new(matchId, "Generated", status, generator, chosen.Sim, rejections.Count, llmCalls);
        }

        // ── girdi ────────────────────────────────────────────────────────────────

        private async Task<AnalysisInput?> BuildInputAsync(Match match, CancellationToken ct)
        {
            // Maç detayının senkron yolu (DB okuması; LLM/dış kaynak yok) sezon kapsamını,
            // lig formunu ve puan durumu kimliğini zaten doğruluyor — aynı kurallar yeniden yazılmaz.
            var detail = _detail.Execute(match.Id);
            if (detail == null) return null;

            var homeFacts = await FactsAsync(match.HomeTeamId, detail.HomeTeamLastMatches.Select(x => x.MatchId), match.MatchDate, ct);
            var awayFacts = await FactsAsync(match.AwayTeamId, detail.AwayTeamLastMatches.Select(x => x.MatchId), match.MatchDate, ct);

            StandingFact? hs = null, aws = null;
            var st = detail.Standing;
            var standingsOk = st != null && st.StandingsAvailability == "Table" && st.LeagueId == match.LeagueId
                              && st.IsComplete == true;
            if (standingsOk)
            {
                if (st!.HomeTeamPeek is { } hp) hs = new StandingFact(hp.Position, hp.Points, hp.Played);
                if (st.AwayTeamPeek is { } ap) aws = new StandingFact(ap.Position, ap.Points, ap.Played);
            }

            LineupFact? hl = null, al = null;
            var header = await _db.MatchLineups.AsNoTracking().FirstOrDefaultAsync(h => h.MatchId == match.Id, ct);
            if (header?.Provider?.StartsWith("official:", StringComparison.Ordinal) == true)
            {
                var starters = await _db.MatchLineupPlayers.AsNoTracking()
                    .Where(p => p.MatchId == match.Id && p.Role == "Starter")
                    .GroupBy(p => p.Side).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
                int Count(string side) => starters.FirstOrDefault(s => s.Key == side)?.Count ?? 0;
                if (header.HomeLineupsReleased) hl = new LineupFact(header.HomeFormation, header.HomeCoach, Count("Home"));
                if (header.AwayLineupsReleased) al = new LineupFact(header.AwayFormation, header.AwayCoach, Count("Away"));
            }

            var allow = CoveragePolicy.LeagueAllowList(_config);
            async Task<DateTime?> PreviousAsync(int teamId)
            {
                var prev = await _db.Matches.AsNoTracking()
                    .Where(m => (m.HomeTeamId == teamId || m.AwayTeamId == teamId)
                                && m.MatchDate < match.MatchDate && m.Status == MatchStatuses.Finished && m.Id != match.Id)
                    .OrderByDescending(m => m.MatchDate)
                    .Select(m => new { m.MatchDate, m.LeagueId })
                    .Take(10).ToListAsync(ct);
                return prev.FirstOrDefault(p => CoveragePolicy.Allows(allow, p.LeagueId))?.MatchDate;
            }

            return new AnalysisInput(
                match.Id, match.HomeTeam?.Name ?? detail.HomeTeam.Name, match.AwayTeam?.Name ?? detail.AwayTeam.Name,
                match.MatchDate, detail.HomeSeasonForm?.LeagueName ?? detail.League,
                homeFacts, awayFacts, hs, aws, standingsOk, hl, al,
                await PreviousAsync(match.HomeTeamId), await PreviousAsync(match.AwayTeamId));
        }

        private async Task<IReadOnlyList<TeamMatchFact>> FactsAsync(int teamId, IEnumerable<int?> ids, DateTime kickoff, CancellationToken ct)
        {
            var idList = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
            if (idList.Count == 0) return Array.Empty<TeamMatchFact>();

            var matches = await _db.Matches.AsNoTracking()
                .Where(m => idList.Contains(m.Id) && m.Status == MatchStatuses.Finished && m.MatchDate < kickoff)
                .Select(m => new { m.Id, m.MatchDate, m.HomeTeamId, m.HomeScore, m.AwayScore })
                .ToListAsync(ct);
            var stats = await _db.MatchTeamStatistics.AsNoTracking()
                .Where(s => idList.Contains(s.MatchId))
                .Select(s => new { s.MatchId, s.Side, s.TotalShots, s.ShotsOnTarget })
                .ToListAsync(ct);

            return matches.Select(m =>
            {
                var isHome = m.HomeTeamId == teamId;
                var side = isHome ? "Home" : "Away";
                var st = stats.FirstOrDefault(s => s.MatchId == m.Id && string.Equals(s.Side, side, StringComparison.OrdinalIgnoreCase));
                return new TeamMatchFact(m.Id, m.MatchDate, isHome,
                    isHome ? m.HomeScore : m.AwayScore, isHome ? m.AwayScore : m.HomeScore,
                    st?.TotalShots, st?.ShotsOnTarget);
            }).ToList();
        }

        // ── tekrar engeli ────────────────────────────────────────────────────────

        /// <summary>
        /// Başka bir maçın analizinde BİREBİR geçen cümle kabul edilmez. Tek istisna zorunlu örneklem
        /// uyarısıdır (Belirsizlik): takım adıyla aynı kalıp başka maçta da doğru olabilir.
        /// </summary>
        public static MatchAnalysisDocument DropRepeated(MatchAnalysisDocument doc, ISet<string> seen, List<(string, string)> rejections)
        {
            AnalysisSentence? Keep(AnalysisSentence? s)
            {
                if (s == null) return null;
                if (!seen.Contains(s.Text)) return s;
                rejections.Add((s.Text, "RepeatedFromOtherMatch"));
                return null;
            }
            return new MatchAnalysisDocument
            {
                WhyWatch = doc.WhyWatch.Select(Keep).OfType<AnalysisSentence>().ToList(),
                KeyBattle = doc.KeyBattle.Select(Keep).OfType<AnalysisSentence>().ToList(),
                LineupImpact = doc.LineupImpact.Select(Keep).OfType<AnalysisSentence>().ToList(),
                Uncertainty = doc.Uncertainty,
                Scenarios = doc.Scenarios.Select(r => new ScenarioReason(r.Market, Keep(r.Support), Keep(r.Risk)))
                    .Where(r => r.Support != null || r.Risk != null).ToList()
            };
        }

        private static MatchAnalysisDocument Short(MatchAnalysisDocument d) => new()
        {
            KeyBattle = d.KeyBattle,
            LineupImpact = d.LineupImpact,
            Uncertainty = d.Uncertainty
        };

        public static IEnumerable<string> SentencesOf(string contentJson, bool includeUncertainty)
        {
            MatchAnalysisDocument? doc;
            try { doc = JsonSerializer.Deserialize<MatchAnalysisDocument>(contentJson); }
            catch (JsonException) { yield break; }
            if (doc == null) yield break;
            foreach (var s in doc.AllSentences())
                if (includeUncertainty || !ReferenceEquals(s, doc.Uncertainty))
                    yield return s.Text;
        }
    }
}
