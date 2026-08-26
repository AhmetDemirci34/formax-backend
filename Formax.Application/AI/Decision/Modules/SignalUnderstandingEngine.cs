using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;
using Formax.Application.AI.Signals;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 1 — Signal Understanding Engine.
    ///
    /// Unified AI Context'in <see cref="UnifiedMatchAiContext.ActiveSignals"/> listesini okur ve
    /// her sinyali motorun anlayabileceği normalize forma çevirir: yönlü mü (1X2'yi etkiler) yoksa
    /// severity/tempo mu; büyüklüğü ne; hangi güven/çelişki durumunda. Ham veri OKUMAZ — yalnız
    /// AI Signal Factory'nin ürettiği sinyalleri yorumlar. Stateless & deterministik.
    /// </summary>
    internal sealed class SignalUnderstandingEngine
    {
        public IReadOnlyList<UnderstoodSignal> Understand(UnifiedMatchAiContext ctx)
        {
            var result = new List<UnderstoodSignal>();
            if (ctx?.Signals == null) return result;

            foreach (var s in ctx.Signals)
            {
                if (!s.HasData) continue; // fake YOK — yalnız gerçek veriyle dolu sinyal

                var impact = Math.Clamp(s.Impact, -1.0, 1.0);
                var isDirectional = Math.Abs(impact) > 0.0001;

                // Büyüklük: yönlü sinyalde etki mutlak değeri; yönsüzde Value (severity).
                var magnitude = isDirectional
                    ? Math.Abs(impact)
                    : Math.Clamp(s.Value, 0.0, 1.0);

                result.Add(new UnderstoodSignal(
                    Source: s,
                    Name: s.Name,
                    Category: s.Category,
                    Impact: impact,
                    Magnitude: magnitude,
                    IsDirectional: isDirectional,
                    Conflict: s.ConflictStatus,
                    HasData: true));
            }

            return result;
        }
    }
}
