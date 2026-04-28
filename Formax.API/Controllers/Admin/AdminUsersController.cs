using Formax.Application.UseCases.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    [ApiController]
    [Route("admin/users")]
    public class AdminUsersController : ControllerBase
    {
        private readonly GetAdminUserDetailUseCase _getUserDetail;
        private readonly EnableUserPremiumUseCase _enablePremium;
        private readonly DisableUserPremiumUseCase _disablePremium;

        public AdminUsersController(
            GetAdminUserDetailUseCase getUserDetail,
            EnableUserPremiumUseCase enablePremium,
            DisableUserPremiumUseCase disablePremium)
        {
            _getUserDetail = getUserDetail;
            _enablePremium = enablePremium;
            _disablePremium = disablePremium;
        }

        [HttpGet("{userId}")]
        public IActionResult GetDetail(int userId)
        {
            var result = _getUserDetail.Execute(userId);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPost("{userId}/premium/enable")]
        public IActionResult EnablePremium(int userId)
        {
            var success = _enablePremium.Execute(userId);
            if (!success)
                return NotFound();

            return Ok();
        }

        [HttpPost("{userId}/premium/disable")]
        public IActionResult DisablePremium(int userId)
        {
            var success = _disablePremium.Execute(userId);
            if (!success)
                return NotFound();

            return Ok();
        }
    }
}
