using System;
using System.Collections.Generic;
using Formax.Application.DTOs.Admin;

namespace Formax.Application.Services.AdminDashboard
{
    /// <summary>
    /// AI davranış metriklerinden olası anomali ve riskleri tespit eder.
    /// Controller / DB / Side-effect yoktur.
    /// Sadece okur ve risk flag üretir.
    /// </summary>
    public class AdminAiAnomalyDetector
    {
        public List<AdminAiRiskFlagDto> Detect(AdminAiTimelineDto timeline)
        {
            var risks = new List<AdminAiRiskFlagDto>();

            // ---- Guard
            if (timeline == null)
                return risks;

            // -------------------------------
            // 1) AŞIRI KONUŞMA (Extended dominance)
            // -------------------------------
            var last30 = timeline.Last30Days;
            if (last30.Total > 0)
            {
                var extendedRatio = (double)last30.Extended / last30.Total;

                if (extendedRatio >= 0.85)
                {
                    risks.Add(new AdminAiRiskFlagDto
                    {
                        Code = "OverConfidenceRisk",
                        Severity = AdminAiRiskSeverity.Medium,
                        Message = "AI kararlarının çok büyük kısmı genişletilmiş analiz üretmiş.",
                        Evidence = $"Extended oranı: %{Math.Round(extendedRatio * 100)} (30 gün)"
                    });
                }
            }

            // -------------------------------
            // 2) SESSİZLİKTEN KAÇINMA (Silent yok denecek kadar az)
            // -------------------------------
            if (last30.Total > 0 && last30.Silent == 0)
            {
                risks.Add(new AdminAiRiskFlagDto
                {
                    Code = "SilenceAvoidanceRisk",
                    Severity = AdminAiRiskSeverity.Low,
                    Message = "AI son 30 günde hiç Silent duruma geçmedi.",
                    Evidence = "Silent = 0 (30 gün)"
                });
            }

            // -------------------------------
            // 3) DAVRANIŞ KAYMASI (7g vs 30g)
            // -------------------------------
            var last7 = timeline.Last7Days;
            if (last7.Total > 0 && last30.Total > 0)
            {
                var ratio7 = (double)last7.Extended / last7.Total;
                var ratio30 = (double)last30.Extended / last30.Total;

                if (Math.Abs(ratio7 - ratio30) >= 0.20)
                {
                    risks.Add(new AdminAiRiskFlagDto
                    {
                        Code = "BehaviorShiftDetected",
                        Severity = AdminAiRiskSeverity.Medium,
                        Message = "AI davranış oranlarında son 7 günde belirgin değişim var.",
                        Evidence = $"Extended 7g: %{Math.Round(ratio7 * 100)}, 30g: %{Math.Round(ratio30 * 100)}"
                    });
                }
            }

            // -------------------------------
            // 4) KRİTİK — SELF RETRACTED
            // -------------------------------
            if (last30.SelfRetracted > 0)
            {
                risks.Add(new AdminAiRiskFlagDto
                {
                    Code = "SelfRetractionDetected",
                    Severity = AdminAiRiskSeverity.High,
                    Message = "AI kendini geri çekti (SelfRetracted) kararı tespit edildi.",
                    Evidence = $"SelfRetracted = {last30.SelfRetracted} (30 gün)"
                });
            }

            return risks;
        }
    }
}
