using Formax.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/formax/favorites")]
    public class AIFavoritesController : ControllerBase
    {
        private readonly GetDailyAIFavoriteMatchesUseCase _useCase;

        public AIFavoritesController(GetDailyAIFavoriteMatchesUseCase useCase)
        {
            _useCase = useCase;
        }

        [HttpGet("today")]
        public IActionResult GetToday()
        {
            var result = _useCase.Execute();
            return Ok(result);
        }
    }
}
