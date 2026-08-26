using System.Collections.Generic;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// FORMAX AI Brain vNext — MATCH READING (tek futbol zekâsının anlatı sentezi).
    ///
    /// Editorial + Story + Football'ın AYRI çalıştığı üç yorum katmanının yerini alır: her ham blok
    /// TEK kez okunur (MatchReadingEngine), tek gerçek üretilir. Sıralı editoryal okuma:
    /// bağlam → takımlar → kadro → oyuncular → haber → taktik → psikoloji → kırılma → FORMAX görüşü.
    ///
    /// Kurallar: YALNIZ UnifiedMatchAiContext + zaten hesaplanmış bileşenlerden (Football/DNA/Context/
    /// Importance...) türetilir. Olasılık/gol modeline DOKUNMAZ (hash sabit). Her bölüm HasData-gated:
    /// gerçek veri yoksa BOŞ (uydurma YOK). LLM YALNIZ bunu okuyup doğal Türkçeye çevirir.
    ///
    /// <see cref="EditorialIntelligence"/> ve <see cref="MatchStory"/> bu okumadan PROJEKSİYON olarak
    /// türetilir (geriye uyumluluk); ayrı hesaplama yoktur (tek okuma / tek analiz / tek sentez).
    /// </summary>
    public sealed class MatchReading
    {
        public bool HasData { get; init; }
        /// <summary>Gerçek veriyle dolan bölüm sayısı (dürüstlük/derinlik göstergesi).</summary>
        public int CoverageDepth { get; init; }

        /// <summary>1. Maçın bağlamı (lig/kupa/eleme/derbi/şampiyonluk-küme yarışı/sezon fazı).</summary>
        public IReadOnlyList<string> Context { get; init; } = new List<string>();

        /// <summary>2. Takım okuması — ev (form/iç-deplasman/hücum-savunma/gol üretimi).</summary>
        public TeamEditorial HomeTeam { get; init; } = new();
        /// <summary>2. Takım okuması — deplasman.</summary>
        public TeamEditorial AwayTeam { get; init; } = new();

        /// <summary>3. Kadro okuması (eksik/sakat/cezalı, kadro derinliği).</summary>
        public IReadOnlyList<string> Squad { get; init; } = new List<string>();
        /// <summary>4. Fikstür okuması (dinlenme/yoğunluk/rotasyon + rövanş/çift maç).</summary>
        public IReadOnlyList<string> Fixture { get; init; } = new List<string>();
        /// <summary>5. Teknik direktör okuması.</summary>
        public IReadOnlyList<string> Coach { get; init; } = new List<string>();
        /// <summary>6. Kritik oyuncular — isimli GERÇEK oyuncu (Football'dan; tek okuma).</summary>
        public IReadOnlyList<string> KeyPlayers { get; init; } = new List<string>();
        /// <summary>7. Haber okuması — haberin futbol ETKİSİ (implikasyon; Football'dan; ham haber DEĞİL).</summary>
        public IReadOnlyList<string> News { get; init; } = new List<string>();
        /// <summary>8. Transfer okuması.</summary>
        public IReadOnlyList<string> Transfers { get; init; } = new List<string>();
        /// <summary>9. Taktik okuması (YALNIZ DNA + güç-index + gerçek gol/form; xG/possession maç öncesi YOK).</summary>
        public IReadOnlyList<string> Tactical { get; init; } = new List<string>();
        /// <summary>10. Psikoloji okuması (baskı/önem/gündem).</summary>
        public IReadOnlyList<string> Psychology { get; init; } = new List<string>();
        /// <summary>11. Gizli okuma — ilişkili sinyal birleşimi (Interactions/çelişki/sürpriz).</summary>
        public IReadOnlyList<string> Hidden { get; init; } = new List<string>();
        /// <summary>12. Çapraz sentez — çok-faktörlü neden→sonuç çıkarımları (Football Synthesis).</summary>
        public IReadOnlyList<string> Synthesis { get; init; } = new List<string>();

        // ── Hikâye eksenleri (Story projeksiyonu buradan üretilir) ──
        /// <summary>Maçın ANA hikâyesi (en kritik konu).</summary>
        public string MainStory { get; init; } = "";
        /// <summary>YAN hikâye (ikincil belirleyici tema).</summary>
        public string SubStory { get; init; } = "";
        /// <summary>Kırılma noktası — maçın seyrini değiştirebilecek unsur.</summary>
        public string TurningPoint { get; init; } = "";
        /// <summary>En büyük avantaj (gerekçeli).</summary>
        public string BiggestAdvantage { get; init; } = "";
        /// <summary>En büyük risk / belirsizlik.</summary>
        public string BiggestRisk { get; init; } = "";
        /// <summary>Sürpriz ihtimali (kesinlik DEĞİL; gerekçeli).</summary>
        public string SurprisePotential { get; init; } = "";
        /// <summary>FORMAX'ın editoryal görüşü (neden izlenmeli / genel değerlendirme).</summary>
        public string FormaxView { get; init; } = "";

        /// <summary>13. FORMAX görüşü — sentez (en kritik konu/neden izle/avantaj/risk/çevirici).</summary>
        public EditorialVerdict Verdict { get; init; } = new();

        /// <summary>Canlı okuma — YALNIZ in-play (LiveState.HasData). Gerçek skor/dakika/xG/şut/possession/
        /// momentum. Maç öncesi BOŞ (tahmini canlı veri ASLA). </summary>
        public IReadOnlyList<string> Live { get; init; } = new List<string>();

        // ══════════════════════════════════════════════════════════════════════════════
        // vNext — LLM'in OKUYACAĞI 11 ADLI STORY BLOĞU (GÖREV 10). Her blok neden→sonuç anlatısı;
        // istatistik listesi DEĞİL. Yukarıdaki alanlar bu blokların ham/projeksiyon kaynağıdır.
        // LLM YALNIZ bu blokları doğal Türkçeye çevirir; yeni bilgi eklemez. Veri yoksa blok boş.
        // ══════════════════════════════════════════════════════════════════════════════

        /// <summary>Maçın hikâyesi — ana + yan tema (en kritik konu + ikincil belirleyici).</summary>
        public IReadOnlyList<string> MatchStoryLines { get; init; } = new List<string>();
        /// <summary>Takım hikâyesi — form/karakter + karşılaştırma + geçmiş (H2H) okuması.</summary>
        public IReadOnlyList<string> TeamStory { get; init; } = new List<string>();
        /// <summary>Oyuncu hikâyesi — bireysel istatistik DEĞİL, oyuncunun maça ETKİSİ (neden→sonuç).</summary>
        public IReadOnlyList<string> PlayerStory { get; init; } = new List<string>();
        /// <summary>Kadro hikâyesi — eksik/derinlik/rotasyon etkisi.</summary>
        public IReadOnlyList<string> SquadStory { get; init; } = new List<string>();
        /// <summary>Taktik hikâyesi — YALNIZ DNA + güç-index + gerçek gol/form (maç öncesi xG/possession YOK).</summary>
        public IReadOnlyList<string> TacticalStory { get; init; } = new List<string>();
        /// <summary>Zaman çizgisi hikâyesi — rotasyon/yorgunluk/öncelik/risk/önem sırası.</summary>
        public IReadOnlyList<string> TimelineStory { get; init; } = new List<string>();
        /// <summary>Müsabaka hikâyesi — neden önemli / kaybedilirse-kazanılırsa / lig-Avrupa-küme / rövanş / derbi.</summary>
        public IReadOnlyList<string> CompetitionStory { get; init; } = new List<string>();
        /// <summary>Haber hikâyesi — haberin futbol ETKİSİ (kaynak dili YASAK).</summary>
        public IReadOnlyList<string> NewsStory { get; init; } = new List<string>();
        /// <summary>Psikoloji hikâyesi — baskı/önem/gündem → saha psikolojisi.</summary>
        public IReadOnlyList<string> PsychologyStory { get; init; } = new List<string>();
        /// <summary>Gizli hikâye — çapraz sinyal birleşimi (Synthesis + Interactions/çelişki/sürpriz).</summary>
        public IReadOnlyList<string> HiddenStory { get; init; } = new List<string>();
        /// <summary>FORMAX görüşü — sentez (en kritik konu / neden izle / avantaj / risk / çevirici).</summary>
        public IReadOnlyList<string> FormaxOpinion { get; init; } = new List<string>();
    }
}
