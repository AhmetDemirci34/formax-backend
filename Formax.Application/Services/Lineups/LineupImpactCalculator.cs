using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.Outcomes;

namespace Formax.Application.Services.Lineups
{
    /// <summary>Kadro düzeltmesinin tek oyuncu satırı (açıklanabilirlik — UI'da isim verilmez, denetimde kullanılır).</summary>
    public sealed record LineupPlayerContribution(
        string PlayerKey, string DisplayName, string Position, int Matches,
        double NetAttack, double NetDefence, int ReplacementPoolSize);

    /// <summary>
    /// KADRO DÜZELTMESİ — temel modelin ÜSTÜNE uygulanan sınırlı, açıklanabilir delta.
    /// <see cref="Applied"/> false iken temel olasılıklar BİREBİR korunur.
    /// </summary>
    public sealed class LineupAdjustment
    {
        public string Version { get; init; } = LineupImpactVersion.Current;
        public bool Applied { get; init; }
        public string LineupSourceStatus { get; init; } = LineupSourceStatuses.Missing;
        public string LineupConfidence { get; init; } = LineupConfidenceLevels.None;
        public List<string> AdjustmentReasonCodes { get; init; } = new();

        /// <summary>λ_ev üzerindeki toplam log delta (ev hücumu + deplasman savunması).</summary>
        public double HomeLineupDelta { get; init; }
        public double AwayLineupDelta { get; init; }

        public double HomeAttackDelta { get; init; }
        public double HomeDefenceDelta { get; init; }
        public double AwayAttackDelta { get; init; }
        public double AwayDefenceDelta { get; init; }

        /// <summary>Ölçülebilir geçmişi olan başlangıç oyuncusu sayısı (22 üzerinden).</summary>
        public int ResolvedStarters { get; init; }
        public int TotalStarters { get; init; }

        /// <summary>Takım normunun belirgin üstünde/altında kalan başlangıç oyuncuları (denetim).</summary>
        public List<LineupPlayerContribution> ImpactfulStarters { get; init; } = new();
        /// <summary>Son maçlarda başlarken bu maçta ilk 11'de olmayan, ölçülebilir etkili oyuncular.</summary>
        public List<LineupPlayerContribution> ImpactfulAbsences { get; init; } = new();
        /// <summary>Yerine geçen oyuncu havuzunun ortalama kalitesi (hücum, savunma).</summary>
        public double ReplacementQualityAttack { get; init; }
        public double ReplacementQualityDefence { get; init; }

        public static LineupAdjustment None(string sourceStatus, IEnumerable<string> reasons)
            => new()
            {
                Applied = false,
                LineupSourceStatus = sourceStatus,
                LineupConfidence = sourceStatus == LineupSourceStatuses.Missing
                    ? LineupConfidenceLevels.None : LineupConfidenceLevels.Insufficient,
                AdjustmentReasonCodes = reasons.ToList()
            };
    }

    /// <summary>
    /// KADRO → λ DÜZELTMESİ. Oyuncu etkileri KÖRLEMESİNE TOPLANMAZ: kanal toplamı yumuşak doyum
    /// (tanh) ile takım tavanına sıkıştırılır, sonra λ üzerindeki toplam delta ikinci bir tavanla
    /// kesilir. Tavana değildiğinde <see cref="LineupReasonCodes.ImpactCapped"/> yazılır.
    ///
    /// Uygulama:
    ///   λ_ev'  = λ_ev  × e^(hücum_ev  + savunma_dep)
    ///   λ_dep' = λ_dep × e^(hücum_dep + savunma_ev)
    /// Üç olasılığın toplamı yapısal olarak 1 kalır: dağılım tek Poisson matrisinden yeniden türer.
    /// </summary>
    public static class LineupImpactCalculator
    {
        /// <summary>Denetim listelerine girmek için gereken en küçük mutlak net etki.</summary>
        public const double MaterialPlayerImpact = 0.01;

