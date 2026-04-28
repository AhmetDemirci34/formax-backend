using Formax.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/prediction-types")]
    public class PredictionTypesController : ControllerBase
    {
        private readonly PredictionTypeService _service;

        public PredictionTypesController(PredictionTypeService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var result = _service.GetAll();
            return Ok(result);
        }
    }
}
