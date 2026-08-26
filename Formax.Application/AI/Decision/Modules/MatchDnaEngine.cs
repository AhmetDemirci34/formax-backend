using System;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Decision.Modules
{
    /// <summary>
    /// MODÜL 4 — Match DNA Engine.
    ///
    /// Maçın karakterini 12 boyutta çıkarır. Her boyut GERÇEK sinyallerden/beklenen-gol
    /// modelinden deterministik türetilir. Doğrudan veri olmayan boyutlar (Fiziksel Sertlik,
    /// Duran Top) proxy'den türetilir ve DÜRÜSTÇE düşük güvenle işaretlenir (fake YOK; türetim
    /// gücü şeffaftır). Stateless.
    /// </summary>
    internal sealed class MatchDnaEngine
    {
        public MatchDna Profile(UnifiedMatchAiContext ctx, SignalField field, PoissonGoalModel model,
            ContextIntelligence context = null)
        {
            var expHome = model.ExpHome;
            var expAway = model.ExpAway;
            var totalExp = expHome + expAway;
            var iyExp = totalExp * 0.42;

            // Yön/denge referansı: sinyal alanı + GücSkoru (ikisinin daha güçlüsü).
            var gucEdge = (ctx.GucSkoru - 50) / 50.0;         // -1..+1
            var edge = Math.Abs(field.NetHomeEdge) >= Math.Abs(gucEdge) ? field.NetHomeEdge : gucEdge;
            var absEdge = Math.Clamp(Math.Abs(edge), 0, 1);

            var attack = field.AttackPressure;                 // 0..1
            var dataConf = BaseConfidence(ctx, field);         // veri-destekli boyutların taban güveni

            // ── 1. Tempo ──────────────────────────────────────────────────────
            var goalPot100 = Clamp100(totalExp / 4.0 * 100);
            var tempo = Clamp100(goalPot100 * 0.6 + attack * 100 * 0.4);

            // ── 2. Gol Potansiyeli ────────────────────────────────────────────
            var goalPotential = goalPot100;

            // v2 bağlam girdileri (yalnız gerçek veri; yoksa 0 → v1 davranışı).
            var derbyK = context?.Derby?.HasData == true ? Math.Clamp(context.Derby.Intensity, 0, 1) : 0.0;
            var pressureK = context?.Pressure?.HasData == true ? context.Pressure.OverallPressure / 100.0 : 0.0;
            // v2.5 — müsabaka önemi + sezon-sonu stake (Competition/Season blokları; boşsa 0 → değişiklik yok).
            var compStake = context?.Competition?.HasData == true ? context.Competition.StakeLevel : 0.0;
            var seasonEnd = context?.Season?.HasData == true ? context.Season.EndStake : 0.0;
            var stakeK = Math.Clamp(compStake * 0.7 + seasonEnd * 0.3, 0, 1);

            // ── 3. Maç Dengesi (yüksek = dengeli; derbi daha dengeli/temkinli) ─
            var balance = Clamp100((1.0 - absEdge) * 100 + derbyK * 10);

            // ── 4. Baskı (favori üstünlüğü + hücum baskısı) ───────────────────
            var pressure = Clamp100(absEdge * 100 * 0.5 + attack * 100 * 0.5);

            // ── 5. Fiziksel Sertlik ───────────────────────────────────────────
            // PROXY REDUCTION (v2): gerçek derbi sinyali varsa proxy YERİNE onu kullan → daha yüksek
            // güven. Derbi yoksa v1 proxy'sine (derbi-haber/lig önemi + dar maç) düşülür (kart verisi yok).
            var tightProxy = balance / 100.0;
            int physical, physicalConf;
            if (derbyK > 0)
            {
                physical = Clamp100(50 + derbyK * 40 + tightProxy * 10);
                physicalConf = (int)Math.Clamp(60 + derbyK * 20, 55, 85);
            }
            else
            {
                var derbyProxy = ctx.News.HasData ? Math.Min(1.0, ctx.News.CompetitionNews / 4.0) : 0.0;
                physical = Clamp100(42 + derbyProxy * 30 + tightProxy * 18);
                physicalConf = ReduceConf(dataConf, 0.5, 45); // doğrudan kart verisi yok
            }

            // ── 6. Kontratak Eğilimi (savunma sağlamlığı farkı + edge) ────────
            var defGap = 0.0;
            if (ctx.Strength.HomeDefenceIndex > 0 || ctx.Strength.AwayDefenceIndex > 0)
                defGap = Math.Clamp((ctx.Strength.HomeDefenceIndex + ctx.Strength.AwayDefenceIndex) / 4.0, 0, 1);
            var counter = Clamp100(45 + absEdge * 30 + defGap * 25);

            // ── 7. Duran Top Etkisi (PROXY: gol potansiyeli) → düşük güven ────
            var setPiece = Clamp100(35 + goalPot100 * 0.30);
            var setPieceConf = ReduceConf(dataConf, 0.45, 40);

            // ── 8. Erken Gol Eğilimi (ilk yarı gol beklentisi) ────────────────
            var earlyGoalP = (1.0 - Math.Exp(-iyExp)) * 100;
            var earlyGoal = Clamp100(earlyGoalP);

            // ── 9. Geç Gol Eğilimi (açılma + toplam gol) ──────────────────────
            var openness = Clamp100(goalPot100 * 0.6 + attack * 100 * 0.4);
            var lateGoal = Clamp100(openness * 0.55 + goalPot100 * 0.45);

            // ── 10. Kaos Riski (belirsizlik + denge + gol bolluğu + derbi/baskı + stake) ──
            var chaos = Clamp100(field.Instability * 50 + (balance / 100.0) * 30 + (goalPot100 / 100.0) * 20
                                 + derbyK * 15 + pressureK * 10 + stakeK * 10);

            // ── 11. Sürpriz Potansiyeli (düşük güven + darlık + sağlayıcı çelişkisi + baskı) ──
            var providerDisagree = ProviderDisagreement(ctx, field.NetHomeEdge);
            var surprise = Clamp100((1.0 - field.AvgConfidence) * 40 + (balance / 100.0) * 30
                                    + providerDisagree * 30 + pressureK * 15);

            // ── 12. Maçın Açılma Eğilimi ──────────────────────────────────────
            var opennessDim = openness;

            return new MatchDna
            {
                Tempo = Dim("Tempo", tempo, dataConf, TempoLabel(tempo),
                    $"Beklenen toplam gol {totalExp:0.0}, hücum baskısı {(int)(attack * 100)}."),
                GoalPotential = Dim("Gol Potansiyeli", goalPotential, dataConf, LevelLabel(goalPotential, "gol"),
                    $"Beklenen goller Ev {expHome:0.0} / Dep {expAway:0.0}."),
                Balance = Dim("Maç Dengesi", balance, dataConf, BalanceLabel(balance),
                    $"Güç sapması {(int)(absEdge * 100)} (0=denge)."),
                Pressure = Dim("Baskı", pressure, dataConf, LevelLabel(pressure, "baskı"),
                    $"Favori üstünlüğü {(int)(absEdge * 100)}, hücum baskısı {(int)(attack * 100)}."),
                PhysicalIntensity = Dim("Fiziksel Sertlik", physical, physicalConf, LevelLabel(physical, "sertlik"),
                    derbyK > 0
                        ? $"Gerçek derbi sinyali (şiddet {(int)(derbyK * 100)}) — proxy yerine kullanıldı."
                        : "Kart verisi yok; derbi-haber/lig önemi ve maç darlığından proxy türetim."),
                CounterAttack = Dim("Kontratak Eğilimi", counter, dataConf, LevelLabel(counter, "kontratak"),
                    "Savunma sağlamlığı farkı ve güç dengesinden."),
                SetPieceThreat = Dim("Duran Top Etkisi", setPiece, setPieceConf, LevelLabel(setPiece, "duran top"),
                    "Duran top istatistiği yok; gol potansiyelinden proxy."),
                EarlyGoalTendency = Dim("Erken Gol Eğilimi", earlyGoal, dataConf, LevelLabel(earlyGoal, "erken gol"),
                    $"İlk yarı beklenen gol {iyExp:0.0}."),
                LateGoalTendency = Dim("Geç Gol Eğilimi", lateGoal, dataConf, LevelLabel(lateGoal, "geç gol"),
                    "Açılma eğilimi ve toplam gol beklentisinden."),
                ChaosRisk = Dim("Kaos Riski", chaos, dataConf, LevelLabel(chaos, "kaos"),
                    $"Belirsizlik {(int)(field.Instability * 100)}, denge {balance}, gol bolluğu {goalPot100}."),
                SurprisePotential = Dim("Sürpriz Potansiyeli", surprise, dataConf, LevelLabel(surprise, "sürpriz"),
                    "Sinyal güveni, maç darlığı ve sağlayıcı-yapı çelişkisinden."),
                Openness = Dim("Maçın Açılma Eğilimi", opennessDim, dataConf, LevelLabel(opennessDim, "açık oyun"),
                    $"Gol potansiyeli {goalPot100}, hücum baskısı {(int)(attack * 100)}.")
            };
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private static DnaDimension Dim(string name, int score, int conf, string label, string basis) => new()
        {
            Name = name, Score = score, Confidence = conf, Label = label, Basis = basis
        };

        /// <summary>Veri-destekli boyutların taban güveni: aktif sinyal sayısı + veri kalitesi.</summary>
        private static int BaseConfidence(UnifiedMatchAiContext ctx, SignalField field)
        {
            var dq = ctx.Quality != null && ctx.Quality.ActiveSignalCount > 0
                ? ctx.Quality.OverallDataQuality
                : ctx.DataQuality;
            var conf = 40 + field.ActiveCount * 4 + dq * 30;
            return (int)Math.Clamp(conf, 25, 92);
        }

        /// <summary>Proxy boyut güveni: taban güveni faktörle kıs, tavanla sınırla (dürüst).</summary>
        private static int ReduceConf(int baseConf, double factor, int cap)
            => (int)Math.Clamp(baseConf * factor, 15, cap);

        /// <summary>Sağlayıcı öngörüsü yapısal net yönle çelişiyor mu (0..1).</summary>
        private static double ProviderDisagreement(UnifiedMatchAiContext ctx, double netHomeEdge)
        {
            if (!ctx.Prediction.HasData) return 0.0;
            var providerEdge = (ctx.Prediction.PercentHome - ctx.Prediction.PercentAway) / 100.0;
            if (Math.Abs(providerEdge) < 0.05 || Math.Abs(netHomeEdge) < 0.05) return 0.0;
            return Math.Sign(providerEdge) != Math.Sign(netHomeEdge) ? 1.0 : 0.0;
        }

        private static int Clamp100(double v) => (int)Math.Round(Math.Clamp(v, 0, 100));

        private static string TempoLabel(int v) => v >= 66 ? "Yüksek tempo" : v >= 40 ? "Dengeli tempo" : "Kontrollü tempo";
        private static string BalanceLabel(int v) => v >= 66 ? "Dengeli maç" : v >= 40 ? "Hafif üstünlük" : "Net favori";

        private static string LevelLabel(int v, string noun)
        {
            var lvl = v >= 66 ? "Yüksek" : v >= 40 ? "Orta" : "Düşük";
            return $"{lvl} {noun}";
        }
    }
}
