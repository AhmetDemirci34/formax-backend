using System;
using Formax.Domain.States;

namespace Formax.Application.AI.World
{
    public class WorldPerceptionProvider
    {
        public WorldPerceptionSummary GetToday(AIUxState state)
        {
            return state switch
            {
                AIUxState.Extended => new WorldPerceptionSummary
                {
                    Headline = "Bugün futbol gündemi yüksek etkileşimli",
                    Description =
                        "Takvim sıkışıklığı, kadro rotasyonları ve psikolojik baskılar " +
                        "birçok maçta belirleyici rol oynuyor.",
                    ConfidenceLevel = "high",
                    Source = "system",
                    LastUpdatedUtc = DateTime.UtcNow
                },

                AIUxState.Short => new WorldPerceptionSummary
                {
                    Headline = "Bugün futbol gündemi temkinli ilerliyor",
                    Description =
                        "Veri mevcut ancak bağlam henüz tam olgunlaşmış değil.",
                    ConfidenceLevel = "medium",
                    Source = "system",
                    LastUpdatedUtc = DateTime.UtcNow
                },

                AIUxState.Silent or AIUxState.SelfRetracted => new WorldPerceptionSummary
                {
                    Headline = "Bugün futbol gündemi sakin",
                    Description =
                        "Anlamlı bir bağlam oluşmadığı için genel değerlendirme sınırlı tutuluyor.",
                    ConfidenceLevel = "low",
                    Source = "system",
                    LastUpdatedUtc = DateTime.UtcNow
                },

                _ => new WorldPerceptionSummary
                {
                    Headline = "Bugün futbol gündemi dengeli",
                    Description =
                        "Maçlar genel akış içinde izleniyor.",
                    ConfidenceLevel = "low",
                    Source = "system",
                    LastUpdatedUtc = DateTime.UtcNow
                }
            };
        }
    }
}
