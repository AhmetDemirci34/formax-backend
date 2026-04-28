using Formax.Application.AI.Templates;
using Formax.Domain.States;

namespace Formax.Application.AI.Narrative
{
    public class AINarrativeBuilder : IAINarrativeBuilder
    {
        private readonly INarrativeToneResolver _toneResolver;

        public AINarrativeBuilder(INarrativeToneResolver toneResolver)
        {
            _toneResolver = toneResolver;
        }

        public NarrativeResult Build(
            NarrativeContext context,
            AIUxState uxState,
            AIContextKey contextKey)
        {
            if (uxState == AIUxState.Silent)
            {
                return new NarrativeResult
                {
                    IsSilent = true,
                    SilentReason = SilentNarratives.Reasons[0]
                };
            }

            var tone = _toneResolver.Resolve(context.IsPremium);

            if (contextKey == AIContextKey.PreMatchSummary)
            {
                return new NarrativeResult
                {
                    IsSilent = false,
                    Title = "Maç Öncesi Genel Bakış",
                    Body = tone == NarrativeTone.Detailed
                        ? PreMatchNarratives.Neutral[0]
                        : PreMatchNarratives.Neutral[1]
                };
            }

            if (contextKey == AIContextKey.LiveMatchSummary)
            {
                return new NarrativeResult
                {
                    IsSilent = false,
                    Title = "Canlı Maç Okuması",
                    Body = LiveMatchNarratives.Neutral[0]
                };
            }

            if (contextKey == AIContextKey.PostMatch)
            {
                return new NarrativeResult
                {
                    IsSilent = false,
                    Title = "Maç Sonu Değerlendirme",
                    Body = PostMatchNarratives.Neutral[0]
                };
            }

            return new NarrativeResult
            {
                IsSilent = true,
                SilentReason = "Bu bağlam için henüz anlatı tanımlanmadı."
            };
        }
    }
}
