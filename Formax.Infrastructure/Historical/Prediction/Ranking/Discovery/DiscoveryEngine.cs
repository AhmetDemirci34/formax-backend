using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Discovery;

/// <summary>Discovery Feed üreten motor sözleşmesi (saf, deterministik).</summary>
public interface IDiscoveryEngine
{
    DiscoveryFeed BuildFeed(IReadOnlyList<DiscoveryCandidate> candidates, DiscoveryWeights weights);
}

/// <summary>
/// Discovery Engine — Radar Engine'in ÜST katmanı. Personal Radar Score'u DEĞİŞTİRMEZ; onu kullanarak en
/// değerli + en ÇEŞİTLİ feed'i kurar. Greedy (MMR-benzeri) seçim: her adımda kalan adaylardan, relevance
/// (Personal Score) + çeşitlilik/güncellik/güven − lig/takım/büyük-takım cezalarını en yükseğe çıkaranı seçer.
/// Lig/takım domination'ı HARD CAP ile mutlak engellenir. Hidden gem'ler boost ile görünür olur. Tamamen
/// DETERMİNİSTİK (sabit tie-break: skor → Personal Score → MatchId). Breakdown toplamı = Discovery Score.
/// </summary>
public sealed class DiscoveryEngine : IDiscoveryEngine
{
    public DiscoveryFeed BuildFeed(IReadOnlyList<DiscoveryCandidate> candidates, DiscoveryWeights w)
    {
        if (candidates is null || candidates.Count == 0)
            return new DiscoveryFeed { Items = Array.Empty<DiscoveryFeedItem>(), CandidateCount = 0 };

        // Deterministik freshness normalizasyonu (aday tarih aralığı).
        long minTicks = candidates.Min(c => c.MatchDate.Ticks);
        long maxTicks = candidates.Max(c => c.MatchDate.Ticks);
        double range = maxTicks - minTicks;

        var remaining = candidates.ToList();
        var selected = new List<DiscoveryFeedItem>();
        var leagueCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var teamCount = new Dictionary<int, int>();
        var usedDateBuckets = new HashSet<DateTime>();
        string? lastLeague = null;

        var feedSize = Math.Min(w.FeedSize, candidates.Count);

        for (var rank = 1; rank <= feedSize; rank++)
        {
            DiscoveryCandidate? best = null;
            double bestScore = double.NegativeInfinity;
            List<SignalContribution>? bestBreakdown = null;

            foreach (var c in remaining)
            {
                // Hard cap'ler — domination'ı mutlak engelle.
                if (leagueCount.GetValueOrDefault(c.League) >= w.MaxPerLeague) continue;
                if (teamCount.GetValueOrDefault(c.HomeTeamId) >= w.MaxPerTeam) continue;
                if (teamCount.GetValueOrDefault(c.AwayTeamId) >= w.MaxPerTeam) continue;

                var (score, breakdown) = ScoreCandidate(c, w, leagueCount, teamCount, usedDateBuckets, minTicks, range, lastLeague);

                // Deterministik argmax: skor → Personal Score → küçük MatchId.
                if (best is null
                    || score > bestScore
                    || (score == bestScore && c.PersonalScore > best.PersonalScore)
                    || (score == bestScore && c.PersonalScore == best.PersonalScore && c.MatchId < best.MatchId))
                {
                    best = c; bestScore = score; bestBreakdown = breakdown;
                }
            }

            if (best is null) break; // kalan tüm adaylar cap'leri aşıyor

            selected.Add(new DiscoveryFeedItem
            {
                MatchId = best.MatchId,
                DiscoveryRank = rank,
                DiscoveryScore = Math.Round(bestScore, 4),
                DiscoveryReason = BuildReason(best, bestBreakdown!),
                HiddenGem = best.HiddenGem,
                PersonalScore = best.PersonalScore,
                BaseScore = best.BaseScore,
                Breakdown = bestBreakdown!
            });

            remaining.Remove(best);
            leagueCount[best.League] = leagueCount.GetValueOrDefault(best.League) + 1;
            teamCount[best.HomeTeamId] = teamCount.GetValueOrDefault(best.HomeTeamId) + 1;
            teamCount[best.AwayTeamId] = teamCount.GetValueOrDefault(best.AwayTeamId) + 1;
            usedDateBuckets.Add(best.MatchDate.Date);
            lastLeague = best.League;
        }

        return new DiscoveryFeed { Items = selected, CandidateCount = candidates.Count };
    }

