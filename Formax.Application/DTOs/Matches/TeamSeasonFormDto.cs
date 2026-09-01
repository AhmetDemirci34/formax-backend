using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Matches
{
    /// <summary>
    /// BİR TAKIMIN MEVCUT SEZON LİG FORMU — "bu sezon" ifadesinin arkasındaki kanıt.
    ///
    /// Her alan TEK bir maç kümesinden gelir: aynı lig + aynı sezon + maç saatinden önce
    /// oynanmış + Status=Finished. Kullanılan maçların id'leri <see cref="MatchIds"/>
    /// içinde açıkça taşınır; anlatı ile veri arasında denetlenebilir bağ kurulur.
    ///
    /// Eksik maç BAŞKA sezondan TAMAMLANMAZ: takım bu sezon 2 maç oynadıysa Played=2'dir.
    /// </summary>
    public class TeamSeasonFormDto
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        // ── Kapsam (hangi lig, hangi sezon, hangi pencere) ──────────────────────
        public int LeagueId { get; set; }
        public string LeagueName { get; set; } = string.Empty;
        public int SeasonYear { get; set; }
        /// <summary>"2026/27".</summary>
        public string SeasonLabel { get; set; } = string.Empty;
        public DateTime SeasonStartUtc { get; set; }
        /// <summary>Kapsamın üst sınırı — incelenen maçın kickoff'u.</summary>
        public DateTime WindowEndUtc { get; set; }
        public DateTime? FirstMatchUtc { get; set; }
        public DateTime? LastMatchUtc { get; set; }

        // ── Sezon toplamı ───────────────────────────────────────────────────────
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points => Won * 3 + Drawn;

        // ── İç saha / deplasman ayrımı ──────────────────────────────────────────
        public TeamSeasonSplitDto Home { get; set; } = new();
        public TeamSeasonSplitDto Away { get; set; } = new();

        /// <summary>Hesaba giren maçların GERÇEK id'leri (en yeniden eskiye).</summary>
        public List<int> MatchIds { get; set; } = new();

        /// <summary>Kullanılan maç sayısı = <see cref="Played"/>. Ayrı alan: anlatı bunu okur.</summary>
        public int UsedMatchCount => Played;

        /// <summary>3'ten az tamamlanmış maç → örneklem sınırlı; anlatı bunu SÖYLEMEK ZORUNDA.</summary>
        public bool IsLimitedSample => Played > 0 && Played < 3;

        /// <summary>Hiç tamamlanmış lig maçı yok → form cümlesi kurulamaz.</summary>
        public bool HasNoData => Played == 0;

        // ── SEZON VERİ TAMLIĞI (30.08.2026) ─────────────────────────────────────
        /// <summary>Bu ana kadar oynanmış OLMASI GEREKEN lig maçı sayısı (ligin tamamı).</summary>
        public int SeasonExpectedFixtures { get; set; }
        /// <summary>Sonucu hâlâ kesinleşmemiş lig maçı sayısı (ligin tamamı).</summary>
        public int SeasonMissingFixtures { get; set; }
        /// <summary>
        /// false → ligin bu sezonki verisi EKSİK. Genel form değerlendirmesi YAPILMAZ;
        /// eksik veriden başarı/başarısızlık genellemesi çıkarılamaz.
        /// </summary>
        public bool IsSeasonDataComplete { get; set; } = true;

        /// <summary>"Son 5 maç" ifadesi YALNIZ bu true iken kullanılabilir.</summary>
        public bool AllowsLastFivePhrase => Played >= 5 && IsSeasonDataComplete;

        /// <summary>
        /// GENEL FORM DEĞERLENDİRMESİ İZNİ — veri tam VE örneklem yeterli olmalı.
        /// false → "formda/formsuz/bir adım önde" türü hiçbir genelleme kurulamaz.
        /// </summary>
        public bool AllowsGeneralization => IsSeasonDataComplete && Played >= 3;

        /// <summary>
        /// Backend'in yazdığı DETERMİNİSTİK form cümlesi. Model bunu yeniden yazmaz,
        /// olduğu gibi kullanır — sayı yorumlama hatası kalmaz.
        /// </summary>
        public string Sentence { get; set; } = string.Empty;

        /// <summary>G/B/M dizisi (en yeni önce), ör. "G B M".</summary>
        public string ResultSequence { get; set; } = string.Empty;
    }

    /// <summary>İç saha veya deplasman alt toplamı.</summary>
    public class TeamSeasonSplitDto
    {
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
    }
}
