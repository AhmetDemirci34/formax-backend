using System;
using System.Collections.Generic;
using System.Globalization;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Providers.Sources.FootballDataUk;

/// <summary>
/// Football-Data.co.uk ham CSV'sini ortak <see cref="RawFixture"/> listesine dönüştürür.
/// SADECE alan eşlemesi: iş kuralı/normalize/isim düzeltmesi YOK; bulunamayan alanlar null.
/// </summary>
public sealed class FootballDataUkFixtureMapper : IProviderMapper<RawFixture>
{
    public string ProviderName => "football-data-uk";

    public IReadOnlyList<RawFixture> Map(object? payload)
    {
        if (payload is not string csv || string.IsNullOrWhiteSpace(csv))
            return Array.Empty<RawFixture>();

        var lines = csv.Split('\n');
        if (lines.Length < 2)
            return Array.Empty<RawFixture>();

        var header = SplitCsv(lines[0]);
        var iDiv = IndexOf(header, "Div");
        var iDate = IndexOf(header, "Date");
        var iHome = IndexOf(header, "HomeTeam");
        var iAway = IndexOf(header, "AwayTeam");
        var iHomeScore = IndexOf(header, "FTHG");
        var iAwayScore = IndexOf(header, "FTAG");

        var fixtures = new List<RawFixture>();
        for (var i = 1; i < lines.Length; i++)
        {
            var cells = SplitCsv(lines[i]);
            var home = Cell(cells, iHome);
            var away = Cell(cells, iAway);
            if (string.IsNullOrWhiteSpace(home) && string.IsNullOrWhiteSpace(away))
                continue;

            fixtures.Add(new RawFixture
            {
                ProviderName = ProviderName,
                Competition = Cell(cells, iDiv),
                HomeTeam = home,
                AwayTeam = away,
                Kickoff = Cell(cells, iDate),
                HomeScore = ParseInt(Cell(cells, iHomeScore)),
                AwayScore = ParseInt(Cell(cells, iAwayScore))
            });
        }

        return fixtures;
    }

    private static string[] SplitCsv(string line) => line.TrimEnd('\r').Split(',');

    private static int IndexOf(string[] header, string name)
    {
        for (var i = 0; i < header.Length; i++)
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static string? Cell(string[] cells, int index)
    {
        if (index < 0 || index >= cells.Length)
            return null;
        var value = cells[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
