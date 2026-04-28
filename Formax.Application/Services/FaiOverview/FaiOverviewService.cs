using Formax.Application.DTOs.FaiOverview;
using Formax.Application.Interfaces.FaiOverview;

namespace Formax.Application.Services.FaiOverview;

public class FaiOverviewService : IFaiOverviewService
{
    public Task<List<FaiOverviewItemDto>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        // DEMO: yönlendirme/tahmin yok. Sadece genel okuma dili.
        var items = new List<FaiOverviewItemDto>
        {
            new()
            {
                Title = "Bugün fikstür yoğun",
                Summary = "Birçok ligde maç aralığı dar. Rotasyon ve tempo dalgalanmaları görülebilir.",
                Tone = "Neutral",
                IsPremiumHint = false
            },
            new()
            {
                Title = "Erken goller oyunu değiştirebilir",
                Summary = "Bazı eşleşmelerde ilk 20 dakikadaki tempo, maçın geri kalanını belirleyebilir.",
                Tone = "Momentum",
                IsPremiumHint = false
            },
            new()
            {
                Title = "Beklenmedik kontrol kayıpları olabilir",
                Summary = "Kadro sürekliliği zayıf olan takımlarda kısa süreli oyun kopmaları yaşanabilir.",
                Tone = "Caution",
                IsPremiumHint = false
            },
            new()
            {
                Title = "FORMAX AI",
                Summary = "Detaylı okuma Premium’da açılır. İster sessiz, ister hazır—duruma göre konuşur.",
                Tone = "Neutral",
                IsPremiumHint = true
            }
        };

        return Task.FromResult(items);
    }

    public Task<FaiStateDto> GetStateAsync(CancellationToken cancellationToken = default)
    {
        // DEMO: sonra state-machine/guard bağlanacak
        return Task.FromResult(new FaiStateDto { State = "SILENT" });
    }
}
