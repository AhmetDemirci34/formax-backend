using Formax.Application.AI.Enums;
using Formax.Domain.Subscriptions;

namespace Formax.Application.AI.Depth
{
    /// <summary>
    /// 🔒 APPLICATION MAPPING
    /// AccessLevel → AiDepthLevel ince ayarı
    /// </summary>
    public static class AccessLevelToDepthResolver
    {
        public static AiDepthLevel Resolve(AccessLevel accessLevel)
        {
            return accessLevel switch
            {
                // 🌱 Free: kısa, temkinli, bağlam sınırlı
                AccessLevel.Free => AiDepthLevel.Basic,

                // 🧪 Intro: tadımlık derinlik
                AccessLevel.Intro => AiDepthLevel.Reduced,

                // ⭐ Premium: tam bağlam, tam anlatı
                AccessLevel.Premium => AiDepthLevel.Full,

                _ => AiDepthLevel.Basic
            };
        }
    }
}
