using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Engine;

public class ResultDto
{
    public bool IsWin { get; set; }
    public string FinalScore { get; set; } = "";
    public string UserPick { get; set; } = "";

    public List<string> Why { get; set; } = new();

    public ImpactDto Impact { get; set; } = new();
}

public class ImpactDto
{
    public int WinRateBefore { get; set; }
    public int WinRateAfter { get; set; }

    public int ConfidenceBefore { get; set; }
    public int ConfidenceAfter { get; set; }

    public int Streak { get; set; }
}
