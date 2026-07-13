using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Radar;
using Formax.Application.Interfaces;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.3) — default orchestration. Does ALL I/O: gets the user profile
    /// (R.14.2), the match+teams and intelligence snapshot (R.9), parses SignalsJson, then
    /// calls the pure <see cref="IMatchAffinityEngine"/>. No persistence.
    /// </summary>
    public sealed class MatchAffinityService : IMatchAffinityService
    {
        private readonly IUserInterestProfileService _profileService;
        private readonly IMatchIntelligenceRepository _matchIntel;
        private readonly IMatchAffinityEngine _engine;

        public MatchAffinityService(
            IUserInterestProfileService profileService,
            IMatchIntelligenceRepository matchIntel,
            IMatchAffinityEngine engine)
        {
            _profileService = profileService;
            _matchIntel = matchIntel;
            _engine = engine;
        }

        public async Task<MatchAffinityDto> GetAffinityAsync(int userId, int matchId, CancellationToken ct = default)
        {
            var profile = await _profileService.GetProfileAsync(userId, ct);

            var match = await _matchIntel.GetMatchWithTeamsAsync(matchId, ct);
            var snapshot = await _matchIntel.GetByMatchIdAsync(matchId, ct);

            var context = new MatchAffinityContext
            {
                MatchId = matchId,
                HomeTeamName = match?.HomeTeam?.Name ?? string.Empty,
                AwayTeamName = match?.AwayTeam?.Name ?? string.Empty,
                League = match?.League ?? string.Empty,
                ImportanceScore = snapshot?.ImportanceScore ?? 0,
                Signals = ParseSignals(snapshot?.SignalsJson)
            };

            return _engine.Compute(profile, context);
        }

        private static IReadOnlyList<MatchAffinitySignal> ParseSignals(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<MatchAffinitySignal>();
            try
            {
                var dtos = JsonSerializer.Deserialize<List<SignalDto>>(json);
                if (dtos is null) return Array.Empty<MatchAffinitySignal>();
                return dtos
                    .Select(d => new MatchAffinitySignal
                    {
                        Type = (MatchSignalType)d.Type,
                        Weight = d.Weight
                    })
                    .ToList();
            }
            catch
            {
                return Array.Empty<MatchAffinitySignal>();
            }
        }

        private sealed class SignalDto
        {
            public int Type { get; set; }
            public double Weight { get; set; }
        }
    }
}
