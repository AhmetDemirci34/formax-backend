namespace Formax.Application.DTOs.AI
{
    public class AIHomeVitrineDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsPremiumHint { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string Tone { get; set; } = string.Empty;
    }
}
