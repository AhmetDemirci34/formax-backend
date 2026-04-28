using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Common;

public static class BrandToneResolver
{
    public static string Apply(string tone, string text)
    {
        return tone switch
        {
            "Informative" => AIToneTemplates.Informative(text),
            "Cautious" => AIToneTemplates.Cautious(text),
            _ => AIToneTemplates.Neutral(text)
        };
    }
}

