using System;
using System.Collections.Generic;
using Formax.Application.AI.Context;
using Formax.Application.AI.Decision;

namespace Formax.Application.Services.Radar.Intelligence.Scenarios
{
    /// <summary>
    /// FORMAX Radar v2.2 — geniş market havuzunu DETERMİNİSTİK değerlendirir.
    /// Mevcut Statistical Intelligence (TeamComparison + H2H + GücSkoru) üzerine kurulur;
    /// yeni veri kaynağı eklemez. Her market için olasılık + güven + kanıt etiketi üretir.
    ///
    /// Basit, açıklanabilir bir beklenen-gol modeli kullanır (bahis hassasiyeti değil,
    /// senaryo SIRALAMASI hedeflenir). LLM bu sayılara dokunmaz.
    ///
    /// FORMAX BEYNİ (Evolution): <see cref="BuildDecisionPackage"/> aynı UnifiedMatchAiContext'ten
    /// TAM AI Decision Package üretir (Match DNA + 13 olasılık + senaryolar + güven + risk +
    /// açıklanabilirlik + canlı projeksiyon). Mevcut <see cref="Evaluate"/> (DTO+LLM geri-uyum)
    /// DEĞİŞMEDEN korunur. Modüller saf/stateless → motor onları içeride kompoze eder (DI'a dokunmaz).
    /// </summary>
    public sealed class MarketProbabilityEngine
    {
        // FORMAX BEYNİ — Decision Package boru hattı (saf/stateless modüller kompoze edilir).
        private readonly DecisionPackageBuilder _decisionBuilder = new();

        /// <summary>
        /// FORMAX'ın TEK kararı: Unified AI Context'i okuyup AI Decision Package üretir.
        /// Deterministik (aynı context → aynı paket). Repo/provider/ham veri YOK; LLM'e dokunmaz.
        /// </summary>
        public AiDecisionPackage BuildDecisionPackage(UnifiedMatchAiContext ctx)
            => _decisionBuilder.Build(ctx);

        public IReadOnlyList<ScenarioCandidate> Evaluate(UnifiedMatchAiContext ctx)
        {
            // FORMAX AI Evolution — motor artık YALNIZ UnifiedMatchAiContext okur:
            // repository yok, provider yok, ham istatistik hesabı yok. Aşağıdaki beklenen-gol
            // matematiği FAZ 1'de BİREBİR korunur; girdiler context'ten alias'lanır.
            var home     = ctx.Home;
            var away     = ctx.Away;
            var h2h      = ctx.H2H;
            var gucSkoru = ctx.GucSkoru;
            var homeName = ctx.HomeName;
            var awayName = ctx.AwayName;

            var list = new List<ScenarioCandidate>();

            // ── Beklenen goller (clamp ile güvenli) ──────────────────────────────
            // FAZ 2 — veri eksik olduğunda sabit 1.2/1.0 yerine LİG-UYARLI baseline'a düş
            // (context'ten). Ev avantajı asimetrisi korunur. Veri VARSA çıktı DEĞİŞMEZ
            // (Avg gerçek değeri döndürür; baseline yalnız (a+b)<=0 iken devreye girer).
            var baseline = ctx.Strength.LeagueGoalBaseline;
            var fbHome = baseline > 0 ? Clamp(baseline * 1.09, 0.4, 2.6) : 1.2;
            var fbAway = baseline > 0 ? Clamp(baseline * 0.91, 0.4, 2.6) : 1.0;
            var expHome = Clamp(Avg(home.AvgGoalsFor, away.AvgGoalsAgainst, fbHome), 0.2, 3.5);
            var expAway = Clamp(Avg(away.AvgGoalsFor, home.AvgGoalsAgainst, fbAway), 0.2, 3.5);

            // FAZ 3 — KADRO UYGUNLUĞU (yalnız gerçek MatchPlayerStatuses verisi varsa): eksik
            // oyuncular ilgili takımın gol üretimini kısar. GücSkoru bu sinyali İÇERMEZ (yalnız
            // skor geçmişi) → çift sayım yok. HasData=false ise hiçbir etki yok (davranış aynı).
            if (ctx.Availability.HasData)
            {
                var homePen = Math.Min(0.30, ctx.Availability.HomeKeyAbsences * 0.06);
                var awayPen = Math.Min(0.30, ctx.Availability.AwayKeyAbsences * 0.06);
                expHome = Clamp(expHome * (1.0 - homePen), 0.2, 3.5);
                expAway = Clamp(expAway * (1.0 - awayPen), 0.2, 3.5);
            }

            var totalExp = expHome + expAway;
            var iyExp = totalExp * 0.42;

            var edge = gucSkoru - 50; // + ev sahibi lehine
            // FAZ 3 — kadro dengesi maç sonucuna: rakip daha çok eksikse ev lehine (bounded ±15).
            if (ctx.Availability.HasData)
                edge += Math.Clamp((ctx.Availability.AwayKeyAbsences - ctx.Availability.HomeKeyAbsences) * 3, -15, 15);
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
            // İLK YARI GOL MARKETLERİ ÜRETİLMEZ (ürün kararı, 18.08.2026): "İlk Yarı 0.5 Üst"
            // ("ilk yarıda gol var" ile aynı anlam) ve "İlk Yarı 1.5 Alt" (İY gol toplamı)
            // buradan kaldırıldı. Kullanıcıya ilk yarı GOL marketi gösterilmiyor; frontend'de
            // gizlenmiyor, kaynakta üretilmiyor.
            //
            // İlk yarı SONUCU marketleri (Ev Sahibi / Beraberlik / Deplasman) gol marketi
            // DEĞİLDİR ve korunur.
            var iyDraw = (int)Clamp(44 - Math.Abs(edge) * 0.12, 34, 46);
            var iyRem = 100 - iyDraw;
            var iyHome = (int)Math.Round(iyRem * Clamp(0.5 + edge / 140.0, 0.2, 0.8));
            Add(list, "İlk Yarı Ev Sahibi", iyHome, ScenarioFamily.Half, 0.75, strongTag);
            Add(list, "İlk Yarı Beraberlik", iyDraw, ScenarioFamily.Half, 0.75, "İlk yarı dengeli");
            Add(list, "İlk Yarı Deplasman", Math.Max(6, iyRem - iyHome), ScenarioFamily.Half, 0.75, strongTag);

            // FAZ 2 — DÜRÜST GÜVEN: bağlam verisi zayıfsa (eksik gol geçmişi) güven etiketlerini
            // bir kademe kıs. Olasılık/sıralama DEĞİŞMEZ (ranking edge'e göre); yalnız kullanıcıya
            // gösterilen güven, gerçek veri kadar iddialı olur (açıklanabilir AI).
            if (ctx.DataQuality < 1.0)
                foreach (var c in list)
                    c.Confidence = Downgrade(c.Confidence, ctx.DataQuality);

            return list;
        }

        // Güven bir kademe aşağı: veri kısmen eksikse (0.5≤dq<1) yalnız YÜKSEK→ORTA;
        // çok eksikse (dq<0.5) YÜKSEK→ORTA, ORTA→DÜŞÜK.
        private static string Downgrade(string confidence, double dq)
        {
            if (dq < 0.5)
                return confidence == "YÜKSEK" ? "ORTA" : "DÜŞÜK";
            return confidence == "YÜKSEK" ? "ORTA" : confidence;
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
