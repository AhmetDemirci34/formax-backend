using Formax.Application.DTOs;

namespace Formax.Application.Interfaces
{
    public interface IAIAnalysisService
    {
        AIAnalysisDto AnalyzeMatch(int matchId);
     
    }
}

