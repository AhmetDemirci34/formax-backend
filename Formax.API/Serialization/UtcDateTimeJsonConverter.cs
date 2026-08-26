using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Formax.API.Serialization
{
    /// <summary>
    /// TÜM API yanıtlarında DateTime'ı ISO-8601 UTC ("…Z") olarak yazar.
    ///
    /// NEDEN VAR (ölçülen kök neden, 18.08.2026):
    /// FORMAX'ta bütün zamanlar UTC saklanır (GdpMatchPersister → kickoff.UtcDateTime), ancak
    /// EF Core SQL Server'dan okunan DateTime'ın Kind'ı <c>Unspecified</c>'dır. System.Text.Json
    /// Unspecified bir DateTime'ı zaman dilimi eki OLMADAN yazar: <c>"2026-08-18T19:00:00"</c>.
    /// Tarayıcı bu biçimi ECMA-262 gereği YEREL saat kabul eder → TR kullanıcısında kickoff
    /// 19:00 sanılır (gerçek: 22:00). Ölçülen sonuç: 19:06'da Fenerbahçe–Lyon maçının kickoff'u
    /// "geçmiş" görünüyor ve Maç Merkezi geri sayım yerine CANLI gösteriyordu.
    ///
    /// Keşfet akışı bu hatadan kurtulmuştu çünkü tek bir yerde (GetRecommendationFeedUseCase)
    /// <c>DateTime.SpecifyKind(..., Utc)</c> yapılıyordu — yani düzeltme noktasaldı. Bu converter
    /// aynı garantiyi TEK ORTAK yerde tüm DTO'lara verir; nokta çözümlere gerek kalmaz.
    ///
    /// Değer DEĞİŞMEZ: yalnız "bu değer UTC'dir" bilgisi seri hâle eklenir (+3 gibi hiçbir kayma
    /// uygulanmaz). Local Kind gelirse gerçek UTC karşılığına çevrilir.
    /// </summary>
    public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
    {
        internal const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ToUtc(reader.GetDateTime());

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(ToUtc(value).ToString(Format, CultureInfo.InvariantCulture));

        internal static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc   => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            // Depodan gelen Unspecified değerler zaten UTC'dir; yalnız etiketlenir.
            _                  => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    /// <summary>Nullable karşılığı — bkz. <see cref="UtcDateTimeJsonConverter"/>.</summary>
    public sealed class NullableUtcDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null
                ? null
                : UtcDateTimeJsonConverter.ToUtc(reader.GetDateTime());

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(
                UtcDateTimeJsonConverter.ToUtc(value.Value)
                    .ToString(UtcDateTimeJsonConverter.Format, CultureInfo.InvariantCulture));
        }
    }
}
