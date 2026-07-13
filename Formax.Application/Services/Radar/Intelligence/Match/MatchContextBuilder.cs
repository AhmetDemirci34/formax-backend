using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Intelligence.Match
{
    /// <summary>
    /// Radar Match Intelligence (R.9.2) — default context builder. Reads existing DB
    /// data and assembles teams, league, ranks, both teams' form, the H2H summary, and
    /// an importance assessment into <see cref="MatchContextData"/>. Hardcoded fallbacks
    /// are used when data is absent (the point is richer context, not perfect data).
    /// </summary>
    public sealed class MatchContextBuilder : IMatchContextBuilder
    {
        private const int FormWindow = 5;
        private const int H2HWindow = 10;

        private static readonly HashSet<int> BigFour = new() { 1, 2, 3, 4 };

        private readonly IMatchIntelligenceRepository _repository;
        private readonly ILogger<MatchContextBuilder> _logger;

        public MatchContextBuilder(
            IMatchIntelligenceRepository repository,
            ILogger<MatchContextBuilder> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<MatchContextData?> BuildAsync(int matchId, CancellationToken ct = default)
        {
            var match = await _repository.GetMatchWithTeamsAsync(matchId, ct);
            if (match is null)
            {
                _logger.LogDebug("[MATCH CONTEXT] match {Id} not found.", matchId);
                return null;
            }

            var homeRecent = await _repository.GetRecentFinishedByTeamAsync(
                match.HomeTeamId, match.MatchDate, FormWindow, ct);
            var awayRecent = await _repository.GetRecentFinishedByTeamAsync(
                match.AwayTeamId, match.MatchDate, FormWindow, ct);
            var h2h = await _repository.GetH2HFinishedAsync(
                match.HomeTeamId, match.AwayTeamId, match.MatchDate, H2HWindow, ct);

            var homeForm = BuildForm(match.HomeTeamId, match.HomeTeam?.Name ?? string.Empty, homeRecent);
            var awayForm = BuildForm(match.AwayTeamId, match.AwayTeam?.Name ?? string.Empty, awayRecent);
            var h2hContext = BuildH2H(match.HomeTeamId, match.AwayTeamId,
                match.HomeTeam?.Name ?? string.Empty, match.AwayTeam?.Name ?? string.Empty, h2h);
            var importance = BuildImportance(match);

            return new MatchContextData
            {
                MatchId = match.Id,
                HomeTeamId = match.HomeTeamId,
                AwayTeamId = match.AwayTeamId,
                HomeTeamName = match.HomeTeam?.Name ?? string.Empty,
                AwayTeamName = match.AwayTeam?.Name ?? string.Empty,
                League = match.League,
                MatchDate = match.MatchDate,
                HomeRank = match.HomeTeam?.LeagueRank,
                AwayRank = match.AwayTeam?.LeagueRank,
                HomeForm = homeForm,
                AwayForm = awayForm,
                H2H = h2hContext,
                Importance = importance
            };
        }

        // ── Form ──────────────────────────────────────────────────────────────
        private static TeamFormContext BuildForm(int teamId, string teamName, IReadOnlyList<Formax.Domain.Entities.Match> recent)
        {
            if (recent.Count == 0)
                return new TeamFormContext { TeamId = teamId, TeamName = teamName };

            int wins = 0, draws = 0, losses = 0, gf = 0, ga = 0;
            var form = new StringBuilder(recent.Count);

            // recent is newest-first.
            foreach (var m in recent)
            {
                var isHome = m.HomeTeamId == teamId;
                var scored = isHome ? m.HomeScore : m.AwayScore;
                var conceded = isHome ? m.AwayScore : m.HomeScore;

                gf += scored;
                ga += conceded;

                if (scored > conceded) { wins++; form.Append('W'); }
                else if (scored == conceded) { draws++; form.Append('D'); }
                else { losses++; form.Append('L'); }
            }

            var played = recent.Count;
            var points = wins * 3 + draws;
            var formScore = Math.Round((double)points / (played * 3) * 100, 1);

            return new TeamFormContext
            {
                TeamId = teamId,
                TeamName = teamName,
                Played = played,
                Wins = wins,
                Draws = draws,
                Losses = losses,
                GoalsFor = gf,
                GoalsAgainst = ga,
                FormString = form.ToString(),
                Points = points,
                FormScore = formScore
            };
        }

        // ── H2H ───────────────────────────────────────────────────────────────
        private static H2HContext BuildH2H(
            int homeId, int awayId, string homeName, string awayName, IReadOnlyList<Formax.Domain.Entities.Match> h2h)
        {
            if (h2h.Count == 0)
                return new H2HContext();

            int homeWins = 0, awayWins = 0, draws = 0, totalGoals = 0;

            foreach (var m in h2h)
            {
                totalGoals += m.HomeScore + m.AwayScore;

                // Resolve winner to current match's home/away teams.
                int homeTeamScore, awayTeamScore;
                if (m.HomeTeamId == homeId)
                {
                    homeTeamScore = m.HomeScore;
                    awayTeamScore = m.AwayScore;
                }
                else
                {
                    homeTeamScore = m.AwayScore;
                    awayTeamScore = m.HomeScore;
                }

                if (homeTeamScore > awayTeamScore) homeWins++;
                else if (homeTeamScore == awayTeamScore) draws++;
                else awayWins++;
            }

            var last = h2h[0];
            var lastHome = last.HomeTeamId == homeId ? homeName : awayName;
            var lastAway = last.HomeTeamId == homeId ? awayName : homeName;
            var lastSummary = $"{lastHome} {last.HomeScore}-{last.AwayScore} {lastAway}";

            return new H2HContext
            {
                Meetings = h2h.Count,
                HomeWins = homeWins,
                AwayWins = awayWins,
                Draws = draws,
                TotalGoals = totalGoals,
                AvgGoals = Math.Round((double)totalGoals / h2h.Count, 2),
                LastMeetingSummary = lastSummary
            };
        }

        // ── Importance ────────────────────────────────────────────────────────
        private static MatchImportanceContext BuildImportance(Formax.Domain.Entities.Match match)
        {
            var homeRank = match.HomeTeam?.LeagueRank;
            var awayRank = match.AwayTeam?.LeagueRank;
            var homeStable = match.HomeTeam?.IsStableTeam ?? false;
            var awayStable = match.AwayTeam?.IsStableTeam ?? false;

            var factors = new List<string>();
            double score = 0;

            var ranksClose = homeRank is > 0 && awayRank is > 0
                             && Math.Abs(homeRank!.Value - awayRank!.Value) <= 2;
            if (ranksClose)
            {
                score += 35;
                factors.Add($"close ranks ({homeRank} vs {awayRank})");
            }

            var bothTopTier = homeRank is > 0 and <= 4 && awayRank is > 0 and <= 4;
            if (bothTopTier)
            {
                score += 35;
                factors.Add("both top-4");
            }

            var bigClubs = BigFour.Contains(match.HomeTeamId) && BigFour.Contains(match.AwayTeamId);
            if (bigClubs)
            {
                score += 20;
                factors.Add("big-four clubs");
            }

            var hasStable = homeStable && awayStable;
            if (hasStable)
            {
                score += 10;
                factors.Add("established clubs");
            }

            return new MatchImportanceContext
            {
                ImportanceScore = Math.Clamp(score, 0, 100),
                RanksClose = ranksClose,
                BothTopTier = bothTopTier,
                HasStableClubs = hasStable,
                Factors = factors
            };
        }
    }
}
