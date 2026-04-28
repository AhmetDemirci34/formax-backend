using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.FaiOverview;

public class FaiOverviewItemDto
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    // "Neutral" | "Caution" | "Momentum"
    public string Tone { get; set; } = "Neutral";

    public bool IsPremiumHint { get; set; }
}
