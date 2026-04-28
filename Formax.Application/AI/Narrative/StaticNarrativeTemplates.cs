using System.Collections.Generic;

namespace Formax.Application.AI.Narrative
{
    public static class StaticNarrativeTemplates
    {
        public static readonly IReadOnlyDictionary<NarrativeTemplateType, string> Templates
            = new Dictionary<NarrativeTemplateType, string>
        {
            {
                NarrativeTemplateType.Silent,
                "Bu an için aktarılacak yeni bir bağlam bulunmuyor."
            },

            {
                NarrativeTemplateType.LiveBasic,
                "Maç canlı olarak devam ediyor. Temel gelişmeler izleniyor."
            },

            {
                NarrativeTemplateType.LiveWithContext,
                "Maç canlı. Son gelişmeler maçın akışı açısından dikkat çekici."
            },

            {
                NarrativeTemplateType.PreMatchBasic,
                "Maç öncesi genel bilgiler mevcut."
            },

            {
                NarrativeTemplateType.PreMatchWithContext,
                "Maç öncesi bağlamlar, karşılaşmanın önemini artıran unsurlar içeriyor."
            }
        };
    }
}