        /// <param name="gate">
        /// Doğrulama kapısı. Varsayılan üretim kapısıdır (yapı + resmî kaynak damgası); ölçüm yolu
        /// duyarlılık analizinde yalnız yapı kapısını geçebilir.
        /// </param>
        public static LineupAdjustment Compute(
            MatchLineupObservation? observation,
            PlayerImpactModel model,
            int homeTeamId,
            int awayTeamId,
            Func<MatchLineupObservation?, LineupVerificationRule.Verdict>? gate = null)
        {
            var verdict = (gate ?? LineupVerificationRule.Check)(observation);
            if (!verdict.Accepted) return LineupAdjustment.None(verdict.SourceStatus, verdict.ReasonCodes);

            var obs = observation!;
            var reasons = new List<string>(verdict.ReasonCodes);

            var (homeAtt, homeDef, homeRows, homeRepl, homeUnknown) = Side(obs.Home, model, homeTeamId);
            var (awayAtt, awayDef, awayRows, awayRepl, awayUnknown) = Side(obs.Away, model, awayTeamId);

            if (homeUnknown + awayUnknown > 0) reasons.Add(LineupReasonCodes.PositionUnknown);
            if (homeRepl.PoolSize == 0 && awayRepl.PoolSize == 0) reasons.Add(LineupReasonCodes.ReplacementBaselineUnknown);

            var p = model.Parameters;
            var capped = false;
            double Damp(double sum)
            {
                var v = p.MaxSideLogDelta * Math.Tanh(sum / Math.Max(1e-9, p.MaxSideLogDelta));
                if (Math.Abs(sum) > p.MaxSideLogDelta) capped = true;
                return v;
            }

            var hAtt = Damp(homeAtt);
            var hDef = Damp(homeDef);
            var aAtt = Damp(awayAtt);
            var aDef = Damp(awayDef);

            var homeDelta = Clamp(hAtt + aDef, p.MaxLambdaLogDelta, ref capped);
            var awayDelta = Clamp(aAtt + hDef, p.MaxLambdaLogDelta, ref capped);
            if (capped) reasons.Add(LineupReasonCodes.ImpactCapped);

            var resolved = homeRows.Count + awayRows.Count;
            var total = obs.Home.Count(x => x.Starter) + obs.Away.Count(x => x.Starter);
            if (resolved == 0) reasons.Add(LineupReasonCodes.PlayerSampleInsufficient);

            var material = Math.Abs(homeDelta) >= p.MaterialChangeThreshold || Math.Abs(awayDelta) >= p.MaterialChangeThreshold;
            if (!material) reasons.Add(LineupReasonCodes.NoMaterialChange);

            return new LineupAdjustment
            {
                Applied = material && resolved > 0,
                LineupSourceStatus = LineupSourceStatuses.Verified,
                LineupConfidence = Confidence(resolved, total),
                AdjustmentReasonCodes = reasons,
                HomeLineupDelta = Math.Round(homeDelta, 6),
                AwayLineupDelta = Math.Round(awayDelta, 6),
                HomeAttackDelta = Math.Round(hAtt, 6),
                HomeDefenceDelta = Math.Round(hDef, 6),
                AwayAttackDelta = Math.Round(aAtt, 6),
                AwayDefenceDelta = Math.Round(aDef, 6),
                ResolvedStarters = resolved,
                TotalStarters = total,
                ImpactfulStarters = homeRows.Concat(awayRows)
                    .Where(r => Math.Abs(r.NetAttack) + Math.Abs(r.NetDefence) >= MaterialPlayerImpact)
                    .OrderByDescending(r => Math.Abs(r.NetAttack) + Math.Abs(r.NetDefence)).Take(6).ToList(),
                ImpactfulAbsences = Absences(obs, model, homeTeamId, awayTeamId),
                ReplacementQualityAttack = Math.Round((homeRepl.Attack + awayRepl.Attack) / 2, 6),
                ReplacementQualityDefence = Math.Round((homeRepl.Defence + awayRepl.Defence) / 2, 6)
            };
        }

