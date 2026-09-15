using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// RESMÎ SONUÇ GÖZLEMİ — kaynağın bir turda gördüğü durum/skor ve FORMAX'ın kararı.
    ///
    /// Kanonik veri körlemesine ezilmez: kayıtlı sonuç başka bir kaynaktan geldiyse ve resmî kaynak farklı bir skor
    /// yayımlıyorsa bu satır çelişkiyi (kaynak, zaman, iki değer) saklar; sonuç ancak resmî skor ardışık iki
    /// gözlemde aynı kalınca değiştirilir. Ham HTML saklanmaz: kaynak kimliği/adresi, ayrıştırılmış alanlar,
    /// parser sürümü ve içerik özeti saklanır.
    /// </summary>
    public sealed class MatchResultObservation
    {
        public long Id { get; set; }
        public int MatchId { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public string OfficialStatus { get; set; } = string.Empty;
        public int? OfficialHomeScore { get; set; }
        public int? OfficialAwayScore { get; set; }
        public string? ExistingStatus { get; set; }
        public int? ExistingHomeScore { get; set; }
        public int? ExistingAwayScore { get; set; }
        public string? ExistingSource { get; set; }
        /// <summary>"Applied" | "Unchanged" | "ConflictRecorded" | "ConflictResolvedAfterConfirmation" | "StatusApplied" | "Observed".</summary>
        public string Decision { get; set; } = string.Empty;
        public DateTime ObservedAtUtc { get; set; }

        // ── Gözlem defteri (15.09.2026, additive) ────────────────────────────────
        public string? SourceMatchId { get; set; }
        public string? SourceUrl { get; set; }
        public string? SourceHomeName { get; set; }
        public string? SourceAwayName { get; set; }
        public DateTime? SourceKickoffUtc { get; set; }
        public int? OfficialHalfTimeHome { get; set; }
        public int? OfficialHalfTimeAway { get; set; }
        /// <summary>FT | AET | PEN (yalnız bitmiş maçta).</summary>
        public string? OfficialResultDetail { get; set; }
        public int? OfficialPenaltyHome { get; set; }
        public int? OfficialPenaltyAway { get; set; }
        /// <summary>Accepted | IdentityRejected | WrongDate | WrongTeams | OrientationMismatch.</summary>
        public string? ValidationResult { get; set; }
        /// <summary>None | Conflict | Resolved.</summary>
        public string? ConflictStatus { get; set; }
        public string? ParserVersion { get; set; }
        public string? ContentHash { get; set; }
    }
}
