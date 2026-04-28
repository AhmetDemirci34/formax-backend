using Formax.Application.DTOs.Common;

namespace Formax.Application.AI.Metadata
{
    public static class AIReasonExplanationResolver
    {
        public static string Resolve(AIStateMetaDto meta)
        {
            return meta.ReasonCode switch
            {
                "sufficient_confidence" =>
                    "Takımların güncel form durumu ve bağlamsal veriler yeterli güven seviyesinde olduğu için analiz genişletilmiştir.",

                "confidence_below_threshold" =>
                    "Mevcut veriler yeterli güven seviyesini sağlamadığı için AI analiz üretmemeyi tercih etmiştir.",

                "conflicting_signals" =>
                    "Çelişen sinyaller tespit edildiği için AI analizini geri çekmiştir.",

                _ =>
                    "AI bu analiz için temkinli bir değerlendirme yapmayı tercih etmiştir."
            };
        }
    }
}
