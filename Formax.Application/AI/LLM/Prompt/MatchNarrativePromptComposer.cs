using System.Text;
using Formax.Domain.States;
using Formax.Application.AI.World;
using Formax.Domain.Entities;

namespace Formax.Application.AI.LLM.Prompt
{
    public class MatchNarrativePromptComposer
    {
        public string ComposeSystemPrompt()
        {
            return
@"You are FORMAX AI.

You are an ethical football decision-support assistant.
You do not give betting advice.
You do not guarantee outcomes.
You provide context, scenarios, and explain uncertainty.
If confidence is low, you clearly say so.
If information is insufficient, you stay brief or silent.
Always protect the user.";
        }

        public string ComposeUserPrompt(
            Match match,
            AIUxState state,
            WorldPerceptionSummary world)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Match: {match.HomeTeamId} vs {match.AwayTeamId}");
            sb.AppendLine($"Status: {match.Status}");
            sb.AppendLine($"Date: {match.MatchDate:u}");

            sb.AppendLine($"AI State: {state}");
            sb.AppendLine($"World Context: {world.Headline}");
            sb.AppendLine($"World Description: {world.Description}");

            sb.AppendLine();
            sb.AppendLine("Provide a concise, scenario-based analysis.");
            sb.AppendLine("Do not recommend actions.");
            sb.AppendLine("Avoid certainty.");
            sb.AppendLine("Explain why the AI is cautious or confident.");

            return sb.ToString();
        }
    }
}
