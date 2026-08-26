using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 9 — Live Projection Engine.
    ///
    /// Canlı AI projeksiyonları. Beklenen-gol oranından (λ = toplam beklenen gol / 90 dk)
    /// deterministik zaman-pencereli gol olasılıkları türetir. Context'te canlı blok YOKKEN bunlar
    /// MAÇ ÖNCESİ projeksiyondur (IsLive=false) — gerçek xG model çıktısıdır, fake DEĞİL. Canlı
    /// sinyaller context'e geldikçe aynı motor bu değerleri yeniden hesaplar. Stateless.
    /// </summary>
    internal sealed class LiveProjectionEngine
    {
        public LiveProjection Project(UnifiedMatchAiContext ctx, PoissonGoalModel model, MatchDna dna, int baseConfidence)
        {
            // v3 — In-play ise GERÇEK canlı duruma göre yeniden hesapla; değilse maç-öncesi projeksiyon.
            if (ctx.LiveState != null && ctx.LiveState.HasData && ctx.LiveState.IsLive)
                return ProjectLive(ctx, model, dna, baseConfidence);

            var expHome = model.ExpHome;
            var expAway = model.ExpAway;
            var totalExp = expHome + expAway;
            var lambdaPerMin = totalExp / 90.0;

            var anyGoal = 1.0 - Math.Exp(-totalExp);
            var homeShare = totalExp > 0 ? expHome / totalExp : 0.5;

            var outcomes = new List<LiveOutcome>
            {
                Out("Önümüzdeki 5 dakikada gol", 1.0 - Math.Exp(-lambdaPerMin * 5), baseConfidence,
                    $"Dakika başına gol oranı {lambdaPerMin:0.000} (λ)."),
                Out("Önümüzdeki 10 dakikada gol", 1.0 - Math.Exp(-lambdaPerMin * 10), baseConfidence,
                    $"Dakika başına gol oranı {lambdaPerMin:0.000} (λ)."),
                Out("İlk yarı bitene kadar gol", 1.0 - Math.Exp(-totalExp * 0.42), baseConfidence,
                    "İlk yarı beklenen gol tabanlı."),
                Out("Maç sonuna kadar gol", anyGoal, baseConfidence,
                    "En az bir gol olma olasılığı (Poisson)."),
                Out("Sonraki gol ev sahibi", homeShare * anyGoal, baseConfidence,
                    $"{ctx.HomeName} gol payı {(int)(homeShare * 100)}%."),
                Out("Sonraki gol deplasman", (1 - homeShare) * anyGoal, baseConfidence,
                    $"{ctx.AwayName} gol payı {(int)((1 - homeShare) * 100)}%."),
                // DNA türevli seyir projeksiyonları (maç öncesi eğilim; canlı veriyle güncellenir).
                Out("Tempo yükseliyor", dna.Tempo.Score / 100.0, ScaleConf(baseConfidence, dna.Tempo.Confidence),
                    $"Tempo DNA {dna.Tempo.Score}."),
                Out("Maç açılıyor", dna.Openness.Score / 100.0, ScaleConf(baseConfidence, dna.Openness.Confidence),
                    $"Açılma DNA {dna.Openness.Score}."),
                Out("Kırmızı kart riski", dna.PhysicalIntensity.Score / 100.0 * 0.5,
                    ScaleConf(baseConfidence, dna.PhysicalIntensity.Confidence),
                    "Fiziksel sertlik proxy'sinden (kart verisi yok, düşük güven).")
            };

            return new LiveProjection
            {
                IsLive = false,
                Basis = "Maç öncesi projeksiyon (beklenen-gol modeli). Canlı sinyaller geldikçe yeniden hesaplanır.",
                Outcomes = outcomes
            };
        }

        /// <summary>
        /// v3 — CANLI yeniden hesaplama: kalan süreye orantılı beklenen gol + canlı momentum/kartlarla.
        /// Deterministik (verilen canlı snapshot için). Zengin istatistik yoksa skor/dakika/tempoya düşer.
        /// </summary>
        private static LiveProjection ProjectLive(UnifiedMatchAiContext ctx, PoissonGoalModel model, MatchDna dna, int baseConfidence)
        {
            var l = ctx.LiveState;
            var minute = Math.Clamp(l.Minute, 1, 95);
            var remaining = Math.Max(1, 90 - minute);
            var totalExp = model.ExpHome + model.ExpAway;
            var expRemaining = Math.Max(0.05, totalExp * remaining / 90.0);
            var lambdaPerMin = expRemaining / remaining;

            // Momentum → sonraki gol payını kaydır (canlı skor durumu).
            var baseShare = totalExp > 0 ? model.ExpHome / totalExp : 0.5;
            var scoreDiff = l.HomeScore - l.AwayScore;
            var homeShare = Math.Clamp(baseShare + Math.Clamp(scoreDiff * 0.05, -0.2, 0.2), 0.1, 0.9);
            var anyRemaining = 1.0 - Math.Exp(-expRemaining);

            var reds = l.RedHome + l.RedAway;
            var yellows = l.YellowHome + l.YellowAway;
            var cardRisk = Math.Clamp(dna.PhysicalIntensity.Score / 100.0 * 0.4 + yellows * 0.04 + reds * 0.15, 0, 0.95);

            var outcomes = new List<LiveOutcome>
            {
                Out("Önümüzdeki 5 dakikada gol", 1.0 - Math.Exp(-lambdaPerMin * Math.Min(5, remaining)), baseConfidence,
                    $"Kalan {remaining} dk, kalan beklenen gol {expRemaining:0.00}."),
                Out("Önümüzdeki 10 dakikada gol", 1.0 - Math.Exp(-lambdaPerMin * Math.Min(10, remaining)), baseConfidence,
                    $"Dakika başına canlı gol oranı {lambdaPerMin:0.000}."),
                Out(minute < 45 ? "İlk yarı bitene kadar gol" : "İlk yarı tamamlandı",
                    minute < 45 ? 1.0 - Math.Exp(-lambdaPerMin * (45 - minute)) : 0.0, baseConfidence,
                    minute < 45 ? $"İlk yarı kalan {45 - minute} dk." : "İlk yarı oynandı."),
                Out("Maç sonuna kadar (bir gol daha)", anyRemaining, baseConfidence,
                    $"Kalan sürede en az bir gol (λ={expRemaining:0.00})."),
                Out("Sonraki gol ev sahibi", homeShare * anyRemaining, baseConfidence,
                    $"{ctx.HomeName} gol payı {(int)(homeShare * 100)}% (skor {l.HomeScore}-{l.AwayScore})."),
                Out("Sonraki gol deplasman", (1 - homeShare) * anyRemaining, baseConfidence,
                    $"{ctx.AwayName} gol payı {(int)((1 - homeShare) * 100)}%."),
                Out("Kırmızı kart riski", cardRisk, ScaleConf(baseConfidence, dna.PhysicalIntensity.Confidence),
                    $"Fiziksellik {dna.PhysicalIntensity.Score}, sarı {yellows}, kırmızı {reds}."),
                Out("Tempo yükseliş eğilimi", dna.Tempo.Score / 100.0, ScaleConf(baseConfidence, dna.Tempo.Confidence),
                    $"Tempo DNA {dna.Tempo.Score}.")
            };

            return new LiveProjection
            {
                IsLive = true,
                Basis = $"CANLI — {minute}. dk, skor {l.HomeScore}-{l.AwayScore}. Kalan süreye orantılı yeniden hesap"
                        + (l.HasRichStats ? " (canlı istatistiklerle)." : " (skor/dakika/tempo; zengin istatistik yok)."),
                Outcomes = outcomes
            };
        }

        private static LiveOutcome Out(string market, double p, int baseConfidence, string reason)
        {
            var prob = (int)Math.Round(Math.Clamp(p * 100, 1, 99));
            var decisiveness = 2.0 * Math.Abs(p - 0.5);
            var s = (int)Math.Clamp(baseConfidence * (0.85 + 0.30 * decisiveness), 0, 99);
            return new LiveOutcome
            {
                Market = market,
                Probability = prob,
                Confidence = s >= 68 ? "YÜKSEK" : s >= 50 ? "ORTA" : "DÜŞÜK",
                Reason = reason
            };
        }

        private static int ScaleConf(int baseConfidence, int dimConfidence)
            => (int)Math.Clamp(baseConfidence * (dimConfidence / 100.0), 0, 99);
    }
}
