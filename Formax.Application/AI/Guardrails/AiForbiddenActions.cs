using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Formax.Application.AI.Guardrails
{
    public static class AiForbiddenActions
{
    // 🔒 AI’nin ASLA yapamayacağı davranışlar

    public const string BettingAdvice =
        "AI_BETTING_ADVICE_FORBIDDEN";

    public const string GuaranteedOutcome =
        "AI_GUARANTEED_OUTCOME_FORBIDDEN";

    public const string UserDirection =
        "AI_USER_DIRECTION_FORBIDDEN";

    public const string CouponEncouragement =
        "AI_COUPON_ENCOURAGEMENT_FORBIDDEN";

    public const string ManipulativeLanguage =
        "AI_MANIPULATIVE_LANGUAGE_FORBIDDEN";
}
}
