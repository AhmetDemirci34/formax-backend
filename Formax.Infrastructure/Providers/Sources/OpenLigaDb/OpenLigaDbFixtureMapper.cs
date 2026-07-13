using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Providers.Sources.OpenLigaDb;

/// <summary>
/// OpenLigaDB'nin ham fikstür JSON'unu (api.openligadb.de /getmatchdata) ortak <see cref="RawFixture"/>
/// listesine dönüştürür. Provider-özgü tek yer burasıdır.
///
/// SADECE alan eşlemesi yapar: iş kuralı YOK, isim düzeltmesi YOK, normalize YOK, TextNormalizer/
/// DateTimeNormalizer KULLANMAZ. Bulunamayan alanlar null bırakılır; sahte veri üretilmez.
/// Kanonik durum/tarih/isim dönüşümleri sonraki Normalize adımının işidir.
/// </summary>
public sealed class OpenLigaDbFixtureMapper : IProviderMapper<RawFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ProviderName => "openligadb";

    public IReadOnlyList<RawFixture> Map(object? payload)
    {
        if (payload is not string json || string.IsNullOrWhiteSpace(json))
            return Array.Empty<RawFixture>();

        List<OpenLigaMatch>? matches;
        try
        {
            matches = JsonSerializer.Deserialize<List<OpenLigaMatch>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Array.Empty<RawFixture>();
        }

        if (matches is null || matches.Count == 0)
            return Array.Empty<RawFixture>();

        var fixtures = new List<RawFixture>(matches.Count);
        foreach (var match in matches)
        {
            if (match is null)
                continue;

            fixtures.Add(MapMatch(match));
        }

        return fixtures;
    }

    private RawFixture MapMatch(OpenLigaMatch match)
    {
        var finalResult = SelectFinalResult(match.MatchResults);

        return new RawFixture
        {
            ProviderName = ProviderName,
            ProviderMatchId = match.MatchID?.ToString(CultureInfo.InvariantCulture),
            Competition = match.LeagueName,
            Season = match.LeagueSeason?.ToString(CultureInfo.InvariantCulture),
            Round = match.Group?.GroupName,
            HomeTeam = match.Team1?.TeamName,
            AwayTeam = match.Team2?.TeamName,
            // Ham başlama zamanı metni (UTC alanı varsa o, yoksa yerel); ayrıştırma yapılmaz.
            Kickoff = match.MatchDateTimeUTC ?? match.MatchDateTime,
            // OpenLigaDB'nin ham matchIsFinished bayrağı (yorumlanmadan); kanonik durum Normalize'da.
            Status = match.MatchIsFinished?.ToString(),
            Venue = match.Location?.LocationStadium,
            HomeScore = finalResult?.PointsTeam1,
            AwayScore = finalResult?.PointsTeam2
        };
    }

    /// <summary>
    /// Skoru okumak için sonuç dizisinden final sonucu seçer (en yüksek resultOrderID = Endergebnis).
    /// Bu bir yorum/iş kuralı değil, OpenLigaDB yapısından skor alanını okuma erişimidir.
    /// </summary>
    private static OpenLigaResult? SelectFinalResult(List<OpenLigaResult>? results)
    {
        if (results is null || results.Count == 0)
            return null;

        OpenLigaResult? final = null;
        foreach (var result in results)
        {
            if (result is null)
                continue;

            if (final is null || (result.ResultOrderID ?? int.MinValue) > (final.ResultOrderID ?? int.MinValue))
                final = result;
        }

        return final;
    }

    // ---- OpenLigaDB ham JSON şeması (yalnızca deserialize için; sınıf dışına sızmaz) ----

    private sealed class OpenLigaMatch
    {
        public int? MatchID { get; set; }
        public string? MatchDateTime { get; set; }
        public string? MatchDateTimeUTC { get; set; }
        public string? LeagueName { get; set; }
        public int? LeagueSeason { get; set; }
        public OpenLigaGroup? Group { get; set; }
        public OpenLigaTeam? Team1 { get; set; }
        public OpenLigaTeam? Team2 { get; set; }
        public bool? MatchIsFinished { get; set; }
        public List<OpenLigaResult>? MatchResults { get; set; }
        public OpenLigaLocation? Location { get; set; }
    }

    private sealed class OpenLigaGroup
    {
        public string? GroupName { get; set; }
        public int? GroupOrderID { get; set; }
    }

    private sealed class OpenLigaTeam
    {
        public int? TeamId { get; set; }
        public string? TeamName { get; set; }
        public string? ShortName { get; set; }
    }

    private sealed class OpenLigaResult
    {
        public int? PointsTeam1 { get; set; }
        public int? PointsTeam2 { get; set; }
        public int? ResultOrderID { get; set; }
        public int? ResultTypeID { get; set; }
        public string? ResultName { get; set; }
    }

    private sealed class OpenLigaLocation
    {
        public string? LocationCity { get; set; }
        public string? LocationStadium { get; set; }
    }
}
