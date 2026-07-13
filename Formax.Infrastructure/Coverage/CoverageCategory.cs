namespace Formax.Infrastructure.Coverage;

/// <summary>
/// Bir maç için doluluk durumu takip edilen veri kategorileri.
/// Yeni kategori eklemek bu listeyi genişletmekle olur; motor tüm değerleri otomatik kapsar.
/// </summary>
public enum CoverageCategory
{
    Fixture = 0,
    Teams = 1,
    Competition = 2,
    Venue = 3,
    Referee = 4,
    Lineups = 5,
    Coaches = 6,
    Players = 7,
    Injuries = 8,
    Suspensions = 9,
    Statistics = 10,
    Standings = 11,
    H2H = 12,
    News = 13,
    Live = 14,
    Weather = 15
}
