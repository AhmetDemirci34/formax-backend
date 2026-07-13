using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Providers.Sources.Weather;

/// <summary>
/// Open-Meteo ham JSON çıktısını ortak <see cref="RawWeather"/> modeline dönüştürür.
/// Provider-özgü TEK yer burasıdır (Normalize Engine Open-Meteo'yu tanımaz).
///
/// Open-Meteo <c>/v1/forecast</c> saatlik seri döndürür (time[], temperature_2m[], weather_code[], …).
/// Bir koordinat için TEK temsili okuma üretiriz: ŞU ANA (UTC) en yakın saat. WMO <c>weather_code</c>
/// ortak condition metnine eşlenir. Ayrıştırılamayan/eksik payload'da boş liste döner (sahte veri yok).
/// </summary>
public sealed class OpenMeteoWeatherMapper : IProviderMapper<RawWeather>
{
    public string ProviderName => "open-meteo";

    public IReadOnlyList<RawWeather> Map(object? payload)
    {
        if (payload is not string json || string.IsNullOrWhiteSpace(json))
            return Array.Empty<RawWeather>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("hourly", out var hourly))
                return Array.Empty<RawWeather>();

            var times = GetArray(hourly, "time");
            var temps = GetArray(hourly, "temperature_2m");
            var codes = GetArray(hourly, "weather_code");

            if (times is null || times.Value.GetArrayLength() == 0)
                return Array.Empty<RawWeather>();

            var index = NearestIndexToNow(times.Value);
            if (index < 0)
                return Array.Empty<RawWeather>();

            var raw = new RawWeather
            {
                ProviderName = ProviderName,
                Latitude = ReadNumberText(root, "latitude"),
                Longitude = ReadNumberText(root, "longitude"),
                TemperatureC = ElementText(temps, index),
                Condition = WmoToCondition(ElementText(codes, index)),
                ForecastTime = ElementText(times, index)
            };

            return new[] { raw };
        }
        catch (JsonException)
        {
            return Array.Empty<RawWeather>();
        }
    }

    private static JsonElement? GetArray(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value
            : (JsonElement?)null;

    /// <summary>time[] içinde UTC şimdiye zaman olarak en yakın indeksi bulur; ayrıştırılamazsa 0.</summary>
    private static int NearestIndexToNow(JsonElement times)
    {
        var now = DateTimeOffset.UtcNow;
        var bestIndex = 0;
        var bestDelta = TimeSpan.MaxValue;
        var any = false;

        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            var text = times[i].GetString();
            if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t))
                continue;

            any = true;
            var delta = (t - now).Duration();
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = i;
            }
        }

        return any ? bestIndex : (times.GetArrayLength() > 0 ? 0 : -1);
    }

    private static string? ReadNumberText(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetRawText()
            : null;

    private static string? ElementText(JsonElement? array, int index)
    {
        if (array is null || index < 0 || index >= array.Value.GetArrayLength())
            return null;

        var element = array.Value[index];
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString(),
            _ => null
        };
    }

    /// <summary>
    /// WMO hava durumu kodunu (<c>weather_code</c>) ortak condition metnine eşler.
    /// Bilinmeyen/eksik kod null döner (sahte veri üretilmez).
    /// </summary>
    private static string? WmoToCondition(string? codeText)
    {
        if (string.IsNullOrWhiteSpace(codeText)
            || !int.TryParse(codeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            return null;

        return code switch
        {
            0 => "Clear",
            1 or 2 or 3 => "Clouds",
            45 or 48 => "Fog",
            51 or 53 or 55 or 56 or 57 => "Drizzle",
            61 or 63 or 65 or 66 or 67 => "Rain",
            71 or 73 or 75 or 77 => "Snow",
            80 or 81 or 82 => "Rain",
            85 or 86 => "Snow",
            95 or 96 or 99 => "Thunderstorm",
            _ => null
        };
    }
}
