using Formax.Application.AI.Contexts;

namespace Formax.Application.Services
{
    public class UserExperienceUpdater
    {
        public void OnAiResponseProduced(
            UserExperienceContext ctx,
            bool isNewMatch)
        {
            ctx.AiContextShownCount++;

            if (isNewMatch)
                ctx.MatchesWithAiCount++;
        }
    }
}
