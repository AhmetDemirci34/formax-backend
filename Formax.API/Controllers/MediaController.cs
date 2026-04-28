using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Branding;
using Formax.Application.UseCases.Media;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/media")]
    public class MediaController : BaseApiController
    {
        private readonly GetReadableMatchAnalysisUseCase _useCase;

        public MediaController(GetReadableMatchAnalysisUseCase useCase)
        {
            _useCase = useCase;
        }

        /// <summary>
        /// Medya ve bilgilendirme amaçlı okunabilir maç analizi döner.
        /// Response, AIStateMeta alanı ile AI karar davranışını açıklar.
        /// </summary>
        /// <param name="matchId">Analizi istenen maçın ID'si</param>
        /// <returns>ReadableMatchAnalysisDto</returns>
        [HttpGet("match/{matchId}/analysis")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReadableAnalysis(
            int matchId,
            CancellationToken cancellationToken)
        {
            var brand = new BrandProfileDto
            {
                BrandCode = "FORMAX",
                DisplayName = "FORMAX",
                Tone = "Neutral",
                DisclaimerSuffix = "FORMAX markası altında sunulmaktadır.",
                IsPremium = IsPremiumUser
            };

            var result = await _useCase.Execute(
                matchId,
                brand,
                cancellationToken);

            return Ok(result);
        }
    }
}
