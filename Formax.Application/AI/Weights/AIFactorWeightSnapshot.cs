using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Weights;

public sealed class AIFactorWeightSnapshot
{
    public int TeamForm { get; init; }
    public int PlayerPerformance { get; init; }
    public int LeagueContext { get; init; }
    public int PsychologicalPressure { get; init; }
    public int InjuryImpact { get; init; }
}

