namespace Formax.Domain.Entities
{
    /// <summary>
    /// Feature Store kaydı — bir HistoricalMatch için üretilmiş özellik vektörünün TEK GERÇEK kaynağı.
    /// Probability Engine yalnızca buradan okur. <see cref="HistoricalMatchId"/> benzersiz (maç başına tek
    /// kayıt → duplicate yok). <see cref="FeatureHash"/> içerik değişimini yakalar (idempotent upsert).
    /// Feature vektörü <see cref="FeaturesJson"/>'da serileştirilir (tipli, sürüm-esnek).
    /// </summary>
    public class MatchFeatureRecord
    {
        public int Id { get; set; }

        public int HistoricalMatchId { get; set; }

        public System.DateTime MatchDate { get; set; }
        public int HistoricalCompetitionId { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        /// <summary>Serileştirilmiş MatchFeatureVector (feature'ların tek gerçek kaynağı).</summary>
        public string FeaturesJson { get; set; } = string.Empty;

        /// <summary>İçerik hash'i — aynıysa update atlanır (idempotent).</summary>
        public string FeatureHash { get; set; } = string.Empty;

        // ── Model TARGET (label) — tahmin edilen sonuç. Feature DEĞİL (leakage değil); X yanında y saklanır. ──
        /// <summary>1X2 sonucu: "H"/"D"/"A". Oynanmamışsa null.</summary>
        public string? TargetResult { get; set; }
        public int? TargetHomeGoals { get; set; }
        public int? TargetAwayGoals { get; set; }

        public System.DateTime LastUpdatedUtc { get; set; }
    }
}
