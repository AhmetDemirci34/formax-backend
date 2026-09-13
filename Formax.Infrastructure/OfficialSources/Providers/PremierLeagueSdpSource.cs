using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;

namespace Formax.Infrastructure.OfficialSources.Providers
{
    /// <summary>
    /// PREMIER LEAGUE — resmî maç merkezi verisi (sdp-prem-prod.premier-league-prod.pulselive.com).
    ///
    /// Uç adresi premierleague.com sayfasında <c>window.SDP_API</c> olarak herkese açık yayımlanır;
    /// anahtar ya da başlık hilesi YOKTUR (ölçüldü 11.09.2026).
    ///
    /// EKONOMİ: sezon listesi kickoff'a göre sıralı 100'lük sayfalardır; pencerenin sonunu
    /// geçen ilk sayfada durulur (sezon başında tek istek). Kadro maç başına okunur.
    /// </summary>
    public sealed class PremierLeagueSdpSource : IOfficialCompetitionSource
    {
        public const string ProviderName = "PremierLeagueSdp";
        public const string Root = "https://sdp-prem-prod.premier-league-prod.pulselive.com/api";
        public const int CompetitionId = 8;
        private const int MaxPages = 6;

        private readonly IOfficialContentFetcher _fetcher;

        public PremierLeagueSdpSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => OfficialSourceRegistry.PremierLeagueSdp;

        public IReadOnlyCollection<string> Purposes { get; } = new[]
        {
            OfficialPurposes.Schedule, OfficialPurposes.Lineup, OfficialPurposes.Result,
            OfficialPurposes.Events, OfficialPurposes.Statistics
        };

        /// <summary>Sezon yılı: Temmuz ve sonrası o yıl, öncesi bir önceki yıl ("2026" = 2026/27).</summary>
        public static int SeasonYear(DateTime utc) => utc.Month >= 7 ? utc.Year : utc.Year - 1;

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(
            OfficialRoundContext round, CancellationToken ct = default)
        {
            var season = SeasonYear(round.UtcNow);
            var windowStart = round.UtcNow.Date.AddDays(-1);
            var windowEnd = round.UtcNow.Date.AddDays(3);

            var records = new List<OfficialMatchRecord>();
            string? cursor = null;
            OfficialFetchResult? last = null;
            for (var page = 0; page < MaxPages; page++)
            {
                var url = $"{Root}/v2/matches?competition={CompetitionId}&season={season}&_limit=100"
                          + (cursor == null ? string.Empty : "&_next=" + Uri.EscapeDataString(cursor));
                var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                    SourceKey, ProviderName, url, round.Purpose, round.RoundKey, Accept: "application/json"), ct);
                last = f;
                if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);

