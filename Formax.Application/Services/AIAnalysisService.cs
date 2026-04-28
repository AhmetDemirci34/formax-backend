using System;
using Formax.Application.DTOs;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;

namespace Formax.Application.Services
{
    public class AIAnalysisService : IAIAnalysisService
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly ICouponReadRepository _couponReadRepository;

        public AIAnalysisService(
            IMatchReadRepository matchReadRepository,
            ICouponReadRepository couponReadRepository)
        {
            _matchReadRepository = matchReadRepository;
            _couponReadRepository = couponReadRepository;
        }

        public AIAnalysisDto AnalyzeMatch(int matchId)
        {
            var match = _matchReadRepository.GetById(matchId);
            if (match == null)
            {
                return new AIAnalysisDto
                {
                    Probability = 0,
                    Comment = "Maç bulunamadı"
                };
            }

            double probability = 0.50;

            if (match.HomeScore > match.AwayScore)
                probability += 0.03;

            if (match.Status == "Live")
                probability -= 0.02;

            probability = Normalize(probability);

            return new AIAnalysisDto
            {
                Probability = probability,
                Comment = "Maç bazlı analiz"
            };
        }

        public AIAnalysisDto AnalyzeCoupon(int couponId)
        {
            var coupon = _couponReadRepository.GetById(couponId);
            if (coupon == null || coupon.Items.Count == 0)
            {
                return new AIAnalysisDto
                {
                    Probability = 0,
                    Comment = "Kupon boş"
                };
            }

            double probability = 0.55 - (coupon.Items.Count * 0.03);
            probability = Normalize(probability);

            return new AIAnalysisDto
            {
                Probability = probability,
                Comment = "Kupon bazlı analiz"
            };
        }

        private double Normalize(double value)
        {
            if (value < 0.40) return 0.40;
            if (value > 0.55) return 0.55;
            return Math.Round(value, 2);
        }
    }
}
