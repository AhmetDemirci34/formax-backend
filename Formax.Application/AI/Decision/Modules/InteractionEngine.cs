using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Interaction Intelligence.
    ///
    /// Sinyalleri tek tek değil, BİRLİKTE okur: birden çok gerçek sinyalin ÖRTÜŞMESİ tek tek toplamdan
    /// daha güçlü bir etki üretir (ör. derbi + kesin eksik savunma + yüksek fiziksellik → kart/kaos
    /// olasılığı artar). Bir etki YALNIZ ≥2 gerçek sürücü örtüştüğünde üretilir (fake YOK). Hakem-kart
    /// eğilimi ve hava verisi context'te olmadığından o sürücüler bugün dahil edilmez (dürüst). Stateless.
    /// </summary>
    internal sealed class InteractionEngine
    {
        public IReadOnlyList<InteractionEffect> Analyze(UnifiedMatchAiContext ctx, MatchDna dna, ContextIntelligence ci)
        {
            var effects = new List<InteractionEffect>();

            // ── Kart/kaos etkileşimi: derbi + kesin eksik + yüksek fiziksellik ──
            {
                var drivers = new List<string>();
                double m = 0;
                if (ci.Derby.HasData) { drivers.Add("derbi"); m += ci.Derby.Intensity * 40; }
                if (ctx.Availability.HasData && (ctx.Availability.HomeKeyAbsences + ctx.Availability.AwayKeyAbsences) > 0)
                { drivers.Add("kesin eksikler"); m += Math.Min(25, (ctx.Availability.HomeKeyAbsences + ctx.Availability.AwayKeyAbsences) * 8); }
                if (dna.PhysicalIntensity.Score >= 60) { drivers.Add("yüksek fiziksellik"); m += 25; }
                if (drivers.Count >= 2)
                    effects.Add(new InteractionEffect
                    {
                        Name = "Kart/Sertlik Riski",
                        Drivers = drivers,
                        Magnitude = (int)Math.Clamp(m, 0, 100),
                        Effect = "Sürücülerin örtüşmesi kart ve sert oyun olasılığını yükseltir (not: hakem-kart eğilimi/hava verisi kapsam dışı)."
                    });
            }

            // ── Düşük skor etkileşimi: yüksek stake + düşük tempo + dengeli maç ──
            {
                var drivers = new List<string>();
                double m = 0;
                if (ci.Competition.HasData && ci.Competition.StakeLevel >= 0.5) { drivers.Add("yüksek önem"); m += 30; }
                if (dna.Tempo.Score < 42) { drivers.Add("düşük tempo"); m += 25; }
                if (dna.Balance.Score >= 66) { drivers.Add("dengeli maç"); m += 20; }
                if (drivers.Count >= 2)
                    effects.Add(new InteractionEffect
                    {
                        Name = "Düşük Skor Eğilimi",
                        Drivers = drivers,
                        Magnitude = (int)Math.Clamp(m, 0, 100),
                        Effect = "Temkinli/dengeli tablo toplam gol beklentisini baskılar."
                    });
            }

            // ── Belirsizlik etkileşimi: breaking + çelişki/kaos + düşük veri ──
            {
                var drivers = new List<string>();
                double m = 0;
                var breaking = (ctx.News.HasData && ctx.News.HasBreakingNews) || (ctx.Social.HasData && ctx.Social.BreakingOfficialNews);
                if (breaking) { drivers.Add("breaking gelişme"); m += 30; }
                if (dna.ChaosRisk.Score >= 55) { drivers.Add("yüksek kaos"); m += 25; }
                if ((ctx.Quality?.ActiveSignalCount ?? 0) < 5) { drivers.Add("düşük veri kapsamı"); m += 20; }
                if (drivers.Count >= 2)
                    effects.Add(new InteractionEffect
                    {
                        Name = "Belirsizlik Yükselişi",
                        Drivers = drivers,
                        Magnitude = (int)Math.Clamp(m, 0, 100),
                        Effect = "Örtüşen belirsizlik sürücüleri öngörülebilirliği düşürür."
                    });
            }

            return effects;
        }
    }
}
