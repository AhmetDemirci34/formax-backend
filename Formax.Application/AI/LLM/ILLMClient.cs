using System.Threading;
using System.Threading.Tasks;

namespace Formax.Application.AI.LLM
{
    public interface ILLMClient
    {
        Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default);
    }
}
