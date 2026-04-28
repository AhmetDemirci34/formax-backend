using Formax.Domain.States;

namespace Formax.Application.States
{
    public interface IAIStateMachine
    {
        AIStateResult Decide(StateDecisionContext context);
    }
}
