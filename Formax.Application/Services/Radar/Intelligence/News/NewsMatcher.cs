using System;
using System.Collections.Generic;
using Formax.Application.Services.Radar.Sources.Normalization;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.1) — default matcher. Builds the item's searchable
    /// text (raw name + canonical name + payload) and flags a team mention when one of
    /// its aliases (length ≥ 4, to avoid noisy short codes) appears as a case-insensitive
    /// substring. A resolved team id counts as a direct mention. Deterministic.
    /// </summary>
    public sealed class NewsMatcher : INewsMatcher
    {
        private const int MinAliasLength = 4;

        private readonly IReadOnlyList<SourceAlias> _aliases;

        public NewsMatcher(ISourceAliasProvider aliasProvider)
        {
            _aliases = aliasProvider.GetAliases();
        }

        public IReadOnlyList<NewsMention> DetectMentions(StagedSourceItem newsItem)
        {
            var text = $"{newsItem.RawName} {newsItem.CanonicalName} {newsItem.Payload}".ToLowerInvariant();
            var occurredAt = newsItem.NormalizedAtUtc;

            var seenTeamIds = new HashSet<int>();
            var mentions = new List<NewsMention>();

            // Direct: resolved team id on the staged item.
            if (newsItem.ResolvedTeamId is int rid && rid > 0 && seenTeamIds.Add(rid))
            {
                mentions.Add(new NewsMention
                {
                    TeamId = rid,
                    TeamName = newsItem.CanonicalName,
                    MatchedAlias = newsItem.CanonicalName,
                    SourceKey = newsItem.SourceKey,
                    NewsRawId = newsItem.RawId,
                    OccurredAtUtc = occurredAt
                });
            }

            // Alias substring scan.
            foreach (var alias in _aliases)
            {
                if (alias.CanonicalTeamId is not int teamId || teamId <= 0) continue;
                if (string.IsNullOrWhiteSpace(alias.Alias) || alias.Alias.Length < MinAliasLength) continue;
                if (seenTeamIds.Contains(teamId)) continue;

                if (text.Contains(alias.Alias.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    seenTeamIds.Add(teamId);
                    mentions.Add(new NewsMention
                    {
                        TeamId = teamId,
                        TeamName = alias.CanonicalName,
                        MatchedAlias = alias.Alias,
                        SourceKey = newsItem.SourceKey,
                        NewsRawId = newsItem.RawId,
                        OccurredAtUtc = occurredAt
                    });
                }
            }

            return mentions;
        }
    }
}
