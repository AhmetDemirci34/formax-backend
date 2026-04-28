using Formax.Domain.States;

namespace Formax.Application.AI.Guardrails
{
    public static class AiForbiddenActionPolicy
    {
        public static AIUxState ResolveState()
        {
            // 🔒 Yasaklı aksiyon varsa AI GERİ ÇEKİLİR
            return AIUxState.SelfRetracted;
        }

        public static string ResolveReason()
        {
            return "forbidden_action_detected";
        }
    }
}
