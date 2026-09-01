using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// LİG SEZONU METADATA KAYDI — "bu sezon ne zaman başladı" sorusunun TEK RESMÎ kaynağı.
    ///
    /// NEDEN VAR (30.08.2026 düzeltmesi): sezon başlangıcı önce depodaki İLK FİKSTÜR
    /// tarihinden türetiliyordu. Bu bir TEŞHİS değeridir, resmî sezon başlangıcı DEĞİLDİR:
    /// depoya eksik/geç giren fikstür başlangıcı kaydırır ve tüm form/puan hesabını sessizce
    /// yanlış pencereye taşır. Artık başlangıç YALNIZ doğrulanmış metadata kaydından okunur.
    ///
    /// Kimlik LeagueId + SeasonYear'dır; lig ADINA özel kod veya sabit YOKTUR.
    /// Kayıt yoksa çözüm başarısızdır (SEASON_START_UNRESOLVED) — tarih TAHMİN EDİLMEZ ve
    /// eksik pencere önceki sezon maçlarıyla DOLDURULMAZ.
    /// </summary>
    public sealed class LeagueSeason
    {
        /// <summary>Canonical lig id (Match.LeagueId ile aynı uzay).</summary>
        public int LeagueId { get; set; }

        /// <summary>Sezon kimliği = sezon başlangıç yılı (2026 → 2026/27).</summary>
        public int SeasonYear { get; set; }

        /// <summary>Sezonun RESMÎ başlangıcı (UTC gün başı).</summary>
        public DateTime StartUtc { get; set; }

        /// <summary>
        /// Sezonun resmî bitişi. Bilinmiyorsa null → kapsam penceresi bir sonraki sezon
        /// kovasının başına kadar uzanır (sınır olarak kullanılır, "resmî bitiş" diye sunulmaz).
        /// </summary>
        public DateTime? EndUtc { get; set; }

        /// <summary>Kaydın kaynağı: "Verified" | "Configuration" | operasyonun verdiği etiket.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Kaydın doğrulandığı an — kim/ne zaman onayladı izi.</summary>
        public DateTime VerifiedAtUtc { get; set; }

        /// <summary>Serbest not (ör. "kullanıcı tarafından doğrulandı 30.08.2026").</summary>
        public string? Notes { get; set; }
    }
}
