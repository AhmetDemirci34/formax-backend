namespace Formax.Infrastructure.Normalize.Raw;

/// <summary>
/// Provider-bağımsız ortak HAM hava durumu modeli.
/// TÜM weather mapper'ları (Open-Meteo, MET Norway, …) verilerini bu modele üretir.
/// Alanlar ham/temizlenmemiştir (metin, ayrıştırılmamış sayı/tarih); temizleme+dönüşüm Normalize Engine'in işidir.
/// Bir koordinat için TEK temsili okuma taşır (mapper, saatlik seriden temsili saati seçer).
/// Eksik alanlar null bırakılır (sahte veri üretilmez).
/// </summary>
public sealed record RawWeather
{
    /// <summary>Veriyi üreten provider adı (izlenebilirlik; Normalize Engine bunu YORUMLAMAZ).</summary>
    public string? ProviderName { get; init; }

    /// <summary>Ham enlem metni (provider'ın döndürdüğü çözünmüş grid noktası).</summary>
    public string? Latitude { get; init; }

    /// <summary>Ham boylam metni.</summary>
    public string? Longitude { get; init; }

    /// <summary>Ham sıcaklık metni (°C varsayılır; dönüşüm Normalize Engine'de).</summary>
    public string? TemperatureC { get; init; }

    /// <summary>Ham hava durumu ifadesi (ör. "Clear", "Rain"); provider kod→metin eşlemesini mapper yapar.</summary>
    public string? Condition { get; init; }

    /// <summary>Ham tahmin zamanı metni (ör. ISO-8601); UTC dönüşümü Normalize Engine'de yapılır.</summary>
    public string? ForecastTime { get; init; }
}
