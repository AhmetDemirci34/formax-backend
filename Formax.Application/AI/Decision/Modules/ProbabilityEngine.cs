using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 5 — Probability Engine.
    ///
    /// TÜM AI olasılıklarını TEK deterministik Poisson gol matrisinden türetir (1X2, çifte şans,
    /// alt/üst, KG, ilk yarı, ilk gol, toplam-gol aralığı, skor aralığı). En az 13 market; her biri
    /// Probability + Confidence + Reason. Sabit tablo YOK — beklenen goller sinyal alanıyla
    /// değiştikçe olasılıklar dinamik yeniden hesaplanır. Stateless & deterministik.
    /// </summary>
    internal sealed class ProbabilityEngine
    {
        public IReadOnlyList<AiProbability> Compute(
            UnifiedMatchAiContext ctx, PoissonGoalModel model, SignalField field, int baseConfidence)
        {
            var list = new List<AiProbability>();
            var expHome = model.ExpHome;
            var expAway = model.ExpAway;
            var edgeTxt = field.NetHomeEdge >= 0 ? ctx.HomeName : ctx.AwayName;
            var goalsTxt = $"beklenen gol Ev {expHome:0.0} / Dep {expAway:0.0}";

            // ── 1X2 ────────────────────────────────────────────────────────────
            var pHome = model.PHomeWin();
            var pDraw = model.PDraw();
            var pAway = model.PAwayWin();
            Add(list, "Ev Sahibi Kazanır", pHome, baseConfidence, "Outcome",
                $"{goalsTxt}; güç dengesi {(field.NetHomeEdge >= 0 ? "ev" : "deplasman")} yönünde.");
            Add(list, "Beraberlik", pDraw, baseConfidence, "Outcome",
                $"Dengeli gol beklentisi ({goalsTxt}).");
            Add(list, "Deplasman Kazanır", pAway, baseConfidence, "Outcome",
                $"{goalsTxt}; güç dengesi {(field.NetHomeEdge >= 0 ? "ev" : "deplasman")} yönünde.");

            // ── Çifte Şans (favori yönünde) ────────────────────────────────────
            if (field.NetHomeEdge >= 0)
                Add(list, "Çifte Şans (1X)", pHome + pDraw, baseConfidence, "Outcome",
                    $"{ctx.HomeName} kaybetmez — beraberlik payı dahil.");
            else
                Add(list, "Çifte Şans (X2)", pAway + pDraw, baseConfidence, "Outcome",
                    $"{ctx.AwayName} kaybetmez — beraberlik payı dahil.");

            // ── Toplam gol 2.5 alt/üst ─────────────────────────────────────────
            var over25 = model.POver(2.5);
            Add(list, "2.5 Üst", over25, baseConfidence, "Totals",
                over25 >= 0.5 ? "Yüksek toplam gol beklentisi." : "Gol beklentisi 2.5 sınırına yakın.");
            Add(list, "2.5 Alt", 1.0 - over25, baseConfidence, "Totals",
                over25 < 0.5 ? "Kontrollü/düşük gol beklentisi." : "Gol beklentisi 2.5 sınırına yakın.");

            // ── Karşılıklı gol ─────────────────────────────────────────────────
            var btts = model.PBtts();
            Add(list, "Karşılıklı Gol Var", btts, baseConfidence, "Btts",
                btts >= 0.5 ? "İki taraf da gol yolunda üretken." : "En az bir takım gol bulmakta zorlanabilir.");
            Add(list, "Karşılıklı Gol Yok", 1.0 - btts, baseConfidence, "Btts",
                btts < 0.5 ? "En az bir takımın gol bulması zor." : "Karşılıklı gol ihtimali dengeli.");

            // ── İlk yarı sonucu (yarı Poisson: ilk-yarı beklenen gol ≈ toplam ×0.42) ──
            // Aynı deterministik yarı-Poisson'dan TAM İY 1X2 + İY gol türetilir (yeni veri YOK).
            var halfModel = new PoissonGoalModel(expHome * 0.42, expAway * 0.42);
            var htH = expHome * 0.42;
            var htA = expAway * 0.42;
            Add(list, "İlk Yarı Ev Sahibi Önde", halfModel.PHomeWin(), baseConfidence, "Half",
                $"İlk yarı beklenen gol Ev {htH:0.0} / Dep {htA:0.0}; ev sahibi devreyi önde kapatma payı.");
            Add(list, "İlk Yarı Beraberlik", halfModel.PDraw(), baseConfidence, "Half",
                "İlk yarı düşük gol beklentisi berabere payını yükseltir.");
            Add(list, "İlk Yarı Deplasman Önde", halfModel.PAwayWin(), baseConfidence, "Half",
                $"İlk yarı beklenen gol Dep {htA:0.0} / Ev {htH:0.0}; deplasman devreyi önde kapatma payı.");

            // ── İLK YARI GOL MARKETİ ÜRETİLMEZ (ürün kararı, 18.08.2026) ───────
            // "İlk Yarı Gol Var" (İY toplam ≥ 1) burada üretiliyordu ve Keşfet'in
            // "AI Olası Sonuçlar" kartlarına kadar gidiyordu. Ürün kararıyla ilk yarı
            // GOL ailesi kullanıcıya gösterilmiyor. Frontend'de gizlenmiyor — KAYNAKTA
            // üretilmiyor, böylece hiçbir DTO/uç bu marketi taşımıyor.
            //
            // İlk yarı SONUCU marketleri (Ev Sahibi Önde / Beraberlik / Deplasman Önde)
            // gol marketi DEĞİLDİR ve yukarıda korunmuştur.

            // ── İlk gol ────────────────────────────────────────────────────────
            Add(list, "İlk Gol Ev Sahibi", model.PFirstGoalHome(), baseConfidence, "FirstGoal",
                $"{ctx.HomeName} gol payı yüksek ({expHome:0.0} beklenen gol).");
            Add(list, "İlk Gol Deplasman", model.PFirstGoalAway(), baseConfidence, "FirstGoal",
                $"{ctx.AwayName} gol payı ({expAway:0.0} beklenen gol).");

            // ── Toplam gol aralığı (en olası band) ─────────────────────────────
            var band = model.MostLikelyTotalBand();
            Add(list, $"Toplam Gol: {band.label}", band.prob, baseConfidence, "TotalRange",
                $"Poisson dağılımının en olası toplam-gol bandı ({band.label}).");

            // ── Skor aralığı (en olası tam skor) ───────────────────────────────
            var sc = model.MostLikelyScore();
            Add(list, $"Skor Aralığı: {sc.h}-{sc.a} civarı", sc.prob, baseConfidence, "Score",
                $"En olası tam skor {sc.h}-{sc.a} ({(int)Math.Round(sc.prob * 100)}%).");

            return list;
        }

        private static void Add(List<AiProbability> list, string market, double p,
            int baseConfidence, string family, string reason)
        {
            var prob = (int)Math.Round(Math.Clamp(p * 100, 1, 99));
            list.Add(new AiProbability
            {
                Market = market,
                Probability = prob,
                Confidence = MarketConfidence(baseConfidence, p),
                Reason = reason,
                Family = family
            });
        }

        /// <summary>
        /// Market güveni = genel güven × kararlılık faktörü. %50'ye yakın (belirsiz) marketler
        /// bir tık kısılır; uç (kararlı) marketler yükseltilir. Deterministik.
        /// </summary>
        private static string MarketConfidence(int baseConfidence, double p)
        {
            var decisiveness = 2.0 * Math.Abs(p - 0.5);           // 0 (belirsiz) .. 1 (kesin)
            var score = baseConfidence * (0.85 + 0.30 * decisiveness);
            var s = (int)Math.Clamp(score, 0, 99);
            return s >= 68 ? "YÜKSEK" : s >= 50 ? "ORTA" : "DÜŞÜK";
        }
    }
}