    private static (double score, List<SignalContribution> breakdown) ScoreCandidate(
        DiscoveryCandidate c, DiscoveryWeights w,
        Dictionary<string, int> leagueCount, Dictionary<int, int> teamCount,
        HashSet<DateTime> usedDateBuckets, long minTicks, double range, string? lastLeague)
    {
        var lgCount = leagueCount.GetValueOrDefault(c.League);
        var tmCount = teamCount.GetValueOrDefault(c.HomeTeamId) + teamCount.GetValueOrDefault(c.AwayTeamId);
        var strongInFeed = teamCount.Count; // feed'deki farklı takım sayısı (büyük-takım dengeleme sinyali)
        var freshness = range > 0 ? (c.MatchDate.Ticks - minTicks) / range : 0.0;
        var newBucket = !usedDateBuckets.Contains(c.MatchDate.Date);
        var consecutive = lastLeague is not null && string.Equals(lastLeague, c.League, StringComparison.Ordinal);

        var relevance = w.RelevanceWeight * Math.Clamp(c.PersonalScore / 100.0, 0, 1);
        var leaguePenalty = -w.LeagueRepeatPenalty * lgCount;
        var adjacencyPenalty = consecutive ? -w.ConsecutiveLeaguePenalty : 0.0;
        var teamPenalty = -w.TeamRepeatPenalty * tmCount;
        var bigTeamPenalty = c.TeamStrength >= w.BigTeamThreshold ? -w.BigTeamPenalty * strongInFeed : 0.0;
        var gemBoost = c.HiddenGem ? w.HiddenGemBoost : 0.0;
        var freshnessC = w.FreshnessWeight * freshness;
        var confidenceC = w.ConfidenceWeight * Math.Clamp(c.Confidence, 0, 1);
        var timeC = newBucket ? w.TimeDiversityWeight : 0.0;

        var breakdown = new List<SignalContribution>
        {
            Row("Relevance", Math.Clamp(c.PersonalScore / 100.0, 0, 1), w.RelevanceWeight, relevance, "Personal Radar Score (korunur)"),
            Row("LeagueDiversity", lgCount, w.LeagueRepeatPenalty, leaguePenalty, lgCount == 0 ? "yeni lig" : $"feed'de {lgCount} aynı lig"),
            Row("LeagueAdjacency", consecutive ? 1 : 0, w.ConsecutiveLeaguePenalty, adjacencyPenalty, consecutive ? "bir önceki maçla aynı lig" : "farklı lig"),
            Row("TeamDiversity", tmCount, w.TeamRepeatPenalty, teamPenalty, tmCount == 0 ? "yeni takımlar" : $"{tmCount} takım tekrarı"),
            Row("BigTeamBalance", c.TeamStrength, w.BigTeamPenalty, bigTeamPenalty, c.TeamStrength >= w.BigTeamThreshold ? "büyük takım dengesi" : "n/a"),
            Row("HiddenGem", c.HiddenGem ? 1 : 0, w.HiddenGemBoost, gemBoost, c.HiddenGem ? "Hidden Gem görünürlük" : "-"),
            Row("Freshness", freshness, w.FreshnessWeight, freshnessC, "güncellik"),
            Row("Confidence", Math.Clamp(c.Confidence, 0, 1), w.ConfidenceWeight, confidenceC, "tahmin güvenilirliği"),
            Row("TimeDiversity", newBucket ? 1 : 0, w.TimeDiversityWeight, timeC, newBucket ? "yeni zaman kovası" : "aynı gün")
        };

        var score = relevance + leaguePenalty + adjacencyPenalty + teamPenalty + bigTeamPenalty + gemBoost + freshnessC + confidenceC + timeC;
        return (score, breakdown);
    }

    private static SignalContribution Row(string name, double value, double weight, double contribution, string note)
        => new()
        {
            Signal = name,
            Value = Math.Round(value, 4),
            Weight = weight,
            WeightedContribution = Math.Round(contribution, 4),
            DataAvailable = true,
            Note = note
        };

    private static string BuildReason(DiscoveryCandidate c, List<SignalContribution> breakdown)
    {
        if (c.HiddenGem) return "Hidden Gem — radar-altı kaliteli maç öne çıkarıldı";

        // En büyük POZİTİF katkıya göre gerekçe.
        var top = breakdown.Where(b => b.WeightedContribution > 0).OrderByDescending(b => b.WeightedContribution).FirstOrDefault();
        var reason = top?.Signal switch
        {
            "Relevance" => "Yüksek Personal Radar Score",
            "Freshness" => "Güncel maç",
            "Confidence" => "Yüksek güvenilirlik",
            "TimeDiversity" => "Feed'e zaman çeşitliliği katıyor",
            _ => "Feed'e değer katıyor"
        };
        var league = breakdown.First(b => b.Signal == "LeagueDiversity");
        if (league.WeightedContribution == 0) reason += " + lig çeşitliliği";
        return reason + $" (P={c.PersonalScore.ToString("0.0", CultureInfo.InvariantCulture)})";
    }
}
