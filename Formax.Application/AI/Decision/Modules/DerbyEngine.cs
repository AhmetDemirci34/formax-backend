using System;
using System.Linq;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// v2 MODÜL — Derby Engine.
    ///
    /// Derbiyi YALNIZ GERÇEK GDP haber sinyalinden tanır: News evidence içinde "Derby" tipli sinyal
    /// (GlobalNewsDiscovery SignalExtractor üretir). Takım-adı HARDCODE listesi YOK. Derbi tespit
    /// edilirse şiddeti kaynak güveni/öne-çıkma ile ölçeklenir; motor bunu DNA'ya (fiziksel sertlik,
    /// kaos, denge/beraberlik eğilimi) ve gol modeline (edge sıkışması + hafif gol azalması) yansıtır.
    /// Derbi yoksa HasData=false (davranış v1 ile aynı). Stateless & deterministik.
    /// </summary>
    internal sealed class DerbyEngine
    {
        public DerbyInsight Analyze(UnifiedMatchAiContext ctx)
        {
            var news = ctx.News;
            if (news == null || !news.HasData || news.NewsSignals == null)
                return new DerbyInsight { HasData = false };

            var hasDerby = news.NewsSignals.Any(s => s.Equals("Derby", StringComparison.OrdinalIgnoreCase));
            if (!hasDerby)
                return new DerbyInsight { HasData = false };

            // Şiddet: baskın sinyal derbi ise yüksek, aksi halde orta; kaynak güveniyle ölçekli.
            var prominence = string.Equals(news.TopSignal, "Derby", StringComparison.OrdinalIgnoreCase) ? 0.9 : 0.6;
            var trust = Math.Clamp(news.SourceTrust / 100.0, 0.5, 1.0);
            var intensity = Math.Round(Math.Clamp(prominence * trust, 0, 1), 3);

            return new DerbyInsight
            {
                HasData = true,
                Intensity = intensity,
                Source = "News",
                Summary = $"Haber sinyalleri bu maçı derbi olarak işaretliyor (şiddet {(int)(intensity * 100)}, kaynak güven {news.SourceTrust})."
            };
        }
    }
}
