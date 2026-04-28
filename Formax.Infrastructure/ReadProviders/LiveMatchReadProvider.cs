using Formax.Application.AI.Contexts;
using Formax.Application.Interfaces;
using Formax.Application.Live;
using Formax.Infrastructure.Live;

namespace Formax.Infrastructure.ReadProviders
{
    /// <summary>
    /// Canlı maç bilgisini
    /// SADECE OKUMA amacıyla sağlar.
    /// </summary>
    public sealed class LiveMatchReadProvider : ILiveMatchReadProvider
    {
        private readonly ILiveMatchFeed _liveMatchFeed;

        public LiveMatchReadProvider(ILiveMatchFeed liveMatchFeed)
        {
            _liveMatchFeed = liveMatchFeed;
        }

        public LiveMatchContext Read(int matchId)
        {
            var snapshot = _liveMatchFeed.GetSnapshot(matchId);

            // 🔒 DEFANSİF: veri yoksa minimum bağlam
            if (snapshot == null)
            {
                return new LiveMatchContext
                {
                    CurrentMinute = 0,
                    HomeScore = 0,
                    AwayScore = 0,
                    MatchPhase = MatchPhase.FirstHalf,
                    LastEventType = null
                };
            }

            return new LiveMatchContext
            {
                CurrentMinute = snapshot.Minute,
                HomeScore = snapshot.HomeScore,
                AwayScore = snapshot.AwayScore,
                MatchPhase = snapshot.Minute <= 45
                    ? MatchPhase.FirstHalf
                    : MatchPhase.SecondHalf,
                LastEventType = snapshot.LastEventType
            };
        }
    }
}
