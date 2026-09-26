using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Outcomes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// UYGUNLUK YAYIN POLİTİKASI (eligibility-publication-1) — organizasyon × market hücrelerinin KARARLI yayın durumu.
    ///
    /// GET uçları salt okunurdur: DB okur, değerlendirme/model eğitimi/dış istek tetiklemez. POST <c>run</c> üç ayrı mod taşır:
    /// dry-run (hiçbir şey yazmaz), evaluate-only (yalnız değerlendirme kaydı; yayın durumu değişmez), publish (yalnız
    /// <c>confirm=PUBLISH</c> ile). Aynı anda tek tur: meşgulse 409 döner, istek beklemez (kullanıcı yolunu bloklamaz).
    /// </summary>
    [ApiController]
    [Route("admin/eligibility-publication")]
    public sealed class AdminEligibilityPublicationController : ControllerBase
    {
        private readonly FormaxDbContext _db;
        private readonly EligibilityPublicationService _svc;

        public AdminEligibilityPublicationController(FormaxDbContext db, EligibilityPublicationService svc)
        {
            _db = db; _svc = svc;
        }

        /// <summary>Güncel yayın durumu, ham kapı sonucu, son beş değerlendirme, PASS/FAIL sayıları, bekleyen ilerleme, sürümler, takvim.</summary>
        [HttpGet("")]
        public async Task<IActionResult> Status(CancellationToken ct) => Ok(await _svc.StatusAsync(DateTime.UtcNow, ct));

        /// <summary>Bir hücrenin bütün değerlendirme geçmişi (değişmez kayıtlar) ve durum defteri.</summary>
        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] int organizationId, [FromQuery] string marketFamily, CancellationToken ct)
        {
            var evals = await _db.MarketEligibilityEvaluations.AsNoTracking()
                .Where(e => e.OrganizationId == organizationId && e.MarketFamily == marketFamily)
                .OrderBy(e => e.EvaluationCutoffUtc).ThenBy(e => e.Mode).ToListAsync(ct);
            var ledger = await _db.MarketEligibilityStateTransitions.AsNoTracking()
                .Where(t => t.OrganizationId == organizationId && t.MarketFamily == marketFamily)
                .OrderBy(t => t.StateVersion).ThenBy(t => t.Id).ToListAsync(ct);
            return Ok(new
            {
                organizationId, marketFamily,
                evaluations = evals.Select(e => new
                {
                    e.EvaluationCutoffUtc, e.Mode, e.Source, e.ModelVersion, e.ModelRunId, e.ConfigHash, e.PolicyVersion, e.GatePolicyVersion,
                    e.SampleCount, e.LogLoss, e.Brier, e.Ece, e.CalibrationSlope, e.CalibrationIntercept, e.BaselineLogLoss, e.DifferenceFromBaseline,
                    e.ConfidenceIntervalLow, e.ConfidenceIntervalHigh, e.Bias, e.Coverage, e.GateStatus, e.RawGateStatus,
                    rawGateReasons = JsonSerializer.Deserialize<List<string>>(e.RawGateReasonsJson), e.EvaluatedAtUtc,
                    e.PublishedStateBefore, e.PublishedStateAfter, e.TransitionReason, e.PublicationRunKey,
                    e.TransitionStatus, e.NewEvidenceCount, e.EvidenceFingerprint
                }),
                ledger
            });
        }

        /// <summary>
        /// Manuel çalıştırma. <paramref name="mode"/>: dry-run | evaluate-only | publish. <paramref name="cutoffs"/>: virgüllü UTC
        /// tarih/saatler (yoksa en son planlı an). publish yalnız <c>confirm=PUBLISH</c> ile; kesimler son yayımlanandan sonra olmalı
        /// ve hafta başına tek yayın yazılır.
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromQuery] string mode = "dry-run", [FromQuery] string? cutoffs = null,
            [FromQuery] string? confirm = null, [FromQuery] string? source = null, [FromQuery] string transitions = "from-bootstrap",
            CancellationToken ct = default)
        {
            if (transitions is not ("from-bootstrap" or "chronological"))
                return BadRequest(new { error = "transitions ∈ {from-bootstrap, chronological}" });
            var m = mode switch
            {
                "dry-run" => EligibilityPublicationMode.DryRun,
                "evaluate-only" => EligibilityPublicationMode.EvaluateOnly,
                "publish" => EligibilityPublicationMode.Publish,
                _ => (EligibilityPublicationMode?)null
            };
            if (m == null) return BadRequest(new { error = "mode ∈ {dry-run, evaluate-only, publish}" });
            if (m == EligibilityPublicationMode.Publish && confirm != "PUBLISH")
                return StatusCode(403, new { error = "publish yalnız confirm=PUBLISH ile (yetkili akış)" });
            var now = DateTime.UtcNow;
            List<DateTime> list;
            try
            {
                list = string.IsNullOrWhiteSpace(cutoffs)
                    ? new List<DateTime> { EligibilityEvaluationSchedule.LatestSlotAtOrBefore(now) }
                    : cutoffs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(c => DateTime.Parse(c, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)).ToList();
            }
            catch (FormatException) { return BadRequest(new { error = "cutoffs: ISO-8601 UTC, virgülle" }); }
            var src = source == "bootstrap-replay" ? EligibilityPublicationSources.BootstrapReplay : EligibilityPublicationSources.Manual;

            if (!await EligibilityPublicationService.Gate.WaitAsync(TimeSpan.Zero, ct))
                return StatusCode(409, new { error = "BUSY — başka bir uygunluk turu sürüyor" });
            try
            {
                // from-bootstrap: içeri alınan matrisin yayına girdiği andan önceki kesimler yalnız kanıt geçmişidir (geçiş üretmez).
                var anchor = transitions == "from-bootstrap" ? await _svc.BootstrapAnchorAsync(ct) : null;
                var report = await _svc.RunAsync(list, m.Value, src, now, anchor, ct);
                return Ok(new
                {
                    transitions, transitionsFromUtc = anchor,
                    report.Mode, report.PolicyVersion, report.ModelVersion, report.ConfigHash, report.TotalMs, report.PeakWorkingSetBytes,
                    report.RecomputeRequestsEnqueued,
                    cutoffs = report.Cutoffs.Select(c => new
                    {
                        c.CutoffUtc, c.WeekKey, c.Result, c.RunKey, c.DurationMs,
                        cells = c.Evaluations.Select(e =>
                        {
                            var t = c.Transitions.FirstOrDefault(x => x.OrganizationId == e.OrganizationId && x.MarketFamily == e.MarketFamily);
                            return new
                            {
                                e.OrganizationId, e.MarketFamily, e.RawGateStatus, e.GateStatus, reasons = e.RawGateReasons, e.SampleCount,
                                e.LogLoss, e.BaselineLogLoss, e.DifferenceFromBaseline, ciLow = e.ConfidenceIntervalLow, ciHigh = e.ConfidenceIntervalHigh,
                                e.Ece, e.Bias, e.Coverage, e.CalibrationSlope, e.CalibrationIntercept,
                                e.TransitionStatus, e.NewEvidenceCount, e.EvidenceFingerprint,
                                evidenceSamples = e.Evidence?.SampleCount, evidenceMaxKickoffUtc = e.Evidence?.MaxKickoffUtc,
                                evidenceMaxResultUpdatedUtc = e.Evidence?.MaxResultUpdatedUtc,
                                before = t?.Before, after = t?.After, reason = t?.Reason
                            };
                        })
                    }),
                    report.FinalStates
                });
            }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
            finally { EligibilityPublicationService.Gate.Release(); }
        }

        /// <summary>Tek seferlik başlangıç aktarımı (idempotent). Normalde tahmin işi eğitimden önce kendisi çağırır.</summary>
        [HttpPost("bootstrap")]
        public async Task<IActionResult> Bootstrap(CancellationToken ct)
        {
            var (created, run, open, closed) = await _svc.EnsureBootstrappedAsync(DateTime.UtcNow, ct);
            return Ok(new { created, sourceModelRunId = run, open, closed });
        }

        /// <summary>Son yayın turunu geri alır (yalnız confirm=ROLLBACK). Değerlendirme kayıtları silinmez.</summary>
        [HttpPost("rollback")]
        public async Task<IActionResult> Rollback([FromQuery] string runKey, [FromQuery] string? confirm, CancellationToken ct)
        {
            if (confirm != "ROLLBACK") return StatusCode(403, new { error = "rollback yalnız confirm=ROLLBACK ile" });
            if (!await EligibilityPublicationService.Gate.WaitAsync(TimeSpan.Zero, ct)) return StatusCode(409, new { error = "BUSY" });
            try
            {
                var (result, reverted) = await _svc.RollbackAsync(runKey, DateTime.UtcNow, ct);
                return Ok(new { result, reverted });
            }
            finally { EligibilityPublicationService.Gate.Release(); }
        }
    }
}
