using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Pipeline.Abstractions;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Pipeline;

/// <summary>
/// GDP uçtan uca pipeline'ı.
/// Kayıtlı aşamaları <see cref="PipelineStage"/> sırasına göre çalıştırır; her aşamayı İZOLE eder:
/// bir aşama istisna atsa bile pipeline çökmez, hata o aşamanın <see cref="StageResult"/>'ında raporlanır
/// ve kalan aşamalar çalışmaya devam eder (her aşama bağımsızdır).
///
/// Bu faz İSKELET: aşamalar gerçek veri işlemez; amaç uçtan uca çalışan pipeline altyapısıdır.
/// KAPSAM DIŞI: yeni iş kuralı / gerçek algoritma / DB burada YOKTUR.
/// </summary>
public sealed class GlobalDataPipeline : IGlobalDataPipeline
{
    private readonly IReadOnlyList<IPipelineStage> _stages;
    private readonly ILogger<GlobalDataPipeline> _logger;

    public GlobalDataPipeline(IEnumerable<IPipelineStage> stages, ILogger<GlobalDataPipeline> logger)
    {
        _stages = (stages ?? Enumerable.Empty<IPipelineStage>())
            .Where(s => s is not null)
            .OrderBy(s => s.Stage)
            .ToList();
        _logger = logger;
    }

    public async Task<PipelineResult> RunAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        var startedAt = DateTimeOffset.UtcNow;
        var pipelineTimer = Stopwatch.StartNew();

        var stageResults = new List<StageResult>(_stages.Count);
        var allSucceeded = true;

        _logger.LogInformation("GDP pipeline başladı: formaxMatchId={FormaxMatchId} matchId={MatchId} aşamaSayısı={StageCount}",
            context.FormaxMatchId, context.MatchId, _stages.Count);

        foreach (var stage in _stages)
        {
            var stageStartedAt = DateTimeOffset.UtcNow;
            var stageTimer = Stopwatch.StartNew();
            var success = true;
            string? error = null;

            try
            {
                await stage.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // İzolasyon: aşama hatası pipeline'ı çökertmez; raporlanır.
                success = false;
                error = ex.Message;
                allSucceeded = false;
            }
            finally
            {
                stageTimer.Stop();
            }

            if (success)
            {
                _logger.LogInformation("GDP aşaması OK: {Stage} ({DurationMs} ms)",
                    stage.Stage, stageTimer.Elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogError("GDP aşaması HATA: {Stage} ({DurationMs} ms) — {Error}",
                    stage.Stage, stageTimer.Elapsed.TotalMilliseconds, error);
            }

            stageResults.Add(new StageResult
            {
                Stage = stage.Stage,
                Success = success,
                Error = error,
                StartedAt = stageStartedAt,
                Duration = stageTimer.Elapsed
            });
        }

        pipelineTimer.Stop();

        _logger.LogInformation("GDP pipeline bitti: formaxMatchId={FormaxMatchId} success={Success} toplam={DurationMs} ms",
            context.FormaxMatchId, allSucceeded, pipelineTimer.Elapsed.TotalMilliseconds);

        return new PipelineResult
        {
            FormaxMatchId = context.FormaxMatchId,
            StartedAt = startedAt,
            Duration = pipelineTimer.Elapsed,
            Success = allSucceeded,
            Stages = stageResults,
            Context = context
        };
    }
}
