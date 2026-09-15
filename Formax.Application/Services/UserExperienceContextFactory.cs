using System.Collections.Generic;
using System.Linq;
using Formax.Application.AI.Contexts;
using Formax.Application.AI.Narrative;
using Formax.Application.Interfaces;
using Formax.Application.Live;

namespace Formax.Application.Services
{
    public class UserExperienceContextFactory
    {
        private readonly IUserRepository _userRepository;

        // 🔒 FAZ-10.x — OPSİYONEL READ PROVIDER SLOT'LARI
        private readonly ILiveMatchReadProvider? _liveMatchProvider;
        private readonly IPreMatchReadProvider? _preMatchProvider;
        private readonly ISquadReadProvider? _squadProvider;
        private readonly IWorldPerceptionReadProvider? _worldPerceptionProvider;

        public UserExperienceContextFactory(
            IUserRepository userRepository,
            ILiveMatchReadProvider? liveMatchProvider = null,
            IPreMatchReadProvider? preMatchProvider = null,
            ISquadReadProvider? squadProvider = null,
            IWorldPerceptionReadProvider? worldPerceptionProvider = null)
        {
            _userRepository = userRepository;

            _liveMatchProvider = liveMatchProvider;
            _preMatchProvider = preMatchProvider;
            _squadProvider = squadProvider;
            _worldPerceptionProvider = worldPerceptionProvider;
        }

        public UserExperienceContext CreateForUser(int userId)
        {
            var user = _userRepository.GetById(userId);

            if (user == null)
            {
                return new UserExperienceContext
                {
                    IsRegistered = false
                };
            }

            return new UserExperienceContext
            {
                UserId = user.Id,
                IsRegistered = true,
                IsPremium = user.IsPremium,

                MatchesWithAiCount = user.MatchesWithAiCount,
                AiContextShownCount = user.AiContextShownCount,
                HasSeenRegisterHint = user.HasSeenRegisterHint,

                LastExtendedContextKey = null
            };
        }

        public UserExperienceContext CreateForAnon(
            int matchesWithAi,
            int aiContextCount,
            bool hasSeenHint,
            string? lastType)
        {
            return new UserExperienceContext
            {
                IsRegistered = false,
                MatchesWithAiCount = matchesWithAi,
                AiContextShownCount = aiContextCount,
                HasSeenRegisterHint = hasSeenHint,
                LastAiInteractionType = lastType,

                LastExtendedContextKey = null
            };
        }

        // 🔒 FAZ-10.2 — UNIFIED AI READ CONTEXT (DAVRANIŞ YOK)
        // UÇTAN UCA ASYNC (15.09.2026): maç öncesi ve kadro sağlayıcıları async okur; istek iş parçacığı
        // .GetAwaiter().GetResult() ile bloklanmaz (tek çağıran GetLiveMatchAiAnalysisUseCase zaten async).
        public async System.Threading.Tasks.Task<UnifiedAiReadContext> CreateUnifiedAiReadContextAsync(
            int matchId,
            AiReadContext aiReadContext,
            UserExperienceContext userExperienceContext,
            IReadOnlyList<MatchEvent> recentEvents)
        {
            var preMatch = _preMatchProvider != null ? await _preMatchProvider.ReadAsync(matchId).ConfigureAwait(false) : null;
            var squad = _squadProvider != null ? await _squadProvider.ReadAsync(matchId).ConfigureAwait(false) : null;
            return new UnifiedAiReadContext
            {
                // MEVCUT
                MatchRead = aiReadContext,
                RecentEvents = recentEvents,

                User = userExperienceContext,
                IsPremiumUser = userExperienceContext.IsPremium,
                AiUsageCount = userExperienceContext.AiContextShownCount,
                UserState = userExperienceContext.CurrentState,

                SemanticBreakDetected =
                    SemanticBreakDetector.HasSemanticBreak(recentEvents),

                // ✅ LIVE MATCH — GERÇEK OKUMA
                LiveMatch = _liveMatchProvider != null
                    ? _liveMatchProvider.Read(matchId)
                    : null!,

                // 🔒 FAZ-10.2 — GERÇEK OKUMA / YOKSA NULL
                PreMatch = preMatch!,

                Squad = squad!,

                WorldPerception = _worldPerceptionProvider != null
                    ? _worldPerceptionProvider.Read(matchId)
                    : null!
            };
        }
    }
}
