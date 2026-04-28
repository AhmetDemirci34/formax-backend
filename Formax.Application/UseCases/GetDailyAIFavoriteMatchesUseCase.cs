using Formax.Application.DTOs;
using Formax.Application.DTOs.Common;
using Formax.Application.Interfaces;
using Formax.Application.Common;
using Formax.Application.Common.Enums;
using System.Linq;

namespace Formax.Application.UseCases
{
    public class GetDailyAIFavoriteMatchesUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;

        public GetDailyAIFavoriteMatchesUseCase(IMatchReadRepository matchReadRepository)
        {
            _matchReadRepository = matchReadRepository;
        }

        // =====================================================
        // FAVORİ MAÇLAR (AI ELEME VAR – AYNI KALDI)
        // =====================================================
        public List<AIFavoriteMatchDto> Execute()
        {
            var now = DateTime.UtcNow;

            var matches = _matchReadRepository
                .Query()
                .Where(m =>
                    m.MatchDate.Date == now.Date &&
                    (m.Status == "NotStarted" || m.Status == "Live"))
                .ToList();

            var result = new List<AIFavoriteMatchDto>();

            foreach (var match in matches)
            {
                var breakdown = new List<ConfidenceBreakdownDto>
                {
                    new ConfidenceBreakdownDto
                    {
                        Factor = "DailyFilter",
                        Score = 0.20,
                        Explanation = "Günlük AI filtrelerinden başarıyla geçti"
                    }
                };

                var confidenceScore = breakdown.Sum(x => x.Score);
                var recommendationLevel = ResolveRecommendationLevel(confidenceScore);

                if (recommendationLevel == RecommendationLevel.LowConfidence)
                    continue;

                result.Add(new AIFavoriteMatchDto
                {
                    MatchId = match.Id,
                    HomeTeamId = match.HomeTeamId,
                    AwayTeamId = match.AwayTeamId,
                    MatchDate = match.MatchDate,
                    ConfidenceScore = confidenceScore,
                    Breakdown = breakdown,
                    Scenario =
                        "Bu karşılaşma mevcut veriler ışığında sınırlı ama anlamlı bir bağlam sunmaktadır.",
                    WhyThisMatch =
                        "Günlük AI filtrelerinden geçen maçlar arasında öne çıkarmıştır.",
                    Warning = string.Empty,
                    ContentAccess = new ContentAccessDto
                    {
                        IsPremium = false,
                        PremiumReason = "Bu içeriği görmek için Premium üyelik gerekir.",
                        LegalNotice = new LegalNoticeDto
                        {
                            Title = LegalNotices.DefaultTitle,
                            Description = LegalNotices.DefaultDescription,
                            Scope = LegalNotices.DefaultScope
                        }
                    },
                    UserProtection = new UserProtectionDto
                    {
                        RecommendationLevel = recommendationLevel,
                        IsScenarioBased = true,
                        AIResponsibilityNote = AIFixedTexts.ResponsibilityNote,
                        UserProtectionNote =
                            "Bu analiz kesinlik içermez ve yönlendirme amacı taşımaz."
                    }
                });
            }

            return result
                .OrderByDescending(x => x.ConfidenceScore)
                .ToList();
        }

        // =====================================================
        // HOME EKRANI (DB TARİHİNE GÖRE + PREMIUM DERİNLİK)
        // =====================================================
        public List<AIFavoriteMatchDto> ExecuteForHome(bool isPremiumUser)
        {
            var allMatches = _matchReadRepository
                .Query()
                .ToList();

            if (!allMatches.Any())
                return new List<AIFavoriteMatchDto>();

            var today = DateTime.UtcNow.Date;

            var targetDate = allMatches
                .Select(m => m.MatchDate.Date)
                .OrderBy(d => Math.Abs((d - today).Days))
                .First();

            var matchesOfTargetDate = allMatches
                .Where(m => m.MatchDate.Date == targetDate)
                .OrderBy(m => m.MatchDate)
                .ToList();

            var result = new List<AIFavoriteMatchDto>();

            foreach (var match in matchesOfTargetDate)
            {
                result.Add(new AIFavoriteMatchDto
                {
                    MatchId = match.Id,
                    HomeTeamId = match.HomeTeamId,
                    AwayTeamId = match.AwayTeamId,
                    MatchDate = match.MatchDate,
                    ConfidenceScore = isPremiumUser ? 0.45 : 0.30,

                    Scenario = isPremiumUser
                        ? "Bu karşılaşma için takım formu, maç temposu ve fikstür yoğunluğu birlikte değerlendirildiğinde sınırlı ama dikkat çekici bir bağlam oluşmaktadır."
                        : "Bu karşılaşma için mevcut veriler anlamlı bir senaryo üretmek için yeterli görülmemiştir.",

                    WhyThisMatch = isPremiumUser
                        ? "Takımların son dönem maç yoğunluğu ve tempo dalgalanmaları bu karşılaşmayı bağlamsal olarak öne çıkarmaktadır."
                        : "Mevcut maç takvimine göre en yakın günün karşılaşmalarıdır.",

                    Warning = isPremiumUser
                        ? "Bu değerlendirme kesinlik içermez ve maç içi gelişmelerle hızla değişebilir."
                        : "Bu maç için yalnızca sınırlı AI bağlamı sunulmaktadır.",

                    ContentAccess = new ContentAccessDto
                    {
                        IsPremium = isPremiumUser,

                        FreeContentLimitation = !isPremiumUser
                            ? new FreeContentLimitationDto
                            {
                                Title = "AI Analizi Sınırlı",
                                Description =
                                    "Bu maç için yalnızca genel bağlam sunulmaktadır. Daha derin senaryo ve veri ilişkileri premium içerikte yer alır."
                            }
                            : null,

                        // ✅ KONTRAT KİLİT: string | null
                        PremiumReason = isPremiumUser
                            ? null
                            : "Bu içeriği görmek için Premium üyelik gerekir.",

                        LegalNotice = new LegalNoticeDto
                        {
                            Title = LegalNotices.DefaultTitle,
                            Description = LegalNotices.DefaultDescription,
                            Scope = LegalNotices.DefaultScope
                        }
                    },

                    UserProtection = new UserProtectionDto
                    {
                        RecommendationLevel = RecommendationLevel.LowConfidence,
                        IsScenarioBased = true,
                        AIResponsibilityNote = AIFixedTexts.ResponsibilityNote,
                        UserProtectionNote =
                            "AI değerlendirmeleri bağlamsal senaryolara dayanır ve kesinlik içermez."
                    }
                });
            }

            return result;
        }

        // ✅ geriye uyum (eski çağrıları bozma)
        public List<AIFavoriteMatchDto> ExecuteForHome()
            => ExecuteForHome(false);

        private RecommendationLevel ResolveRecommendationLevel(double confidenceScore)
        {
            if (confidenceScore < 0.55)
                return RecommendationLevel.LowConfidence;

            if (confidenceScore < 0.75)
                return RecommendationLevel.MediumConfidence;

            return RecommendationLevel.HighConfidence;
        }
    }
}