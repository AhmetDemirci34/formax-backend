using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Branding;

public class BrandProfileDto
{
    public string BrandCode { get; set; } = "FORMAX";

    public string DisplayName { get; set; } = "FORMAX";

    public string Tone { get; set; } = "Neutral";
    // Neutral | Informative | Cautious

    public string DisclaimerSuffix { get; set; } = string.Empty;
    public bool IsPremium { get; set; }
}

