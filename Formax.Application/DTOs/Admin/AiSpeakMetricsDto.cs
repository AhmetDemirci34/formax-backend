namespace Formax.Application.DTOs.Admin
{
    public class AiSpeakMetricsDto
    {
        public int TotalDecisions { get; set; }

        public int SpeakCount { get; set; }

        public int SilenceCount { get; set; }

        // Dağılım
        public int ContextInsufficientCount { get; set; }
        public int RateLimitedCount { get; set; }
        public int StateBlockedCount { get; set; }

        // Oranlar (hesaplanmış)
        public double SpeakRate { get; set; }
        public double SilenceRate { get; set; }
    }
}
