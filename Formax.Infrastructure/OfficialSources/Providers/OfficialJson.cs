using System;
using System.Globalization;
using System.Text.Json;

namespace Formax.Infrastructure.OfficialSources.Providers
{
    /// <summary>Resmî JSON cevapları için güvenli okuma yardımcıları (alan yoksa null — uydurma yok).</summary>
    internal static class OfficialJson
    {
        public static JsonElement? Prop(this JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
                ? v : null;

        public static string? Str(this JsonElement e, string name)
        {
            var v = e.Prop(name);
            if (v == null) return null;
            return v.Value.ValueKind switch
            {
                JsonValueKind.String => string.IsNullOrWhiteSpace(v.Value.GetString()) ? null : v.Value.GetString()!.Trim(),
                JsonValueKind.Number => v.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        public static int? Int(this JsonElement e, string name)
        {
            var v = e.Prop(name);
            if (v == null) return null;
            if (v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetInt32(out var n)) return n;
            if (v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetDouble(out var d)) return (int)Math.Round(d);
            if (v.Value.ValueKind == JsonValueKind.String &&
                int.TryParse(v.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
            return null;
        }

        public static double? Dbl(this JsonElement e, string name)
        {
            var v = e.Prop(name);
            if (v == null) return null;
            if (v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetDouble(out var d)) return d;
            if (v.Value.ValueKind == JsonValueKind.String &&
                double.TryParse(v.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return s;
            return null;
        }

        public static bool Bool(this JsonElement e, string name)
            => e.Prop(name) is { ValueKind: JsonValueKind.True };

        public static bool IsArray(this JsonElement? e) => e is { ValueKind: JsonValueKind.Array };

        /// <summary>"…Z" ya da ofsetli ISO tarihini UTC'ye çevirir; yalnız tarih ("2026-11-28Z") de kabul edilir.</summary>
        public static DateTime? Utc(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            if (s.Length == 11 && s.EndsWith("Z", StringComparison.Ordinal)) s = s[..10] + "T00:00:00Z";
            return DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : null;
        }

        /// <summary>Yerel saat + IANA saat dilimi → UTC. Dilim çözülemezse null.</summary>
        public static DateTime? LocalToUtc(string? local, string? ianaZone, string windowsFallback)
        {
            if (string.IsNullOrWhiteSpace(local)) return null;
            if (!DateTime.TryParse(local.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return null;
            TimeZoneInfo? tz = null;
            foreach (var id in new[] { ianaZone, windowsFallback })
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                try { tz = TimeZoneInfo.FindSystemTimeZoneById(id!); break; }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            if (tz == null) return null;
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), tz);
        }

        /// <summary>Rol etiketinden FORMAX mevki kısaltması (G/D/M/F); bilinmiyorsa null.</summary>
        public static string? Position(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return null;
            var r = role.Trim().ToLowerInvariant();
            if (r.StartsWith("goal")) return "G";
            if (r.StartsWith("def")) return "D";
            if (r.StartsWith("mid")) return "M";
            if (r.StartsWith("for") || r.StartsWith("att") || r.StartsWith("str")) return "F";
            return null;
        }
    }
}