                (IReadOnlyList<OfficialMatchRecord> Records, string? Next) parsed;
                try { parsed = ParseMatchesPage(f.Body!); }
                catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }

                records.AddRange(parsed.Records.Where(r => r.KickoffUtc == null ||
                                                          (r.KickoffUtc >= windowStart && r.KickoffUtc < windowEnd)));
                var lastKickoff = parsed.Records.Where(r => r.KickoffUtc.HasValue).Select(r => r.KickoffUtc!.Value)
                                        .DefaultIfEmpty(DateTime.MaxValue).Max();
                if (parsed.Next == null || lastKickoff >= windowEnd) break;
                cursor = parsed.Next;
            }
            return new(records, OfficialReadOutcomes.Ok, null, last);
        }

        public async Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            var url = $"{Root}/v3/matches/{Uri.EscapeDataString(match.OfficialMatchId)}/lineups";
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, url, OfficialPurposes.Lineup, round.RoundKey, round.MatchId,
                Accept: "application/json"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            try
            {
                return new(ParseLineup(f.Body!, match, f.Url, f.ContentHash!), OfficialReadOutcomes.Ok, null, f);
            }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
        }

        // ── Saf ayrıştırıcılar ─────────────────────────────────────────────────────

        public static (IReadOnlyList<OfficialMatchRecord> Records, string? Next) ParseMatchesPage(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var list = new List<OfficialMatchRecord>();
            string? next = root.Prop("pagination")?.Str("_next");
            if (root.Prop("data") is not { ValueKind: JsonValueKind.Array } data) return (list, next);

            foreach (var m in data.EnumerateArray())
            {
                var id = m.Str("matchId");
                var home = m.Prop("homeTeam");
                var away = m.Prop("awayTeam");
                if (id == null || home == null || away == null) continue;
                var homeName = home.Value.Str("name");
                var awayName = away.Value.Str("name");
                if (homeName == null || awayName == null) continue;

                var period = m.Str("period");
                var status = MapStatus(period);
                int? hs = home.Value.Int("score"), aws = away.Value.Int("score");
                int? hht = home.Value.Int("halfTimeScore"), aht = away.Value.Int("halfTimeScore");
                if (status is OfficialMatchStatuses.Scheduled or OfficialMatchStatuses.Postponed
                    or OfficialMatchStatuses.Cancelled) { hs = aws = hht = aht = null; }

                var kickoff = OfficialJson.LocalToUtc(m.Str("kickoff"), m.Str("kickoffTimezoneString"), "GMT Standard Time");
                var extra = new Dictionary<string, string>
                {
                    ["homeTeamId"] = home.Value.Str("id") ?? string.Empty,
                    ["awayTeamId"] = away.Value.Str("id") ?? string.Empty
                };
                if (m.Str("tbc") == "1") extra["kickoffUnknown"] = "true";
                if (m.Int("matchWeek") is int wk) extra["matchWeek"] = wk.ToString();

                list.Add(new OfficialMatchRecord(
                    OfficialSourceRegistry.PremierLeagueSdp, id,
                    $"https://www.premierleague.com/en/match/{id}",
                    homeName, awayName, kickoff, status, hs, aws, period, m.Str("ground"),
                    hht, aht, extra));
            }
            return (list, next);
        }

        public static string MapStatus(string? period) => period?.Trim() switch
        {
            "PreMatch" => OfficialMatchStatuses.Scheduled,
            "FullTime" => OfficialMatchStatuses.Finished,
            "FirstHalf" or "HalfTime" or "SecondHalf" or "ExtraTime" or "ExtraFirstHalf" or "ExtraHalfTime"
                or "ExtraSecondHalf" or "ShootOut" or "Live" => OfficialMatchStatuses.Live,
            "Postponed" => OfficialMatchStatuses.Postponed,
            "Abandoned" or "Cancelled" or "Canceled" => OfficialMatchStatuses.Cancelled,
            "Suspended" => OfficialMatchStatuses.Suspended,
            _ => OfficialMatchStatuses.Unknown
        };

        /// <summary>
        /// v3 kadro: <c>formation.lineup</c> ilk 11'in oyuncu kimliklerini hat hat verir,
        /// <c>formation.subs</c> yedekleri. Taraf, cevaptaki takım kimliğinin maç listesindeki
        /// ev/deplasman kimliğiyle eşleşmesiyle belirlenir (sıra bir sözleşme değildir).
        /// </summary>
        public static OfficialLineupDocument? ParseLineup(string json, OfficialMatchRecord match, string url, string hash)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var homeId = match.Extra?.GetValueOrDefault("homeTeamId");
            var awayId = match.Extra?.GetValueOrDefault("awayTeamId");

            OfficialLineupSide? home = null, away = null;
            foreach (var key in new[] { "home_team", "away_team" })
            {
                if (root.Prop(key) is not { } team) continue;
                var teamId = team.Str("teamId");
                string? name = teamId != null && teamId == homeId ? match.HomeName
                             : teamId != null && teamId == awayId ? match.AwayName
                             : null;
                var side = Side(team, name ?? string.Empty);
                if (side == null) continue;
                if (teamId != null && teamId == homeId) home = side;
                else if (teamId != null && teamId == awayId) away = side;
            }
            if (home == null && away == null) return null;
            return new OfficialLineupDocument(OfficialSourceRegistry.PremierLeagueSdp, match.OfficialMatchId,
                url, hash, null, home, away);
        }

        private static OfficialLineupSide? Side(JsonElement team, string teamName)
        {
            var byId = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (team.Prop("players") is { ValueKind: JsonValueKind.Array } players)
                foreach (var p in players.EnumerateArray())
                    if (p.Str("id") is { } pid) byId[pid] = p.Clone();

            var formation = team.Prop("formation");
            // formation.lineup: resmî hat yapısı (1. dizi kaleci, sonra savunmadan hücuma hatlar).
            // Grid "hat:sıra" DOĞRUDAN bu yapıdan gelir; konum tahmin edilmez.
            var starterIds = new List<string>();
            var grids = new Dictionary<string, string>(StringComparer.Ordinal);
            if (formation?.Prop("lineup") is { ValueKind: JsonValueKind.Array } lines)
            {
                var lineNo = 0;
                foreach (var line in lines.EnumerateArray())
                {
                    if (line.ValueKind != JsonValueKind.Array) continue;
                    lineNo++;
                    var slot = 0;
                    foreach (var id in line.EnumerateArray())
                        if (id.ValueKind == JsonValueKind.String && id.GetString() is { } s)
                        {
                            starterIds.Add(s);
                            grids[s] = $"{lineNo}:{++slot}";
                        }
                }
            }
            if (starterIds.Count == 0) return null;

            var benchIds = new List<string>();
            if (formation?.Prop("subs") is { ValueKind: JsonValueKind.Array } subs)
                foreach (var id in subs.EnumerateArray())
                    if (id.ValueKind == JsonValueKind.String && id.GetString() is { } s) benchIds.Add(s);

            OfficialLineupPlayer Map(string id)
            {
                if (!byId.TryGetValue(id, out var p)) return new OfficialLineupPlayer(string.Empty, null, null, false, id);
                var name = string.Join(' ', new[] { p.Str("firstName"), p.Str("lastName") }.Where(x => x != null));
                var pos = p.Str("position");
                if (string.Equals(pos, "Substitute", StringComparison.OrdinalIgnoreCase)) pos = p.Str("subPosition");
                return new OfficialLineupPlayer(name, p.Int("shirtNum"), OfficialJson.Position(pos), p.Bool("isCaptain"), id,
                    grids.GetValueOrDefault(id));
            }

            string? coach = null;
            if (team.Prop("managers") is { ValueKind: JsonValueKind.Array } managers)
                coach = managers.EnumerateArray()
                    .Select(m => string.Join(' ', new[] { m.Str("firstName"), m.Str("lastName") }.Where(x => x != null)))
                    .FirstOrDefault(n => n.Length > 0);

            return new OfficialLineupSide(teamName, formation?.Str("formation"),
                starterIds.Select(Map).ToList(), benchIds.Select(Map).ToList(), coach);
        }
    }
}
