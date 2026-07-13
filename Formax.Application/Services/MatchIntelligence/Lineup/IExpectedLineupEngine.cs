using Formax.Application.DTOs.MatchIntelligence;

namespace Formax.Application.Services.MatchIntelligence.Lineup;

/// <summary>
/// Expected Lineup Engine — bir takımın muhtemel ilk 11'ini üretir (Görev #013).
/// Living Lineup (Görev #014) için beklenen (tarih) ile resmi (açıklanan) ayrı ayrı alınabilir.
/// </summary>
public interface IExpectedLineupEngine
{
    /// <param name="side">Bu takımın BU maçtaki tarafı: "Home" | "Away".</param>
    ExpectedLineupDto Predict(int matchId, int teamId, string teamName, string side);

    /// <summary>Resmi kadrodan bağımsız, HER ZAMAN geçmişten tahmin (beklenen 11 baz çizgisi).</summary>
    ExpectedLineupDto PredictFromHistory(int matchId, int teamId, string teamName, string side);

    /// <summary>Resmi açıklanan 11 (yayınlanmadıysa null).</summary>
    ExpectedLineupDto? GetOfficial(int matchId, string teamName, string side);
}
