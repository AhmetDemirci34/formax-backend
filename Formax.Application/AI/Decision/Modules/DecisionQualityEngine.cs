using System;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v1.1 MODÜL — Decision Quality Engine (yalnız engine-içi metrik).
    ///
    /// Her AI Decision Package için 0-100 DecisionQualityScore üretir. Kullanıcıya gösterilmez; motorun
    /// KENDİ kararının ne kadar sağlam olduğunu ölçer. Bileşenler (hepsi mevcut çıktı/context'ten,
    /// uydurma YOK): Reasoning (gerekçe üretildi mi), Confidence (genel güven), Evidence (kaynak/kanıt
    /// gücü), Consistency (iç tutarlılık), Coverage (aktif sinyal + veri kalitesi). Deterministik.
    /// </summary>
    internal sealed class DecisionQualityEngine
    {
        public int Score(DecisionConfidence confidence, DecisionExplainability explain,
            UnifiedMatchAiContext ctx, DecisionConsistency consistency)
        {
            var q = ctx.Quality ?? new UnifiedContextQuality();

            // Reasoning: gerekçe zinciri zenginliği (0..1) — 3+ cümle tam.
            var reasoning = Math.Clamp((explain?.Reasoning?.Count ?? 0) / 3.0, 0.2, 1.0);

            // Confidence (0..1).
            var conf = Math.Clamp(confidence.Score / 100.0, 0, 1);

            // Evidence: kanıt gücü + kaynak güveni (aktif sinyal varsa gerçek; yoksa düşük).
            var evidence = q.ActiveSignalCount > 0
                ? Math.Clamp(Math.Max(q.OverallEvidenceScore, q.OverallSourceTrust) / 100.0, 0, 1)
                : 0.2;

            // Consistency (0..1).
            var consistency01 = Math.Clamp(consistency.Score / 100.0, 0, 1);

            // Coverage: aktif sinyal kapsamı × veri kalitesi.
            var breadth = Math.Clamp(q.ActiveSignalCount / 8.0, 0, 1);
            var dq = q.ActiveSignalCount > 0 ? q.OverallDataQuality : ctx.DataQuality;
            var coverage = Math.Clamp(breadth * 0.6 + dq * 0.4, 0, 1);

            var score = reasoning * 0.15 + conf * 0.25 + evidence * 0.20
                      + consistency01 * 0.20 + coverage * 0.20;

            return (int)Math.Clamp(Math.Round(score * 100), 0, 100);
        }
    }
}
