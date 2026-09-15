using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>Resmî kaynağın bir önceki gözlemde bildirdiği değerler (bağlantı kaydından).</summary>
    public sealed record OfficialObservedState(DateTime? KickoffUtc, string? Venue, string? Status);

    /// <summary>Tespit edilen tek kritik gelişme.</summary>
    public sealed record CriticalObservation(
        string Type, string Severity, string? PreviousValue, string NewValue, string SummaryTr, string EvidenceHash);

    public static class CriticalDevelopmentTypes
    {
        public const string Postponed = "Postponed";
        public const string Cancelled = "Cancelled";
        public const string Suspended = "Suspended";
        public const string KickoffChanged = "KickoffChanged";
        public const string VenueChanged = "VenueChanged";
    }

    public static class CriticalSeverities
    {
        /// <summary>Erteleme/iptal — maç başına bildirim sınırından bağımsız.</summary>
        public const string Critical = "Critical";
        public const string High = "High";
    }

    /// <summary>
    /// KRİTİK GELİŞME DEDEKTÖRÜ — saf karar (ağ/DB/LLM yok).
    ///
    /// Girdi yalnız DOĞRULANMIŞ resmî kaynağın yapılandırılmış maç kaydıdır (kimliği çözülmüş).
    /// Haber başlığı, söylenti, yorum, tahmin yazısı bu sınıfa hiç gelmez. LLM karar vermez; özet
    /// kalıbı deterministiktir ve kaynağın başlığını kopyalamaz.
    ///
    /// KURALLAR:
    ///  • Erteleme / iptal / askıya alma: kaynak durumu bu değere İLK kez geçtiğinde (Critical/High).
    ///  • Başlama saati: maç henüz başlamamışken resmî saat, referanstan (önceki resmî gözlem; yoksa
    ///    FORMAX'ın kullanıcıya gösterdiği saat) en az <see cref="KickoffTolerance"/> farklıysa.
    ///    Kaynak saati "belirsiz" işaretliyse değişiklik aranmaz.
    ///  • Stat: YALNIZ iki resmî gözlem arasında ad değiştiyse (kaynaklar arası yazım farkı sayılmaz).
    /// </summary>
    public static class CriticalDevelopmentDetector
    {
        public static readonly TimeSpan KickoffTolerance = TimeSpan.FromMinutes(15);

        private static readonly TimeZoneInfo Istanbul = ResolveIstanbul();
        private static readonly CultureInfo Tr = new("tr-TR");

        public static IReadOnlyList<CriticalObservation> Detect(
            int matchId, string home, string away, DateTime formaxKickoffUtc,
            OfficialObservedState? previous, OfficialMatchRecord current, DateTime nowUtc, string? formaxStatus = null)
        {
            var list = new List<CriticalObservation>();
            var pair = $"{home}–{away}";

            // İlk gözlemde FORMAX maçı zaten ertelenmiş/iptal görünüyorsa bu kullanıcı için YENİ gelişme değildir.
            bool AlreadyKnown(string status)
                => previous == null && string.Equals(formaxStatus, status, StringComparison.OrdinalIgnoreCase);

            if (current.Status == OfficialMatchStatuses.Postponed && previous?.Status != OfficialMatchStatuses.Postponed
                && !AlreadyKnown("Postponed"))
                list.Add(Make(matchId, CriticalDevelopmentTypes.Postponed, CriticalSeverities.Critical,
                    previous?.Status, current.Status, $"{pair} maçı resmî kaynağa göre ertelendi."));

            if ((current.Status == OfficialMatchStatuses.Cancelled && previous?.Status != OfficialMatchStatuses.Cancelled && !AlreadyKnown("Cancelled"))
                || (current.Status == OfficialMatchStatuses.Abandoned && previous?.Status != OfficialMatchStatuses.Abandoned && !AlreadyKnown("Abandoned")))
            {
                var abandoned = current.Status == OfficialMatchStatuses.Abandoned
                                || current.RawStatus?.Contains("ABANDON", StringComparison.OrdinalIgnoreCase) == true;
                list.Add(Make(matchId, CriticalDevelopmentTypes.Cancelled, CriticalSeverities.Critical,
                    previous?.Status, current.RawStatus ?? current.Status,
                    abandoned ? $"{pair} maçı resmî kaynağa göre yarıda kaldı." : $"{pair} maçı resmî kaynağa göre iptal edildi."));
            }

            if (current.Status == OfficialMatchStatuses.Suspended && previous?.Status != OfficialMatchStatuses.Suspended)
                list.Add(Make(matchId, CriticalDevelopmentTypes.Suspended, CriticalSeverities.High,
                    previous?.Status, current.Status, $"{pair} maçı resmî kaynağa göre askıya alındı."));

            var unknownTime = current.Extra?.GetValueOrDefault("kickoffUnknown") == "true";
            if (current.Status == OfficialMatchStatuses.Scheduled && current.KickoffUtc is DateTime k && !unknownTime && k > nowUtc)
            {
                var reference = previous?.KickoffUtc ?? formaxKickoffUtc;
                if ((k - reference).Duration() >= KickoffTolerance)
                    list.Add(Make(matchId, CriticalDevelopmentTypes.KickoffChanged, CriticalSeverities.High,
                        reference.ToString("O"), k.ToString("O"),
                        $"{pair} maçının başlama saati resmî kaynağa göre {LocalText(k)} olarak güncellendi."));
            }

            if (current.Status == OfficialMatchStatuses.Scheduled
                && !string.IsNullOrWhiteSpace(previous?.Venue) && !string.IsNullOrWhiteSpace(current.Venue)
                && OfficialTeamNameMatcher.Fold(previous!.Venue) != OfficialTeamNameMatcher.Fold(current.Venue))
                list.Add(Make(matchId, CriticalDevelopmentTypes.VenueChanged, CriticalSeverities.High,
                    previous.Venue, current.Venue!, $"{pair} maçının oynanacağı stat resmî kaynağa göre {current.Venue} olarak değişti."));

            return list;
        }

        /// <summary>"13 Eylül 20:45 (TSİ)".</summary>
        public static string LocalText(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Istanbul);
            return local.ToString("d MMMM HH:mm", Tr) + " (TSİ)";
        }

        public static string Hash(int matchId, string type, string newValue)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{matchId}|{type}|{newValue}"))).ToLowerInvariant();

        private static CriticalObservation Make(int matchId, string type, string severity, string? prev, string next, string summary)
            => new(type, severity, prev, next, summary, Hash(matchId, type, next));

        private static TimeZoneInfo ResolveIstanbul()
        {
            foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("TRT", TimeSpan.FromHours(3), "TRT", "TRT");
        }
    }
}
