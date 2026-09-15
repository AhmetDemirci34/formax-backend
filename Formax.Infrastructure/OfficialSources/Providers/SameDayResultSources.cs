using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;

namespace Formax.Infrastructure.OfficialSources.Providers
{
    /// <summary>
    /// LALIGA — resmî sitenin herkese açık sonuç sayfası (www.laliga.com/laliga-easports/resultados).
    ///
    /// ÖLÇÜLDÜ (15.09.2026): sayfa sunucuda üretilir ve <c>__NEXT_DATA__</c> içinde haftanın maçlarını (status "FullTime",
    /// home_score/away_score, ISO tarih, takımların tüzel ve kısa adları) taşır; Villarreal–Betis 1-2 "FullTime" okundu.
    /// robots.txt bu yola izin veriyor. Abonelik anahtarı isteyen veri ucu (apim.laliga.com) KULLANILMAZ; script çalıştırılmaz.
    /// </summary>
    public sealed class LaLigaSiteSource : IOfficialCompetitionSource
    {
        public const string Key = "laliga-site";
        public const string ProviderName = "LaLigaSite";
        public const string ResultsUrl = "https://www.laliga.com/laliga-easports/resultados";

        private readonly IOfficialContentFetcher _fetcher;
        public LaLigaSiteSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => Key;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(SourceKey, ProviderName, ResultsUrl, round.Purpose, round.RoundKey, Accept: "text/html"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            try
            {
                var (records, week, season) = ParsePage(f.Body!);
                if (records.Count == 0) return new(null, OfficialReadOutcomes.ParseFailed, "__NEXT_DATA__ içinde maç yok", f);
                // Hafta değiştiyse bir önceki haftanın geç biten maçları da okunur (pencere 3 gün).
                if (week is int w && w > 1 && season is int s)
                {
                    var prevUrl = $"{ResultsUrl}/{s}-{(s + 1) % 100:00}/jornada-{w - 1}";
                    var p = await _fetcher.FetchAsync(new OfficialFetchRequest(SourceKey, ProviderName, prevUrl, round.Purpose, round.RoundKey, Accept: "text/html"), ct);
                    if (p.Ok)
                        records = records.Concat(ParsePage(p.Body!).Records.Where(r => records.All(x => x.OfficialMatchId != r.OfficialMatchId))).ToList();
                }
                return new(records, OfficialReadOutcomes.Ok, null, f);
            }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
        }

        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));

        private static readonly Regex NextData = new(@"<script id=""__NEXT_DATA__"" type=""application/json"">([\s\S]*?)</script>", RegexOptions.Compiled);

        public static (IReadOnlyList<OfficialMatchRecord> Records, int? Week, int? Season) ParsePage(string html)
        {
            var m = NextData.Match(html ?? string.Empty);
            if (!m.Success) return (Array.Empty<OfficialMatchRecord>(), null, null);
            using var doc = JsonDocument.Parse(m.Groups[1].Value);
            var props = doc.RootElement.Prop("props")?.Prop("pageProps");
            if (props == null) return (Array.Empty<OfficialMatchRecord>(), null, null);
            var list = new List<OfficialMatchRecord>();
            if (props.Value.Prop("matches") is { ValueKind: JsonValueKind.Array } matches)
                foreach (var x in matches.EnumerateArray())
                {
                    var id = x.Str("id");
                    var home = x.Prop("home_team");
                    var away = x.Prop("away_team");
                    var homeName = home?.Str("name") ?? home?.Str("nickname");
                    var awayName = away?.Str("name") ?? away?.Str("nickname");
                    if (id == null || homeName == null || awayName == null) continue;
                    var raw = x.Str("status");
                    var status = MapStatus(raw);
                    int? hs = x.Int("home_score"), aws = x.Int("away_score");
                    if (status != OfficialMatchStatuses.Finished) { hs = null; aws = null; }
                    var extra = new Dictionary<string, string>();
                    if (home?.Str("nickname") is { } hn) extra["homeAltName"] = hn;
                    if (away?.Str("nickname") is { } an) extra["awayAltName"] = an;
                    var slug = x.Str("slug");
                    list.Add(new OfficialMatchRecord(Key, id, slug == null ? null : "https://www.laliga.com/partido/" + slug,
                        homeName, awayName, OfficialJson.Utc(x.Str("date")), status, hs, aws, raw, x.Prop("venue")?.Str("name"), null, null, extra));
                }
            var week = props.Value.Prop("gameweek")?.Int("week");
            var season = props.Value.Int("season");
            return (list, week, season);
        }

        public static string MapStatus(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "fulltime" or "full_time" or "finished" => OfficialMatchStatuses.Finished,
            "prematch" or "pre_match" or "scheduled" or "notstarted" => OfficialMatchStatuses.Scheduled,
            "firsthalf" or "halftime" or "secondhalf" or "extratime" or "penalties" or "live" or "playing" => OfficialMatchStatuses.Live,
            "postponed" => OfficialMatchStatuses.Postponed,
            "cancelled" or "canceled" => OfficialMatchStatuses.Cancelled,
            "suspended" or "interrupted" => OfficialMatchStatuses.Suspended,
            _ => OfficialMatchStatuses.Unknown
        };
    }

    /// <summary>
    /// LIGUE 1 — resmî sitenin (ligue1.com) kendi kullandığı herkese açık maç merkezi ucu (ma-api.ligue1.fr).
    ///
    /// ÖLÇÜLDÜ (15.09.2026): uç ligue1.com JS paketinde L1_API_URL olarak yayımlanıyor; anahtar/kimlik istemiyor,
    /// <c>Access-Control-Allow-Origin: *</c>; robots.txt 404 (RFC 9309: kısıt yok). <c>championship-calendar/1/nearest-game-weeks</c>
    /// önceki/güncel haftayı, <c>championship-matches/championship/1/game-week/{n}</c> maçları (period "fullTime", skor, ISO tarih) verir.
    /// </summary>
    public sealed class Ligue1ApiSource : IOfficialCompetitionSource
    {
        public const string Key = "ligue1-api";
        public const string ProviderName = "Ligue1Api";
        public const string Base = "https://ma-api.ligue1.fr";

        private readonly IOfficialContentFetcher _fetcher;
        public Ligue1ApiSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => Key;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            var cal = await _fetcher.FetchAsync(new OfficialFetchRequest(SourceKey, ProviderName, Base + "/championship-calendar/1/nearest-game-weeks",
                round.Purpose, round.RoundKey, Accept: "application/json"), ct);
            if (!cal.Ok) return new(null, OfficialReadOutcomes.FetchFailed, cal.Outcome, cal);
            List<int> weeks;
            try { weeks = ParseNearestWeeks(cal.Body!); }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, cal); }
            if (weeks.Count == 0) return new(null, OfficialReadOutcomes.ParseFailed, "yakın hafta yok", cal);

            var all = new List<OfficialMatchRecord>();
            OfficialFetchResult? last = cal;
            foreach (var w in weeks)
            {
                var f = await _fetcher.FetchAsync(new OfficialFetchRequest(SourceKey, ProviderName,
                    $"{Base}/championship-matches/championship/1/game-week/{w}", round.Purpose, round.RoundKey, Accept: "application/json"), ct);
                last = f;
                if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
                try { all.AddRange(ParseMatches(f.Body!).Where(r => all.All(x => x.OfficialMatchId != r.OfficialMatchId))); }
                catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
            }
            return all.Count == 0 ? new(null, OfficialReadOutcomes.ParseFailed, "maç yok", last) : new(all, OfficialReadOutcomes.Ok, null, last);
        }

        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));

        public static List<int> ParseNearestWeeks(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var n = doc.RootElement.Prop("nearestGameWeeks");
            var list = new List<int>();
            foreach (var k in new[] { "previousGameWeek", "currentGameWeek" })
                if (n?.Prop(k)?.Int("gameWeekNumber") is int w && !list.Contains(w)) list.Add(w);
            return list;
        }

        public static IReadOnlyList<OfficialMatchRecord> ParseMatches(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<OfficialMatchRecord>();
            if (doc.RootElement.Prop("matches") is not { ValueKind: JsonValueKind.Array } matches) return list;
            foreach (var x in matches.EnumerateArray())
            {
                var id = x.Str("matchId");
                var home = x.Prop("home");
                var away = x.Prop("away");
                var hi = home?.Prop("clubIdentity");
                var ai = away?.Prop("clubIdentity");
                var homeName = hi?.Str("name") ?? hi?.Str("officialName");
                var awayName = ai?.Str("name") ?? ai?.Str("officialName");
                if (id == null || homeName == null || awayName == null || x.Bool("unknownMatch")) continue;
                var raw = x.Str("period");
                var status = MapStatus(raw, x.Bool("isLive"));
                int? hs = home?.Int("score"), aws = away?.Int("score");
                if (status != OfficialMatchStatuses.Finished) { hs = null; aws = null; }
                var extra = new Dictionary<string, string>();
                if (hi?.Str("shortName") is { } hn) extra["homeAltName"] = hn;
                if (ai?.Str("shortName") is { } an) extra["awayAltName"] = an;
                list.Add(new OfficialMatchRecord(Key, id, "https://ligue1.com/fr/match-sheet/" + id, homeName, awayName,
                    OfficialJson.Utc(x.Str("date")), status, hs, aws, raw, null, null, null, extra));
            }
            return list;
        }

        public static string MapStatus(string? raw, bool isLive) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "fulltime" when !isLive => OfficialMatchStatuses.Finished,
            "prematch" => OfficialMatchStatuses.Scheduled,
            "postponed" => OfficialMatchStatuses.Postponed,
            "cancelled" or "canceled" => OfficialMatchStatuses.Cancelled,
            "suspended" or "interrupted" => OfficialMatchStatuses.Suspended,
            _ when isLive => OfficialMatchStatuses.Live,
            "firsthalf" or "halftime" or "secondhalf" or "extratime" or "penalties" or "fulltime" => OfficialMatchStatuses.Live,
            _ => OfficialMatchStatuses.Unknown
        };
    }

    /// <summary>
    /// EFL CHAMPIONSHIP — efl.com'un kendi kullandığı herkese açık maç ucu (multi-club-matches.webapi.gc.eflservices.co.uk).
    ///
    /// ÖLÇÜLDÜ (15.09.2026): uç efl.com paketinde MULTI_CLUB_API olarak yayımlanıyor; anahtar istemiyor,
    /// <c>Access-Control-Allow-Origin: *</c>; robots.txt 403 (RFC 9309: 4xx = kısıt yok). Championship competitionID=10;
    /// <c>matches?competitionID=10&amp;seasonID={yıl}&amp;page.size=100</c> başlama saatine göre artan sırada (matchPeriod "FullTime",
    /// skor, ilk yarı skoru, postponementReason) döner.
    /// </summary>
    public sealed class EflMultiClubSource : IOfficialCompetitionSource
    {
        public const string Key = "efl-api";
        public const string ProviderName = "EflMultiClub";
        public const string Base = "https://multi-club-matches.webapi.gc.eflservices.co.uk/v2";
        public const int ChampionshipId = 10;
        private const int PageSize = 100;
        private const int MaxPages = 7;

        private readonly IOfficialContentFetcher _fetcher;
        public EflMultiClubSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => Key;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            var now = round.UtcNow;
            var season = now.Month >= 7 ? now.Year : now.Year - 1;
            var from = now.AddDays(-10);   // bitmemiş kalmış eski maçlar da kapanabilsin (maç merkezi 10 gün geriye bakar)
            var to = now.AddDays(3);
            var all = new List<OfficialMatchRecord>();
            OfficialFetchResult? last = null;
            for (var page = 1; page <= MaxPages; page++)
            {
                var url = $"{Base}/matches?competitionID={ChampionshipId}&seasonID={season}&page.size={PageSize}&page.number={page}";
                var f = await _fetcher.FetchAsync(new OfficialFetchRequest(SourceKey, ProviderName, url, round.Purpose, round.RoundKey, Accept: "application/json"), ct);
                last = f;
                if (!f.Ok) return all.Count > 0 ? new(all, OfficialReadOutcomes.Ok, "kısmi: " + f.Outcome, f) : new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
                (IReadOnlyList<OfficialMatchRecord> Records, bool HasNext) parsed;
                try { parsed = ParsePage(f.Body!); }
                catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
                all.AddRange(parsed.Records.Where(r => r.KickoffUtc >= from && r.KickoffUtc <= to));
                // Liste başlama saatine göre artan: pencerenin sonunu geçen sayfadan sonrası okunmaz.
                if (!parsed.HasNext || parsed.Records.Count == 0 || parsed.Records.Max(r => r.KickoffUtc ?? DateTime.MinValue) > to) break;
            }
            return new(all, OfficialReadOutcomes.Ok, null, last);
        }

        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));

        public static (IReadOnlyList<OfficialMatchRecord> Records, bool HasNext) ParsePage(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<OfficialMatchRecord>();
            if (doc.RootElement.Prop("data") is { ValueKind: JsonValueKind.Array } data)
                foreach (var x in data.EnumerateArray())
                {
                    var id = x.Str("id");
                    var a = x.Prop("attributes");
                    var home = a?.Prop("homeTeam");
                    var away = a?.Prop("awayTeam");
                    var homeName = home?.Str("officialName") ?? home?.Str("name");
                    var awayName = away?.Str("officialName") ?? away?.Str("name");
                    if (id == null || a == null || homeName == null || awayName == null) continue;
                    var raw = a.Value.Str("matchPeriod");
                    var status = a.Value.Str("postponementReason") != null ? OfficialMatchStatuses.Postponed : MapStatus(raw);
                    int? hs = home?.Int("score"), aws = away?.Int("score"), hht = home?.Int("halfScore"), aht = away?.Int("halfScore");
                    if (status != OfficialMatchStatuses.Finished) { hs = aws = hht = aht = null; }
                    DateTime? kickoff = DateTime.TryParseExact(a.Value.Str("kickOffDateUTC"), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var k) ? DateTime.SpecifyKind(k, DateTimeKind.Utc) : null;
                    var extra = new Dictionary<string, string>();
                    if ((home?.Str("shortName") ?? home?.Str("name")) is { } hn) extra["homeAltName"] = hn;
                    if ((away?.Str("shortName") ?? away?.Str("name")) is { } an) extra["awayAltName"] = an;
                    if (a.Value.Str("TBC") == "true") extra["kickoffUnknown"] = "true";
                    list.Add(new OfficialMatchRecord(Key, id, null, homeName, awayName, kickoff, status, hs, aws, raw, null, hht, aht, extra));
                }
            var hasNext = doc.RootElement.Prop("links")?.Str("next") != null;
            return (list, hasNext);
        }

        public static string MapStatus(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "fulltime" or "fulltimeaet" or "fulltimepens" or "aftereextratime" or "afterextratime" or "afterpenalties" => OfficialMatchStatuses.Finished,
            "prematch" or "notstarted" => OfficialMatchStatuses.Scheduled,
            "abandoned" or "cancelled" or "canceled" => OfficialMatchStatuses.Cancelled,
            "postponed" => OfficialMatchStatuses.Postponed,
            "" => OfficialMatchStatuses.Unknown,
            _ => OfficialMatchStatuses.Live
        };
    }
}
