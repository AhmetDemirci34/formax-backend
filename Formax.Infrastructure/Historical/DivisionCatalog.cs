using System.Collections.Generic;

namespace Formax.Infrastructure.Historical;

/// <summary>
/// CSV Division kodu → (kanonik ad, ülke). football-data.co.uk kod şeması. Bilinmeyen kod graceful:
/// ham kod ad olur, ülke null (uydurma yok). Competition Resolver'ın girdisi.
/// </summary>
public static class DivisionCatalog
{
    public readonly record struct Entry(string Name, string? Country);

    private static readonly IReadOnlyDictionary<string, Entry> Map = new Dictionary<string, Entry>
    {
        ["E0"] = new("England Premier League", "England"),
        ["E1"] = new("England Championship", "England"),
        ["E2"] = new("England League One", "England"),
        ["E3"] = new("England League Two", "England"),
        ["EC"] = new("England National League", "England"),
        ["SC0"] = new("Scotland Premiership", "Scotland"),
        ["SC1"] = new("Scotland Championship", "Scotland"),
        ["SC2"] = new("Scotland League One", "Scotland"),
        ["SC3"] = new("Scotland League Two", "Scotland"),
        ["D1"] = new("Germany Bundesliga", "Germany"),
        ["D2"] = new("Germany 2. Bundesliga", "Germany"),
        ["I1"] = new("Italy Serie A", "Italy"),
        ["I2"] = new("Italy Serie B", "Italy"),
        ["SP1"] = new("Spain La Liga", "Spain"),
        ["SP2"] = new("Spain Segunda Division", "Spain"),
        ["F1"] = new("France Ligue 1", "France"),
        ["F2"] = new("France Ligue 2", "France"),
        ["N1"] = new("Netherlands Eredivisie", "Netherlands"),
        ["B1"] = new("Belgium Pro League", "Belgium"),
        ["P1"] = new("Portugal Primeira Liga", "Portugal"),
        ["T1"] = new("Turkey Super Lig", "Turkey"),
        ["G1"] = new("Greece Super League", "Greece"),
        ["ARG"] = new("Argentina Primera Division", "Argentina"),
        ["USA"] = new("USA Major League Soccer", "USA"),
        ["BRA"] = new("Brazil Serie A", "Brazil"),
        ["JAP"] = new("Japan J1 League", "Japan"),
        ["MEX"] = new("Mexico Liga MX", "Mexico"),
        ["ROM"] = new("Romania Liga I", "Romania"),
        ["POL"] = new("Poland Ekstraklasa", "Poland"),
        ["SWE"] = new("Sweden Allsvenskan", "Sweden"),
        ["NOR"] = new("Norway Eliteserien", "Norway"),
        ["RUS"] = new("Russia Premier League", "Russia"),
        ["DEN"] = new("Denmark Superliga", "Denmark"),
        ["CHN"] = new("China Super League", "China"),
        ["IRL"] = new("Ireland Premier Division", "Ireland"),
        ["FIN"] = new("Finland Veikkausliiga", "Finland"),
        ["AUT"] = new("Austria Bundesliga", "Austria"),
        ["SUI"] = new("Switzerland Super League", "Switzerland"),
    };

    /// <summary>Division kodunu çözer. Bilinmeyen kod → (ham kod, null) — graceful, uydurma yok.</summary>
    public static Entry Resolve(string division)
    {
        if (!string.IsNullOrWhiteSpace(division) && Map.TryGetValue(division.Trim(), out var entry))
            return entry;
        return new Entry(string.IsNullOrWhiteSpace(division) ? "Unknown" : division.Trim(), null);
    }
}