        private static (double Attack, double Defence, List<LineupPlayerContribution> Rows,
                        (double Attack, double Defence, int PoolSize) Replacement, int Unknown)
            Side(IReadOnlyList<LineupPlayerObservation> players, PlayerImpactModel model, int teamId)
        {
            double att = 0, def = 0;
            var rows = new List<LineupPlayerContribution>();
            double rAtt = 0, rDef = 0;
            var rCount = 0;
            var unknown = 0;

            foreach (var pl in players.Where(x => x.Starter))
            {
                var pos = LineupPositions.Normalize(pl.Position);
                if (pos == null) { unknown++; continue; }
                var impact = model.Impact(pl.PlayerKey);
                if (!impact.Sufficient) continue;
                // Ablasyon: yedek çıkarımı kapalıyken ham başlangıç etkisi ölçülür.
                var repl = model.Parameters.ApplyReplacement
                    ? model.Replacement(teamId, pos, pl.PlayerKey)
                    : (Attack: 0.0, Defence: 0.0, PoolSize: 0);
                // OYUNCU ETKİSİ = oyuncu katkısı − muhtemel yedeğin katkısı.
                var netAtt = impact.AttackImpact - repl.Attack;
                var netDef = impact.DefenceImpact - repl.Defence;
                att += netAtt;
                def += netDef;
                rAtt += repl.Attack; rDef += repl.Defence; rCount++;
                rows.Add(new LineupPlayerContribution(pl.PlayerKey, pl.DisplayName, pos, impact.Matches,
                    Math.Round(netAtt, 6), Math.Round(netDef, 6), repl.PoolSize));
            }
            var replacement = rCount == 0 ? (0.0, 0.0, 0) : (rAtt / rCount, rDef / rCount, rCount);
            return (att, def, rows, replacement, unknown);
        }

        /// <summary>
        /// ETKİLİ EKSİKLER — takımın ölçülebilir etkiye sahip oyuncularından bu maçta İLK 11'de OLMAYANLAR.
        /// Uydurma yok: yalnız modelin gerçekten öğrendiği (yeterli örneklemli) oyuncular listelenir.
        /// </summary>
        private static List<LineupPlayerContribution> Absences(
            MatchLineupObservation obs, PlayerImpactModel model, int homeTeamId, int awayTeamId)
        {
            var list = new List<LineupPlayerContribution>();
            foreach (var (teamId, side) in new[] { (homeTeamId, obs.Home), (awayTeamId, obs.Away) })
            {
                var present = side.Where(x => x.Starter).Select(x => x.PlayerKey).ToHashSet(StringComparer.Ordinal);
                foreach (var est in model.SufficientFor(teamId))
                {
                    if (present.Contains(est.PlayerKey)) continue;
                    var repl = model.Replacement(teamId, est.Position, est.PlayerKey);
                    var netAtt = est.AttackImpact - repl.Attack;
                    var netDef = est.DefenceImpact - repl.Defence;
                    if (Math.Abs(netAtt) + Math.Abs(netDef) < MaterialPlayerImpact) continue;
                    list.Add(new LineupPlayerContribution(est.PlayerKey, est.PlayerKey, est.Position, est.Matches,
                        Math.Round(netAtt, 6), Math.Round(netDef, 6), repl.PoolSize));
                }
            }
            return list.OrderByDescending(r => Math.Abs(r.NetAttack) + Math.Abs(r.NetDefence)).Take(6).ToList();
        }

        private static double Clamp(double v, double cap, ref bool capped)
        {
            if (Math.Abs(v) > cap) { capped = true; return Math.Sign(v) * cap; }
            return v;
        }

        private static string Confidence(int resolved, int total)
        {
            if (total == 0) return LineupConfidenceLevels.Insufficient;
            var share = resolved / (double)total;
            if (resolved == 0) return LineupConfidenceLevels.Insufficient;
            if (share < 0.25) return LineupConfidenceLevels.Low;
            if (share < 0.60) return LineupConfidenceLevels.Medium;
            return LineupConfidenceLevels.High;
        }

        /// <summary>
        /// DÜZELTİLMİŞ BEKLENTİ — temel modelin λ'larına kadro deltası uygulanır. Beklentinin diğer
        /// bütün alanları (örneklem, kapsam, kapı gerekçeleri, Elo) DEĞİŞMEDEN taşınır: kadro katmanı
        /// temel modeli değiştirmez, üstüne sınırlı bir delta koyar.
        /// </summary>
        public static OutcomeExpectation Apply(OutcomeExpectation e, LineupAdjustment adjustment)
        {
            if (!adjustment.Applied) return e;
            return e with
            {
                LambdaHome = Math.Max(0.01, e.LambdaHome * Math.Exp(adjustment.HomeLineupDelta)),
                LambdaAway = Math.Max(0.01, e.LambdaAway * Math.Exp(adjustment.AwayLineupDelta))
            };
        }
    }
}
