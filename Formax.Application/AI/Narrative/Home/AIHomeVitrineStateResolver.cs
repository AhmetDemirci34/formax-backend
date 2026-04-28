using Formax.Domain.States;

namespace Formax.Application.AI.Narrative.Home
{
    public class AIHomeVitrineStateResolver
    {
        public string ResolveMessage(AIUxState state)
        {
            return state switch
            {
                AIUxState.Silent =>
                    "AI bugün temkinli. Yeterli bağlam oluşmadığı için sessiz kalıyor.",

                AIUxState.Short =>
                    "AI bugün kısa ve dikkatli yorumlar sunuyor.",

                AIUxState.Extended =>
                    "AI bugün maçları daha derin bağlamla değerlendiriyor.",

                AIUxState.SelfRetracted =>
                    "AI bugün geri planda. Güvenilirlik eşiği korunuyor.",

                _ =>
                    "AI bugünkü maçları genel hatlarıyla izliyor."
            };
        }
    }
}
