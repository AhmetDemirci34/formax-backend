using System.Collections.Generic;

namespace Formax.Infrastructure.Persistence.H2H;

/// <summary>Geçmiş bir karşılaşmanın sadeleştirilmiş sonucu (provider-bağımsız).</summary>
public sealed record H2HPastMeeting
{
    public required int HomeTeamId { get; init; }
    public required int AwayTeamId { get; init; }
    public required int HomeScore { get; init; }
    public required int AwayScore { get; init; }
}

/// <summary>H2H türetiminin sonucu: mevcut fikstürün EV SAHİBİ takımı perspektifinden.</summary>
public sealed record GdpH2HResult
{
    public int Played { get; init; }
    public int HomeWins { get; init; }
    public int Draws { get; init; }
    public int AwayWins { get; init; }
    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// İki takımın geçmiş karşılaşmalarından H2H özetini TÜRETİR. Saf fonksiyon: DB/IO yok,
/// tek merkezde test edilebilir. Kaynak = canonical Match geçmişi (provider değil) → GDP'nin
/// tek-sezon CSV sınırına takılmaz. Sonuç, mevcut fikstürün ev sahibi takımı perspektifindedir.
/// </summary>
public static class H2HSummaryCalculator
{
    public static GdpH2HResult Compute(
        IReadOnlyList<H2HPastMeeting> meetings,
        int homeTeamId,
        int awayTeamId,
        string? homeName,
        string? awayName)
    {
        int played = 0, homeWins = 0, draws = 0, awayWins = 0;

        foreach (var m in meetings)
        {
            // Yalnız bu iki takım arasındaki maçlar (her iki ev/deplasman yönünde).
            var isPair =
                (m.HomeTeamId == homeTeamId && m.AwayTeamId == awayTeamId) ||
                (m.HomeTeamId == awayTeamId && m.AwayTeamId == homeTeamId);
            if (!isPair)
                continue;

            played++;

            // Mevcut fikstürün EV SAHİBİ takımının o maçtaki attığı/yediği (oynadığı yerden bağımsız).
            var homeGoals = m.HomeTeamId == homeTeamId ? m.HomeScore : m.AwayScore;
            var oppGoals = m.HomeTeamId == homeTeamId ? m.AwayScore : m.HomeScore;

            if (homeGoals > oppGoals) homeWins++;
            else if (homeGoals == oppGoals) draws++;
            else awayWins++;
        }

        var h = string.IsNullOrWhiteSpace(homeName) ? "Ev" : homeName!;
        var a = string.IsNullOrWhiteSpace(awayName) ? "Deplasman" : awayName!;
        var summary = played == 0
            ? $"{h} - {a}: geçmiş karşılaşma yok."
            : $"Son {played} karşılaşma — {h}: {homeWins}G {draws}B {awayWins}M ({a} karşısında).";

        return new GdpH2HResult
        {
            Played = played,
            HomeWins = homeWins,
            Draws = draws,
            AwayWins = awayWins,
            Summary = summary
        };
    }
}
