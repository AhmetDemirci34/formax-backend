using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Standings
{
    /// <summary>
    /// İÇ KAYNAKLI PUAN DURUMU (LeagueStandingsSnapshot) — API ve UI sözleşmesi.
    ///
    /// Kaynak sağlayıcı DEĞİL, kendi tamamlanmış maçlarımızdır. Her okuma cache'ten gelir;
    /// maç detayı tıklaması hesap TETİKLEMEZ ve dış istek ÜRETMEZ.
    /// </summary>
    public class LeagueStandingsSnapshotDto
    {
        public int LeagueId { get; set; }
        public string LeagueName { get; set; } = string.Empty;

        /// <summary>Sezon kimliği = sezon başlangıç yılı (2026 → 2026/27).</summary>
        public int SeasonId { get; set; }
        public string SeasonLabel { get; set; } = string.Empty;

        public DateTime SeasonStartDate { get; set; }
        public DateTime CalculatedAtUtc { get; set; }
        public DateTime? LastIncludedMatchUtc { get; set; }

        /// <summary>Sabit: "InternalResultsProjection".</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Snapshot tazelik eşiğinin içinde mi (bkz. StandingsFreshness).</summary>
        public bool IsFresh { get; set; }

        /// <summary>Tabloya giren tamamlanmış maç sayısı.</summary>
        public int MatchesIncluded { get; set; }

        /// <summary>Uygulanan sıralama kuralının kimliği.</summary>
        public string RankingRuleId { get; set; } = string.Empty;

        // ── VERİ TAMLIĞI ────────────────────────────────────────────────────────
        /// <summary>Bu ana kadar oynanmış OLMASI GEREKEN lig maçı sayısı.</summary>
        public int ExpectedCompletedFixtures { get; set; }
        /// <summary>Bunlardan sonucu kesinleşmiş (tabloya giren) maç sayısı.</summary>
        public int IncludedCompletedFixtures { get; set; }
        /// <summary>Sonucu hâlâ gelmemiş maç sayısı.</summary>
        public int MissingCompletedFixtures { get; set; }
        /// <summary>
        /// false → tablo EKSİK. IsFresh true olsa bile "güncel/resmî" diye sunulamaz;
        /// UI "Puan durumu verileri tamamlanıyor" der.
        ///
        /// ERTELENMİŞ MAÇ BUNU false YAPMAZ: oynanmamış maçın sonucu beklenmez.
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>Ertelenmiş maç sayısı — tabloyu eksik YAPMAZ, yalnız bilgidir.</summary>
        public int PostponedFixtures { get; set; }
        /// <summary>İptal edilmiş maç sayısı.</summary>
        public int CancelledFixtures { get; set; }
        /// <summary>Yarıda kalmış maç sayısı.</summary>
        public int AbandonedFixtures { get; set; }
        /// <summary>Oynanması beklenip sonucu hâlâ gelmemiş maç sayısı.</summary>
        public int StaleResultFixtures { get; set; }

        /// <summary>"DomesticLeague" | "LeaguePhase" | "None" — tablonun üretildiği aşama.</summary>
        public string ScopePhase { get; set; } = string.Empty;
        /// <summary>"Resolved" | "STANDINGS_PHASE_UNRESOLVED".</summary>
        public string PhaseResolution { get; set; } = string.Empty;
        /// <summary>Aşaması çözülemediği için tablo dışında bırakılan maç sayısı.</summary>
        public int UnresolvedPhaseFixtures { get; set; }
        /// <summary>Tamlık denetiminin yapıldığı an.</summary>
        public DateTime CompletenessCheckedAtUtc { get; set; }

        /// <summary>
        /// true → sıra RESMÎ DEĞİL (ligin gerçek eşitlik bozma kuralı uygulanamadı ve eşit
        /// puanlı takım var). Sayılar doğrudur; UI sırayı "geçici" olarak işaretler.
        /// </summary>
        public bool IsProvisional { get; set; }

        public List<LeagueStandingsRowDto> Rows { get; set; } = new();
    }

    public class LeagueStandingsRowDto
    {
        public int Position { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference { get; set; }
        public int Points { get; set; }

        /// <summary>Son maçlardan G/B/M dizisi (en yeni SONDA), ör. "GGBMG".</summary>
        public string Form { get; set; } = string.Empty;

        /// <summary>Bu satırın sırası eşitlik nedeniyle geçici mi.</summary>
        public bool IsProvisionalPosition { get; set; }
    }
}
