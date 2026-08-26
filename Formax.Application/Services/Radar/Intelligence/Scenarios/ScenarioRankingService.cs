using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Radar.Intelligence.Scenarios
{
    /// <summary>
    /// FORMAX Radar v2.2 — Dynamic Scenario Ranking.
    ///
    /// MarketProbabilityEngine'in ürettiği geniş havuzu puanlar ve:
    ///   1) her market AİLESİNDEN yalnız en güçlü adayı tutar
    ///      → çelişen senaryolar (2.5 Üst vs 2.5 Alt) ve tekrar eden marketler elenir,
    ///   2) kalan aile-temsilcilerini SİNYAL gücüne göre sıralar,
    ///   3) en güçlü ilk N senaryoyu döndürür.
    ///
    /// SİNYAL = maça-özgü sapma (Probability − marketin nötr baseline'ı) × anlamlılık ağırlığı.
    /// Neden ham olasılık DEĞİL: "0.5 Üst" (~%93) veya "3.5 Alt" (~%80) veya güçlü favoride
    /// "Kaybetmez" gibi marketler HER maçta yüksektir; ham olasılıkla sıralayınca top-3 hep
    /// aynı "güvenli" marketlere düşer. Baseline'dan sapma ise maçın GERÇEKTEN ayrıştığı
    /// (bu maça özel) senaryoları öne çıkarır → her maç farklı, anlamlı bir top-3 üretir.
    /// (Küçük bir olasılık terimi, çok düşük olasılıklı ama sapmalı seçimleri dengeler.)
    /// </summary>
    public sealed class ScenarioRankingService
    {
        // Sıralama saf sapmaya (edge) dayanır; olasılık yalnızca eşitlik bozucudur.
        // (Nudge=0: düşük-sinyalli maçlarda yapısal-yüksek marketlerin haksız avantajı kalkar.)
        private const double ProbabilityNudge = 0.0;

        public IReadOnlyList<ScenarioCandidate> RankTop(
            IReadOnlyList<ScenarioCandidate> candidates, int take = 3)
        {
            if (candidates == null || candidates.Count == 0)
                return new List<ScenarioCandidate>();

            // 1) Aile başına en yüksek SİNYALLİ tek aday (çelişki + tekrar filtresi).
            //    Ham olasılık yerine sinyal → aile içinden maça özgü ayrışan taraf seçilir
            //    (ör. Totals ailesinde her maçta "3.5 Alt" değil, bu maçın ayrıştığı çizgi).
            var perFamilyBest = candidates
                .GroupBy(c => c.Family)
                .Select(g => g.OrderByDescending(Signal).ThenByDescending(c => c.Probability).First());

            // 2-3) Sinyale göre sırala, ilk N. Deterministik tie-break: olasılık.
            return perFamilyBest
                .OrderByDescending(Signal)
                .ThenByDescending(c => c.Probability)
                .Take(take)
                .ToList();
        }

        // Sinyal = (olasılık − baseline) × ağırlık + küçük olasılık dürtüsü.
        private static double Signal(ScenarioCandidate c)
        {
            var weight = c.Weight <= 0 ? 1.0 : c.Weight;
            var edge = c.Probability - Baseline(c);
            return edge * weight + c.Probability * ProbabilityNudge;
        }

        // Baseline = MarketProbabilityEngine'in SİNYALSİZ (nötr güç, ortalama gol beklentisi)
        // ürettiği denge çıktısı. Sapma bunun üzerinden ölçülür → bir market ancak GERÇEK
        // maç verisi onu bu dengenin ötesine ittiğinde öne çıkar. Böylece motorun veri-yokken
        // ürettiği "varsayılan yüksek" marketler (Gol Atamaz~70, KG Yok~80, İY 1.5 Alt~66)
        // otomatik nötrlenir; hiçbir sabit market listesi yoktur, yalnız istatistiksel denge.
        private static double Baseline(ScenarioCandidate c)
        {
            var m = c.Market.ToLowerInvariant();
            var ust = m.Contains("üst") || m.Contains("ust");
            var alt = m.Contains("alt");
            // NOT: "İlk" → ToLowerInvariant → "i̇lk" (İ, düz 'i' değil combining-dot'lu 'i̇')
            // olduğundan "ilk yarı" ile eşleşmez. İ içermeyen "yarı"/"yari" üzerinden tespit et.
            var half = m.Contains("yarı") || m.Contains("yari");

            // ── İlk yarı marketleri (yapısal yüksek → tipik-yüksek baseline ile sönümlenir) ──
            if (half)
            {
                // NOT: İlk yarı GOL marketleri (0.5 Üst / 1.5 Alt) artık ÜRETİLMİYOR
                // (bkz. MarketProbabilityEngine) — bu ailenin baseline satırları kaldırıldı.
                if (m.Contains("beraber")) return 44;
                if (m.Contains("ev sahibi")) return 30;
                if (m.Contains("deplasman")) return 28;
                return 44;
            }

            // ── Toplam gol çizgileri (tipik) ──
            if (m.Contains("0.5")) return ust ? 90 : 10;
            if (m.Contains("1.5")) return ust ? 76 : 24;
            if (m.Contains("2.5")) return ust ? 42 : 58;
            if (m.Contains("3.5")) return ust ? 22 : 78;

            // ── KG (tipik; düşük-skor liglerde KG Yok yapısal yüksek → dengeli baseline) ──
            if (m.Contains("kg var")) return 48;
            if (m.Contains("kg yok")) return 52;

            // ── Takım gol (bimodal: takım ya golcü ya değil → İKİ tarafı da yüksek baseline'la
            //    sönümle, yalnız uç değerler öne çıksın; aksi halde bu aile her maçı domine eder) ──
            if (m.Contains("gol atamaz")) return 58;
            if (m.Contains("gol atar")) return 78;

            // ── Maç sonucu / çifte şans (tipik güç dengesi) ──
            if (m.Contains("kaybetmez")) return 66;
            if (m.Contains("çifte") || m.Contains("cifte")) return 72;
            if (m.Contains("beraber")) return 28;
            if (m.Contains("ev sahibi")) return 40;
            if (m.Contains("deplasman")) return 35;

            return 50; // bilinmeyen market → nötr
        }
    }
}
