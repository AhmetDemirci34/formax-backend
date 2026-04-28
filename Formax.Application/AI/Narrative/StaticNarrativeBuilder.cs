using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Narrative
{
    public static class StaticNarrativeBuilder
    {
        public static string Build(NarrativeContext context)
        {
            var templateType =
                NarrativeTemplateResolver.Resolve(context);

            if (!StaticNarrativeTemplates.Templates.TryGetValue(
                    templateType,
                    out var template))
            {
                // Güvenli fallback
                return StaticNarrativeTemplates
                    .Templates[NarrativeTemplateType.Silent];
            }

            return template;
        }
    }
}

