using Formax.Application.DTOs.Matches;

namespace Formax.Application.Interfaces;

/// <summary>
/// FAZ-1: Oynanma Skoru Matematiği
/// Bu bir tahmin/oran/yüzde değildir.
/// UI'ya sayısal kesinlik vermeden "band" ve "label" üretir.
/// </summary>
public interface IPlayedScoreCalculator
{
    PlayedScoreResult CalculateForListItem(MatchListItemDto match);
}

public sealed class PlayedScoreResult
{
    public int RawScore { get; init; }
    public string Band { get; init; } = "";
    public string Label { get; init; } = "";
}
