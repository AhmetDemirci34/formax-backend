using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.Common;

public static class AIToneTemplates
{
    public static string Neutral(string text) =>
        $"Değerlendirme: {text}";

    public static string Cautious(string text) =>
        $"Dikkat: {text}";

    public static string Informative(string text) =>
        $"Bilgi: {text}";
}

