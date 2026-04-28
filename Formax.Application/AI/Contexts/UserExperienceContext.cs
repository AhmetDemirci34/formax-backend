using Formax.Application.AI.Enums;
using Formax.Application.States;
using Formax.Domain.States;
using Formax.Domain.Subscriptions;

namespace Formax.Application.AI.Contexts
{
    public class UserExperienceContext
    {
        // AUTH
        public bool IsRegistered { get; set; }
        public bool IsPremium { get; set; } // ⚠️ GERİYE DÖNÜK, KULLANIMI AZALACAK
        public int? UserId { get; set; }

        // ACCESS (FAZ-15)
        // 🔒 Domain’den resolve edilir, UX flag değildir
        public AccessLevel AccessLevel { get; set; } = AccessLevel.Free;

        // ANON / USER SHARED
        public int MatchesWithAiCount { get; set; }
        public int AiContextShownCount { get; set; }
        public bool HasSeenRegisterHint { get; set; }
        public string? LastAiInteractionType { get; set; }

        // AI
        public AiDepthLevel AiDepthLevel { get; set; }
        public bool CanExpandAiContext { get; set; }

        // EXTENDED BAĞLAM SÜREKLİLİĞİ İÇİN
        // Premium değil, state değil
        public string? LastExtendedContextKey { get; set; }

        // EXTENDED BAĞLAM SÖNME KONTROLÜ
        // Premium değil, state değil
        public int ExtendedContextShownCount { get; set; }

        // STATE
        // State-machine bağlanınca set edilecek
        public UserState CurrentState { get; set; }

        // İleride StateAiDepthMap ile set edilecek
        public AiDepthLevel AllowedAiDepthByState { get; set; }

        public AiSpeakState AiSpeakState { get; set; } = AiSpeakState.Initial;
    }
}
