using System;
using System.Collections.Generic;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 2 — Dynamic Weight Engine.
    ///
    /// Her AI sinyaline SABİT OLMAYAN ağırlık atar. Ağırlık; sinyalin kalite zarfından
    /// (Confidence, SourceTrust, EvidenceScore, Freshness, DataQuality) ve etkisinden (Impact/
    /// Magnitude) birlikte türetilir. GDP zenginleştikçe daha kaliteli sinyal daha yüksek ağırlık
    /// alır — motor kodu yeniden yazılmaz (OCP). Stateless & deterministik.
    /// </summary>
    internal sealed class DynamicWeightEngine
    {
        // Kalite bileşenlerinin göreli katkısı (toplam = 1.0). Bunlar SABİT ağırlık DEĞİL;
        // bir sinyalin KENDİ kalite skorlarını harmanlayan katsayılardır (her sinyal için farklı çıktı).
        private const double WConfidence   = 0.30;
        private const double WSourceTrust  = 0.25;
        private const double WEvidence     = 0.20;
        private const double WFreshness    = 0.15;
        private const double WDataQuality  = 0.10;

        public IReadOnlyList<WeightedSignal> Weigh(IReadOnlyList<UnderstoodSignal> understood)
        {
            var result = new List<WeightedSignal>();
            if (understood == null) return result;

            foreach (var u in understood)
            {
                var s = u.Source;

                // Kalite skoru (0..1): sinyalin kendi zarf alanlarının harmanı.
                var quality =
                      s.Confidence   / 100.0 * WConfidence
                    + s.SourceTrust  / 100.0 * WSourceTrust
                    + s.EvidenceScore/ 100.0 * WEvidence
                    + s.Freshness            * WFreshness
                    + s.DataQuality          * WDataQuality;
                quality = Math.Clamp(quality, 0.0, 1.0);

                // Etki gücü (0..1): yönlü sinyalde etkinin mutlak değeri, yönsüzde severity.
                var force = Math.Clamp(u.Magnitude, 0.0, 1.0);

                // Dinamik ağırlık = kalite × (0.4 taban + 0.6 etki). Yönsüz-severity sinyaller de
                // (kaliteli haber/resmi açıklama) belirsizliği/riski beslesin diye taban katkı alır.
                var weight = quality * (0.4 + 0.6 * force);
                weight = Math.Clamp(weight, 0.0, 1.0);

                result.Add(new WeightedSignal(u, Math.Round(weight, 4), Suppressed: false));
            }

            return result;
        }
    }
}
