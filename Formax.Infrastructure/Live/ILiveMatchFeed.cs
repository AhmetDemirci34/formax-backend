using Formax.Application.Live;

namespace Formax.Infrastructure.Live
{
    /// <summary>
    /// Canlı maç bilgisinin
    /// PASİF okuma arayüzü.
    /// Gerçek implementasyon FAZ-10.2+.
    /// </summary>
    public interface ILiveMatchFeed
    {
        LiveMatchSnapshot? GetSnapshot(int matchId);
    }
}
