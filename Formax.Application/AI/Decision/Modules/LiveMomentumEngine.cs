using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v3 MODÜL — Live Momentum Engine.
    ///
    /// In-play maçta anlık momentumu MEVCUT canlı istatistiklerden (skor/dakika + varsa topa sahip
    /// olma/şut/tehlikeli atak/korner) üretir. Zengin istatistik ingest'te dolmadıysa momentum
    /// yalnız skor+dakikadan hesaplanır (dürüst — HasRichStats=false). In-play değilse HasData=false.
    /// Stateless & deterministik (verilen canlı snapshot için).
    /// </summary>
    internal sealed class LiveMomentumEngine
    {
        public LiveMomentum Analyze(UnifiedMatchAiContext ctx)
        {
            var l = ctx.LiveState;
            if (l == null || !l.HasData || !l.IsLive)
                return new LiveMomentum { HasData = false };

            double m = 0;
            var drivers = new List<string>();

            var scoreDiff = l.HomeScore - l.AwayScore;
            if (scoreDiff != 0)
            {
                m += Math.Clamp(scoreDiff * 22, -55, 55);
                drivers.Add($"skor {l.HomeScore}-{l.AwayScore}");
            }

            if (l.HasRichStats)
            {
                if (l.PossessionHome + l.PossessionAway > 0)
                { m += Math.Clamp((l.PossessionHome - l.PossessionAway) * 0.6, -20, 20); drivers.Add("topa sahip olma"); }
                if (l.ShotsHome + l.ShotsAway > 0)
                { m += Math.Clamp((l.ShotsHome - l.ShotsAway) * 2.0, -20, 20); drivers.Add("şut farkı"); }
                if (l.DangerousAttacksHome + l.DangerousAttacksAway > 0)
                { m += Math.Clamp((l.DangerousAttacksHome - l.DangerousAttacksAway) * 0.5, -20, 20); drivers.Add("tehlikeli atak"); }
                if (l.CornersHome + l.CornersAway > 0)
                { m += Math.Clamp((l.CornersHome - l.CornersAway) * 1.5, -10, 10); drivers.Add("korner"); }
            }

            if (drivers.Count == 0)
                drivers.Add("dakika/faz");

            var momentum = (int)Math.Clamp(Math.Round(m), -100, 100);
            var dir = momentum > 10 ? ctx.HomeName : momentum < -10 ? ctx.AwayName : "dengeli";

            return new LiveMomentum
            {
                HasData   = true,
                Minute    = l.Minute,
                HomeScore = l.HomeScore,
                AwayScore = l.AwayScore,
                Momentum  = momentum,
                Drivers   = drivers,
                Summary   = $"{l.Minute}. dk {l.HomeScore}-{l.AwayScore}; momentum {momentum:+0;-0;0} ({dir})" +
                            (l.HasRichStats ? "." : " — yalnız skor/dakika (zengin istatistik yok).")
            };
        }
    }
}
