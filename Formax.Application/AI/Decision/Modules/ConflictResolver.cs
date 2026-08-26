using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.AI.Signals;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 3 — Conflict Resolver.
    ///
    /// Çelişen sinyalleri çözer ve düşük-güvenli/çözülemeyen sinyalleri motoru YÖNLENDİRMEKTEN
    /// alıkoyar (ağırlığını baskılar). AI Signal Factory zaten kaynak-içi füzyon+çelişki çözer
    /// (ConflictStatus taşır); bu modül sinyaller-ARASI (ör. yapısal güç ev derken sağlayıcı
    /// öngörüsü deplasman derken) tutarlılığı gözetir ve şu kuralları uygular:
    ///   - Unresolved çelişki → ağırlık ciddi kısılır (motoru saptırmasın).
    ///   - Resolved çelişki → ağırlık hafif kısılır (resmi kaynakla çözülmüş; güven bir tık düşük).
    ///   - Suppressed → zaten baskılanmış; işaretlenir.
    ///   - Yönlü sinyaller net alan yönüne AYKIRI ve düşük güvenliyse → ek baskı (azınlık gürültüsü).
    /// Deterministik & stateless.
    /// </summary>
    internal sealed class ConflictResolver
    {
        public IReadOnlyList<WeightedSignal> Resolve(IReadOnlyList<WeightedSignal> weighted)
        {
            var result = new List<WeightedSignal>();
            if (weighted == null || weighted.Count == 0) return result;

            // Yönlü sinyallerin ağırlıklı net yönü (azınlık-aykırı tespiti için referans).
            double dirW = 0, dirNum = 0;
            foreach (var w in weighted)
            {
                if (!w.Signal.IsDirectional) continue;
                dirW += w.Weight;
                dirNum += w.Weight * w.Signal.Impact;
            }
            var netDir = dirW > 0 ? dirNum / dirW : 0.0; // -1..+1

            foreach (var w in weighted)
            {
                var weight = w.Weight;
                var suppressed = false;
                var s = w.Signal;

                // Kaynak-içi çelişki durumuna göre ağırlık düzeltmesi.
                switch (s.Conflict)
                {
                    case SignalConflictStatus.Unresolved:
                        weight *= 0.45;            // otorite yok → motoru yönlendirmesin
                        suppressed = true;
                        break;
                    case SignalConflictStatus.Resolved:
                        weight *= 0.80;            // resmi kaynakla çözülmüş → hafif indirim
                        break;
                    case SignalConflictStatus.Suppressed:
                        weight *= 0.70;            // bayat/zayıf zaten baskılı
                        suppressed = true;
                        break;
                }

                // Sinyaller-arası: yönlü + net alana AYKIRI + düşük güven → azınlık gürültüsü, baskıla.
                if (s.IsDirectional && Math.Abs(netDir) > 0.15
                    && Math.Sign(s.Impact) != 0 && Math.Sign(s.Impact) != Math.Sign(netDir)
                    && s.Source.Confidence < 60)
                {
                    weight *= 0.60;
                    suppressed = true;
                }

                result.Add(w with { Weight = Math.Round(Math.Clamp(weight, 0, 1), 4), Suppressed = suppressed });
            }

            return result;
        }

        /// <summary>
        /// Ağırlıklı sinyallerden agregat alanı üretir (beklenen-gol ve yön kararının girdisi).
        /// Yönlü sinyaller net edge'i; severity/tempo sinyalleri hücum baskısını; çelişki/breaking
        /// ise instability'yi besler.
        /// </summary>
        public SignalField BuildField(IReadOnlyList<WeightedSignal> resolved)
        {
            if (resolved == null || resolved.Count == 0)
                return new SignalField(0, 0, 0, 0, 0);

            double dirW = 0, dirNum = 0;
            double atkW = 0, atkNum = 0;
            double instW = 0, instNum = 0;
            double confSum = 0; int active = 0;

            foreach (var w in resolved)
            {
                var s = w.Signal;
                active++;
                confSum += s.Source.Confidence / 100.0;

                if (s.IsDirectional)
                {
                    dirW += w.Weight;
                    dirNum += w.Weight * s.Impact;
                }

                // Hücum/tempo baskısı: hücum-güç ve gol-üretim sinyalleri.
                if (IsAttackSignal(s.Name))
                {
                    atkW += w.Weight;
                    atkNum += w.Weight * s.Magnitude;
                }

                // Belirsizlik: çelişki (unresolved/resolved) + breaking + baskılanmış sinyaller.
                var instC = s.Conflict == SignalConflictStatus.Unresolved ? 1.0
                          : s.Conflict == SignalConflictStatus.Resolved ? 0.5
                          : w.Suppressed ? 0.4 : 0.0;
                if (s.Name.Contains("Breaking", StringComparison.OrdinalIgnoreCase)) instC = Math.Max(instC, 0.6);
                if (instC > 0)
                {
                    instW += w.Weight;
                    instNum += w.Weight * instC;
                }
            }

            var netHomeEdge = dirW > 0 ? Math.Clamp(dirNum / dirW, -1, 1) : 0.0;
            var attackPressure = atkW > 0 ? Math.Clamp(atkNum / atkW, 0, 1) : 0.0;
            var instability = instW > 0 ? Math.Clamp(instNum / instW, 0, 1) : 0.0;
            var avgConf = active > 0 ? Math.Clamp(confSum / active, 0, 1) : 0.0;

            return new SignalField(
                Math.Round(netHomeEdge, 4),
                Math.Round(attackPressure, 4),
                Math.Round(instability, 4),
                Math.Round(avgConf, 4),
                active);
        }

        private static bool IsAttackSignal(string name) =>
            name is "AttackStrength" or "TeamStrength" or "HomeAdvantage" or "FormStrength";
    }
}
