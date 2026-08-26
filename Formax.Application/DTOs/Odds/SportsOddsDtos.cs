using System.Collections.Generic;

namespace Formax.Application.DTOs.Odds
{
    /// <summary>
    /// Bir fikstürün sağlayıcıdan gelen NORMALİZE market oranları.
    /// Anahtarlar <see cref="Formax.Domain.Constants.OddsMarketKeys"/> setindendir; sağlayıcının
    /// eşlenemeyen marketleri buraya hiç girmez (uydurma yok).
    /// </summary>
    public sealed class SportsFixtureOdds
    {
        /// <summary>api-football fixture id (Match.ExternalMatchId ile eşleşir).</summary>
        public string FixtureExternalId { get; init; } = string.Empty;

        /// <summary>MarketKey → (oran, bahis sağlayıcısı).</summary>
        public Dictionary<string, SportsMarketOdd> Markets { get; init; } = new();
    }

    /// <summary>Tek market oranı + hangi bahis sağlayıcısından alındığı.</summary>
    public sealed class SportsMarketOdd
    {
        public decimal Odd { get; init; }
        public int BookmakerId { get; init; }
        public string BookmakerName { get; init; } = string.Empty;
    }

    /// <summary>
    /// /odds?date= sayfalı yanıtı. <see cref="TotalPages"/> sağlayıcının bildirdiği toplam sayfa
    /// sayısıdır; ingestion bunu okuyup döngüyü sonlandırır (tahmin etmez).
    /// </summary>
    public sealed class SportsOddsPage
    {
        public List<SportsFixtureOdds> Items { get; init; } = new();
        public int TotalPages { get; init; }
    }
}
