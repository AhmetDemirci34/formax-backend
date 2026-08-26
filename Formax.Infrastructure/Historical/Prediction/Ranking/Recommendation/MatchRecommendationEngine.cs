using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Recommendation;

/// <summary>Bir maç için öneri (skor+tag+gerekçe) üreten motor sözleşmesi (saf, deterministik).</summary>
public interface IMatchRecommendationEngine
{
    MatchRecommendation Recommend(RecommendationInput input, RecommendationWeights weights);
}

/// <summary>
/// Recommendation Engine — Discovery Feed'in ÜST açıklama katmanı. SIRALAMA YAPMAZ (o Discovery'nin işi); "bu maçı
/// neden öneriyorum?" cevabını üretir. MEVCUT sinyalleri (Base Radar breakdown + Personal Score + probability +
/// confidence + Discovery) OKUR, hiçbirini değiştirmez. SAF + DETERMİNİSTİK. Tag/gerekçe yalnız verisi OLAN
/// sinyalden ve config eşiğini aşınca üretilir → uydurma yok (ör. Derby verisi yoksa Derby tag'i çıkmaz).
/// Breakdown toplamı = RecommendationScore.
/// </summary>
public sealed class MatchRecommendationEngine : IMatchRecommendationEngine
{
    public MatchRecommendation Recommend(RecommendationInput input, RecommendationWeights w)
    {
        var sig = input.Radar.Breakdown.ToDictionary(b => b.Signal, b => b, StringComparer.Ordinal);
        double SigVal(string name) => sig.TryGetValue(name, out var s) && s.DataAvailable ? s.Value : -1;
        bool Has(string name, double thr) { var v = SigVal(name); return v >= 0 && v >= thr; }

        var confidence = Math.Clamp(input.Confidence.ConfidenceScore, 0, 1);
        var interestDelta = input.Discovery.PersonalScore - input.Discovery.BaseScore; // Personal−Base (kişiselleştirme)
        var hiddenGem = input.Discovery.HiddenGem || input.Radar.HiddenGem;

        // ── TAG'ler (yalnız gerçek veri + eşik) ──
        var tags = new List<string>();
        var reasons = new List<string>();
        void Add(string tag, string reason) { tags.Add(tag); reasons.Add(reason); }

        if (hiddenGem) Add("Hidden Gem", "Hidden Gem — radar-altı kaliteli maç");
        if (Has("Competitiveness", w.TightMatchThreshold)) Add("Tight Match", $"Çekişmeli maç — sonuç belirsiz (rekabet {Pct(SigVal("Competitiveness"))})");
        if (Has("GoalPotential", w.GoalFestThreshold)) Add("Goal Fest", $"Yüksek gol beklentisi ({Pct(SigVal("GoalPotential"))})");
        if (Has("Form", w.InFormThreshold)) Add("In Form", $"İki takım da formda ({Pct(SigVal("Form"))})");
        if (Has("Upset", w.UpsetThreshold)) Add("Upset Potential", $"Sürpriz ihtimali ({Pct(SigVal("Upset"))})");
        if (Has("TeamStrength", w.EliteStrengthThreshold) && Has("LeagueQuality", w.EliteLeagueThreshold))
            Add("Elite Match", "Elit eşleşme — güçlü takımlar, kaliteli lig");
        if (Has("Stakes", w.TitleRaceThreshold)) Add("Title Race", $"Zirve yarışı — önemli maç ({Pct(SigVal("Stakes"))})");
        if (Has("H2HIntensity", w.H2HThreshold)) Add("H2H Rivalry", $"Çekişmeli geçmiş karşılaşmalar ({Pct(SigVal("H2HIntensity"))})");
        if (Has("Derby", w.DerbyThreshold)) Add("Derby", "Derbi maçı"); // Derby verisi yoksa asla üretilmez
        if (confidence >= w.HighConfidenceThreshold) Add("High Confidence", $"Yüksek güvenilir tahmin ({Pct(confidence)})");
        if (interestDelta > w.InterestPointsThreshold) Add("Your Interest", "İlgi alanına uygun (kişiselleştirildi)");

        // ── RECOMMENDATION SCORE (Discovery'yi DEĞİŞTİRMEZ; ayrı açıklama skoru) ──
        var relevance = Math.Clamp(input.Discovery.PersonalScore / 100.0, 0, 1);
        var positive = new[] { "Competitiveness", "GoalPotential", "Form", "Upset", "Stakes", "TeamStrength" }
            .Select(SigVal).Where(v => v >= 0).DefaultIfEmpty(0).Max();
        var distinctiveness = Math.Clamp(positive, 0, 1);

        var totalW = w.RelevanceWeight + w.ConfidenceWeight + w.DistinctivenessWeight;
        var relC = totalW > 0 ? 100.0 * w.RelevanceWeight * relevance / totalW : 0;
        var confC = totalW > 0 ? 100.0 * w.ConfidenceWeight * confidence / totalW : 0;
        var distC = totalW > 0 ? 100.0 * w.DistinctivenessWeight * distinctiveness / totalW : 0;
        var baseScore = relC + confC + distC;
        var gemApplied = hiddenGem ? Math.Max(0, Math.Min(w.HiddenGemBonus, 100.0 - baseScore)) : 0.0;
        var score = Math.Round(Math.Clamp(baseScore + gemApplied, 0, 100), 4);

        var breakdown = new List<SignalContribution>
        {
            Row("Relevance", relevance, w.RelevanceWeight, relC, "Personal Radar Score"),
            Row("Confidence", confidence, w.ConfidenceWeight, confC, "tahmin güvenilirliği"),
            Row("Distinctiveness", distinctiveness, w.DistinctivenessWeight, distC, "en güçlü sinyal"),
            Row("HiddenGem", hiddenGem ? 1 : 0, w.HiddenGemBonus, gemApplied, hiddenGem ? "Hidden Gem bonusu" : "-")
        };

        return new MatchRecommendation
        {
            MatchId = input.MatchId,
            RecommendationScore = score,
            Level = Band(score, w),
            HiddenGem = hiddenGem,
            Tags = tags,
            Reasons = reasons,
            Breakdown = breakdown,
            DiscoveryRank = input.Discovery.DiscoveryRank,
            DiscoveryScore = input.Discovery.DiscoveryScore,
            PersonalScore = input.Discovery.PersonalScore,
            BaseScore = input.Discovery.BaseScore
        };
    }

    private static SignalContribution Row(string name, double value, double weight, double contribution, string note)
        => new() { Signal = name, Value = Math.Round(value, 4), Weight = weight, WeightedContribution = Math.Round(contribution, 4), DataAvailable = true, Note = note };

    private static string Pct(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static RecommendationLevel Band(double score, RecommendationWeights w) =>
        score >= w.StronglyAt ? RecommendationLevel.StronglyRecommended
        : score >= w.RecommendedAt ? RecommendationLevel.Recommended
        : score >= w.OptionalAt ? RecommendationLevel.Optional
        : RecommendationLevel.Low;
}
