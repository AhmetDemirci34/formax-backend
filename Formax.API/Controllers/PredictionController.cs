using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Infrastructure.Historical.Prediction.Api;
using Formax.Infrastructure.Historical.Prediction.Registry;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    /// <summary>
    /// Probability Engine Prediction API (FAZ 3.7 tahmin + FAZ 3.8 model yönetimi). Tek endpoint ile bir maç için
    /// Probability + Confidence + Explainability birleşik döner; ayrıca model versiyonlarını raporlar/değiştirir.
    /// </summary>
    [ApiController]
    [Route("api/predictions")]
    [Produces("application/json")]
    public sealed class PredictionController : ControllerBase
    {
        private readonly IMatchPredictionService _predictionService;
        private readonly IModelRegistry _modelRegistry;

        public PredictionController(IMatchPredictionService predictionService, IModelRegistry modelRegistry)
        {
            _predictionService = predictionService;
            _modelRegistry = modelRegistry;
        }

        /// <summary>Verilen MatchId için birleşik tahmin üretir (Probability + Confidence + Explainability).</summary>
        /// <response code="200">Tahmin üretildi.</response>
        /// <response code="400">Geçersiz istek (MatchId pozitif olmalı).</response>
        /// <response code="404">Maç için feature bulunamadı (Feature Store'da yok).</response>
        [HttpPost]
        [ProducesResponseType(typeof(MatchPredictionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Predict([FromBody] MatchPredictionRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            try
            {
                var result = await _predictionService.PredictAsync(request.MatchId, cancellationToken);
                if (result is null)
                    return NotFound(new { message = $"MatchId {request.MatchId} için feature bulunamadı (Feature Store'da yok)." });

                return Ok(result);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Prediction API'nin ŞU AN kullandığı aktif modelin metadata'sını döner.</summary>
        [HttpGet("model")]
        [ProducesResponseType(typeof(ModelMetadata), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetActiveModel(CancellationToken cancellationToken)
        {
            var metadata = await _modelRegistry.GetActiveMetadataAsync(cancellationToken);
            return metadata is null
                ? NotFound(new { message = "Aktif model yok (henüz eğitilmedi)." })
                : Ok(metadata);
        }

        /// <summary>Kayıtlı tüm model versiyonlarının metadata'sını listeler.</summary>
        [HttpGet("models")]
        [ProducesResponseType(typeof(IReadOnlyList<ModelMetadata>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListModels(CancellationToken cancellationToken)
            => Ok(await _modelRegistry.ListAsync(cancellationToken));

        /// <summary>Aktif modeli değiştirir (switch veya eski versiyona rollback).</summary>
        /// <response code="200">Aktif model değişti.</response>
        /// <response code="400">Geçersiz istek.</response>
        /// <response code="404">Model versiyonu bulunamadı.</response>
        [HttpPost("model/activate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActivateModel([FromBody] ActivateModelRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            try
            {
                await _modelRegistry.SetActiveAsync(request.VersionId, cancellationToken);
                return Ok(new { activeVersionId = request.VersionId });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
