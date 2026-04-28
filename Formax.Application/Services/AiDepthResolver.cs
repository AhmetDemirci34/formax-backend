using Formax.Domain.Subscriptions;
using Formax.Application.AI.Enums;
using Formax.Application.AI.Depth;

namespace Formax.Application.Services
{
    /// <summary>
    /// 🔒 APPLICATION SERVICE
    /// AI derinliği artık UserExperienceContext’ten DEĞİL,
    /// Domain AccessLevel üzerinden belirlenir.
    /// </summary>
    public sealed class AiDepthResolver
    {
        public AiDepthLevel Resolve(AccessLevel accessLevel)
        {
            return AccessLevelToDepthResolver.Resolve(accessLevel);
        }
    }
}
