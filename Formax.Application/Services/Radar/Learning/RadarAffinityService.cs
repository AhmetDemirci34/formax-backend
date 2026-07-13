using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Radar;

namespace Formax.Application.Services.Radar.Learning
{
    /// <summary>
    /// Radar Learning (R.14.5) — default orchestration. Does the I/O (gets the interest
    /// profile via the R.14.2 service), then calls the pure engine. No persistence.
    /// </summary>
    public sealed class RadarAffinityService : IRadarAffinityService
    {
        private readonly IUserInterestProfileService _profileService;
        private readonly IRadarAffinityEngine _engine;

        public RadarAffinityService(
            IUserInterestProfileService profileService,
            IRadarAffinityEngine engine)
        {
            _profileService = profileService;
            _engine = engine;
        }

        public async Task<RadarAffinityDto> GetAsync(int userId, CancellationToken ct = default)
        {
            var profile = await _profileService.GetProfileAsync(userId, ct);
            return _engine.Compute(profile);
        }
    }
}
