using System;
using System.Globalization;

namespace Formax.Infrastructure.Normalize.Time;

/// <summary>
/// Tarih/saat normalize altyapısı (saf, durumsuz).
/// Tüm sağlayıcı zamanlarını ortak formata — UTC <see cref="DateTimeOffset"/>'e — dönüştürür.
/// </summary>
public static class DateTimeNormalizer
{
    /// <summary>Offset'li bir değeri UTC'ye çevirir.</summary>
    public static DateTimeOffset ToUtc(DateTimeOffset value) => value.ToUniversalTime();

    /// <summary>
    /// <see cref="DateTime"/>'ı UTC'ye çevirir.
    /// Kind bilinmiyorsa (Unspecified) UTC varsayılır; sağlayıcı zamanları çoğunlukla UTC verilir.
    /// </summary>
    public static DateTimeOffset ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
        DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
        _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero)
    };

    /// <summary>
    /// Metin bir tarih/saati ISO-8601 veya yaygın formatlarda çözerek UTC'ye çevirir.
    /// Offset yoksa UTC varsayılır. Başarısızsa false döner.
    /// </summary>
    public static bool TryParseUtc(string? value, out DateTimeOffset utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            utc = parsed;
            return true;
        }

        return false;
    }
}
