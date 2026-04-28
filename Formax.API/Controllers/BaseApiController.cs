using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Formax.API.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected bool IsPremiumUser =>
            User?.FindFirst("premium")?.Value == "true";
    }
}
