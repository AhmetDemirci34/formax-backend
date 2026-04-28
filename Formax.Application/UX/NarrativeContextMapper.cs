using Formax.Application.AI.Narrative;

namespace Formax.Application.UX;

public sealed class NarrativeContextMapper
{
    public NarrativeContext MapForUx(NarrativeContext source)
    {
        return new NarrativeContext
        {
            // Kimlik
            UserId = source.UserId,
            MatchId = source.MatchId,

            // Kullanıcı
            IsPremium = source.IsPremium,

            // Zaman / dil
            UtcNow = source.UtcNow,
            Language = source.Language,

            // Okunan bağlamlar (aynen taşınır)
            HasLiveContext = source.HasLiveContext,
            HasRecentEvent = source.HasRecentEvent,
            HasPreMatchContext = source.HasPreMatchContext,
            HasSquadContext = source.HasSquadContext,
            HasWorldPerceptionContext = source.HasWorldPerceptionContext,

            // Sessizlik kararı (aynen)
            ShouldRemainSilent = source.ShouldRemainSilent
        };
    }
}
