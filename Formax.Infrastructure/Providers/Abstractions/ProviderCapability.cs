namespace Formax.Infrastructure.Providers.Abstractions;

/// <summary>
/// Bir sağlayıcının sunabileceği veri türleri.
/// Bir sağlayıcı birden fazla yeteneğe sahip olabilir; manifest içinde küme olarak tutulur.
/// Yeni yetenekler bu listeye eklenerek genişletilir — başka hiçbir yeri değiştirmek gerekmez.
/// </summary>
public enum ProviderCapability
{
    Fixture = 0,
    Live = 1,
    News = 2,
    Lineup = 3,
    Statistics = 4,
    Standings = 5,
    Team = 6,
    Player = 7,
    Venue = 8,
    Weather = 9,
    // FAZ 9 genişletmesi — mevcut değerler korunarak eklendi (additive, non-breaking)
    Coach = 10,
    Referee = 11,
    H2H = 12,
    Injuries = 13,
    Suspensions = 14,
    Transfers = 15
}
