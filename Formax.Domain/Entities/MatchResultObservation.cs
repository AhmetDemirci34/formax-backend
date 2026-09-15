using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// RESMÎ SONUÇ GÖZLEMİ — kaynağın bir turda gördüğü durum/skor ve FORMAX'ın kararı.
    ///
    /// Kanonik veri körlemesine ezilmez: kayıtlı sonuç başka bir kaynaktan geldiyse ve resmî kaynak farklı bir skor
    /// yayımlıyorsa bu satır çelişkiyi (kaynak, zaman, iki değer) saklar; sonuç ancak resmî skor ardışık iki
    /// gözlemde aynı kalınca değiştirilir.
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
        /// <summary>"Applied" | "Unchanged" | "ConflictRecorded" | "ConflictResolvedAfterConfirmation" | "StatusApplied".</summary>
        public string Decision { get; set; } = string.Empty;
        public DateTime ObservedAtUtc { get; set; }
    }
}
