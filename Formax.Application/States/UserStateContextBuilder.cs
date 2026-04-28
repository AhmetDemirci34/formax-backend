using Formax.Application.AI.Contexts;

namespace Formax.Application.States
{
    // AŞAMA-3.5
    // FLAG VAR, AMA STATE HALA PASİF
    public class UserStateContextBuilder : IUserStateContextBuilder
    {
        private readonly IUserStateResolver _stateResolver;

        public UserStateContextBuilder(IUserStateResolver stateResolver)
        {
            _stateResolver = stateResolver;
        }

        public void Build(UserExperienceContext ctx)
        {
            if (!StateMachineOptions.Enabled)
            {
                // STATE-MACHINE KAPALI
                return;
            }

            // 🔓 AÇILDIĞINDA:
            // ctx.CurrentState = _stateResolver.Resolve(ctx);
            // ctx.AllowedAiDepthByState =
            //     StateAiDepthMap.GetAllowedDepth(ctx.CurrentState);
        }
    }
}
