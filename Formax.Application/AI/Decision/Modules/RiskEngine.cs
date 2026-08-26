using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;
using Formax.Application.AI.Signals;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 8 — Risk Engine.
    ///
    /// Motorun kararına eşlik eden riskleri tespit eder: kaos, çelişki, düşük veri, kadro belirsizliği,
    /// sürpriz potansiyeli, breaking gelişme. Her risk şiddet + seviye + açıklama taşır. Gerçek
    /// sinyal/DNA temelli; uydurma risk YOK. Stateless & deterministik.
    /// </summary>
    internal sealed class RiskEngine
    {
        public IReadOnlyList<AiRisk> Assess(UnifiedMatchAiContext ctx, MatchDna dna, SignalField field,
            ContextIntelligence context = null)
        {
            var risks = new List<AiRisk>();

            // ── v2 Derbi riski (gerçek derbi sinyali) ──────────────────────────
            if (context?.Derby?.HasData == true)
            {
                var sev = (int)Math.Clamp(45 + context.Derby.Intensity * 45, 0, 95);
                risks.Add(Risk("Derbi Riski", sev,
                    "Derbi atmosferi: tempo/kart/kırmızı kart ve beraberlik eğilimi yükselir, öngörülebilirlik düşer."));
            }

            // ── v2 Baskı riski (News/Social psikolojik baskı) ──────────────────
            if (context?.Pressure?.HasData == true && context.Pressure.OverallPressure >= 45)
                risks.Add(Risk("Baskı Riski", context.Pressure.OverallPressure,
                    context.Pressure.Summary));

            // ── v2.5 Müsabaka önemi riski (eleme/kritik → tek maç, öngörülemezlik artar) ──
            if (context?.Competition?.HasData == true && context.Competition.StakeLevel >= 0.5)
            {
                var sev = (int)Math.Clamp(40 + context.Competition.StakeLevel * 50, 0, 95);
                risks.Add(Risk("Müsabaka Önemi", sev,
                    $"{context.Competition.Summary} Yüksek stake tek maçta temkin/öngörülemezlik artırır."));
            }

            // ── Kaos riski (DNA) ───────────────────────────────────────────────
            if (dna.ChaosRisk.Score >= 50)
                risks.Add(Risk("Kaos Riski", dna.ChaosRisk.Score,
                    "Maçın seyri belirsiz; gol dağılımı ve tempo öngörülemez olabilir."));

            // ── Çelişki riski ──────────────────────────────────────────────────
            var q = ctx.Quality ?? new UnifiedContextQuality();
            var unresolved = q.ConflictSummary != null && q.ConflictSummary.TryGetValue(
                SignalConflictStatus.Unresolved.ToString(), out var u) ? u : 0;
            if (unresolved > 0)
                risks.Add(Risk("Çelişki Riski", Math.Min(90, 40 + unresolved * 20),
                    $"{unresolved} çelişkili sinyal otorite kaynakla çözülemedi; yön güveni azaldı."));

            // ── Düşük veri riski ───────────────────────────────────────────────
            var dq = q.ActiveSignalCount > 0 ? q.OverallDataQuality : ctx.DataQuality;
            if (q.ActiveSignalCount < 5 || dq < 0.5)
                risks.Add(Risk("Düşük Veri Riski", (int)Math.Clamp((1 - dq) * 100, 30, 90),
                    $"Yalnız {q.ActiveSignalCount} aktif sinyal / veri kalitesi {(int)(dq * 100)}; kapsam sınırlı."));

            // ── Kadro belirsizliği riski ───────────────────────────────────────
            if (ctx.Availability.HasData)
            {
                var totalAbs = ctx.Availability.HomeKeyAbsences + ctx.Availability.AwayKeyAbsences;
                if (totalAbs > 0)
                    risks.Add(Risk("Kadro Riski", Math.Min(85, 30 + totalAbs * 10),
                        $"Kesin eksikler — Ev {ctx.Availability.HomeKeyAbsences} / Dep {ctx.Availability.AwayKeyAbsences}; güç dengesi kayabilir."));
            }

            // ── Sürpriz riski (DNA) ────────────────────────────────────────────
            if (dna.SurprisePotential.Score >= 55)
                risks.Add(Risk("Sürpriz Riski", dna.SurprisePotential.Score,
                    "Yapısal favori ile sinyaller/sağlayıcı ayrışıyor; beklenmedik sonuç payı yüksek."));

            // ── Breaking gelişme riski ─────────────────────────────────────────
            var breaking = (ctx.News.HasData && ctx.News.HasBreakingNews)
                        || (ctx.Social.HasData && ctx.Social.BreakingOfficialNews);
            if (breaking)
                risks.Add(Risk("Breaking Gelişme", 60,
                    "Son saatlerde resmi/haber kaynaklı gelişme var; tablo hızla değişebilir."));

            // Hiç risk yoksa dürüst "düşük risk" bilgisi.
            if (risks.Count == 0)
                risks.Add(Risk("Düşük Risk", 20, "Belirgin bir belirsizlik/çelişki tespit edilmedi."));

            return risks;
        }

        private static AiRisk Risk(string type, int severity, string desc)
        {
            var s = Math.Clamp(severity, 0, 100);
            return new AiRisk
            {
                Type = type,
                Severity = s,
                Level = s >= 66 ? "YÜKSEK" : s >= 40 ? "ORTA" : "DÜŞÜK",
                Description = desc
            };
        }
    }
}
