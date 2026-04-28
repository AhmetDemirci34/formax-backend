using Formax.Application.AI.Metadata;
namespace Formax.Application.DTOs

{
    public class AIAnalysisDto
    {
        public double Probability { get; set; }
        public string Comment { get; set; } = null!;
        public AIAnalysisMeta Meta { get; init; } = default!;
    }
}

