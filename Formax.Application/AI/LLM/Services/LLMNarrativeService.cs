using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM.Prompt;
using Formax.Application.AI.World;
using Formax.Domain.Entities;
using Formax.Domain.States;

namespace Formax.Application.AI.LLM.Services
{
    public class LLMNarrativeService
    {
        private readonly ILLMClient _llmClient;
        private readonly MatchNarrativePromptComposer _promptComposer;

        public LLMNarrativeService(
            ILLMClient llmClient,
            MatchNarrativePromptComposer promptComposer)
        {
            _llmClient = llmClient;
            _promptComposer = promptComposer;
        }

        public async Task<string> GenerateMatchNarrativeAsync(
            Match match,
            AIUxState state,
            WorldPerceptionSummary world,
            CancellationToken cancellationToken = default)
        {
            var systemPrompt = _promptComposer.ComposeSystemPrompt();
            var userPrompt = _promptComposer.ComposeUserPrompt(match, state, world);

            var response = await _llmClient.GenerateAsync(
                systemPrompt,
                userPrompt,
                cancellationToken);

            return response;
        }
    }
}
