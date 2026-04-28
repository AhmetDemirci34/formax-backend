using Formax.Application.AI.Contexts;

namespace Formax.Application.States
{
    // State çözümlemesi için tek giriş noktası
    // ŞU AN PASİF
    public interface IUserStateContextBuilder
    {
        void Build(UserExperienceContext ctx);
    }
}
