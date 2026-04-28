using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Home;

namespace Formax.Application.UseCases.Home
{
    /// <summary>
    /// FAZ 6 sonrası — Home AI konuşma motoru.
    /// Tahmin vermez; radar, ölçüm ve canlı derinlikten anlatı üretir.
    /// </summary>
    public sealed class GetHomeNarrativeUseCase
    {
        private readonly GetHomeRadarUseCase _getHomeRadarUseCase;
        private readonly GetHomeLiveSignalsUseCase _getHomeLiveSignalsUseCase;

        public GetHomeNarrativeUseCase(
            GetHomeRadarUseCase getHomeRadarUseCase,
            GetHomeLiveSignalsUseCase getHomeLiveSignalsUseCase)
        {
            _getHomeRadarUseCase = getHomeRadarUseCase;
            _getHomeLiveSignalsUseCase = getHomeLiveSignalsUseCase;
        }

        public async Task<HomeNarrativeResponseDto> ExecuteAsync(int? userId, bool isPremium, DateTime utcNow)
        {
            var radar = await _getHomeRadarUseCase.ExecuteAsync(userId, isPremium, utcNow);
            var live = await _getHomeLiveSignalsUseCase.ExecuteAsync(utcNow);

            var topRadar = radar.Matches.FirstOrDefault();
            var topSignal = live.Matches.FirstOrDefault();

            var cards = new List<HomeNarrativeCardDto>();

            if (topRadar is not null)
            {
                cards.Add(new HomeNarrativeCardDto
                {
                    MatchId = topRadar.MatchId,
                    Title = $"{topRadar.Teams.Home} – {topRadar.Teams.Away}",
                    Tag = "Ana Anlatı",
                    Tone = topRadar.RadarScore >= 65 ? "watch" : "quiet",
                    Message = isPremium ? BuildPrimaryNarrative(topRadar, true) : BuildPrimaryNarrative(topRadar, false),
                    PreviewText = isPremium ? null : BuildPreviewNarrative(topRadar),
                    IsPremiumHint = !isPremium,
                    IsBlurred = !isPremium
                });
            }

            if (topSignal is not null)
            {
                cards.Add(new HomeNarrativeCardDto
                {
                    MatchId = topSignal.MatchId,
                    Title = "Canlı derinlik okuması",
                    Tag = "Tempo + Momentum",
                    Tone = topSignal.Momentum.Value >= 60 || topSignal.KritikKirilma.Value >= 60 ? "watch" : "quiet",
                    Message = isPremium
                        ? $"Tempo {topSignal.Tempo.Level.ToLowerInvariant()}, momentum {topSignal.Momentum.Level.ToLowerInvariant()} ve kırılma {topSignal.KritikKirilma.Level.ToLowerInvariant()} seviyede. Home ekranı maçı başlamadan bile hangi hattın ısındığını gösteriyor."
                        : "Canlı derinlik kartı preview modunda. Hangi hattın önce ısındığını özetler; tam bağlam ürünün derinlik anında açılır.",
                    PreviewText = isPremium ? null : $"Tempo {topSignal.Tempo.Level.ToLowerInvariant()}, momentum {topSignal.Momentum.Level.ToLowerInvariant()}...",
                    IsPremiumHint = !isPremium,
                    IsBlurred = !isPremium
                });
            }

            cards.Add(new HomeNarrativeCardDto
            {
                Title = "Radar explainability",
                Tag = "Neden bu maç?",
                Tone = "watch",
                Message = topRadar is null
                    ? "Radar şu an sessiz. Anlatı motoru veri gelince nedeni açar."
                    : isPremium
                        ? $"Radar skoru; sapma {topRadar.Sapma}, takım ilgisi {topRadar.TeamInterestScore}, lig ilgisi {topRadar.LeagueInterestScore}, içerik ilgisi {topRadar.ContentInterestScore}, league baseline {topRadar.LeagueBaselineScore} ve zaman yakınlığı {topRadar.TimeProximityScore} birleşiminden okunur."
                        : "Explainability preview modunda. Free kullanıcı sebebin ana iskeletini görür; tam breakdown Match Detail içinde açılır.",
                PreviewText = isPremium || topRadar is null ? null : $"Sapma {topRadar.Sapma} + Takım {topRadar.TeamInterestScore} + Lig {topRadar.LeagueInterestScore}...",
                IsPremiumHint = !isPremium,
                IsBlurred = !isPremium && topRadar is not null
            });

            cards.Add(new HomeNarrativeCardDto
            {
                Title = "Derin anlatı preview modunda",
                Tag = "Preview",
                Tone = "premium",
                Message = isPremium
                    ? "Premium kullanıcı için anlatı kapısı açık. Home kartı yalnızca özet verir; derin analiz maç detayına taşınır."
                    : "Free kullanıcı ana okumayı görür. Derin anlatı sert duvar kurmadan preview mantığıyla hissedilir; tam açılım maç detayında premium katmanda kalır.",
                PreviewText = isPremium ? null : "Blur preview aktif · giriş noktası Match Detail",
                IsPremiumHint = !isPremium,
                IsBlurred = !isPremium
            });

            var summary = topRadar is null
                ? "AI konuşma motoru şu an sessiz. Radar veya canlı sinyal güçlenince anlatı açılır."
                : topRadar.RadarScore >= 65
                    ? "AI konuşma motoru bugün bazı maçları daha yüksek dikkatle anlatıyor."
                    : "AI konuşma motoru sessiz ama izliyor; şimdilik sayıların arkasındaki bağlamı yumuşak dille açıyor.";

            return new HomeNarrativeResponseDto
            {
                SummaryText = summary,
                ContentAccess = new HomeContentAccessDto
                {
                    IsPremiumUser = isPremium,

                    PreviewOnly = !isPremium,

                    RequiresPremium = !isPremium,

                    LockReason = isPremium
                    ? "Premium kullanıcı derin anlatı özetlerini görebilir."
                    : "Derin anlatı preview modunda. Tam açılım Match Detail içinde premium katmanda görünür.",

                    CallToAction = isPremium
                    ? "Premium açık"
                    : "Premium ile detayda aç",

                    UnlockTarget = "Match Detail"
                },
                Cards = cards
            };
        }

        private static string BuildPrimaryNarrative(HomeRadarMatchDto match, bool isPremium)
        {
            var lead = match.RadarScore >= 65
                ? "Bu maç home radarında normal listenin üstüne taşındı."
                : "Bu maç şu an sessiz bölgede ama radar görünür tutuyor.";

            var quietText = match.SessizMi
                ? "Sapma düşük olduğu için ürün bağırmıyor."
                : "Sapma yükseldiği için kullanıcı algısındaki aşırılık daha dikkat çekici okunuyor.";

            var depthTail = isPremium
                ? $" League baseline {match.LeagueBaselineScore}, takım ilgisi {match.TeamInterestScore} ve zaman yakınlığı {match.TimeProximityScore} birlikte okununca radar skoru {match.RadarScore} seviyesine geliyor."
                : " Derin katman preview modunda tutuluyor.";

            return $"{lead} {quietText}{depthTail} Bu bir yönlendirme değil; sadece ölçüm + bağlam anlatısıdır.";
        }

        private static string BuildPreviewNarrative(HomeRadarMatchDto match)
        {
            var lead = match.RadarScore >= 65
                ? "Bu maç bugün normal listenin üstüne taşındı"
                : "Bu maç radar içinde görünür tutuluyor";

            return $"{lead} · Sapma {match.Sapma} · Radar {match.RadarScore}";
        }
    }
}
