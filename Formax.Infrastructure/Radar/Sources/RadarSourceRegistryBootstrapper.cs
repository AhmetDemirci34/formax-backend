using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Formax.Infrastructure.Radar.Sources
{
    /// <summary>
    /// Radar Source Engine (R.8.1) — syncs appsettings-authored source definitions
    /// (Radar:Sources[]) into the SourceDefinitions table and guarantees a paired
    /// SourceStatus row for each. Idempotent: safe to run on every startup.
    ///
    /// Invoked once during application startup (alongside seeding). Does not collect
    /// data — it only keeps the registry tables in sync with configuration.
    /// </summary>
    public sealed class RadarSourceRegistryBootstrapper
    {
        private readonly ISourceDefinitionRepository _definitions;
        private readonly ISourceStatusRepository _statuses;
        private readonly RadarSourceOptions _options;
        private readonly ILogger<RadarSourceRegistryBootstrapper> _logger;

        public RadarSourceRegistryBootstrapper(
            ISourceDefinitionRepository definitions,
            ISourceStatusRepository statuses,
            IOptions<RadarSourceOptions> options,
            ILogger<RadarSourceRegistryBootstrapper> logger)
        {
            _definitions = definitions;
            _statuses = statuses;
            _options = options.Value;
            _logger = logger;
        }

        public async Task SyncAsync(CancellationToken ct = default)
        {
            var sources = _options.Sources;
            if (sources is null || sources.Count == 0)
            {
                _logger.LogInformation("[RADAR SOURCE REGISTRY] no sources configured; skipping sync.");
                return;
            }

            var now = DateTime.UtcNow;

            foreach (var cfg in sources)
            {
                if (string.IsNullOrWhiteSpace(cfg.SourceKey))
                {
                    _logger.LogWarning("[RADAR SOURCE REGISTRY] skipping source with empty SourceKey ({Name}).", cfg.Name);
                    continue;
                }

                var existing = await _definitions.GetByKeyAsync(cfg.SourceKey, ct);

                var definition = new SourceDefinition
                {
                    SourceKey = cfg.SourceKey,
                    Name = cfg.Name,
                    Type = cfg.Type,
                    Category = cfg.Category,
                    FailoverGroup = cfg.FailoverGroup,
                    Priority = cfg.Priority,
                    Endpoint = cfg.Endpoint,
                    ScheduleExpr = cfg.ScheduleExpr,
                    Enabled = cfg.Enabled,
                    Lifecycle = cfg.Lifecycle,
                    CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                    UpdatedAtUtc = now
                };

                await _definitions.UpsertAsync(definition, ct);
            }

            await _definitions.SaveChangesAsync(ct);

            // Guarantee a status row per definition (ids are now populated).
            var all = await _definitions.GetAllAsync(ct);
            foreach (var def in all)
            {
                await _statuses.EnsureExistsAsync(def.Id, ct);
            }
            await _statuses.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[RADAR SOURCE REGISTRY] synced {Count} source definition(s).", all.Count);
        }
    }
}
