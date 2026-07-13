using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.AI.Radar.Reasoning
{
    /// <summary>
    /// FORMAX Radar v3 — Intelligence Reasoning Layer (FORMAX'ın beyni).
    ///
    /// MatchIntelligenceContext'ten anlam çıkarır; LLM'e ham veri yerine sindirilmiş
    /// bir <see cref="IntelligencePack"/> hazırlar. Tamamen deterministik:
    ///   1) Signal Extraction  2) Contradiction Detection
    ///   3) Narrative Focus     4) Evidence Pack   5) Reasoning Confidence
    /// Yeni veri kaynağı eklemez; mevcut context üzerinde akıl yürütür.
    /// </summary>
    public sealed class ReasoningEngine
    {
        public IntelligencePack Build(MatchIntelligenceContext ctx)
        {
            var attack = (ctx.Stats.HomeGoalScoringRate + ctx.Stats.AwayGoalScoringRate) / 2;
            var defense = (ctx.Stats.HomeCleanSheetRate + ctx.Stats.AwayCleanSheetRate) / 2; // yüksek = güçlü savunma
            var newsVol = ctx.News.Volume24h;
            var importanceHigh = ctx.Importance.Level.Contains("Yüksek", StringComparison.OrdinalIgnoreCase)
                                 || ctx.Importance.WatchersCount >= 5000;

            var signals = ExtractSignals(ctx, attack, defense, newsVol, importanceHigh);
            var contradictions = DetectContradictions(ctx, attack, defense, newsVol, importanceHigh);
            var focus = BuildFocus(signals);
            var evidence = BuildEvidence(ctx, attack, defense, newsVol, importanceHigh);
            var confidence = ScoreConfidence(ctx, signals, contradictions, attack);

            return new IntelligencePack
            {
                MatchId = ctx.MatchId,
                HomeTeam = ctx.HomeTeam,
                AwayTeam = ctx.AwayTeam,
                League = ctx.League,
                KickoffUtc = ctx.KickoffUtc,
                WorldHeadline = ctx.WorldHeadline,
                ReasoningConfidence = confidence,
                Signals = signals,
                Contradictions = contradictions,
                NarrativeFocus = focus,
                Evidence = evidence,
                Scenarios = ctx.Scenarios.Select(s => new IntelligencePack.ScenarioInsight
                {
                    Market = s.Market,
                    Probability = s.Probability,
                    Confidence = s.Confidence,
                    Reason = s.EvidenceTags.Count > 0 ? string.Join(", ", s.EvidenceTags.Take(2)) : ""
                }).ToList()
            };
        }

        // 1) ── Signal Extraction ────────────────────────────────────────────────
        private static List<IntelligencePack.ReasonedSignal> ExtractSignals(
            MatchIntelligenceContext ctx, int attack, int defense, int newsVol, bool importanceHigh)
        {
            var s = new List<IntelligencePack.ReasonedSignal>();

            void Add(string name, int strength, int conf, string ev) =>
                s.Add(new IntelligencePack.ReasonedSignal
                {
                    Name = name,
                    Strength = Math.Clamp(strength, 0, 100),
                    Confidence = Math.Clamp(conf, 0, 100),
                    Evidence = ev
                });

            if (attack >= 60)
                Add("High Attack Tempo", attack,
                    Math.Min(ctx.Stats.HomeGoalScoringRate, ctx.Stats.AwayGoalScoringRate) >= 55 ? 85 : 70,
                    "İki tarafın hücum üretimi yüksek seyrediyor");

            if (defense <= 40)
                Add("Defensive Weakness", 100 - defense, 75, "Savunma istikrarı düşük");
            else if (defense >= 60)
                Add("Defensive Stability", defense, 75, "Savunmalar istikrarlı");

            if (ctx.Form.HomeFormScore >= 65)
                Add("Strong Home Form", ctx.Form.HomeFormScore, 80, "Ev sahibi form grafiği güçlü");
            if (ctx.Form.AwayFormScore >= 65)
                Add("Strong Away Form", ctx.Form.AwayFormScore, 80, "Deplasman form grafiği güçlü");

            if (newsVol >= 8)
                Add("High News Volume", Math.Min(100, 50 + newsVol * 3), 70, "Son 24 saatte haber hareketi yoğun");

            // FINAL — v2.1 Evidence sinyalleri Reasoning'e taşınır (ham haber değil).
            if (ctx.News.FromEvidence)
                foreach (var sig in ctx.News.Signals.Take(3))
                {
                    var (name, ev) = MapEvidenceSignal(sig);
                    if (name != null) Add(name, 70, 72, ev!);
                }

            if (importanceHigh)
                Add("High Match Importance", 80, 75, "Maç önem sinyali yüksek");

            if (string.Equals(ctx.Social.Level, "Yüksek", StringComparison.OrdinalIgnoreCase))
                Add("Community Attention", 75, 65, "FORMAX topluluk ilgisi ortalamanın üzerinde");

            if (Math.Abs(ctx.Form.HomeFormScore - ctx.Form.AwayFormScore) <= 6)
                Add("Balanced Contest", 60, 60, "Form göstergeleri birbirine yakın");

            return s.OrderByDescending(x => x.Strength).ToList();
        }

        // Evidence sinyalini (Transfer/Injury…) Reasoning sinyaline + Türkçe kanıta çevirir.
        private static (string? Name, string? Evidence) MapEvidenceSignal(string signal) => signal switch
        {
            "Injury" => ("Injury Watch", "Sakatlık haberleri gündemde"),
            "Transfer" => ("Transfer Activity", "Transfer hareketliliği öne çıkıyor"),
            "Lineup" => ("Lineup News", "Kadro/diziliş haberleri akışta"),
            "Coach" => ("Coach Focus", "Teknik ekibe dair gelişmeler var"),
            "Suspension" => ("Availability Risk", "Ceza/eksik oyuncu sinyali"),
            "Referee" => ("Referee Focus", "Hakem gündemi öne çıkıyor"),
            "Derby" => ("Derby Context", "Karşılaşma derbi/rekabet ekseninde"),
            "Pressure" => ("Pressure Context", "Maç üzerinde baskı/önem vurgusu"),
            "Fan Interest" => ("Fan Interest", "Taraftar ilgisi belirgin"),
            "Form" => ("Form Narrative", "Form üzerine haber yoğunluğu"),
            "Press Conference" => ("Pre-Match Talk", "Basın toplantısı açıklamaları"),
            "Schedule" => ("Schedule Note", "Maç programına dair gelişme"),
            _ => (null, null)
        };

        // 2) ── Contradiction Detection ──────────────────────────────────────────
        private static List<string> DetectContradictions(
            MatchIntelligenceContext ctx, int attack, int defense, int newsVol, bool importanceHigh)
        {
            var c = new List<string>();

            if (attack >= 60 && defense >= 60)
                c.Add("Hücum sinyalleri güçlü ama iki takım da savunmada istikrarlı; tempo beklentisi temkinli okunmalı.");

            if (ctx.Stats.HomeRank is int hr && ctx.Stats.AwayRank is int ar && hr > 0 && ar > 0)
            {
                var favoredHome = hr < ar;
                var favForm = favoredHome ? ctx.Form.HomeFormScore : ctx.Form.AwayFormScore;
                var othForm = favoredHome ? ctx.Form.AwayFormScore : ctx.Form.HomeFormScore;
                var favName = favoredHome ? ctx.HomeTeam : ctx.AwayTeam;
                if (othForm - favForm >= 8)
                    c.Add($"Sıralamada önde olan {favName}, son form göstergesinde geride; üstünlük tartışmalı.");
            }

            if (newsVol >= 8 && !importanceHigh)
                c.Add("Haber hacmi yüksek ancak maçın önem sinyali sınırlı; ilgi ile ağırlık örtüşmüyor.");

            var top = ctx.Scenarios.FirstOrDefault();
            if (attack >= 60 && top != null && top.Market.Contains("Alt", StringComparison.OrdinalIgnoreCase))
                c.Add("Hücum sinyali güçlü olsa da en güçlü senaryo düşük skor yönünde; veriler ayrışıyor.");

            return c;
        }

        // 3) ── Narrative Focus ──────────────────────────────────────────────────
        private static List<string> BuildFocus(List<IntelligencePack.ReasonedSignal> signals)
        {
            string? Map(string name) => name switch
            {
                "High Attack Tempo" => "Tempo ve hücum",
                "Defensive Weakness" => "Savunma kırılganlığı",
                "Defensive Stability" => "Savunma istikrarı",
                "Strong Home Form" or "Strong Away Form" => "Form",
                "High News Volume" => "Haber gündemi",
                "High Match Importance" => "Maç önemi",
                "Community Attention" => "Taraftar ilgisi",
                "Balanced Contest" => "Denge",
                _ => null
            };

            var focus = new List<string>();
            foreach (var sig in signals)
            {
                var f = Map(sig.Name);
                if (f != null && !focus.Contains(f)) focus.Add(f);
                if (focus.Count == 3) break;
            }
            if (focus.Count == 0) focus.Add("Genel maç bağlamı");
            return focus;
        }

        // 4) ── Evidence Pack ────────────────────────────────────────────────────
        private static List<IntelligencePack.EvidenceItem> BuildEvidence(
            MatchIntelligenceContext ctx, int attack, int defense, int newsVol, bool importanceHigh)
        {
            var e = new List<IntelligencePack.EvidenceItem>();
            void Add(string l, string v) => e.Add(new IntelligencePack.EvidenceItem { Label = l, Value = v });

            Add("Haber Hacmi", newsVol >= 8 ? "Yüksek" : newsVol >= 3 ? "Orta" : "Düşük");
            Add("Hücum Sinyali", attack >= 60 ? "Güçlü" : attack >= 45 ? "Orta" : "Zayıf");
            Add("Savunma Sinyali", defense >= 60 ? "Güçlü" : defense >= 45 ? "Orta" : "Zayıf");
            Add("Maç Önemi", importanceHigh ? "Yüksek" : ctx.Importance.WatchersCount >= 1000 ? "Orta" : "Düşük");
            if (!string.IsNullOrWhiteSpace(ctx.Social.Level))
                Add("Topluluk İlgisi", ctx.Social.Level);

            var top = ctx.Scenarios.FirstOrDefault();
            if (top != null)
            {
                Add("En Güçlü Senaryo", $"{top.Market} (%{top.Probability})");
                if (top.EvidenceTags.Count > 0)
                    Add("Senaryo Nedeni", string.Join(", ", top.EvidenceTags.Take(2)));
            }

            return e;
        }

        // 5) ── Reasoning Confidence (0–100) ─────────────────────────────────────
        private static int ScoreConfidence(
            MatchIntelligenceContext ctx, List<IntelligencePack.ReasonedSignal> signals,
            List<string> contradictions, int attack)
        {
            var score = 40;
            if (ctx.Scenarios.Count > 0) score += 10;   // senaryo verisi var
            if (attack > 0) score += 10;                 // istatistik verisi var
            if (ctx.H2H.Total > 0) score += 8;           // geçmiş veri var
            if (ctx.News.Volume24h > 0) score += 7;      // haber verisi var

            if (signals.Count > 0) score += signals[0].Strength / 10; // en güçlü sinyal katkısı (0–10)
            score -= contradictions.Count * 8;           // çelişki güveni düşürür

            return Math.Clamp(score, 0, 100);
        }
    }
}
