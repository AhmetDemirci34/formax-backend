using System;
using System.Linq;
using Formax.Application.AI.Context;
using Formax.Application.AI.Signals;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 7 — Confidence Engine.
    ///
    /// Kararın GENEL güvenini üretir. Unified AI Context kalitesinden (aktif sinyal güveni, veri
    /// kalitesi) + sinyal uzlaşısından (düşük belirsizlik) + çelişki cezasından deterministik
    /// türetir. Veri zayıfsa dürüstçe düşük güven döner. Stateless.
    /// </summary>
    internal sealed class ConfidenceEngine
    {
        public DecisionConfidence Assess(UnifiedMatchAiContext ctx, SignalField field)
        {
            var q = ctx.Quality ?? new UnifiedContextQuality();
            var active = q.ActiveSignalCount;

            // Bileşenler (0..1) — v1.1: Quality'nin TÜM boyutları (SourceTrust/Evidence/Freshness) dahil.
            var signalConfidence = q.OverallConfidence / 100.0;
            var dataQuality = active > 0 ? q.OverallDataQuality : ctx.DataQuality;
            var agreement = 1.0 - field.Instability;
            var sourceTrust = active > 0 ? q.OverallSourceTrust / 100.0 : 0.0;
            var evidence = active > 0 ? q.OverallEvidenceScore / 100.0 : 0.0;
            // Tazelik faktörü (yalnız aktif veri varsa; yoksa nötr → mevcut davranışı bozmaz).
            var freshness = active > 0 && q.OverallFreshness > 0 ? Math.Clamp(q.OverallFreshness, 0.5, 1.0) : 1.0;

            // Çelişki cezası: çözülemeyen çelişkiler güveni düşürür.
            var unresolved = q.ConflictSummary != null && q.ConflictSummary.TryGetValue(
                SignalConflictStatus.Unresolved.ToString(), out var u) ? u : 0;
            var conflictPenalty = Math.Min(0.20, unresolved * 0.07);

            // Bilinmeyen faktör cezası: karar-kritik bloklardan verisi olmayanlar güveni kısar (dürüst).
            var unknown = UnknownKeyBlocks(ctx);
            var unknownPenalty = Math.Min(0.15, unknown * 0.03);

            // Az sinyal → güven tavanı düşer (dürüst).
            var coverage = Math.Clamp(active / 8.0, 0.3, 1.0);

            // Ağırlıklı harman: sinyal güveni + veri kalitesi + uzlaşı + kaynak güveni + kanıt.
            var blend = signalConfidence * 0.42 + dataQuality * 0.20 + agreement * 0.18
                      + sourceTrust * 0.10 + evidence * 0.10;
            var score01 = blend * coverage * freshness - conflictPenalty - unknownPenalty;
            var score = (int)Math.Clamp(Math.Round(score01 * 100), 0, 99);

            var level = score >= 68 ? "YÜKSEK" : score >= 50 ? "ORTA" : "DÜŞÜK";
            var basis = $"{active} aktif sinyal, veri kalitesi {(int)(dataQuality * 100)}, uzlaşı {(int)(agreement * 100)}, "
                      + $"kaynak güveni {(int)(sourceTrust * 100)}, kanıt {(int)(evidence * 100)}, tazelik {(int)(freshness * 100)}"
                      + (unresolved > 0 ? $", {unresolved} çözülemeyen çelişki" : "")
                      + (unknown > 0 ? $", {unknown} bilinmeyen blok" : "") + ".";

            return new DecisionConfidence { Score = score, Level = level, Basis = basis };
        }

        /// <summary>Karar-kritik bloklardan (Availability/Standings/News/TeamStats/Competition) verisi olmayanların sayısı.</summary>
        private static int UnknownKeyBlocks(UnifiedMatchAiContext ctx)
        {
            var n = 0;
            if (!ctx.Availability.HasData) n++;
            if (!ctx.Standings.HasData) n++;
            if (!ctx.News.HasData) n++;
            if (!ctx.TeamStats.HasData) n++;
            if (!ctx.Competition.HasData) n++;
            return n;
        }
    }
}
