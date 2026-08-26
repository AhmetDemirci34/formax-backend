using System;
using System.Collections.Generic;
using Formax.Infrastructure.Historical.Features;

namespace Formax.Infrastructure.Historical.Prediction.Ranking;

/// <summary>Radar Score üreten motor sözleşmesi (saf, deterministik).</summary>
public interface IRadarScoreEngine
{
    RadarScore Score(RadarInput input, RadarWeights weights);
}

/// <summary>
/// Match Ranking Engine çekirdeği. "Bugün bu maç ne kadar izlemeye değer?" sorusuna, SADECE olasılık değil
/// birden çok watchability sinyalini birleştirerek 0-100 Radar Score üretir. Tamamen SAF + DETERMİNİSTİK:
/// aynı girdi + aynı ağırlık → aynı skor. Sabit normalizasyon sınırları belgelidir. Hiçbir sinyal uydurma
/// değildir; veri yoksa sinyal nötrdür ve paydadan düşer (breakdown'da "Data Not Available"). Confidence puan
/// DEĞİL, olasılık-türevli sinyaller için güvenilirlik çarpanıdır.
/// </summary>
public sealed class RadarScoreEngine : IRadarScoreEngine
{
    // ── Deterministik normalizasyon sınırları ──
    private const double EloMin = 1000, EloMax = 2200;         // takım gücü
    private const double EloDiffScale = 400;                    // güç dengesi / upset
    private const double LeagueStrengthMin = 1300, LeagueStrengthMax = 1900;
    private const double Ln3 = 1.0986122886681098;             // ln(3) — 3-sınıf max entropi

