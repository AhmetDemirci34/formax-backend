using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Lineup;

public interface ILivingLineupEngine
{
    /// <param name="side">Bu takımın BU maçtaki tarafı: "Home" | "Away".</param>
    LivingLineupDto Analyze(int matchId, int teamId, string teamName, string side);
}

/// <summary>
/// Living Lineup Engine (Görev #014). Beklenen (tarih tahmini) ile resmi (açıklanan) 11'i
/// karşılaştırır. Resmi kadro yoksa graceful fallback (Announced=false). Tüm hesap backend'de.
/// </summary>
public sealed class LivingLineupEngine : ILivingLineupEngine
{
    private readonly IExpectedLineupEngine _expected;

    public LivingLineupEngine(IExpectedLineupEngine expected)
    {
        _expected = expected;
    }

    public LivingLineupDto Analyze(int matchId, int teamId, string teamName, string side)
    {
        var expected = _expected.PredictFromHistory(matchId, teamId, teamName, side);
        var official = _expected.GetOfficial(matchId, teamName, side);

        // 1) Resmi kadro açıklanmadı → karşılaştırma yok (graceful).
        if (official == null)
        {
            return new LivingLineupDto
            {
                TeamName = teamName,
                Announced = false,
                ExpectedFormation = expected.Formation,
                OfficialFormation = "",
                Formation = expected.Formation,
                ExpectedPlayers = expected.Players,
                OfficialPlayers = new(),
                Confidence = 0,
                AiSummary = $"{teamName}: Resmi kadro henüz açıklanmadı; karşılaştırma maç saatine yakın oluşacak.",
                Evidence = new() { new() { Label = "Durum", Detail = "Resmi kadro bekleniyor" } }
            };
        }

        // 2) Resmi var ama beklenen baz çizgisi yok (geçmiş kadro verisi yok) → kıyas yapılamaz.
        if (expected.Players.Count == 0)
        {
            return new LivingLineupDto
            {
                TeamName = teamName,
                Announced = true,
                ExpectedFormation = "",
                OfficialFormation = official.Formation,
                Formation = official.Formation,
                ExpectedPlayers = new(),
                OfficialPlayers = official.Players,
                Players = official.Players.Select(p => new LivingPlayerDto
                {
                    Number = p.Number, Name = p.Name, Position = p.Position, Role = p.Role, Status = "same"
                }).ToList(),
                Confidence = official.Confidence,
                AiSummary = $"{teamName}: Resmi kadro açıklandı; beklenen baz çizgisi olmadığından karşılaştırma yapılamadı.",
                Evidence = new() { new() { Label = "Durum", Detail = "Resmi kadro açıklandı (beklenen veri yok)" } }
            };
        }

        // 3) Karşılaştırma.
        var diff = LineupDifferenceAnalyzer.Compare(expected.Players, official.Players);
        var analysis = LineupComparisonService.Analyze(diff, expected.Formation, official.Formation, teamName);

        return new LivingLineupDto
        {
            TeamName = teamName,
            Announced = true,
            Formation = official.Formation,
            ExpectedFormation = expected.Formation,
            OfficialFormation = official.Formation,
            Players = diff.OfficialWithStatus,
            ExpectedPlayers = expected.Players,
            OfficialPlayers = official.Players,
            AddedPlayers = diff.Added,
            RemovedPlayers = diff.Removed,
            MovedPlayers = diff.Moved,
            UnchangedPlayers = diff.Unchanged,
            FormationChanged = analysis.FormationChanged,
            ChangeCount = analysis.ChangeCount,
            AffectedLines = analysis.AffectedLines,
            Confidence = official.Confidence, // açıklandı → kesin
            AiSummary = analysis.AiSummary,
            Evidence = analysis.Evidence
        };
    }
}
