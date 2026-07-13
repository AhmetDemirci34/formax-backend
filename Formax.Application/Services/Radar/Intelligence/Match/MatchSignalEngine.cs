using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.3) — rule-based signal engine. Runs a fixed rule
    /// set over the enriched <see cref="MatchContextData"/>: the existing structural
    /// rules (Derby/Final/Playoff/TitleRace/RelegationBattle/Rivalry/ImportantMatch)
    /// are preserved, and new context rules (form / H2H / importance) are added. A match
    /// may fire several signals; the highest weight becomes primary.
    /// </summary>
    public sealed class MatchSignalEngine : IMatchSignalEngine
    {
        private const double StrongFormThreshold = 75;
        private const double WeakFormThreshold = 30;
        private const double FormAdvantageGap = 30;
        private const double HighImportanceThreshold = 70;
        private const int H2HMinMeetings = 3;
        private const double H2HDominanceRatio = 0.60;

        // ── R.9.5: enrichment thresholds ──────────────────────────────────────
        private const int NewsAttentionThreshold = 3;
        private const int NewsMomentumThreshold = 5;
        private const int UserAttentionThreshold = 3;
        private const int FollowAttentionThreshold = 5;
        private const int SourceConfidenceDistinctSources = 2;

        private static readonly HashSet<int> BigFour = new() { 1, 2, 3, 4 };
        private static readonly (int, int) DerbyPair = (1, 2);

        private readonly IReadOnlyList<MatchSignalRule> _rules;

        public MatchSignalEngine()
        {
            _rules = BuildRules();
        }

        public MatchSignalResult Evaluate(MatchContextData context)
        {
            var signals = new List<MatchSignal>();
            foreach (var rule in _rules)
            {
                var signal = rule.Evaluate(context);
                if (signal is not null)
                    signals.Add(signal);
            }

            if (signals.Count == 0)
                return new MatchSignalResult { PrimarySignalType = MatchSignalType.None };

            var ordered = signals.OrderByDescending(s => s.Weight).ToList();
            return new MatchSignalResult
            {
                Signals = ordered,
                PrimarySignalType = ordered[0].Type,
                Summary = string.Join(" · ", ordered.Select(s => s.Label))
            };
        }

        private static List<MatchSignalRule> BuildRules() => new()
        {
            // ── Structural rules (preserved from R.9.1) ───────────────────────
            new("Final", c =>
                (c.League?.Contains("Final", StringComparison.OrdinalIgnoreCase) ?? false)
                    ? Sig(MatchSignalType.Final, "Final", "competition stage: final")
                    : null),

            new("Playoff", c =>
                (c.League?.Contains("Play", StringComparison.OrdinalIgnoreCase) ?? false)
                    ? Sig(MatchSignalType.Playoff, "Playoff", "competition stage: playoff")
                    : null),

            new("Derby", c =>
                IsPair(c, DerbyPair.Item1, DerbyPair.Item2)
                    ? Sig(MatchSignalType.Derby, "Derbi", $"{c.HomeTeamName} - {c.AwayTeamName}")
                    : null),

            new("Rivalry", c =>
                !IsPair(c, DerbyPair.Item1, DerbyPair.Item2)
                && BigFour.Contains(c.HomeTeamId) && BigFour.Contains(c.AwayTeamId)
                    ? Sig(MatchSignalType.Rivalry, "Rekabet", "both teams are big-four clubs")
                    : null),

            new("TitleRace", c =>
                c.HomeRank is > 0 and <= 3 && c.AwayRank is > 0 and <= 3
                    ? Sig(MatchSignalType.TitleRace, "Zirve Yarışı", $"ranks {c.HomeRank} vs {c.AwayRank}")
                    : null),

            new("RelegationBattle", c =>
                c.HomeRank is >= 15 && c.AwayRank is >= 15
                    ? Sig(MatchSignalType.RelegationBattle, "Düşme Hattı", $"ranks {c.HomeRank} vs {c.AwayRank}")
                    : null),

            new("ImportantMatch", c =>
            {
                var closeRanks = c.HomeRank is > 0 && c.AwayRank is > 0
                                 && Math.Abs(c.HomeRank!.Value - c.AwayRank!.Value) <= 2;
                return closeRanks
                    ? Sig(MatchSignalType.ImportantMatch, "Önemli Maç", "close league positions")
                    : null;
            }),

            // ── R.9.3: form-based ─────────────────────────────────────────────
            new("StrongForm", c =>
            {
                var strong = MaxForm(c);
                return strong is not null && strong.FormScore >= StrongFormThreshold
                    ? Sig(MatchSignalType.StrongForm, "Güçlü Form",
                        $"{strong.TeamName} form {strong.FormString} ({strong.FormScore})")
                    : null;
            }),

            new("WeakForm", c =>
            {
                var weak = MinForm(c);
                return weak is not null && weak.FormScore <= WeakFormThreshold
                    ? Sig(MatchSignalType.WeakForm, "Zayıf Form",
                        $"{weak.TeamName} form {weak.FormString} ({weak.FormScore})")
                    : null;
            }),

            new("FormAdvantage", c =>
                c.HomeForm.HasData && c.AwayForm.HasData
                && Math.Abs(c.HomeForm.FormScore - c.AwayForm.FormScore) >= FormAdvantageGap
                    ? Sig(MatchSignalType.FormAdvantage, "Form Avantajı",
                        $"home {c.HomeForm.FormScore} vs away {c.AwayForm.FormScore}")
                    : null),

            // ── R.9.3: H2H-based ──────────────────────────────────────────────
            new("HistoricalDominance", c =>
            {
                if (c.H2H.Meetings < H2HMinMeetings) return null;
                var hw = (double)c.H2H.HomeWins / c.H2H.Meetings;
                var aw = (double)c.H2H.AwayWins / c.H2H.Meetings;
                if (hw >= H2HDominanceRatio)
                    return Sig(MatchSignalType.HistoricalDominance, "Tarihsel Üstünlük",
                        $"{c.HomeTeamName} {c.H2H.HomeWins}/{c.H2H.Meetings}");
                if (aw >= H2HDominanceRatio)
                    return Sig(MatchSignalType.HistoricalDominance, "Tarihsel Üstünlük",
                        $"{c.AwayTeamName} {c.H2H.AwayWins}/{c.H2H.Meetings}");
                return null;
            }),

            new("BalancedRivalry", c =>
            {
                if (c.H2H.Meetings < H2HMinMeetings) return null;
                var diff = Math.Abs(c.H2H.HomeWins - c.H2H.AwayWins);
                return diff <= 1
                    ? Sig(MatchSignalType.BalancedRivalry, "Dengeli Rekabet",
                        $"H2H {c.H2H.HomeWins}-{c.H2H.Draws}-{c.H2H.AwayWins}")
                    : null;
            }),

            // ── R.9.3: importance-based ───────────────────────────────────────
            new("HighImportance", c =>
                c.Importance.ImportanceScore >= HighImportanceThreshold
                    ? Sig(MatchSignalType.HighImportance, "Yüksek Öneme Sahip Maç",
                        $"importance {c.Importance.ImportanceScore}")
                    : null),

            // ── R.9.5: enrichment-based (deterministic; reads context.Enrichment) ─
            new("NewsAttention", c =>
                (c.Enrichment?.NewsCount ?? 0) >= NewsAttentionThreshold
                && (c.Enrichment!.NewsCount) < NewsMomentumThreshold
                    ? Sig(MatchSignalType.NewsAttention, "Haber İlgisi",
                        $"news items {c.Enrichment.NewsCount}")
                    : null),

            new("NewsMomentum", c =>
                (c.Enrichment?.NewsCount ?? 0) >= NewsMomentumThreshold
                    ? Sig(MatchSignalType.NewsMomentum, "Haber Momentumu",
                        $"news items {c.Enrichment!.NewsCount}")
                    : null),

            new("UserAttention", c =>
                (c.Enrichment?.InternalSignalCount ?? 0) >= UserAttentionThreshold
                && (c.Enrichment!.InternalSignalCount) < FollowAttentionThreshold
                    ? Sig(MatchSignalType.UserAttention, "Kullanıcı İlgisi",
                        $"internal signals {c.Enrichment.InternalSignalCount}")
                    : null),

            new("FollowAttention", c =>
                (c.Enrichment?.InternalSignalCount ?? 0) >= FollowAttentionThreshold
                    ? Sig(MatchSignalType.FollowAttention, "Takip İlgisi",
                        $"internal signals {c.Enrichment!.InternalSignalCount}")
                    : null),

            new("SourceConfidence", c =>
                DistinctSourceCount(c.Enrichment) >= SourceConfidenceDistinctSources
                    ? Sig(MatchSignalType.SourceConfidence, "Kaynak Doğrulaması",
                        $"{DistinctSourceCount(c.Enrichment)} distinct sources")
                    : null),

            // ── R.10.4: News Intelligence integration ─────────────────────────
            new("NewsDrivenMatch", c =>
                c.NewsImpactLevel >= NewsImpactLevel.High
                    ? Sig(MatchSignalType.NewsDrivenMatch, "Haber Odaklı Maç",
                        $"news impact {c.NewsImpactLevel} ({c.NewsImpactScore})")
                    : null),

            // ── R.11.4: Synthetic Odds integration ────────────────────────────
            new("MarketAttention", c =>
                c.SyntheticSignalLevel >= SyntheticOddsLevel.High
                    ? Sig(MatchSignalType.MarketAttention, "Piyasa İlgisi",
                        $"synthetic {c.SyntheticSignalLevel} ({c.SyntheticSignalScore})")
                    : null),
        };

        private static MatchSignal Sig(MatchSignalType type, string label, string reason)
            => MatchSignal.Of(type, label, reason, MatchSignalWeight.For(type));

        private static bool IsPair(MatchContextData c, int a, int b)
            => (c.HomeTeamId == a && c.AwayTeamId == b)
            || (c.HomeTeamId == b && c.AwayTeamId == a);

        /// <summary>Number of enrichment source categories that contributed items
        /// (News / InternalSignal / SourceMetadata). Used for multi-source confidence.</summary>
        private static int DistinctSourceCount(ContextEnrichmentResult? e)
        {
            if (e is null || !e.HasEnrichment) return 0;
            var count = 0;
            if (e.NewsCount > 0) count++;
            if (e.InternalSignalCount > 0) count++;
            if (e.MetadataCount > 0) count++;
            return count;
        }

        private static TeamFormContext? MaxForm(MatchContextData c)
        {
            if (!c.HomeForm.HasData && !c.AwayForm.HasData) return null;
            if (!c.AwayForm.HasData) return c.HomeForm;
            if (!c.HomeForm.HasData) return c.AwayForm;
            return c.HomeForm.FormScore >= c.AwayForm.FormScore ? c.HomeForm : c.AwayForm;
        }

        private static TeamFormContext? MinForm(MatchContextData c)
        {
            if (!c.HomeForm.HasData && !c.AwayForm.HasData) return null;
            if (!c.AwayForm.HasData) return c.HomeForm;
            if (!c.HomeForm.HasData) return c.AwayForm;
            return c.HomeForm.FormScore <= c.AwayForm.FormScore ? c.HomeForm : c.AwayForm;
        }
    }
}
