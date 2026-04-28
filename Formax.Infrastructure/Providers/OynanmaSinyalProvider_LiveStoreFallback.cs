using System.Threading.Tasks;
using Formax.Application.Interfaces;

namespace Formax.Infrastructure.Providers
{
    /// <summary>
    /// Canlı oynanma store'dan sinyal varsa onu döner,
    /// yoksa Default provider'a düşer.
    /// </summary>
    public sealed class OynanmaSinyalProvider_LiveStoreFallback : IOynanmaSinyalProvider
    {
        private readonly ILiveOynanmaSignalStore _store;
        private readonly OynanmaSinyalProvider_Default _fallback;

        public OynanmaSinyalProvider_LiveStoreFallback(
            ILiveOynanmaSignalStore store,
            OynanmaSinyalProvider_Default fallback)
        {
            _store = store;
            _fallback = fallback;
        }

        public async Task<OynanmaSinyalleri?> GetAsync(int matchId)
        {
            var live = await _store.GetAsync(matchId);
            if (live != null)
                return live;

            return await _fallback.GetAsync(matchId);
        }
    }
}
