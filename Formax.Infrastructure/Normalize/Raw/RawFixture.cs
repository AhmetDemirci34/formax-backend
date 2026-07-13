namespace Formax.Infrastructure.Normalize.Raw;

/// <summary>
/// Provider-bağımsız ortak HAM fikstür modeli.
/// TÜM provider mapper'ları (OpenLigaDB, Football-Data, RSS, …) verilerini bu modele üretir.
/// Alanlar ham/temizlenmemiştir (metin, ayrıştırılmamış tarih); temizleme+dönüşüm Normalize Engine'in işidir.
/// Eksik alanlar null bırakılır (sahte veri üretilmez).
/// </summary>
public sealed record RawFixture
{
    /// <summary>Veriyi üreten provider adı (izlenebilirlik; Normalize Engine bunu YORUMLAMAZ).</summary>
    public string? ProviderName { get; init; }

    public string? ProviderMatchId { get; init; }

    public string? Competition { get; init; }

    public string? Season { get; init; }

    public string? Round { get; init; }

    public string? HomeTeam { get; init; }

    public string? AwayTeam { get; init; }

    /// <summary>Ham başlama zamanı metni (ör. ISO-8601); UTC dönüşümü Normalize Engine'de yapılır.</summary>
    public string? Kickoff { get; init; }

    public string? Status { get; init; }

    public string? Venue { get; init; }

    public int? HomeScore { get; init; }

    public int? AwayScore { get; init; }
}
