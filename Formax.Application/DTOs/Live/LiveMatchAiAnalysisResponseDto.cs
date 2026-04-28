namespace Formax.Application.DTOs.Live
{
    public class LiveMatchAiAnalysisResponseDto
    {
        public string? AnalysisText { get; set; } = string.Empty;

        public bool ShowRegisterHint { get; set; }

        public string? HintMessage { get; set; }
        public bool AdditionalContextAvailable { get; set; }
        public List<string>? ExtendedContextBlocks { get; set; }
        public string? AdditionalContextInfo { get; set; }
        public string? ContinuityHint { get; set; }
        public string? SilenceReason { get; set; }
        public string? SilenceMessage { get; set; }
        public DateTime? NextAllowedAtUtc { get; set; }


    }
}
