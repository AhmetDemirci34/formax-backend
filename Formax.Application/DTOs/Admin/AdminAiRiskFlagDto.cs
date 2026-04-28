namespace Formax.Application.DTOs.Admin
{
    /// <summary>
    /// AI davranışından türetilen risk işareti
    /// </summary>
    public class AdminAiRiskFlagDto
    {
        public string Code { get; set; } = string.Empty;
        public AdminAiRiskSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
    }
}
