using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Narrative
{
    public static class NarrativeTemplateResolver
    {
        public static NarrativeTemplateType Resolve(NarrativeContext context)
        {
            // 🔒 Sessizlik her şeyden önce gelir
            if (context.ShouldRemainSilent)
            {
                return NarrativeTemplateType.Silent;
            }

            // Live match öncelikli
            if (context.HasLiveContext)
            {
                return context.HasRecentEvent
                    ? NarrativeTemplateType.LiveWithContext
                    : NarrativeTemplateType.LiveBasic;
            }

            // Pre-match
            if (context.HasPreMatchContext)
            {
                return NarrativeTemplateType.PreMatchWithContext;
            }

            // Varsayılan: sessizlik
            return NarrativeTemplateType.Silent;
        }
    }
}
