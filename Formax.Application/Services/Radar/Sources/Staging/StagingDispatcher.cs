using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Radar.Sources.Normalization;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Radar.Sources.Staging
{
    /// <summary>
    /// Radar Source Engine (R.8.5) — default dispatcher. Maps each
    /// <see cref="CanonicalSourceItem"/> to a <see cref="StagedSourceItem"/>
    /// (Processed=false, ExpiresAt = NormalizedAt + TTL) and persists the batch.
    /// Unresolved items are NOT staged this sprint (their durable queue is a later
    /// sprint); they are only counted here.
    /// </summary>
    public sealed class StagingDispatcher : IStagingDispatcher
    {
        /// <summary>Default staging TTL (mirrors the NABIZ 48h retention convention).</summary>
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(48);

        private readonly IStagingRepository _repository;
        private readonly ILogger<StagingDispatcher> _logger;

        public StagingDispatcher(
            IStagingRepository repository,
            ILogger<StagingDispatcher> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<StagingWriteResult> DispatchAsync(
            SourceDefinition definition,
            SourceNormalizationResult normalizationResult,
            CancellationToken ct = default)
        {
            var canonical = normalizationResult.Canonical;
            if (canonical.Count == 0)
                return StagingWriteResult.Empty(definition.SourceKey);

            var now = DateTime.UtcNow;
            var ttlSeconds = (int)DefaultTtl.TotalSeconds;

            var rows = new List<StagedSourceItem>(canonical.Count);
            foreach (var item in canonical)
            {
                rows.Add(new StagedSourceItem
                {
                    SourceId = definition.Id,
                    SourceKey = definition.SourceKey,
                    Category = definition.Category,
                    RawId = item.RawId,
                    EntityType = item.EntityType,
                    RawName = item.RawName,
                    CanonicalName = item.CanonicalName,
                    ResolvedTeamId = item.ResolvedTeamId,
                    Payload = item.Payload,
                    CollectedAtUtc = item.OccurredAtUtc ?? item.NormalizedAtUtc,
                    NormalizedAtUtc = item.NormalizedAtUtc,
                    TtlSeconds = ttlSeconds,
                    ExpiresAtUtc = item.NormalizedAtUtc.Add(DefaultTtl),
                    Processed = false,
                    CreatedAtUtc = now
                });
            }

            try
            {
                await _repository.AddRangeAsync(rows, ct);
                await _repository.SaveChangesAsync(ct);

                _logger.LogDebug(
                    "[RADAR STAGING] {Key} wrote {Count} item(s).",
                    definition.SourceKey, rows.Count);

                return StagingWriteResult.Written(definition.SourceKey, rows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RADAR STAGING] {Key} write failed.", definition.SourceKey);
                return StagingWriteResult.Failed(definition.SourceKey, ex.Message);
            }
        }
    }
}
