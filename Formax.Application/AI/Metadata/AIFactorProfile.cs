using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Metadata;

public sealed class AIFactorProfile
{
    public bool TeamForm { get; init; }
    public bool PlayerPerformance { get; init; }
    public bool LeagueContext { get; init; }
    public bool PsychologicalPressure { get; init; }
    public bool InjuryImpact { get; init; }
}
