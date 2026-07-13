using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Conflict;
using Formax.Infrastructure.MatchIdentity;
using Formax.Infrastructure.Merge;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Persistence;
using Formax.Infrastructure.Pipeline;
using Formax.Infrastructure.Pipeline.Abstractions;
using Formax.Infrastructure.Providers.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// FORMAX GDP pipeline tetikleyici (FAZ 13 doğrulama noktası).
    /// Onaylı ilk provider'dan (OpenLigaDB) ham veriyi pipeline üzerinden geçirir; sonucu döndürür.
    /// </summary>
    [ApiController]
    [Route("api/gdp")]
    public class GdpController : ControllerBase
    {
        private readonly IGlobalDataPipeline _pipeline;

        public GdpController(IGlobalDataPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        // 🚀 GDP pipeline'ını uçtan uca çalıştırır (Provider → … → Coverage).
        [HttpGet("run")]
        public async Task<IActionResult> Run([FromQuery] int? matchId, [FromQuery] string? formaxMatchId, CancellationToken cancellationToken)
        {
            // matchId = gerçek Domain Match.Id → Match Context Resolver takım/lig/koordinatı çözer.
            var context = new PipelineContext { MatchId = matchId, FormaxMatchId = formaxMatchId };
            var result = await _pipeline.RunAsync(context, cancellationToken);

            return Ok(new
            {
                result.FormaxMatchId,
                result.StartedAt,
                result.Success,
                DurationMs = result.Duration.TotalMilliseconds,
                Stages = result.Stages.Select(s => new
                {
                    s.Stage,
                    s.Success,
                    s.Error,
                    DurationMs = s.Duration.TotalMilliseconds
                }),
                Raw = new
                {
                    Fixtures = Summarize(context.Get<IReadOnlyList<ProviderResult>>("gdp.raw.fixtures")),
                    Standings = Summarize(context.Get<IReadOnlyList<ProviderResult>>("gdp.raw.standings")),
                    Teams = Summarize(context.Get<IReadOnlyList<ProviderResult>>("gdp.raw.teams")),
                    Weather = Summarize(context.Get<IReadOnlyList<ProviderResult>>("gdp.raw.weather"))
                },
                MappedWeather = context.Get<IReadOnlyList<RawWeather>>("gdp.rawmodel.weather"),
                NormalizedWeather = context.Get<IReadOnlyList<NormalizedWeather>>("gdp.normalized.weather"),
                MergedWeather = (context.Get<IReadOnlyList<MergeResult<NormalizedWeather>>>("gdp.merged.weather") ?? new List<MergeResult<NormalizedWeather>>())
                    .Select(m => new
                    {
                        m.Value,
                        m.Contributors,
                        MergedFields = m.MergedFields,
                        MissingFields = m.MissingFields,
                        ConflictedFields = m.ConflictedFields,
                        Fields = m.Fields.Select(f => new { f.Field, State = f.State.ToString(), f.Provider }),
                        ConflictResolutions = m.ConflictResolutions.Select(res => new
                        {
                            res.Field,
                            res.IsResolved,
                            res.ResolutionStrategy,
                            res.SelectedProvider
                        })
                    }),
                PersistWeather = (context.Get<IReadOnlyList<GdpWeatherPersistOutcome>>("gdp.persist.weather.outcomes") ?? new List<GdpWeatherPersistOutcome>())
                    .Select(o => new { o.FormaxMatchId, Status = o.Status.ToString(), o.Error }),
                MappedFixtures = context.Get<IReadOnlyList<RawFixture>>("gdp.rawmodel.fixtures"),
                NormalizedFixtures = context.Get<IReadOnlyList<NormalizedFixture>>("gdp.normalized.fixtures"),
                Identities = (context.Get<IReadOnlyList<MatchIdentityResult>>("gdp.identities") ?? new List<MatchIdentityResult>())
                    .Select(id => new
                    {
                        FormaxMatchId = id.FormaxMatchId.Value,
                        id.IsNew,
                        id.Score,
                        Confidence = id.Confidence.ToString(),
                        ReferenceCount = id.References.Count
                    }),
                Merged = (context.Get<IReadOnlyList<MergeResult<NormalizedFixture>>>("gdp.merged.fixtures") ?? new List<MergeResult<NormalizedFixture>>())
                    .Select(m => new
                    {
                        m.Value,
                        m.Contributors,
                        MergedFields = m.MergedFields,
                        MissingFields = m.MissingFields,
                        ConflictedFields = m.ConflictedFields,
                        Fields = m.Fields.Select(f => new
                        {
                            f.Field,
                            State = f.State.ToString(),
                            f.Provider
                        }),
                        ConflictResolutions = m.ConflictResolutions.Select(res => new
                        {
                            res.Field,
                            res.IsResolved,
                            res.ResolutionStrategy,
                            res.Confidence,
                            res.SelectedProvider,
                            CandidateCount = res.Candidates.Count
                        })
                    }),
                Persist = (context.Get<IReadOnlyList<GdpPersistOutcome>>("gdp.persist.outcomes") ?? new List<GdpPersistOutcome>())
                    .Select(o => new
                    {
                        o.FormaxMatchId,
                        Status = o.Status.ToString(),
                        o.Error
                    })
            });
        }

        private static object Summarize(IReadOnlyList<ProviderResult>? results) =>
            (results ?? new List<ProviderResult>()).Select(r => new
            {
                r.ProviderName,
                r.Success,
                r.Error,
                PayloadLength = (r.Payload as string)?.Length ?? 0
            });
    }
}
