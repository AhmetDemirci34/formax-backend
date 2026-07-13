using System.Globalization;
using Formax.Infrastructure.Normalize.Geo;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Normalize.Text;
using Formax.Infrastructure.Normalize.Time;

namespace Formax.Infrastructure.Normalize.Normalizers;

/// <summary>
/// Ortak HAM hava durumu (<see cref="RawWeather"/>) → <see cref="NormalizedWeather"/> dönüştürür.
///
/// TAMAMEN provider-bağımsızdır: <see cref="RawWeather.ProviderName"/>'e göre HİÇBİR dallanma yapmaz.
/// Koordinat/sıcaklık invariant kültürle sayıya çözülür, zaman <see cref="DateTimeNormalizer"/> ile UTC'ye
/// çevrilir, condition <see cref="TextNormalizer"/> ile temizlenir. Eksik/geçersiz alanlarda istisna
/// fırlatmaz; null bırakır, sahte veri üretmez.
/// </summary>
public sealed class WeatherNormalizer : NormalizerBase<RawWeather, NormalizedWeather>
{
    public WeatherNormalizer(ICountryNormalizer country) : base(country)
    {
    }

    public override NormalizedWeather Normalize(RawWeather raw)
    {
        if (raw is null)
            return new NormalizedWeather();

        return new NormalizedWeather
        {
            Latitude = TryParseDouble(raw.Latitude),
            Longitude = TryParseDouble(raw.Longitude),
            TemperatureC = TryParseDouble(raw.TemperatureC),
            Condition = CleanOrNull(raw.Condition),
            ForecastUtc = DateTimeNormalizer.TryParseUtc(raw.ForecastTime, out var forecast) ? forecast : null
        };
    }

    private static double? TryParseDouble(string? value) =>
        !string.IsNullOrWhiteSpace(value)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (double?)null;

    private static string? CleanOrNull(string? value)
    {
        var cleaned = TextNormalizer.Clean(value);
        return cleaned.Length == 0 ? null : cleaned;
    }
}
