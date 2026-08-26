using System.Collections.Generic;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// v4 — EDITORIAL INTELLIGENCE. MarketProbabilityEngine'in olasılık DIŞI, editoryal çıktısı.
    /// TÜM alanlar YALNIZ UnifiedMatchAiContext'ten (mevcut GDP sinyalleri) türetilir; yeni provider/
    /// entity/migration YOK, olasılık matematiği DEĞİŞMEZ. Her bölüm HasData-gated: veri yoksa BOŞ kalır
    /// (uydurma YOK). LLM bu paketi yalnız doğal Türkçeye çevirir; yeni bilgi/analiz eklemez.
    ///
    /// Bağlam single-match/agregat düzeyinde olduğundan isimli oyuncu / fikstür takvimi gibi bilgiler
    /// context'te yoksa ilgili bölüm dürüstçe boş bırakılır (FORMAX kuralı: veri yoksa yazma).
    /// </summary>
    public sealed class EditorialIntelligence
    {
        /// <summary>1. Maçın bağlamı (lig/kupa/eleme/derbi/şampiyonluk-küme yarışı/sezon fazı).</summary>
        public IReadOnlyList<string> MatchContext { get; init; } = new List<string>();

        /// <summary>2. Takım zekâsı — ev sahibi (form/iç-deplasman/hücum-savunma/gol üretimi).</summary>
        public TeamEditorial HomeTeam { get; init; } = new();
        /// <summary>2. Takım zekâsı — deplasman.</summary>
        public TeamEditorial AwayTeam { get; init; } = new();

        /// <summary>3. Kadro zekâsı (eksik/sakat/cezalı sayısı, kadro derinliği). İsimli oyuncu context'te yoksa boş.</summary>
        public IReadOnlyList<string> Squad { get; init; } = new List<string>();

        /// <summary>4. Fikstür zekâsı (rövanş/çift maç/sezon fazı). Takvim yoğunluğu context'te yoksa sınırlı.</summary>
        public IReadOnlyList<string> Fixture { get; init; } = new List<string>();

        /// <summary>5. Teknik direktör zekâsı (haber/açıklama/durum). İsim/derinlik context kadar.</summary>
        public IReadOnlyList<string> Coach { get; init; } = new List<string>();

        /// <summary>6. Haber zekâsı — maçı etkileyen haberler önem sırasıyla ÖZETLENİR (listelenmez).</summary>
        public IReadOnlyList<string> News { get; init; } = new List<string>();

        /// <summary>7. Transfer zekâsı (gelen/giden sayısı + transfer haberi etkisi).</summary>
        public IReadOnlyList<string> Transfers { get; init; } = new List<string>();

        /// <summary>8. Psikolojik zekâ (News+Social+Context+Importance → baskı yorumu).</summary>
        public IReadOnlyList<string> Psychology { get; init; } = new List<string>();

        /// <summary>9. Taktik zekâ (hücum/savunma indeksi + maç karakteri). Veri yoksa yazılmaz.</summary>
        public IReadOnlyList<string> Tactical { get; init; } = new List<string>();

        /// <summary>10. Kritik oyuncular — isimli oyuncu verisi context'te yoksa yalnız eksik-etkisi.</summary>
        public IReadOnlyList<string> KeyPlayers { get; init; } = new List<string>();

        /// <summary>11. Gizli zekâ — ilişkili sinyallerin birleşimi (Interactions/çelişki/sürpriz).</summary>
        public IReadOnlyList<string> Hidden { get; init; } = new List<string>();

        /// <summary>12. FORMAX editoryal görüşü — sentez (en kritik konu/neden izle/avantaj/risk/çevirici).</summary>
        public EditorialVerdict Verdict { get; init; } = new();

        /// <summary>Kaç editoryal bölümün gerçek veriyle dolduğunun sayısı (dürüstlük/derinlik göstergesi).</summary>
        public int CoverageDepth { get; init; }
        /// <summary>Editoryal katmanın gerçek veriyle desteklendiği (≥3 bölüm) — düşükse LLM temkinli/kısa konuşur.</summary>
        public bool HasData { get; init; }
    }

    /// <summary>Tek takımın editoryal analizi — yalnız gerçek istatistik/sıralama sinyallerinden.</summary>
    public sealed class TeamEditorial
    {
        public string TeamName { get; init; } = "";
        public bool HasData { get; init; }
        public IReadOnlyList<string> Points { get; init; } = new List<string>();
    }

    /// <summary>FORMAX'ın editoryal görüşü — bütün analizlerin sentezi (motorun kendi futbol yorumu).</summary>
    public sealed class EditorialVerdict
    {
        public bool HasData { get; init; }
        /// <summary>Bu maçın en kritik konusu.</summary>
        public string CriticalTopic { get; init; } = "";
        /// <summary>Kullanıcı neden bu maçı izlemeli / araştırmalı.</summary>
        public string WhyWatch { get; init; } = "";
        /// <summary>En büyük avantaj kimde ve neden.</summary>
        public string BiggestAdvantage { get; init; } = "";
        /// <summary>En büyük risk / belirsizlik.</summary>
        public string BiggestRisk { get; init; } = "";
        /// <summary>Maçın kaderini ne değiştirebilir (tek cümle).</summary>
        public string WhatCouldChange { get; init; } = "";
    }
}
