using System;
using System.Collections.Generic;
using Formax.Application.DTOs.Matches;

namespace Formax.Application.Services.Radar.Intelligence.Scenarios
{
    /// <summary>
    /// FORMAX Radar v2.2 — geniş market havuzunu DETERMİNİSTİK değerlendirir.
    /// Mevcut Statistical Intelligence (TeamComparison + H2H + GücSkoru) üzerine kurulur;
    /// yeni veri kaynağı eklemez. Her market için olasılık + güven + kanıt etiketi üretir.
    ///
    /// Basit, açıklanabilir bir beklenen-gol modeli kullanır (bahis hassasiyeti değil,
    /// senaryo SIRALAMASI hedeflenir). LLM bu sayılara dokunmaz.
    /// </summary>
    public sealed class MarketProbabilityEngine
    {
        public IReadOnlyList<ScenarioCandidate> Evaluate(
            TeamComparisonDto home,
            TeamComparisonDto away,
            H2HDto h2h,
            int gucSkoru,
            string homeName,
            string awayName)
        {
            var list = new List<ScenarioCandidate>();

            // ── Beklenen goller (clamp ile güvenli) ──────────────────────────────
            var expHome = Clamp(Avg(home.AvgGoalsFor, away.AvgGoalsAgainst, 1.2), 0.2, 3.5);
            var expAway = Clamp(Avg(away.AvgGoalsFor, home.AvgGoalsAgainst, 1.0), 0.2, 3.5);
            var totalExp = expHome + expAway;
            var iyExp = totalExp * 0.42;

            var edge = gucSkoru - 50; // + ev sahibi lehine
            var h2hGollu = h2h.TotalMatches > 0 && (h2h.HomeWins + h2h.AwayWins) >= h2h.Draws;

            // ── 1X2 ──────────────────────────────────────────────────────────────
            var pDraw = (int)Clamp(30 - Math.Abs(edge) * 0.18, 18, 32);
            var rem = 100 - pDraw;
            var homeShare = Clamp(0.5 + edge / 110.0, 0.12, 0.88);
            var pHome = (int)Math.Round(rem * homeShare);
            var pAway = Math.Max(6, rem - pHome);

            var strongTag = edge >= 0 ? $"Güç dengesi {homeName} lehine" : $"Güç dengesi {awayName} lehine";

            Add(list, "Ev Sahibi", pHome, ScenarioFamily.Outcome, 1.0, strongTag, "Saha avantajı");
            Add(list, "Beraberlik", pDraw, ScenarioFamily.Outcome, 0.9, "İki taraf dengeli");
            Add(list, "Deplasman", pAway, ScenarioFamily.Outcome, 1.0, strongTag);

            // ── Çifte şans / kaybetmez (Outcome ailesi) ─────────────────────────
            Add(list, $"{homeName} Kaybetmez", Math.Min(95, pHome + pDraw), ScenarioFamily.Outcome, 1.0,
                strongTag, "Beraberlik payı dahil");
            Add(list, $"{awayName} Kaybetmez", Math.Min(95, pAway + pDraw), ScenarioFamily.Outcome, 1.0,
                strongTag, "Beraberlik payı dahil");
            Add(list, "Çifte Şans (1-2)", Math.Min(96, pHome + pAway), ScenarioFamily.Outcome, 0.8,
                "Beraberlik dışı sonuç");

            // ── Toplam gol (Totals ailesi) ──────────────────────────────────────
            var goalTag = totalExp >= 2.6 ? "Yüksek toplam gol beklentisi" : "Kontrollü gol beklentisi";
            foreach (var line in new[] { 0.5, 1.5, 2.5, 3.5 })
            {
                var over = POver(totalExp, line);
                var w = line == 2.5 ? 1.0 : line == 0.5 ? 0.45 : 0.85;
                Add(list, $"{line:0.0} Üst".Replace(",", "."), over, ScenarioFamily.Totals, w, goalTag);
                Add(list, $"{line:0.0} Alt".Replace(",", "."), 100 - over, ScenarioFamily.Totals, w,
                    totalExp < 2.4 ? "Düşük tempo beklentisi" : "Dengeli tempo");
            }

            // ── KG (Btts ailesi) ────────────────────────────────────────────────
            var kgBase = (home.GoalScoringRate + away.GoalScoringRate) / 2.0 - 5 + (totalExp >= 2.6 ? 8 : 0);
            var pKg = (int)Clamp(kgBase, 20, 88);
            var kgTag = "Karşılıklı gol eğilimi";
            Add(list, "KG Var", pKg, ScenarioFamily.Btts, 1.0, kgTag,
                h2hGollu ? "Geçmiş maçlar gollü" : "Hücum üretimi dengeli");
            Add(list, "KG Yok", 100 - pKg, ScenarioFamily.Btts, 0.85, "En az bir takım gol bulmakta zorlanabilir");

            // ── Takım gol (TeamGoals ailesi) ────────────────────────────────────
            var pHomeScore = (int)Clamp(home.GoalScoringRate * 0.7 + (100 - away.CleanSheetRate) * 0.3, 25, 92);
            var pAwayScore = (int)Clamp(away.GoalScoringRate * 0.7 + (100 - home.CleanSheetRate) * 0.3, 25, 92);
            Add(list, $"{homeName} Gol Atar", pHomeScore, ScenarioFamily.TeamGoals, 0.85, $"{homeName} hücumda üretken");
            Add(list, $"{homeName} Gol Atamaz", 100 - pHomeScore, ScenarioFamily.TeamGoals, 0.7, $"{awayName} savunması direngen");
            Add(list, $"{awayName} Gol Atar", pAwayScore, ScenarioFamily.TeamGoals, 0.85, $"{awayName} hücumda üretken");
            Add(list, $"{awayName} Gol Atamaz", 100 - pAwayScore, ScenarioFamily.TeamGoals, 0.7, $"{homeName} savunması direngen");

            // ── İlk yarı (Half ailesi) ──────────────────────────────────────────
            Add(list, "İlk Yarı 0.5 Üst", POver(iyExp, 0.5), ScenarioFamily.Half, 0.8, "Erken gol eğilimi");
            Add(list, "İlk Yarı 1.5 Alt", 100 - POver(iyExp, 1.5), ScenarioFamily.Half, 0.8, "İlk yarı temkinli tempo");
            var iyDraw = (int)Clamp(44 - Math.Abs(edge) * 0.12, 34, 46);
            var iyRem = 100 - iyDraw;
            var iyHome = (int)Math.Round(iyRem * Clamp(0.5 + edge / 140.0, 0.2, 0.8));
            Add(list, "İlk Yarı Ev Sahibi", iyHome, ScenarioFamily.Half, 0.75, strongTag);
            Add(list, "İlk Yarı Beraberlik", iyDraw, ScenarioFamily.Half, 0.75, "İlk yarı dengeli");
            Add(list, "İlk Yarı Deplasman", Math.Max(6, iyRem - iyHome), ScenarioFamily.Half, 0.75, strongTag);

            return list;
        }

        // ── Yardımcılar ─────────────────────────────────────────────────────────
        private static void Add(List<ScenarioCandidate> list, string market, int prob,
            ScenarioFamily family, double weight, params string[] tags)
        {
            var p = Math.Clamp(prob, 3, 97);
            list.Add(new ScenarioCandidate
            {
                Market = market,
                Probability = p,
                Confidence = ConfidenceOf(p),
                Family = family,
                Weight = weight,
                EvidenceTags = new List<string>(tags)
            });
        }

        private static string ConfidenceOf(int p) => p >= 72 ? "YÜKSEK" : p >= 58 ? "ORTA" : "DÜŞÜK";

        // Lojistik tabanlı over olasılığı.
        private static int POver(double totalExp, double line)
        {
            var x = (totalExp - line) * 1.15;
            var p = 100.0 / (1.0 + Math.Exp(-x));
            return (int)Math.Round(Clamp(p, 5, 95));
        }

        private static double Avg(double a, double b, double fallback)
        {
            var v = (a + b) / 2.0;
            return v <= 0 ? fallback : v;
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
    }
}
