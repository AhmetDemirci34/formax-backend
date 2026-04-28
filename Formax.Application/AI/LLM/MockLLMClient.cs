using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;

namespace Formax.Infrastructure.AI.LLM
{
    public class MockLLMClient : ILLMClient
    {
        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            // 🔒 GERÇEK LLM DAVRANIŞINI TAKLİT EDEN METİN
            var response =
@"Based on the available data and current context,
this match does not present a clear dominant scenario.

Recent form indicators are mixed, and the broader football context
suggests cautious interpretation rather than strong conclusions.

This analysis is provided for contextual understanding only.";

            return Task.FromResult(response);
        }
    }
}
