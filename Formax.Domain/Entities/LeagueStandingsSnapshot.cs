using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX — LİG PUAN DURUMU PROJEKSİYONU (iç kaynak).
    ///
    /// NEDEN VAR: puan durumu şimdiye kadar sağlayıcıdan (api-football /standings →
    /// <see cref="LeagueStanding"/>) geliyordu; tablo hem sağlayıcı tazeleme sıklığına
    /// bağlıydı hem de kimliği EXTERNAL takım id'siydi. Bu snapshot ise TAMAMEN
    /// KENDİ VERİMİZDEN üretilir: kapsam içi, MEVCUT SEZONA ait, TAMAMLANMIŞ lig
    /// maçları toplanır. Yeni sağlayıcı isteği YOKTUR (kota etkisi sıfır).
    ///
    /// Lig + sezon başına TEK satır tutulur (unique index). Satırlar JSON taşınır:
    /// okuma tek satırlık bir sorgudur ve doğrudan cache'e konur — maç detayı her
    /// tıklamada yeniden HESAPLAMAZ.
    /// </summary>
    public sealed class LeagueStandingsSnapshot
    {
        public int Id { get; set; }

        /// <summary>Canonical lig id (Match.LeagueId ile aynı uzay).</summary>
        public int LeagueId { get; set; }

        /// <summary>Sezon başlangıç yılı (2026 = 2026/27 sezonu).</summary>
        public int SeasonYear { get; set; }

        /// <summary>Sezonun GERÇEK başlangıcı — ligin bu sezondaki ilk maçının tarihi.</summary>
        public DateTime SeasonStartUtc { get; set; }

        /// <summary>Projeksiyonun üretildiği an.</summary>
        public DateTime CalculatedAtUtc { get; set; }

        /// <summary>Tabloya giren EN SON tamamlanmış maçın tarihi. Hiç maç yoksa null.</summary>
        public DateTime? LastIncludedMatchUtc { get; set; }

        /// <summary>Sabit: InternalResultsProjection (dış kaynak değil, kendi sonuçlarımız).</summary>
        public string Source { get; set; } = StandingsSnapshotSources.InternalResultsProjection;

        /// <summary>Tabloya giren tamamlanmış maç sayısı.</summary>
        public int MatchesIncluded { get; set; }

        /// <summary>
        /// Sıralama kuralının kimliği (ör. "LaLiga.HeadToHead", "Generic.PointsGoalDiff").
        /// Ligin RESMÎ eşitlik bozma kuralı uygulanamıyorsa <see cref="IsProvisional"/> true olur.
        /// </summary>
        public string RankingRuleId { get; set; } = string.Empty;

        /// <summary>
        /// true = sıralama RESMÎ değildir (ligin gerçek tie-break kuralı bilinmiyor ve
        /// eşit puanlı takım var). Veri alanları doğrudur, YALNIZ sıra geçicidir.
        /// </summary>
        public bool IsProvisional { get; set; }

        /// <summary>Satırlar (LeagueStandingsSnapshotRow[]) JSON olarak.</summary>
        public string RowsJson { get; set; } = "[]";

        // ── VERİ TAMLIĞI (30.08.2026) ───────────────────────────────────────────
        // Tablo "17 maçtan hesaplandı" demek yetmez: o tarihe kadar OYNANMIŞ OLMASI
        // GEREKEN kaç maç var, kaçı depoda kesinleşmiş? Eksik varsa tablo "güncel" ya da
        // "resmî" diye SUNULMAZ (IsFresh true olsa bile).

        /// <summary>Hesap anına kadar başlama saati GEÇMİŞ lig maçı sayısı (beklenen).</summary>
        public int ExpectedCompletedFixtures { get; set; }

        /// <summary>Bunlardan Status=Finished olan, tabloya GERÇEKTEN giren maç sayısı.</summary>
        public int IncludedCompletedFixtures { get; set; }

        /// <summary>Beklenen ama kesinleşmemiş (sonucu gelmemiş) maç sayısı.</summary>
        public int MissingCompletedFixtures { get; set; }

        /// <summary>Eksik maç yok mu? false → tablo tamamlanıyor demektir.</summary>
        public bool IsComplete { get; set; }

        // ── OYNANMAMIŞ MAÇLAR (31.08.2026) ──────────────────────────────────────
        // Bunlar beklenenin DIŞINDADIR: sonuçları yoktur ve BEKLENMEZ, bu yüzden
        // tabloyu "eksik" yapmazlar. Yine de gizlenmez — UI istenirse "1 ertelenmiş
        // maç bulunuyor" diyebilsin diye ayrı ayrı sayılır.

        /// <summary>Ertelenmiş maç sayısı (ileri bir tarihte oynanacak).</summary>
        public int PostponedFixtures { get; set; }

        /// <summary>İptal edilmiş maç sayısı.</summary>
        public int CancelledFixtures { get; set; }

        /// <summary>Yarıda kalmış maç sayısı.</summary>
        public int AbandonedFixtures { get; set; }

        /// <summary>
        /// Oynanması beklendiği hâlde hâlâ NotStarted/Live duran maç sayısı —
        /// sonuç alım hattının borcu. <see cref="MissingCompletedFixtures"/> ile aynı
        /// kümedir; ayrıştığı an status eşlemesinde kapatılmamış bir kod var demektir.
        /// </summary>
        public int StaleResultFixtures { get; set; }

        /// <summary>Tamlık denetiminin yapıldığı an.</summary>
        public DateTime CompletenessCheckedAtUtc { get; set; }
    }

    /// <summary>Snapshot kaynağı sabitleri — metin tekrarı olmasın diye tek yerde.</summary>
    public static class StandingsSnapshotSources
    {
        public const string InternalResultsProjection = "InternalResultsProjection";
    }
}
