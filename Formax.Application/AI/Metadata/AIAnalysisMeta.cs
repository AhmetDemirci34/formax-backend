using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Formax.Application.AI.Weights;

namespace Formax.Application.AI.Metadata;

public sealed class AIAnalysisMeta
{
    public AIModelVersion ModelVersion { get; init; } = default!;
    public AIFactorProfile FactorProfile { get; init; } = default!;
    public string ToneTemplateCode { get; init; } = default!;
    public DateTime GeneratedAt { get; init; }
    public AIFactorWeightSnapshot? FactorWeights { get; init; }
}
