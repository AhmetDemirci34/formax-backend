using System;
using System.Collections.Generic;
using Formax.Infrastructure.Providers.Abstractions;
using Formax.Infrastructure.Providers.Configuration;
using Formax.Infrastructure.Providers.Scheduling;

namespace Formax.Infrastructure.Providers.Bootstrap;

/// <summary>
/// GDP provider başlangıç kayıt katmanı.
/// Onaylı provider'ların merkezi <see cref="IProviderConfigurationService"/> ve
/// <see cref="ProviderScheduleRegistry"/> başlangıç kayıtlarını yapar.
///
/// TÜM provider ayarları (BaseUrl / Timeout / Priority / RefreshInterval / lig / sezon) YALNIZCA
/// burada tanımlanır — provider sınıfları ve Program.cs hiçbir ayar tutmaz.
/// İleride appsettings / database / admin paneli geldiğinde YALNIZCA bu katman değişecektir
/// (bu sınıf o kaynaklardan besleyecek şekilde güncellenir; başka hiçbir yer değişmez).
/// </summary>
public sealed class ProviderBootstrap
{
    private readonly IProviderConfigurationService _configuration;
    private readonly ProviderScheduleRegistry _schedules;

    public ProviderBootstrap(IProviderConfigurationService configuration, ProviderScheduleRegistry schedules)
    {
        _configuration = configuration;
        _schedules = schedules;
    }

    /// <summary>Başlangıç kayıtlarını uygular (uygulama başlangıcında bir kez çağrılır).</summary>
    public void Run()
    {
        _configuration.Load(BuildConfigurations());

        foreach (var schedule in BuildSchedules())
            _schedules.Register(schedule);
    }

    // ---- Başlangıç kayıtları (sabit varsayılanlar; ileride appsettings/db/panel'den) ----

    private static IReadOnlyList<ProviderConfiguration> BuildConfigurations()
    {
        var configs = new List<ProviderConfiguration>();
        var season = DateTime.UtcNow.Year.ToString();

        // OpenLigaDB (Core §1 — DE fixtures/standings/teams)
        var openLiga = Params(("league", "bl1"), ("season", season));
        configs.Add(Cfg("openligadb", ProviderCapability.Fixture, "https://api.openligadb.de", 15, 90, openLiga));
        configs.Add(Cfg("openligadb", ProviderCapability.Standings, "https://api.openligadb.de", 15, 90, openLiga));
        configs.Add(Cfg("openligadb", ProviderCapability.Team, "https://api.openligadb.de", 15, 85, openLiga));

        // Football-Data.co.uk (Core §1 — tarihsel sonuçlar; CSV)
        configs.Add(Cfg("football-data-uk", ProviderCapability.Fixture, "https://www.football-data.co.uk", 20, 40,
            Params(("season", "2425"), ("league", "E0"))));

        // StatsBomb Open (Core §1 — tarihsel maçlar; JSON)
        configs.Add(Cfg("statsbomb", ProviderCapability.Fixture, "https://raw.githubusercontent.com/statsbomb/open-data/master/data/matches", 20, 45,
            Params(("competitionId", "43"), ("seasonId", "3"))));

        // Weather (Core §1)
        var coords = Params(("latitude", "48.13"), ("longitude", "11.57"));
        configs.Add(Cfg("open-meteo", ProviderCapability.Weather, "https://api.open-meteo.com/v1/forecast", 15, 90, coords));
        configs.Add(Cfg("met-norway", ProviderCapability.Weather, "https://api.met.no/weatherapi/locationforecast/2.0/compact", 15, 70, coords));

        // News (Core §1)
        configs.Add(Cfg("rss", ProviderCapability.News, "https://feeds.bbci.co.uk/sport/football/rss.xml", 15, 90, Params()));
        configs.Add(Cfg("gdelt", ProviderCapability.News, "https://api.gdeltproject.org/api/v2/doc/doc", 15, 70, Params(("query", "football"))));

        // Wikidata metadata (Core §1) — her capability için aynı arama API'si
        var wikidata = Params(("query", "FC Bayern Munich"));
        foreach (var capability in new[]
                 {
                     ProviderCapability.Team, ProviderCapability.Player, ProviderCapability.Coach,
                     ProviderCapability.Venue, ProviderCapability.Referee
                 })
        {
            configs.Add(Cfg("wikidata", capability, "https://www.wikidata.org/w/api.php", 15, 55, wikidata));
        }

        // Wikipedia metadata (Core §1)
        var wikipedia = Params(("title", "FC Bayern Munich"));
        configs.Add(Cfg("wikipedia", ProviderCapability.Team, "https://en.wikipedia.org/api/rest_v1", 15, 50, wikipedia));
        configs.Add(Cfg("wikipedia", ProviderCapability.Player, "https://en.wikipedia.org/api/rest_v1", 15, 50, wikipedia));

        // OpenStreetMap / Nominatim (Core §1 — venue → koordinat)
        configs.Add(Cfg("openstreetmap", ProviderCapability.Venue, "https://nominatim.openstreetmap.org/search", 15, 50,
            Params(("venue", "Allianz Arena"))));

        return configs;
    }

