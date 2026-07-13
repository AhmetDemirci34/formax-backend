using Formax.Infrastructure.Normalize.Geo;
using Formax.Infrastructure.Normalize.Models;
using Formax.Infrastructure.Normalize.Raw;
using Formax.Infrastructure.Normalize.Text;
using Formax.Infrastructure.Normalize.Time;

namespace Formax.Infrastructure.Normalize.Normalizers;

/// <summary>
/// Ortak HAM fikstürü (<see cref="RawFixture"/>) → <see cref="NormalizedFixture"/> dönüştürür.
///
/// TAMAMEN provider-bağımsızdır: <see cref="RawFixture.ProviderName"/>'e göre HİÇBİR dallanma yapmaz,
/// hiçbir provider'a özel bilgi kullanmaz. İsimler <see cref="TextNormalizer"/> ile temizlenir,
/// zaman <see cref="DateTimeNormalizer"/> ile UTC'ye çevrilir, durum ortak <see cref="FixtureStatus"/>'a
/// eşlenir. Eksik/geçersiz alanlarda istisna fırlatmaz; null bırakır, sahte veri üretmez.
/// </summary>
public sealed class FixtureNormalizer : NormalizerBase<RawFixture, NormalizedFixture>
{
    public FixtureNormalizer(ICountryNormalizer country) : base(country)
    {
    }

    public override NormalizedFixture Normalize(RawFixture raw)
    {
        if (raw is null)
            return new NormalizedFixture();

        return new NormalizedFixture
        {
            ProviderMatchId = TrimOrNull(raw.ProviderMatchId),
            HomeTeam = Team(raw.HomeTeam),
            AwayTeam = Team(raw.AwayTeam),
            Competition = Competition(raw.Competition, raw.Season),
            Season = TrimOrNull(raw.Season),
            Round = TrimOrNull(raw.Round),
            Venue = Venue(raw.Venue),
            KickoffUtc = DateTimeNormalizer.TryParseUtc(raw.Kickoff, out var kickoff) ? kickoff : null,
            Status = MapStatus(raw.Status),
            HomeScore = raw.HomeScore,
            AwayScore = raw.AwayScore
        };
    }

    private static NormalizedTeam? Team(string? rawName)
    {
        var name = CleanOrNull(rawName);
        return name is null ? null : new NormalizedTeam { Name = name };
    }

    private static NormalizedVenue? Venue(string? rawName)
    {
        var name = CleanOrNull(rawName);
        return name is null ? null : new NormalizedVenue { Name = name };
    }

    private static NormalizedCompetition? Competition(string? rawName, string? rawSeason)
    {
        var name = CleanOrNull(rawName);
        return name is null ? null : new NormalizedCompetition { Name = name, Season = TrimOrNull(rawSeason) };
    }

    /// <summary>
    /// Ham durum ifadesini ortak <see cref="FixtureStatus"/>'a eşler. Generic token sözlüğü kullanır
    /// (boolean tamamlanma bayrakları dahil); <see cref="RawFixture.ProviderName"/>'e göre DALLANMAZ.
    /// Tanınmayan/boş durum <see cref="FixtureStatus.Unknown"/> olur.
    /// </summary>
    private static FixtureStatus MapStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return FixtureStatus.Unknown;

        return rawStatus.Trim().ToLowerInvariant() switch
        {
            "finished" or "ft" or "full-time" or "fulltime" or "ended" or "aet" or "pen"
                or "match finished" or "true" => FixtureStatus.Finished,
            "live" or "in play" or "inplay" or "playing" or "1h" or "2h" or "ht"
                or "halftime" or "first half" or "second half" => FixtureStatus.Live,
            "scheduled" or "ns" or "not started" or "notstarted" or "upcoming"
                or "timed" or "pre" or "false" => FixtureStatus.Scheduled,
            "postponed" or "pst" => FixtureStatus.Postponed,
            "cancelled" or "canceled" or "abandoned" or "suspended" or "susp" => FixtureStatus.Cancelled,
            _ => FixtureStatus.Unknown
        };
    }

    /// <summary>İsim temizleme (TextNormalizer); boşsa null.</summary>
    private static string? CleanOrNull(string? value)
    {
        var cleaned = TextNormalizer.Clean(value);
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>İsim olmayan alanlar için basit trim; boşsa null (TextNormalizer kullanılmaz).</summary>
    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
