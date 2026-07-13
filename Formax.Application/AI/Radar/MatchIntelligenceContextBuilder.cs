using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.Services.Radar.Intelligence.Scenarios;
using EvidenceContext = Formax.Application.Services.News.Intelligence.MatchIntelligenceContext;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v2 — mevcut <see cref="MatchDetailDto"/>'yu (News + Stats + Form +
    /// H2H + Importance + Discovery sinyalleri + deterministik olasılıklar zaten
    /// içinde) tek bir sindirilmiş <see cref="MatchIntelligenceContext"/>'e indirger.
    ///
    /// Yeni veri çekmez, repo'ya dokunmaz → mevcut akışı bozmaz, maksimum yeniden
    /// kullanım. Ham metin/istatistik LLM'e gitmez; yalnız özet sinyaller geçer.
    /// </summary>
    public sealed class MatchIntelligenceContextBuilder
    {
        private const int RecentFormCount = 5;
        private const int MaxNewsThemes = 3;
        private const int FreshNewsHours = 24;

        public MatchIntelligenceContext Build(
            MatchDetailDto detail,
            string? worldHeadline = null,
            IReadOnlyList<ScenarioCandidate>? rankedScenarios = null,
            EvidenceContext? evidence = null)
        {
            return new MatchIntelligenceContext
            {
                MatchId = detail.MatchId,
                HomeTeam = detail.HomeTeam.Name,
                AwayTeam = detail.AwayTeam.Name,
                League = detail.League,
                Round = detail.Round,
                Status = detail.Status,
                KickoffUtc = detail.MatchDate.ToString("u"),
                WorldHeadline = worldHeadline,

                Importance = new MatchIntelligenceContext.ImportanceBlock
                {
                    Level = string.IsNullOrWhiteSpace(detail.Sapma.SapmaBolgesi) ? "Denge" : detail.Sapma.SapmaBolgesi,
                    WatchersCount = detail.WatchersCount,
                    Note = Trim(detail.Sapma.SapmaMetni, 120)
                },

                Form = new MatchIntelligenceContext.FormBlock
                {
                    HomeRecent = RecentForm(detail.HomeTeamLastMatches),
                    AwayRecent = RecentForm(detail.AwayTeamLastMatches),
                    HomeFormScore = detail.Comparison.Home.FormScore,
                    AwayFormScore = detail.Comparison.Away.FormScore
                },

                Stats = new MatchIntelligenceContext.StatsBlock
                {
                    HomeAvgGoalsFor = Math.Round(detail.Comparison.Home.AvgGoalsFor, 2),
                    AwayAvgGoalsFor = Math.Round(detail.Comparison.Away.AvgGoalsFor, 2),
                    HomeGoalScoringRate = detail.Comparison.Home.GoalScoringRate,
                    AwayGoalScoringRate = detail.Comparison.Away.GoalScoringRate,
                    HomeCleanSheetRate = detail.Comparison.Home.CleanSheetRate,
                    AwayCleanSheetRate = detail.Comparison.Away.CleanSheetRate,
                    HomeRank = detail.HomeTeam.Rank,
                    AwayRank = detail.AwayTeam.Rank
                },

                H2H = new MatchIntelligenceContext.H2HBlock
                {
                    Total = detail.H2H.TotalMatches,
                    HomeWins = detail.H2H.HomeWins,
                    AwayWins = detail.H2H.AwayWins,
                    Draws = detail.H2H.Draws
                },

                News = BuildNews(detail.NabizFeed, evidence),

                Social = new MatchIntelligenceContext.SocialBlock
                {
                    CommunityInterest = detail.WatchersCount,
                    Level = InterestLevel(detail.WatchersCount)
                },

                Scenarios = BuildScenarios(detail, rankedScenarios)
            };
        }

        // Senaryolar: ranked aday varsa (kanıt etiketli) onu kullan; yoksa DTO'daki
        // probabilities'e düş (geri-uyum). Yüzdeler her iki yolda da deterministik.
        private static List<MatchIntelligenceContext.ScenarioBlock> BuildScenarios(
            MatchDetailDto detail, IReadOnlyList<ScenarioCandidate>? ranked)
        {
            if (ranked is { Count: > 0 })
                return ranked.Select(c => new MatchIntelligenceContext.ScenarioBlock
                {
                    Market = c.Market,
                    Probability = c.Probability,
                    Confidence = c.Confidence,
                    EvidenceTags = c.EvidenceTags
                }).ToList();

            return detail.Probabilities.Select(p => new MatchIntelligenceContext.ScenarioBlock
            {
                Market = p.Market,
                Probability = p.Probability,
                Confidence = p.Confidence
            }).ToList();
        }

        // Son N maçın sonucunu G/B/M dizisine indirger (en yeni → en eski varsayımıyla).
        private static string RecentForm(IReadOnlyList<LastMatchDto> matches)
        {
            if (matches == null || matches.Count == 0) return "";
            return string.Join(" ", matches
                .Take(RecentFormCount)
                .Select(m => m.Result switch
                {
                    "W" => "G",
                    "D" => "B",
                    "L" => "M",
                    _ => "-"
                }));
        }

        // ÖNCELİK: v2.1 Evidence Store (signal-typed, taze, kalite-ağırlıklı). Yoksa eski NABIZ.
        // Ham haber yine gönderilmez: hacim + baskın sinyal + en çok 3 başlık teması.
        private static MatchIntelligenceContext.NewsBlock BuildNews(NabizSectionDto feed, EvidenceContext? evidence)
        {
            if (evidence != null && evidence.TotalEvidence > 0)
                return BuildNewsFromEvidence(evidence);

            var items = feed?.Items ?? new List<NabizFeedItemDto>();
            var fresh = items
                .Where(i => (DateTime.UtcNow - i.PublishedAt).TotalHours <= FreshNewsHours)
                .ToList();

            var topType = fresh
                .GroupBy(i => i.Type)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var themes = fresh
                .OrderByDescending(i => i.PublishedAt)
                .Select(i => Trim(i.Headline, 70))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .Take(MaxNewsThemes)
                .ToList();

            return new MatchIntelligenceContext.NewsBlock
            {
                Volume24h = fresh.Count,
                TopType = string.IsNullOrWhiteSpace(topType) ? null : topType,
                Themes = themes
            };
        }

        // v2.1 Evidence Store → NewsBlock. Reasoning artık ham haberi değil, sinyalleri görür.
        private static MatchIntelligenceContext.NewsBlock BuildNewsFromEvidence(EvidenceContext ev)
        {
            var signals = ev.Signals
                .Where(s => !s.Key.Equals("General", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.Value)
                .Select(s => s.Key)
                .ToList();

            return new MatchIntelligenceContext.NewsBlock
            {
                Volume24h = ev.TotalEvidence,
                TopType = signals.FirstOrDefault()
                          ?? ev.Signals.OrderByDescending(s => s.Value).Select(s => s.Key).FirstOrDefault(),
                Themes = ev.TopHeadlines.Select(h => Trim(h, 70))
                           .Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>()
                           .Take(MaxNewsThemes).ToList(),
                Signals = signals.Take(5).ToList(),
                FromEvidence = true
            };
        }

        // Takip eden kullanıcı sayısını niteliksel banda indirir (dürüst proxy).
        private static string InterestLevel(int watchers) =>
            watchers >= 5000 ? "Yüksek" : watchers >= 1000 ? "Orta" : "Düşük";

        private static string? Trim(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length <= max ? s : s[..max].TrimEnd() + "…";
        }
    }
}
