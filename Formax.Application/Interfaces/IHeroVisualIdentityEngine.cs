using Formax.Application.Services.Hero.VisualIdentity;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// HeroVisualIdentityEngine — takım kimliğinden Hero'nun TÜM görsel atmosferini üretir
    /// (TeamColorProvider'ın yerine geçer). Backend CSS üretmez; yapılandırılmış veri döner.
    /// </summary>
    public interface IHeroVisualIdentityEngine
    {
        HeroVisualIdentity Build(string homeTeamName, string awayTeamName);
    }
}
