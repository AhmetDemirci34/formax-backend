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
    /// Radar Learning (R.14.2) — default orchestration service. Does ALL I/O (reads events
    /// + match/team + signals via existing repositories), assembles plain context, and
    /// hands it to the pure <see cref="IUserInterestEngine"/>. No persistence.
    /// </summary>
    public sealed class UserInterestProfileService : IUserInterestProfileService
    {
        private const int EventWindow = 1000;

        private readonly ILearningEventRepository _events;
        private readonly IMatchIntelligenceRepository _matchIntel;
        private readonly IUserInterestEngine _engine;

        public UserInterestProfileService(
            ILearningEventRepository events,
            IMatchIntelligenceRepository matchIntel,
            IUserInterestEngine engine)
        {
            _events = events;
            _matchIntel = matchIntel;
            _engine = engine;
        }

        public async Task<UserInterestProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
        {
            var events = await _events.GetByUserAsync(userId, EventWindow, ct);
            if (events.Count == 0)
                return new UserInterestProfileDto { UserId = userId };

            var matchIds = events.Select(e => e.MatchId).Distinct().ToList();

            var matchContext = new Dictionary<int, InterestMatchContext>();
            var signalContext = new Dictionary<int, IReadOnlyList<InterestSignal>>();

            foreach (var id in matchIds)
            {
                var match = await _matchIntel.GetMatchWithTeamsAsync(id, ct);
                if (match is not null)
                {
                    matchContext[id] = new InterestMatchContext
                    {
                        MatchId = id,
                        HomeTeamName = match.HomeTeam?.Name ?? string.Empty,
                        AwayTeamName = match.AwayTeam?.Name ?? string.Empty,
                        League = match.League
                    };
                }

                var snapshot = await _matchIntel.GetByMatchIdAsync(id, ct);
                if (snapshot is not null)
                    signalContext[id] = ParseSignals(snapshot.SignalsJson, snapshot.PrimarySignalType);
            }

            return _engine.Compute(userId, events, matchContext, signalContext);
        }

        private static IReadOnlyList<InterestSignal> ParseSignals(string? json, MatchSignalType primary)
        {
            if (string.IsNullOrWhiteSpace(json)) return System.Array.Empty<InterestSignal>();
            try
            {
                var dtos = JsonSerializer.Deserialize<List<SignalDto>>(json);
                if (dtos is null) return System.Array.Empty<InterestSignal>();
                return dtos
                    .Select(d => new InterestSignal
                    {
                        Type = (MatchSignalType)d.Type,
                        IsPrimary = (MatchSignalType)d.Type == primary
                    })
                    .ToList();
            }
            catch
            {
                return System.Array.Empty<InterestSignal>();
            }
        }

        private sealed class SignalDto
        {
            public int Type { get; set; }
        }
    }
}
