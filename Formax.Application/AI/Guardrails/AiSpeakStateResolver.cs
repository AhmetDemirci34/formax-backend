using Formax.Application.AI.Contexts;
using Formax.Domain.States;
using Formax.Domain.Subscriptions;

namespace Formax.Application.AI.Guardrails
{
    /// <summary>
    /// 🔒 AI konuşma state resolver
    /// SADECE mevcut UserExperienceContext alanlarını okur
    /// </summary>
    public class AiSpeakStateResolver
    {
        public AiSpeakState Resolve(
            UserExperienceContext ctx,
            AccessLevel accessLevel)
        {
            // 1️⃣ Genişletilmiş bağlam verilemiyorsa → sessiz
            if (!ctx.CanExpandAiContext)
                return AiSpeakState.Silent;

            // 2️⃣ Henüz ek bağlam açılmadıysa → yüzeysel
            if (ctx.ExtendedContextShownCount == 0)
                return AiSpeakState.SoftRead;

            // 3️⃣ Bağlam derinleşmiş
            return AiSpeakState.DeepRead;
        }
    }
}
