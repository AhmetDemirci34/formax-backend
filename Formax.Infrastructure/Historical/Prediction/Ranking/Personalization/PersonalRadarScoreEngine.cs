using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Personalization;

/// <summary>Base Radar Score'a kullanıcı ilgisini uygulayan kişiselleştirme motoru sözleşmesi (saf, deterministik).</summary>
public interface IPersonalRadarScoreEngine
{
    PersonalRadarScore Personalize(RadarScore baseScore, UserInterestSignal interest, PersonalRadarWeights weights);
}

/// <summary>
/// Kişiselleştirme motoru: Personal = Base + User Interest. SAF + DETERMİNİSTİK. Base Radar Score'a DOKUNMAZ
/// (BaseScore ayrı korunur, base breakdown olduğu gibi taşınır). User Interest yalnız EK katmandır ve daima
/// pozitiftir → PersonalScore ≥ BaseScore. Interest Decay burada uygulanır (config yarı-ömrü; mevcut interest
/// engine'i değiştirmeden): decay = 0.5^(ageDays / halfLife). Veri yoksa Personal = Base (değişmez).
/// Breakdown'daki tüm katkıların toplamı = PersonalScore.
/// </summary>
public sealed class PersonalRadarScoreEngine : IPersonalRadarScoreEngine
{
    public PersonalRadarScore Personalize(RadarScore baseScore, UserInterestSignal interest, PersonalRadarWeights weights)
    {
        var breakdown = new List<SignalContribution>(baseScore.Breakdown); // base korunur (kopya)

        // Kullanıcı verisi yok → Base ile çalış (değişmez).
        if (interest is null || !interest.HasData || interest.AffinityScore < weights.MinAffinityToApply)
        {
            breakdown.Add(new SignalContribution
            {
                Signal = "UserInterest",
                Value = 0,
                Weight = weights.UserInterestBonus,
                WeightedContribution = 0,
                DataAvailable = false,
                Note = "Data Not Available (kullanıcı ilgisi yok) — Base Radar Score"
            });

            return new PersonalRadarScore
            {
                MatchId = baseScore.MatchId,
                UserId = interest?.UserId ?? 0,
                BaseScore = baseScore.Score,
                PersonalScore = baseScore.Score,
                Level = baseScore.Level,
                Personalized = false,
                UserInterestApplied = 0,
                HiddenGem = baseScore.HiddenGem,
                Breakdown = breakdown
            };
        }

        // Interest Decay (config yarı-ömrü). Taze ilgi → 1.0; eskidikçe → 0'a iner.
        var age = Math.Max(0, interest.InterestAgeDays);
        var decay = weights.InterestDecayHalfLifeDays > 0
            ? Math.Clamp(Math.Pow(0.5, age / weights.InterestDecayHalfLifeDays), 0, 1)
            : 1.0;

        // effective ∈ [0,1] = ilgi yoğunluğu × decay → bonus.
        var effective = Math.Clamp(interest.AffinityScore / 100.0, 0, 1) * decay;
        var personalScore = Math.Round(Math.Clamp(baseScore.Score + weights.UserInterestBonus * effective, 0, 100), 4);
        var applied = Math.Round(personalScore - baseScore.Score, 4);

        breakdown.Add(new SignalContribution
        {
            Signal = "UserInterest",
            Value = Math.Round(effective, 4),
            Weight = weights.UserInterestBonus,
            WeightedContribution = applied,
            DataAvailable = true,
            Note = $"affinity={interest.AffinityScore} (takım {interest.TeamComponent}/lig {interest.LeagueComponent}/sinyal {interest.SignalComponent}), " +
                   $"decay={decay.ToString("0.000", CultureInfo.InvariantCulture)} (yaş {age.ToString("0.#", CultureInfo.InvariantCulture)}g)"
        });

        return new PersonalRadarScore
        {
            MatchId = baseScore.MatchId,
            UserId = interest.UserId,
            BaseScore = baseScore.Score,
            PersonalScore = personalScore,
            Level = Band(personalScore),
            Personalized = applied > 0,
            UserInterestApplied = applied,
            HiddenGem = baseScore.HiddenGem,
            Breakdown = breakdown
        };
    }

    // Base motorla aynı bantlama (Radar Score Engine'e dokunmadan; deterministik).
    private static RadarLevel Band(double score) => score switch
    {
        >= 75 => RadarLevel.MustWatch,
        >= 55 => RadarLevel.High,
        >= 35 => RadarLevel.Medium,
        _ => RadarLevel.Low
    };
}
