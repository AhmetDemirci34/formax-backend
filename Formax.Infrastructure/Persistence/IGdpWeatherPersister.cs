using System.Threading;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Persistence;

/// <summary>
/// GDP weather kalıcılaştırma sözleşmesi (Canonical Domain <see cref="Formax.Domain.Entities.MatchWeather"/>).
/// Idempotenttir: aynı (FormaxMatchId + koordinat) için aynı veriyle tekrar çağrıldığında Unchanged döner.
/// </summary>
public interface IGdpWeatherPersister
{
    Task<GdpWeatherPersistOutcome> PersistAsync(GdpWeatherPersistRequest request, CancellationToken cancellationToken = default);
}
