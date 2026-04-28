namespace Formax.Application.AI.Guardrails
{
    public class AiDecisionResult
    {
        public bool ShouldSpeak { get; private set; }
        public bool CanExpand { get; private set; }
        public double? Confidence { get; private set; }

        private AiDecisionResult() { }

        public static AiDecisionResult Silent()
        {
            return new AiDecisionResult
            {
                ShouldSpeak = false,
                CanExpand = false,
                Confidence = null
            };
        }

        public static AiDecisionResult Basic()
        {
            return new AiDecisionResult
            {
                ShouldSpeak = true,
                CanExpand = false,
                Confidence = null
            };
        }

        public static AiDecisionResult Extended(double confidence)
        {
            return new AiDecisionResult
            {
                ShouldSpeak = true,
                CanExpand = true,
                Confidence = confidence
            };
        }
    }
}
