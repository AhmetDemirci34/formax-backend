using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// GEÇMİŞ KADRO DOLDURMA CHECKPOINT'İ — kaynak × lig × sezon başına tek satır.
    ///
    /// Süreç durdurulup yeniden başlatıldığında iş buradan devam eder: hangi sezonun kaç maçı
    /// işlendi, en son hangi maçta kalındı, kaç maç doğrulandı. Uygulama açılışında KENDİLİĞİNDEN
    /// çalışmaz; yalnız açıkça tetiklendiğinde ilerler.
    /// </summary>
    public sealed class LineupBackfillCheckpoint
    {
        public long Id { get; set; }

        /// <summary>Resmî kaynak anahtarı ("premier-league-sdp", "seriea-sdp", "tff-site").</summary>
        public string SourceKey { get; set; } = string.Empty;

        public int LeagueId { get; set; }

        /// <summary>Kaynağın kendi sezon kimliği (PL: "2025", Serie A: "serie-a::Football_Season::…").</summary>
        public string SeasonId { get; set; } = string.Empty;

        /// <summary>İnsan okunur sezon etiketi ("2025/2026").</summary>
        public string SeasonLabel { get; set; } = string.Empty;

        /// <summary>Pending | Running | Completed | Failed | Stopped.</summary>
        public string Status { get; set; } = LineupBackfillStatuses.Pending;

        /// <summary>Kaynağın bu sezonda listelediği maç sayısı (son sayım).</summary>
        public int SourceMatches { get; set; }

        /// <summary>DB'de canonical karşılığı bulunan maç sayısı.</summary>
        public int MatchedMatches { get; set; }

        public int ProcessedMatches { get; set; }
        public int VerifiedMatches { get; set; }
        public int SkippedMatches { get; set; }
        public int FailedMatches { get; set; }

        /// <summary>Sıralı işlemede en son tamamlanan kaynak maç kimliği — devam noktası.</summary>
        public string? LastOfficialMatchId { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? LastRunAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>Son hata özeti (kısaltılmış); başarıyla tamamlanınca temizlenir.</summary>
        public string? LastError { get; set; }
    }

    public static class LineupBackfillStatuses
    {
        public const string Pending = "Pending";
        public const string Running = "Running";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Stopped = "Stopped";
    }

    /// <summary>
    /// TEK MAÇIN DOLDURMA DENEMESİ — idempotensin ve "sonsuz döngü yok" güvencesinin deposu.
    ///
    /// Aynı maç ikinci kez işlenmeye kalkarsa: içerik özeti (ContentHash) aynıysa hiçbir şey
    /// yazılmaz; farklıysa kontrollü güncelleme yapılır. Başarısız maçlar denemeleriyle birlikte
    /// burada birikir ve <see cref="LineupBackfillPolicyLimits.MaxAttempts"/> sonrası bir daha
    /// denenmez (başarısız kuyruğu).
    /// </summary>
    public sealed class LineupBackfillAttempt
    {
        public long Id { get; set; }

        public string SourceKey { get; set; } = string.Empty;

        /// <summary>Kaynağın maç kimliği — canonical eşleme kurulamasa bile deneme kaydedilir.</summary>
        public string OfficialMatchId { get; set; } = string.Empty;

        /// <summary>Canonical FORMAX maç kimliği; eşleme kurulamadıysa null.</summary>
        public int? MatchId { get; set; }

        public int LeagueId { get; set; }
        public string SeasonId { get; set; } = string.Empty;

        /// <summary>
        /// Verified | Partial | NotPublished | Rejected | IdentityRejected | FetchFailed |
        /// NoCanonicalFixture | Unchanged.
        /// </summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>Ayrıştırılan içeriğin SHA-256 özeti — aynı içerik ikinci kez işlenmez.</summary>
        public string? ContentHash { get; set; }

        public int Attempts { get; set; }
        public DateTime FirstAttemptAtUtc { get; set; }
        public DateTime LastAttemptAtUtc { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>Politika sınırları — kod ve testler tek yerden okur.</summary>
    public static class LineupBackfillPolicyLimits
    {
        /// <summary>Bir maç bu kadar başarısız denemeden sonra bir daha denenmez.</summary>
        public const int MaxAttempts = 3;
    }
}
