using Formax.Infrastructure.Live;

namespace Formax.Infrastructure.Live
{
    /// <summary>
    /// FAZ-10.1
    /// Canlı veri YOK.
    /// Sadece DI zincirini ayakta tutar.
    /// </summary>
    public sealed class DummyLiveMatchFeed : ILiveMatchFeed
    {
        public LiveMatchSnapshot? GetSnapshot(int matchId)
        {
            return null;
        }
    }
}
