using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Formax.Application.Services.Lineups
{
    /// <summary>Kadro/oyuncu etki katmanının sürümü — snapshot ve ölçüm kayıtlarına yazılır.</summary>
    public static class LineupImpactVersion
    {
        /// <summary>
        /// lineup-impact-1 (19.09.2026): DOĞRULANMIŞ RESMÎ KADRODAN ÖĞRENİLEN OYUNCU ETKİSİ.
        /// Etki elle yazılmış bir kural değildir; yalnız maçtan ÖNCE bitmiş maçların artıklarından
        /// (gerçek gol − modelin beklediği gol) öğrenilir, örnekleme göre daraltılır ve takım
        /// tavanıyla sınırlanır. Veri yetersizse etki tam olarak 0'dır (uydurma değer üretilmez).
        /// </summary>
        public const string Current = "lineup-impact-1";
    }

    /// <summary>Oyuncu mevkii — etkinin hangi kanaldan (hücum/savunma) ölçüleceğini belirler.</summary>
    public static class LineupPositions
    {
        public const string Goalkeeper = "G";
        public const string Defender = "D";
        public const string Midfielder = "M";
        public const string Forward = "F";

        /// <summary>Kaynak yazımını FORMAX mevkiine indirger; tanınmayan değer null (mevki UYDURULMAZ).</summary>
        public static string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim().ToUpperInvariant();
            return s switch
            {
                "G" or "GK" or "KL" or "KALECI" or "KALECİ" or "GOALKEEPER" or "PORTIERE" or "POR" => Goalkeeper,
                "D" or "DF" or "DEF" or "DEFENDER" or "DIFENSORE" or "DIF" or "SAVUNMA" => Defender,
                "M" or "MF" or "MID" or "MIDFIELDER" or "CENTROCAMPISTA" or "CEN" or "ORTA SAHA" => Midfielder,
                "F" or "FW" or "ATT" or "FORWARD" or "ATTACKER" or "ATTACCANTE" or "FORVET" => Forward,
                _ => null
            };
        }

        /// <summary>
        /// MEVKİNİN HÜCUM/SAVUNMA KANALI AĞIRLIĞI. Kaleci ve savunma oyuncusunun etkisi hücum
        /// artığından HİÇ okunmaz (ağırlık 0): "savunmacıyı gol/asistle ölçme" kuralı burada
        /// yapısal olarak uygulanır. Orta saha iki kanaldan yarım ağırlıkla katılır.
        /// </summary>
        public static (double Attack, double Defence) Channel(string? position) => Normalize(position) switch
        {
            Goalkeeper => (0.0, 1.0),
            Defender => (0.0, 0.8),
            Midfielder => (0.5, 0.5),
            Forward => (0.9, 0.0),
            _ => (0.0, 0.0) // mevki bilinmiyorsa etki hesaplanmaz
        };

        /// <summary>Mevki bilinmiyorsa oyuncu etki hesabına GİRMEZ (yerine geçecek değer uydurulmaz).</summary>
        public static bool IsKnown(string? position) => Normalize(position) != null;
    }

    /// <summary>Kadro güven düzeyi — kullanıcıya ve karar kapısına aynı sözlük gider.</summary>
    public static class LineupConfidenceLevels
    {
        /// <summary>Doğrulanmış kadro yok.</summary>
        public const string None = "None";
        /// <summary>Kadro var ama doğrulama kuralını geçmedi (tek taraf, eksik ilk 11...).</summary>
        public const string Insufficient = "Insufficient";
        /// <summary>İki taraf doğrulandı ama oyuncuların çok azının ölçülebilir geçmişi var.</summary>
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
    }

    /// <summary>Kadronun kaynak durumu (UI ve denetim aynı sözlüğü okur).</summary>
    public static class LineupSourceStatuses
    {
        public const string Missing = "Missing";
        public const string Partial = "Partial";
        public const string Verified = "Verified";
    }

    /// <summary>Kadro düzeltmesinin makine okunabilir gerekçeleri — kullanıcıya teknik metin olarak GİTMEZ.</summary>
    public static class LineupReasonCodes
    {
        public const string LineupMissing = "LINEUP_MISSING";
        public const string LineupPartial = "LINEUP_PARTIAL";
        public const string LineupVerified = "LINEUP_VERIFIED";
        public const string LineupRejected = "LINEUP_REJECTED";
        public const string PlayerSampleInsufficient = "PLAYER_SAMPLE_INSUFFICIENT";
        public const string ReplacementBaselineUnknown = "REPLACEMENT_BASELINE_UNKNOWN";
        public const string PositionUnknown = "POSITION_UNKNOWN";
        public const string ImpactCapped = "IMPACT_CAPPED";
        public const string NoMaterialChange = "NO_MATERIAL_CHANGE";
        /// <summary>Katman ÜRETİMDE DEĞİL: hesaplandı, kaydedildi, ama yayımlanan olasılığa uygulanmadı.</summary>
        public const string ShadowNotProduction = "SHADOW_NOT_PRODUCTION";
    }

    /// <summary>
    /// TEK OYUNCU SATIRI — doğrulanmış kadrodan. <paramref name="PlayerKey"/> canonical oyuncu
    /// anahtarıdır; belirsiz eşleşmede satır <c>Resolved=false</c> yazılır ve etki hesabına girmez.
    /// </summary>
    public sealed record LineupPlayerObservation(
        string PlayerKey,
        string DisplayName,
        string? Position,
        int? ShirtNumber,
        bool Starter)
    {
        public bool Resolved => !string.IsNullOrEmpty(PlayerKey) && LineupPositions.IsKnown(Position);
    }

    /// <summary>
    /// TEK MAÇIN KADRO GÖZLEMİ — doğrulanmış ilk 11'ler. Ham sayfa içeriği taşınmaz; yalnız canonical
    /// kadro ve kanıt üst verisi.
    /// </summary>
    public sealed record MatchLineupObservation(
        int MatchId,
        DateTime KickoffUtc,
        int LeagueId,
        int HomeTeamId,
        int AwayTeamId,
        IReadOnlyList<LineupPlayerObservation> Home,
        IReadOnlyList<LineupPlayerObservation> Away,
        string VerificationStatus,
        string? SourceKey,
        DateTime? VerifiedAtUtc)
    {
        public IReadOnlyList<LineupPlayerObservation> Side(bool home) => home ? Home : Away;
    }

    /// <summary>
    /// KADRO DOĞRULAMA KURALI (C) — bir kadronun OYUNCU-ETKİ MODELİNE girebilmesi için geçmesi
    /// gereken kapı. UI eksik kadroyu açıklayabilir; model giremez.
    ///
    /// Kurallar: iki takımın da ilk 11'i, her takımda 11 BENZERSİZ başlangıç oyuncusu, aynı oyuncunun
    /// iki takımda birden bulunmaması, doğrulanmış kaynak durumu ve doğrulama zamanı.
    /// </summary>
    public static class LineupVerificationRule
    {
        public const int StartersPerSide = 11;

        public sealed record Verdict(bool Accepted, string SourceStatus, IReadOnlyList<string> ReasonCodes);

        /// <summary>
        /// ÜRETİM KAPISI — yapı VE kaynak doğrulaması birlikte. Modele yalnız bunu geçen kadro girer.
        /// </summary>
        public static Verdict Check(MatchLineupObservation? obs) => Check(obs, requireVerifiedSource: true);

        /// <summary>
        /// YAPI KAPISI (YALNIZ ÖLÇÜM) — 22 benzersiz başlangıç oyuncusu kuralı geçerli, kaynak damgası
        /// aranmaz. Yalnız "resmî kaynak kapsamı büyüseydi ne olurdu?" duyarlılık ölçümü içindir;
        /// üretim yolu bunu KULLANMAZ ve rapor bu ölçümü ayrı etiketler.
        /// </summary>
        public static Verdict CheckStructureOnly(MatchLineupObservation? obs) => Check(obs, requireVerifiedSource: false);

        private static Verdict Check(MatchLineupObservation? obs, bool requireVerifiedSource)
        {
            if (obs == null)
                return new Verdict(false, LineupSourceStatuses.Missing, new[] { LineupReasonCodes.LineupMissing });

            var reasons = new List<string>();
            var home = obs.Home.Where(p => p.Starter).ToList();
            var away = obs.Away.Where(p => p.Starter).ToList();

            var homeNames = home.Select(p => p.PlayerKey).Where(k => !string.IsNullOrEmpty(k)).ToHashSet(StringComparer.Ordinal);
            var awayNames = away.Select(p => p.PlayerKey).Where(k => !string.IsNullOrEmpty(k)).ToHashSet(StringComparer.Ordinal);

            var complete = home.Count == StartersPerSide && away.Count == StartersPerSide
                           && homeNames.Count == StartersPerSide && awayNames.Count == StartersPerSide
                           && !homeNames.Overlaps(awayNames);

            if (home.Count == 0 || away.Count == 0)
            {
                reasons.Add(LineupReasonCodes.LineupPartial);
                return new Verdict(false,
                    home.Count == 0 && away.Count == 0 ? LineupSourceStatuses.Missing : LineupSourceStatuses.Partial,
                    reasons);
            }
            if (!complete)
            {
                reasons.Add(LineupReasonCodes.LineupRejected);
                return new Verdict(false, LineupSourceStatuses.Partial, reasons);
            }
            if (requireVerifiedSource && obs.VerificationStatus != "Verified")
            {
                reasons.Add(LineupReasonCodes.LineupPartial);
                return new Verdict(false, LineupSourceStatuses.Partial, reasons);
            }
            reasons.Add(LineupReasonCodes.LineupVerified);
            return new Verdict(true, LineupSourceStatuses.Verified, reasons);
        }
    }

    /// <summary>
    /// OYUNCU KİMLİĞİ — canonical anahtar. Kimlik YALNIZ metin benzerliğinden kurulmaz: anahtar
    /// (takım, normalleştirilmiş ad) çiftidir; iki farklı takımın aynı adlı oyuncuları birleşmez.
    /// Ad normalizasyonu diakritik, noktalama ve büyük/küçük harf farkını siler; Türkçe İ/ı tuzağı
    /// için kültürden BAĞIMSIZ dönüşüm kullanılır.
    /// </summary>
    public static class PlayerIdentity
    {
        public static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var lowered = name.Trim().ToLowerInvariant()
                .Replace('ı', 'i').Replace('İ', 'i').Replace('ş', 's').Replace('ğ', 'g')
                .Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');
            var decomposed = lowered.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (char.IsWhiteSpace(ch) && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
            }
            return sb.ToString().Trim();
        }

        /// <summary>Canonical oyuncu anahtarı: takım + normalleştirilmiş ad. Ad çözülemezse boş (unresolved).</summary>
        public static string Key(int teamId, string? name)
        {
            var n = NormalizeName(name);
            return n.Length < 2 ? string.Empty : teamId.ToString(CultureInfo.InvariantCulture) + ":" + n;
        }
    }
}
