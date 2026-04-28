using Formax.Application.AI.Contexts;

namespace Formax.Application.States
{
    public interface IUserStateResolver
    {
        UserState Resolve(UserExperienceContext ctx);
    }
}
