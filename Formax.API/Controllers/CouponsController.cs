using Formax.Application.UseCases.Coupons;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers
{
    [ApiController]
    [Route("api/coupons")]
    public class CouponsController : ControllerBase
    {
        private readonly CreateCouponUseCase _createCouponUseCase;
        private readonly AddCouponItemUseCase _addCouponItemUseCase;
        private readonly EvaluateCouponUseCase _evaluateCouponUseCase;
        private readonly GetCouponsByResultUseCase _getCouponsByResultUseCase;
        private readonly GetCouponDetailUseCase _getCouponDetailUseCase;

        public CouponsController(
            CreateCouponUseCase createCouponUseCase,
            AddCouponItemUseCase addCouponItemUseCase,
            EvaluateCouponUseCase evaluateCouponUseCase,
            GetCouponsByResultUseCase getCouponsByResultUseCase,
            GetCouponDetailUseCase getCouponDetailUseCase)
        {
            _createCouponUseCase = createCouponUseCase;
            _addCouponItemUseCase = addCouponItemUseCase;
            _evaluateCouponUseCase = evaluateCouponUseCase;
            _getCouponsByResultUseCase = getCouponsByResultUseCase;
            _getCouponDetailUseCase = getCouponDetailUseCase;
        }

        // POST /api/coupons
        [HttpPost]
        public IActionResult Create([FromBody] CreateCouponRequest request)
        {
            var result = _createCouponUseCase.Execute(request);
            return Ok(result);
        }

        // POST /api/coupons/{couponId}/items
        [HttpPost("{couponId}/items")]
        public IActionResult AddItem(
            int couponId,
            [FromBody] AddCouponItemRequest request)
        {
            request.CouponId = couponId;
            _addCouponItemUseCase.Execute(request);
            return Ok();
        }

        // POST /api/coupons/{couponId}/evaluate
        [HttpPost("{couponId}/evaluate")]
        public IActionResult Evaluate(int couponId)
        {
            _evaluateCouponUseCase.Execute(couponId);
            return Ok();
        }

        // GET /api/coupons/by-result?userId=1&result=Win
        [HttpGet("by-result")]
        public async Task<IActionResult> GetByResult(
            [FromQuery] int userId,
            [FromQuery] string result)
        {
            // Eksik parametre 500 yerine 400 dönmeli (endpoint doğrulaması bulgusu).
            if (userId <= 0)
                return BadRequest(new { message = "userId gerekli (pozitif tam sayı)." });

            if (string.IsNullOrWhiteSpace(result))
                return BadRequest(new { message = "result gerekli (örn. Win / Lose / Pending)." });

            var coupons = await _getCouponsByResultUseCase.ExecuteAsync(userId, result);
            return Ok(coupons);
        }

        // GET /api/coupons/{couponId}
        [HttpGet("{couponId}")]
        public IActionResult GetDetail(int couponId)
        {
            var detail = _getCouponDetailUseCase.Execute(couponId);
            return Ok(detail);
        }
    }
}
