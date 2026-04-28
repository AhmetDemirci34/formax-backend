using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/debug/ai")]
    public class AiDebugController : ControllerBase
    {
        [HttpGet("force-active")]
        public IActionResult ForceActive()
        {
            return Ok(new
            {
                faiState = "ACTIVE",
                analysisText = "Mevcut koşullar; form durumu, kadro istikrarı ve maç temposu açısından değerlendirilebilir seviyede."
            });
        }
    }
}
