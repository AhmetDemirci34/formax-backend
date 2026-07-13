using System;
using System.Collections.Generic;
using Formax.Application.Services.Radar.Sources.Collection;

namespace Formax.Application.Services.Radar.Sources.Normalization
{
    /// <summary>
    /// Radar Source Engine (R.8.4) — default two-stage normalizer.
    ///
    /// Stage 1 (structural): reject items with no usable raw name → Invalid.
    /// Stage 2 (entity resolution): resolve the raw name via the alias resolver.
    ///   - resolved   → CanonicalSourceItem
    ///   - unresolved → UnresolvedSourceItem (kept, never discarded)
    ///
    /// Raw name is read from the item's "name" field when present, else falls back to
    /// RawId. Pure and side-effect free; persistence happens in a later sprint.
    /// </summary>
    public sealed class NormalizerPipeline : ISourceNormalizer
    {
        private readonly ISourceAliasResolver _resolver;

        public NormalizerPipeline(ISourceAliasResolver resolver)
        {
            _resolver = resolver;
        }

        public SourceNormalizationResult Normalize(
            NormalizationContext context, IReadOnlyList<SourceCollectorItem> items)
        {
            if (items is null || items.Count == 0)
                return SourceNormalizationResult.Empty(context.SourceKey, context.NowUtc);

            var canonical = new List<CanonicalSourceItem>();
            var unresolved = new List<UnresolvedSourceItem>();
            int invalid = 0;

            foreach (var item in items)
            {
                var rawName = ExtractRawName(item);

                // Stage 1 — structural validity.
                if (string.IsNullOrWhiteSpace(rawName))
                {
                    invalid++;
                    continue;
                }

                // Stage 2 — entity resolution.
                var resolution = _resolver.Resolve(rawName);
                if (resolution.Resolved)
                {
                    canonical.Add(new CanonicalSourceItem
                    {
                        SourceKey = context.SourceKey,
                        RawId = item.RawId,
                        EntityType = resolution.EntityType,
                        RawName = rawName,
                        CanonicalName = resolution.CanonicalName,
                        ResolvedTeamId = resolution.CanonicalTeamId,
                        OccurredAtUtc = item.OccurredAtUtc,
                        Payload = item.RawPayload,
                        NormalizedAtUtc = context.NowUtc
                    });
                }
                else
                {
                    unresolved.Add(new UnresolvedSourceItem
                    {
                        SourceKey = context.SourceKey,
                        RawId = item.RawId,
                        RawName = rawName,
                        EntityType = "unknown",
                        Reason = "no alias match",
                        Payload = item.RawPayload,
                        SeenAtUtc = context.NowUtc
                    });
                }
            }

            return new SourceNormalizationResult
            {
                SourceKey = context.SourceKey,
                Canonical = canonical,
                Unresolved = unresolved,
                InvalidCount = invalid,
                NormalizedAtUtc = context.NowUtc
            };
        }

        /// <summary>Raw name from the item's "name" field, falling back to RawId.</summary>
        private static string ExtractRawName(SourceCollectorItem item)
        {
            if (item.Fields is not null &&
                item.Fields.TryGetValue("name", out var name) &&
                !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
            return item.RawId ?? string.Empty;
        }
    }
}
