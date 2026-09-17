using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// RESMÎ VERİ KAYNAĞI KATALOĞU — sonuç ve istatistik botunun kaynak başına kalıcı sağlık kaydı.
    ///
    /// Statik alanlar (ad, alan adı, organizasyonlar, kanıt) kod içindeki ölçülmüş kayıt defterinden eşitlenir;
    /// sağlık alanları (son kontrol, ardışık hata, devre kesici, robots durumu) yalnız botun gerçek okumasından yazılır
    /// ve restart sonrasında korunur.
    /// </summary>
    public sealed class OfficialDataSource
    {
        public string SourceId { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string OfficialDomain { get; set; } = string.Empty;
        /// <summary>Virgülle ayrılmış FORMAX lig kimlikleri.</summary>
        public string OrganizationIds { get; set; } = string.Empty;
        /// <summary>Federation | LeagueMatchCentre | HomeClub | AwayClub | Broadcaster …</summary>
        public string SourceType { get; set; } = string.Empty;
        public string ContentKind { get; set; } = string.Empty;
        /// <summary>Virgülle ayrılmış amaçlar (Result, Statistics …).</summary>
        public string Capabilities { get; set; } = string.Empty;
        public string VerificationEvidence { get; set; } = string.Empty;
        /// <summary>Kayıt defterindeki ölçülmüş durum (Verified / Blocked / NeedsManualReview …).</summary>
        public string RegistryStatus { get; set; } = string.Empty;
        /// <summary>Unknown | Allowed | Disallowed | Unreachable.</summary>
        public string RobotsStatus { get; set; } = "Unknown";
        public bool IsEnabled { get; set; }
        public string ParserVersion { get; set; } = string.Empty;
        public DateTime? LastCheckedUtc { get; set; }
        public DateTime? LastSuccessUtc { get; set; }
        public string? LastError { get; set; }
        public int ConsecutiveFailureCount { get; set; }
        public DateTime? CircuitBreakerUntilUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    /// <summary>
    /// SONUÇ KONTROL PLANI — maç başına tek satır. Bot maç sırasında canlı veri aramaz; ilk kontrol beklenen bitişe
    /// yakın başlar ve sonuç yayımlanana kadar kalıcı takvimle tekrarlanır. Satır kilidi aynı maçın iki işçi tarafından
    /// aynı anda işlenmesini engeller.
    /// </summary>
    public sealed class MatchResultCheck
    {
        public int MatchId { get; set; }
        public int LeagueId { get; set; }
        public DateTime KickoffUtc { get; set; }
        /// <summary>Pending | Resolved | Postponed | Cancelled | Abandoned | NoOfficialSource.</summary>
        public string State { get; set; } = "Pending";
        public int AttemptCount { get; set; }
        public DateTime NextCheckUtc { get; set; }
        public DateTime? LastCheckUtc { get; set; }
        public string? LastOutcome { get; set; }
        public string? LastSourceKey { get; set; }
        /// <summary>Son denemenin hata sınıfı; hata yoksa null (NotFinalYet | NoOfficialSource | CircuitOpen | SourceReadFailed | IdentityNotMatched | Conflict | VerificationPending).</summary>
        public string? LastErrorClass { get; set; }
        /// <summary>Son denemenin doğrulama durumu (Verified | NotFinal | NotChecked | Rejected:{neden} | Conflict | Pending).</summary>
        public string? LastValidationStatus { get; set; }
        /// <summary>Kaynağın maçı ilk kez "bitti" olarak gösterdiği gözlem zamanı.</summary>
        public DateTime? FirstFinalSeenUtc { get; set; }
        /// <summary>Kaynağın en son "henüz final değil" dediği kontrol (yayın anının alt sınırı; gecikme ölçümü).</summary>
        public DateTime? LastNotFinalCheckUtc { get; set; }
        /// <summary>Kaynağın kendi yayımladığı final anı (ör. UEFA fullTimeAt); yayımlamıyorsa null.</summary>
        public DateTime? SourcePublishedFinalAtUtc { get; set; }
        /// <summary>Kanonik sonucun DB'ye yazıldığı zaman.</summary>
        public DateTime? ResolvedAtUtc { get; set; }
        /// <summary>FT | AET | PEN | Postponed | Cancelled | Abandoned.</summary>
        public string? ResolvedStatus { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public string? LockOwner { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    /// <summary>
    /// İSTATİSTİK KONTROL PLANI — kesin sonuçtan sonra resmî istatistik aranır (+10 dk … +24 sa, sonra günlük düşük
    /// öncelik). Tam veri bulununca kapanır; kısmi veri daha zengin kaynak için takvimde kalır.
    /// </summary>
    public sealed class MatchStatisticsCheck
    {
        public int MatchId { get; set; }
        public int LeagueId { get; set; }
        public DateTime FinalResultAtUtc { get; set; }
        /// <summary>Pending | Complete | Exhausted.</summary>
        public string State { get; set; } = "Pending";
        /// <summary>None | Partial | Full.</summary>
        public string Completeness { get; set; } = "None";
        public int AttemptCount { get; set; }
        public DateTime NextCheckUtc { get; set; }
        public DateTime? LastCheckUtc { get; set; }
        public string? LastOutcome { get; set; }
        public string? LastSourceKey { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public string? LockOwner { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    /// <summary>
    /// İSTATİSTİK GÖZLEMİ — kaynağın bir taraf için yayımladığı alanlar (null = yayımlanmadı) ve karar.
    /// Ham HTML saklanmaz; yalnız ayrıştırılmış alanlar, özet ve karar.
    /// </summary>
    public sealed class MatchStatisticObservation
    {
        public long Id { get; set; }
        public int MatchId { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        /// <summary>Home | Away.</summary>
        public string Side { get; set; } = string.Empty;
        public string FieldsJson { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string ParserVersion { get; set; } = string.Empty;
        /// <summary>Written | Updated | Unchanged | ConflictRecorded | OrientationRejected.</summary>
        public string Decision { get; set; } = string.Empty;
        public string? ConflictDetail { get; set; }
        public DateTime ObservedAtUtc { get; set; }
    }
}