    private static IReadOnlyList<ProviderSchedule> BuildSchedules()
    {
        var t = TimeSpan.FromSeconds(15);
        return new[]
        {
            // OpenLigaDB
            ProviderSchedule.Create("openligadb", ProviderCapability.Fixture, TimeSpan.FromHours(1), priority: 90, timeout: t),
            ProviderSchedule.Create("openligadb", ProviderCapability.Standings, TimeSpan.FromHours(3), priority: 90, timeout: t),
            ProviderSchedule.Create("openligadb", ProviderCapability.Team, TimeSpan.FromDays(1), priority: 85, timeout: t),
            // Fixture (tarihsel)
            ProviderSchedule.Create("football-data-uk", ProviderCapability.Fixture, TimeSpan.FromDays(1), priority: 40, timeout: t),
            ProviderSchedule.Create("statsbomb", ProviderCapability.Fixture, TimeSpan.FromDays(7), priority: 45, timeout: t),
            // Weather
            ProviderSchedule.Create("open-meteo", ProviderCapability.Weather, TimeSpan.FromHours(1), priority: 90, timeout: t),
            ProviderSchedule.Create("met-norway", ProviderCapability.Weather, TimeSpan.FromHours(1), priority: 70, timeout: t),
            // News
            ProviderSchedule.Create("rss", ProviderCapability.News, TimeSpan.FromMinutes(15), priority: 90, timeout: t),
            ProviderSchedule.Create("gdelt", ProviderCapability.News, TimeSpan.FromMinutes(15), priority: 70, timeout: t),
            // Metadata (statik)
            ProviderSchedule.Create("wikidata", ProviderCapability.Team, TimeSpan.FromDays(7), priority: 55, timeout: t),
            ProviderSchedule.Create("wikipedia", ProviderCapability.Team, TimeSpan.FromDays(7), priority: 50, timeout: t),
            ProviderSchedule.Create("openstreetmap", ProviderCapability.Venue, TimeSpan.FromDays(7), priority: 50, timeout: t)
        };
    }

    private static IReadOnlyDictionary<string, string?> Params(params (string Key, string? Value)[] entries)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in entries)
            dict[key] = value;
        return dict;
    }

    private static ProviderConfiguration Cfg(
        string name, ProviderCapability capability, string baseUrl, int timeoutSeconds, int priority,
        IReadOnlyDictionary<string, string?> parameters) =>
        ProviderConfiguration.Create(
            providerName: name,
            capability: capability,
            priority: priority,
            timeout: TimeSpan.FromSeconds(timeoutSeconds),
            baseUrl: baseUrl,
            queryParameters: parameters,
            notes: "Core Strategy §1 — ücretsiz/açık.");
}
