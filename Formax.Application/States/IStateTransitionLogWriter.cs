using Formax.Domain.Entities;

namespace Formax.Application.States
{
    public interface IStateTransitionLogWriter
    {
        void Write(StateTransitionLog log);
    }
}
