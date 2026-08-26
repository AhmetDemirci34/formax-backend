using Formax.Application.DTOs.Matches;
using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Outlook;

public interface IMatchOutlookEngine
{
    MatchOutlookDto Analyze(MatchDetailDto detail);
}

/// <summary>
/// Match Outlook Engine (Görev #016). Maçın genel görünümünü backend'de üretir:
/// form, performans, gol eğilimi, tempo, denge, momentum, AI güveni, AI değerlendirmesi, evidence.
/// Kaynak: mevcut MatchDetailDto (Comparison + LastMatches + AiNarrative). Frontend hesaplama yapmaz.
/// </summary>
public sealed class MatchOutlookEngine : IMatchOutlookEngine
{
    public MatchOutlookDto Analyze(MatchDetailDto d)
    {
        var homeName = d.HomeTeam?.Name ?? "Ev sahibi";
        var awayName = d.AwayTeam?.Name ?? "Deplasman";
        var hc = d.Comparison?.Home ?? new TeamComparisonDto();
        var ac = d.Comparison?.Away ?? new TeamComparisonDto();
        var nar = d.AiNarrative;

        var home = new OutlookTeamDto
        {
            Team = homeName,
            Form = FormAnalyzer.Recent(d.HomeTeamLastMatches),
            Performance = PerformanceAnalyzer.Label(hc, isHome: true),
            PerformanceScore = PerformanceAnalyzer.Score(hc),
            GoalsFor = hc.AvgGoalsFor,
            GoalsAgainst = hc.AvgGoalsAgainst,
        };
        var away = new OutlookTeamDto
        {
            Team = awayName,
            Form = FormAnalyzer.Recent(d.AwayTeamLastMatches),
            Performance = PerformanceAnalyzer.Label(ac, isHome: false),
            PerformanceScore = PerformanceAnalyzer.Score(ac),
            GoalsFor = ac.AvgGoalsFor,
            GoalsAgainst = ac.AvgGoalsAgainst,
        };

        var goalTrend = GoalTrendAnalyzer.Trend(hc, ac, d.H2H);
        var tempo = TempoAnalyzer.Tempo(hc, ac);
        var balance = BalanceAnalyzer.Balance(hc, ac);
        var momentum = MomentumAnalyzer.Momentum(d.HomeTeamLastMatches, d.AwayTeamLastMatches);
        var confidence = nar != null && nar.ReasoningConfidence > 0
            ? nar.ReasoningConfidence
            : DeriveConfidence(hc, ac);

        var heroOutlook = OutlookSummaryGenerator.Hero(balance, tempo, homeName, awayName, nar?.WhyThisMatch);
        var aiSummary = OutlookSummaryGenerator.Summary(balance, tempo, momentum, goalTrend, home, away);
        var evidence = OutlookEvidenceBuilder.Build(home, away, goalTrend, tempo, momentum);

        return new MatchOutlookDto
        {
            Home = home,
            Away = away,
            GoalTrend = goalTrend,
            Tempo = tempo,
            Balance = balance,
            Momentum = momentum,
            ConfidenceSummary = new OutlookConfidenceDto { Label = "Güven", Value = confidence },
            HeroOutlook = heroOutlook,
            AiSummary = aiSummary,
            Evidence = evidence,
            Confidence = confidence,
        };
    }

    /// <summary>
    /// Narrative güveni yoksa: veri ayrışmasından türetilmiş güven.
    ///
    /// YETERLİLİK ÖLÇÜTÜ ARTIK ÖRNEKLEM SAYISI. Eskiden "FormScore &gt; 0 ya da AvgGoalsFor &gt; 0"
    /// bakılıyordu; hiç maçı olmayan takım da rakibinin değerleri yüzünden bu testi geçiyor ve
    /// ölçülmemiş bir ayrışmadan güven üretiliyordu. Kanıt eşiği tüm yüzeylerle ortaktır.
    /// </summary>
    private static int DeriveConfidence(TeamComparisonDto home, TeamComparisonDto away)
    {
        if (!OutlookEvidence.Both(home, away)) return 0;
        var separation = Math.Abs(home.FormScore - away.FormScore);
        return Math.Clamp(55 + Math.Min(30, separation * 2), 0, 90);
    }
}