    public RadarScore Score(RadarInput input, RadarWeights w)
    {
        var f = input.Features;
        var home = f.Home;
        var away = f.Away;
        var p = input.Probability;

        // Confidence = güvenilirlik kapısı (puan değil). reliability = 1 − m·(1 − confidence).
        var reliability = Math.Clamp(1.0 - w.ConfidenceMultiplier * (1.0 - input.Confidence.ConfidenceScore), 0.0, 1.0);

        var signals = new List<Raw>(10);

        // 1) Competitiveness — sonuç belirsizliği (normalize entropi). Olasılık-türevli → kapılı.
        signals.Add(Raw.On("Competitiveness", NormalizedEntropy(p), w.Competitiveness, gated: true));

        // 2) GoalPotential — gol beklentisi (Over2.5 + BTTS + hücum + gol ort.).
        var over = Avg(home.Over25Rate, away.Over25Rate);
        var btts = Avg(home.BTTSRate, away.BTTSRate);
        var off = Avg(home.OffensiveRating, away.OffensiveRating);
        var goals = Math.Clamp((home.GoalsScoredAverage + away.GoalsScoredAverage) / 6.0, 0, 1);
        signals.Add(Raw.On("GoalPotential", 0.35 * over + 0.25 * btts + 0.20 * off + 0.20 * goals, w.GoalPotential));

        // 3) Form — iki takımın güncel formu + momentumu.
        var formVal = 0.6 * Avg(home.Last5Form, away.Last5Form) + 0.4 * Avg(home.RecentMomentum, away.RecentMomentum);
        signals.Add(Raw.On("Form", Math.Clamp(formVal, 0, 1), w.Form));

        // 4) Stakes — sıra-tabanlı önem (üst sıra yarışı + sıra yakınlığı). Sıra yoksa veri yok.
        if (home.LeaguePosition is int hr and > 0 && away.LeaguePosition is int ar and > 0)
        {
            var avgRank = (hr + ar) / 2.0;
            var topRace = 1.0 - Math.Clamp((avgRank - 1) / 8.0, 0, 1);     // rank 1 → 1.0, rank 9 → 0
            var closeness = 1.0 - Math.Clamp(Math.Abs(hr - ar) / 10.0, 0, 1);
            signals.Add(Raw.On("Stakes", Math.Clamp(0.5 * topRace + 0.5 * closeness, 0, 1), w.Stakes));
        }
        else signals.Add(Raw.Off("Stakes", w.Stakes, "Data Not Available (lig sırası yok)"));

        // 5) TeamStrength — ortalama Elo (yıldız gücü).
        if (home.Elo is double he && away.Elo is double ae)
            signals.Add(Raw.On("TeamStrength", Norm((he + ae) / 2.0, EloMin, EloMax), w.TeamStrength));
        else signals.Add(Raw.Off("TeamStrength", w.TeamStrength, "Data Not Available (Elo yok)"));

        // 6) PowerBalance — maç öncesi parite (1 − |Elo farkı|).
        var eloDiff = f.EloDifference ?? ((home.Elo is double h2 && away.Elo is double a2) ? h2 - a2 : (double?)null);
        if (eloDiff is double ed)
            signals.Add(Raw.On("PowerBalance", 1.0 - Math.Clamp(Math.Abs(ed) / EloDiffScale, 0, 1), w.PowerBalance));
        else signals.Add(Raw.Off("PowerBalance", w.PowerBalance, "Data Not Available (Elo farkı yok)"));

        // 7) H2HIntensity — geçmiş karşılaşma zenginliği (denge + gol + örneklem).
        if (f.HeadToHeadSampleSize >= 1)
        {
            var balance = 1.0 - Math.Abs(2.0 * f.HeadToHeadWinRate - 1.0);
            var h2hGoals = Math.Clamp(f.HeadToHeadGoalsAverage / 3.5, 0, 1);
            var sampleConf = Math.Clamp(f.HeadToHeadSampleSize / 5.0, 0, 1);
            signals.Add(Raw.On("H2HIntensity", (0.5 * balance + 0.5 * h2hGoals) * (0.5 + 0.5 * sampleConf), w.H2HIntensity));
        }
        else signals.Add(Raw.Off("H2HIntensity", w.H2HIntensity, "Data Not Available (H2H geçmişi yok)"));

        // 8) Upset — zayıf takımın (Elo/olasılık) kazanma ihtimali. Olasılık-türevli → kapılı.
        var homeStronger = eloDiff is double e2 ? e2 >= 0 : p[0] >= p[2];
        var underdogProb = homeStronger ? p[2] : p[0];
        signals.Add(Raw.On("Upset", Math.Clamp(underdogProb * 2.0, 0, 1), w.Upset, gated: true));

        // 9) LeagueQuality — lig gücü (LeagueStrength = lig-sezon ortalama Elo). Bu kapsamda GERÇEK.
        if (f.LeagueStrength > 0)
            signals.Add(Raw.On("LeagueQuality", Norm(f.LeagueStrength, LeagueStrengthMin, LeagueStrengthMax), w.LeagueQuality));
        else signals.Add(Raw.Off("LeagueQuality", w.LeagueQuality, "Data Not Available (lig gücü yok)"));

        // 10) Derby — rivalry verisi yok → nötr, breakdown'da açıkça işaretli.
        signals.Add(Raw.Off("Derby", w.Derby, "Data Not Available (rivalry verisi yok)"));

        // ── Agregasyon: yalnız veri olan sinyaller paydaya girer ──
        double totalWeight = 0;
        foreach (var s in signals) if (s.Available) totalWeight += s.Weight;

        var breakdown = new List<SignalContribution>(signals.Count + 1);
        double baseScore = 0;
        foreach (var s in signals)
        {
            double contribution = 0;
            if (s.Available && totalWeight > 0)
            {
                var effective = s.Gated ? s.Value * reliability : s.Value;
                contribution = 100.0 * s.Weight * effective / totalWeight;
                baseScore += contribution;
            }
            breakdown.Add(new SignalContribution
            {
                Signal = s.Name,
                Value = s.Value,
                Weight = s.Weight,
                WeightedContribution = Math.Round(contribution, 4),
                DataAvailable = s.Available,
                Note = s.Note
            });
        }

        // ── Hidden Gem: yüksek içsel kalite (rekabet+gol+form) VE düşük dikkat (düşük Stakes) ──
        var quality = (Get(signals, "Competitiveness") + Get(signals, "GoalPotential") + Get(signals, "Form")) / 3.0;
        var stakesVal = AvailableValue(signals, "Stakes"); // yoksa 0 → radar-altı sayılır
        var isGem = quality >= w.HiddenGemQualityThreshold && stakesVal <= w.HiddenGemStakesCeiling;

        var appliedBonus = 0.0;
        if (isGem)
        {
            appliedBonus = Math.Min(w.HiddenGemBonus, 100.0 - baseScore);
            if (appliedBonus < 0) appliedBonus = 0;
        }
        breakdown.Add(new SignalContribution
        {
            Signal = "HiddenGem",
            Value = Math.Round(quality, 4),
            Weight = 0,
            WeightedContribution = Math.Round(appliedBonus, 4),
            DataAvailable = true,
            Note = isGem ? "Hidden Gem rozeti (yüksek kalite, düşük dikkat)" : "tetiklenmedi"
        });

        var finalScore = Math.Round(Math.Clamp(baseScore + appliedBonus, 0, 100), 4);

        return new RadarScore
        {
            MatchId = input.MatchId,
            Score = finalScore,
            Level = Band(finalScore),
            HiddenGem = isGem,
            HiddenGemBonusApplied = Math.Round(appliedBonus, 4),
            Breakdown = breakdown
        };
    }

    // ── Yardımcılar ──

    private static double NormalizedEntropy(double[] p)
    {
        double h = 0;
        foreach (var pc in p) if (pc > 0) h += -pc * Math.Log(pc);
        return Math.Clamp(h / Ln3, 0, 1);
    }

    private static double Avg(double a, double b) => (a + b) / 2.0;
    private static double Norm(double v, double min, double max) => Math.Clamp((v - min) / (max - min), 0, 1);

    private static double Get(List<Raw> signals, string name)
    {
        foreach (var s in signals) if (s.Name == name) return s.Value;
        return 0;
    }
    private static double AvailableValue(List<Raw> signals, string name)
    {
        foreach (var s in signals) if (s.Name == name) return s.Available ? s.Value : 0;
        return 0;
    }

    private static RadarLevel Band(double score) => score switch
    {
        >= 75 => RadarLevel.MustWatch,
        >= 55 => RadarLevel.High,
        >= 35 => RadarLevel.Medium,
        _ => RadarLevel.Low
    };

    private readonly record struct Raw(string Name, double Value, double Weight, bool Available, bool Gated, string? Note)
    {
        public static Raw On(string name, double value, double weight, bool gated = false)
            => new(name, Math.Clamp(value, 0, 1), weight, true, gated, null);
        public static Raw Off(string name, double weight, string note)
            => new(name, 0, weight, false, false, note);
    }
}
