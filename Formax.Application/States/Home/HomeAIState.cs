using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.States.Home
{
    public sealed class HomeAIState
    {
        public HomeAIStateType StateType { get; }

        public HomeAIState(HomeAIStateType stateType)
        {
            StateType = stateType;
        }

        public bool CanSpeak =>
            StateType == HomeAIStateType.Contextual ||
            StateType == HomeAIStateType.Observational;

        public bool IsSilent =>
            StateType == HomeAIStateType.SilentProtected ||
            StateType == HomeAIStateType.BackoffUncertain;
    }
}

