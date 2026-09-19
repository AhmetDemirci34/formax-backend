using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.OfficialSources.Providers
{
    /// <summary>
    /// LEGA SERIE A — resmî maç merkezi verisi (api-sdp.legaseriea.it).
    ///
    /// Uç adresi legaseriea.it sayfasında <c>SDP_API_URL_WITH_VERSION</c> olarak herkese açık
    /// yayımlanır; anahtar/başlık hilesi YOKTUR (ölçüldü 11.09.2026: Access-Control-Allow-Origin: *).
    ///
    /// EKONOMİ: tur başına 1 hafta listesi (~3 KB) + pencereye düşen hafta başına 1 maç listesi
    /// (~35 KB, 10 maç). Sezonun tamamı (1,2 MB) indirilmez. Kadro maç başına okunur.
    /// </summary>
    public sealed class SerieASdpSource : IOfficialCompetitionSource, IOfficialPostMatchSource, IOfficialHistoricalLineupSource
    {
        public const string ProviderName = "SerieASdp";
        public const string Root = "https://api-sdp.legaseriea.it/v1/serie-a/football/seasons/";

        /// <summary>Sezon listesinin okunduğu kök (19.09.2026: anahtarsız 200, 2024/25'e kadar ölçüldü).</summary>
        public const string CompetitionsRoot = "https://api-sdp.legaseriea.it/v1/serie-a/football/competitions/";

        /// <summary>Serie A müsabaka kimliği — competitions listesinden ölçüldü.</summary>
        public const string CompetitionId = "serie-a::Football_Competition::ec93b94f74294dc98ab5bcfd67fc0d88";

        /// <summary>2026/27 sezon kimliği — legaseriea.it/serie-a sayfasının seasonIds alanından ölçüldü.</summary>
        public const string DefaultSeasonId = "serie-a::Football_Season::ed7fdc2a3e7b408b942ec177b7b956b5";

        private readonly IOfficialContentFetcher _fetcher;
        private readonly string _seasonId;

        public SerieASdpSource(IOfficialContentFetcher fetcher, IConfiguration config)
        {
            _fetcher = fetcher;
            _seasonId = config["OfficialSources:SerieA:SeasonId"] is { Length: > 0 } s ? s : DefaultSeasonId;
        }

        public string SourceKey => OfficialSourceRegistry.SerieASdp;

        public IReadOnlyCollection<string> Purposes { get; } = new[]
        {
            OfficialPurposes.Schedule, OfficialPurposes.Lineup, OfficialPurposes.Result, OfficialPurposes.Events
        };

        private string SeasonRoot => Root + _seasonId;

        public static string MatchPageUrl(string officialMatchId, string home, string away)
        {
            var id = officialMatchId.Split("::").Last();
            static string Slug(string s) => OfficialTeamNameMatcher.Fold(s).Replace(' ', '-');
            return $"https://www.legaseriea.it/serie-a/match/{id}/{Slug(home)}-vs-{Slug(away)}";
        }

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(
            OfficialRoundContext round, CancellationToken ct = default)
        {
            var mdFetch = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, $"{SeasonRoot}/matchdays?locale=it-IT",
                OfficialPurposes.Schedule, round.RoundKey, Accept: "application/json"), ct);
            if (!mdFetch.Ok)
                return new(null, OfficialReadOutcomes.FetchFailed, mdFetch.Outcome, mdFetch);

            IReadOnlyList<(string Id, DateTime Start, DateTime End)> matchdays;
            try { matchdays = ParseMatchdays(mdFetch.Body!); }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, mdFetch); }

            // Pencere: dünden 3 gün sonrasına. Haftanın bitiş tarihi SON GÜNÜN başıdır (ölçüldü:
            // 4. hafta end=14.09T00:00Z ama 14.09 18:45'te maç var) → bitiş +1 gün kapsanır.
            // Geriye bakış botun telafi penceresiyle aynı (kaçırılmış maç günlük kontrolde kaynakta bulunur).
            var from = round.UtcNow.Date - OfficialResultSchedule.SourceLookBack;
            var to = round.UtcNow.Date.AddDays(3);
            var inWindow = matchdays.Where(m => m.Start < to && m.End.AddDays(1) > from).ToList();

            var records = new List<OfficialMatchRecord>();
            OfficialFetchResult? last = mdFetch;
            foreach (var md in inWindow)
            {
                var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                    SourceKey, ProviderName,
                    $"{SeasonRoot}/matches?matchDayId={Uri.EscapeDataString(md.Id)}&locale=it-IT",
                    round.Purpose, round.RoundKey, Accept: "application/json"), ct);
                last = f;
                if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
                try { records.AddRange(ParseMatches(f.Body!)); }
                catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
            }

            return new(records, OfficialReadOutcomes.Ok, $"{inWindow.Count} hafta", last);
        }

        public async Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            var url = $"{SeasonRoot}/matches/{Uri.EscapeDataString(match.OfficialMatchId)}/lineups?locale=it-IT";
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

        // ── Bitmiş maç: olaylar ─────────────────────────────────────────────────────

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchEvent>>> ReadEventsAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            var url = $"{SeasonRoot}/matches/{Uri.EscapeDataString(match.OfficialMatchId)}/lineups?locale=it-IT";
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, url, OfficialPurposes.Events, round.RoundKey, round.MatchId, Accept: "application/json"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            try { return new(ParseEvents(f.Body!), OfficialReadOutcomes.Ok, null, f); }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
        }

        /// <summary>Serie A resmî veri ucunda takım istatistiği bulunamadı (404) — ÜRETİLMEZ.</summary>
        public Task<OfficialRead<OfficialMatchStatistics>> ReadStatisticsAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialMatchStatistics>(null, OfficialReadOutcomes.NotSupported, null, null));

        /// <summary>
        /// Kadro cevabındaki oyuncu olayları (goal / own-goal / penalty-goal / yellow-card / red-card /
        /// second-yellow-card / substitution-in). Tanınmayan olay türü atlanır (uydurulmaz). Oyuncu
        /// değişikliğinde çıkan oyuncu, aynı takım ve aynı dakikada TEK çıkış varsa eşlenir.
        /// </summary>
        public static IReadOnlyList<OfficialMatchEvent> ParseEvents(string lineupsJson)
        {
            using var doc = JsonDocument.Parse(lineupsJson);
            var list = new List<OfficialMatchEvent>();
            foreach (var side in new[] { "home", "away" })
            {
                if (doc.RootElement.Prop(side) is not { } team) continue;
                var players = new List<(string Id, string Name, JsonElement P)>();
                foreach (var arrName in new[] { "fielded", "benched" })
                    if (team.Prop(arrName) is { ValueKind: JsonValueKind.Array } arr)
                        foreach (var p in arr.EnumerateArray())
                            players.Add((p.Str("playerId") ?? string.Empty, p.Str("shortName") ?? p.Str("displayName") ?? string.Empty, p.Clone()));
                string? NameOf(string? pid) => players.FirstOrDefault(x => x.Id == pid).Name is { Length: > 0 } n ? n : null;

                var outs = players
                    .SelectMany(x => PlayerEvents(x.P).Where(e => e.Str("type") == "substitution-out")
                        .Select(e => (x.Name, Time: e.Int("time"), Add: e.Int("additionalTime"))))
                    .ToList();

                foreach (var (id, name, p) in players)
                    foreach (var e in PlayerEvents(p))
                    {
                        var type = e.Str("type");
                        var min = e.Int("time") ?? 0;
                        int? add = e.Int("additionalTime") is int a && a > 0 ? a : null;
                        var sameMinuteOuts = outs.Where(o => o.Time == e.Int("time") && o.Add == e.Int("additionalTime")).ToList();
                        (string Type, string Detail, string? Assist)? mapped = type switch
                        {
                            "goal" => ("Goal", "Normal Goal", NameOf(e.Str("relatedPlayerId"))),
                            "own-goal" => ("Goal", "Own Goal", null),
                            "penalty-goal" or "penalty" => ("Goal", "Penalty", null),
                            "yellow-card" => ("Card", "Yellow Card", null),
                            "red-card" => ("Card", "Red Card", null),
                            "second-yellow-card" or "yellow-red-card" => ("Card", "Second Yellow card", null),
                            "substitution-in" => ("subst", "Substitution", sameMinuteOuts.Count == 1 ? sameMinuteOuts[0].Name : null),
                            _ => null
                        };
                        if (mapped == null) continue;
                        var own = name.Length > 0 ? name : null;
                        // FORMAX olay sözleşmesi (MatchEventDto): oyuncu değişikliğinde Player = ÇIKAN, Assist = GİREN.
                        var (player, assist) = mapped.Value.Type == "subst" ? (mapped.Value.Assist, own) : (own, mapped.Value.Assist);
                        list.Add(new OfficialMatchEvent($"sa:{type}:{side}:{min}:{add}:{id}", min, add, side,
                            mapped.Value.Type, mapped.Value.Detail, player, assist));
                    }
            }
            return list.OrderBy(e => e.Minute).ThenBy(e => e.ExtraMinute ?? 0).ToList();
        }

        private static IEnumerable<JsonElement> PlayerEvents(JsonElement player)
            => player.Prop("events") is { ValueKind: JsonValueKind.Array } arr ? arr.EnumerateArray() : Enumerable.Empty<JsonElement>();

        // ── Saf ayrıştırıcılar (testte gerçek cevaplarla sınanır) ─────────────────

        // ══ GEÇMİŞ KADRO (19.09.2026 · ölçüldü) ══════════════════════════════════════════
        // competitions/{id}/seasons anahtarsız 200 döndü ve 2026/27, 2025/26, 2024/25 sezon
        // kimliklerini verdi; 2024/25 birinci haftanın kadrosu (Parma) ilk 11 + yedek + mevki +
        // grid + kaynak oyuncu kimliği + GERÇEK değişiklik dakikalarıyla okundu.

        /// <summary>Geçmiş kadro için kullanılacak en fazla sezon (en yeniden eskiye).</summary>
        public const int MaxHistoricalSeasons = 3;

        public async Task<OfficialRead<IReadOnlyList<OfficialSeason>>> ReadSeasonsAsync(
            OfficialRoundContext round, CancellationToken ct = default)
        {
            var url = $"{CompetitionsRoot}{Uri.EscapeDataString(CompetitionId)}/seasons?locale=it-IT";
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, url, OfficialPurposes.Schedule, round.RoundKey, Accept: "application/json"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            try
            {
                return new(ParseSeasons(f.Body!).Take(MaxHistoricalSeasons).ToList(), OfficialReadOutcomes.Ok, null, f);
            }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
        }

        /// <summary>Sezon adı "2025/2026" biçimindedir; başlangıç yılı sıralamayı verir.</summary>
        public static IReadOnlyList<OfficialSeason> ParseSeasons(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<OfficialSeason>();
            if (doc.RootElement.Prop("seasons") is not { ValueKind: JsonValueKind.Array } arr) return list;
            foreach (var s in arr.EnumerateArray())
            {
                var id = s.Str("seasonId");
                var name = s.Str("seasonName");
                if (id == null || name == null) continue;
                var yearText = name.Split('/', '-')[0].Trim();
                if (!int.TryParse(yearText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) continue;
                list.Add(new OfficialSeason(id, name, year));
            }
            return list.OrderByDescending(s => s.StartYear).ToList();
        }

        /// <summary>Sezonun BÜTÜN maçları — hafta hafta, tarih penceresi UYGULANMADAN (38 istek).</summary>
        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadSeasonMatchesAsync(
            OfficialSeason season, OfficialRoundContext round, CancellationToken ct = default)
        {
            var seasonRoot = Root + season.SeasonId;
            var mdFetch = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, $"{seasonRoot}/matchdays?locale=it-IT",
                OfficialPurposes.Schedule, round.RoundKey, Accept: "application/json"), ct);
            if (!mdFetch.Ok) return new(null, OfficialReadOutcomes.FetchFailed, mdFetch.Outcome, mdFetch);

            IReadOnlyList<(string Id, DateTime Start, DateTime End)> matchdays;
            try { matchdays = ParseMatchdays(mdFetch.Body!); }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, mdFetch); }

            var records = new List<OfficialMatchRecord>();
            OfficialFetchResult? last = mdFetch;
            foreach (var md in matchdays)
            {
                ct.ThrowIfCancellationRequested();
                var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                    SourceKey, ProviderName,
                    $"{seasonRoot}/matches?matchDayId={Uri.EscapeDataString(md.Id)}&locale=it-IT",
                    round.Purpose, round.RoundKey, Accept: "application/json"), ct);
                last = f;
                if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
                try { records.AddRange(ParseMatches(f.Body!)); }
                catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
            }
            return new(records, OfficialReadOutcomes.Ok, null, last);
        }

        /// <summary>
        /// GEÇMİŞ KADRO OKUMASI — sezon kökü maçın kendi sezonundan gelir (canlı yol varsayılan
        /// sezonu kullanır; geçmiş maç başka sezondadır).
        /// </summary>
        public async Task<OfficialRead<OfficialLineupDocument>> ReadLineupForSeasonAsync(
            OfficialMatchRecord match, string seasonId, OfficialRoundContext round, CancellationToken ct = default)
        {
            var url = $"{Root}{seasonId}/matches/{Uri.EscapeDataString(match.OfficialMatchId)}/lineups?locale=it-IT";
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, url, OfficialPurposes.Lineup, round.RoundKey, round.MatchId,
                Accept: "application/json"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            try { return new(ParseLineup(f.Body!, match, f.Url, f.ContentHash!), OfficialReadOutcomes.Ok, null, f); }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
        }

        public static IReadOnlyList<(string Id, DateTime Start, DateTime End)> ParseMatchdays(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<(string, DateTime, DateTime)>();
            if (doc.RootElement.Prop("matchdays") is not { ValueKind: JsonValueKind.Array } arr) return list;
            foreach (var md in arr.EnumerateArray())
            {
                var id = md.Str("matchSetId");
                var start = OfficialJson.Utc(md.Str("startDateUtc"));
                var end = OfficialJson.Utc(md.Str("endDateUtc"));
                if (id == null || start == null || end == null) continue;
                list.Add((id, start.Value, end.Value));
            }
            return list;
        }

        public static IReadOnlyList<OfficialMatchRecord> ParseMatches(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<OfficialMatchRecord>();
            if (doc.RootElement.Prop("matches") is not { ValueKind: JsonValueKind.Array } arr) return list;
            foreach (var m in arr.EnumerateArray())
            {
                var id = m.Str("matchId");
                var home = m.Prop("home");
                var away = m.Prop("away");
                if (id == null || home == null || away == null) continue;
                var homeName = home.Value.Str("shortName") ?? home.Value.Str("officialName");
                var awayName = away.Value.Str("shortName") ?? away.Value.Str("officialName");
                if (homeName == null || awayName == null) continue;

                var raw = m.Str("status");
                var status = MapStatus(raw);
                var unknownTime = m.Bool("isUnknownKickOffTime");
                int? hs = m.Int("providerHomeScore"), aws = m.Int("providerAwayScore");
                // Oynanmamış maçta skor null kalır; 0-0 uydurulmaz.
                if (status is OfficialMatchStatuses.Scheduled or OfficialMatchStatuses.Postponed
                    or OfficialMatchStatuses.Cancelled) { hs = null; aws = null; }

                var extra = new Dictionary<string, string>();
                if (unknownTime) extra["kickoffUnknown"] = "true";
                if (m.Str("phase") is { } phase) extra["phase"] = phase;
                if (home.Value.Str("officialName") is { } ho) extra["homeOfficialName"] = ho;
                if (away.Value.Str("officialName") is { } ao) extra["awayOfficialName"] = ao;

                list.Add(new OfficialMatchRecord(
                    OfficialSourceRegistry.SerieASdp, id, MatchPageUrl(id, homeName, awayName),
                    homeName, awayName, OfficialJson.Utc(m.Str("matchDateUtc")),
                    status, hs, aws, raw, m.Str("stadiumName"), Extra: extra));
            }
            return list;
        }

        /// <summary>SDP durum sözlüğü (legaseriea.it istemcisindeki kümelerle aynı).</summary>
        public static string MapStatus(string? raw) => raw?.Trim().ToUpperInvariant() switch
        {
            "FINISHED" => OfficialMatchStatuses.Finished,
            "LIVE" => OfficialMatchStatuses.Live,
            "SUSPENDED" => OfficialMatchStatuses.Suspended,
            "POSTPONED" => OfficialMatchStatuses.Postponed,
            "CANCELED" or "CANCELLED" or "ABANDONED" => OfficialMatchStatuses.Cancelled,
            "UPCOMING" or "LINEUP" or "TACTICAL" => OfficialMatchStatuses.Scheduled,
            _ => OfficialMatchStatuses.Unknown
        };

        /// <summary>
        /// Kadro cevabı. <c>fielded</c> = ilk 11 (maç sırasında değişiklikler olay olarak işaretlenir,
        /// liste değişmez — ölçüldü), <c>benched</c> = yedekler, <c>staff</c> rol 101 = teknik direktör.
        /// Kadro yayımlanmadıysa taraflar null ya da boştur → taraf null döner (uydurma yok).
        /// </summary>
        public static OfficialLineupDocument? ParseLineup(string json, OfficialMatchRecord match, string url, string hash)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var home = Side(root.Prop("home"));
            var away = Side(root.Prop("away"));
            if (home == null && away == null) return null;
            return new OfficialLineupDocument(OfficialSourceRegistry.SerieASdp, match.OfficialMatchId, url, hash,
                null, home, away);
        }

        private static OfficialLineupSide? Side(JsonElement? team)
        {
            if (team == null) return null;
            var t = team.Value;
            var starters = WithTacticalGrid(Players(t.Prop("fielded")), t.Prop("fielded"));
            if (starters.Count == 0) return null;
            var bench = Players(t.Prop("benched"));

            string? coach = null;
            if (t.Prop("staff") is { ValueKind: JsonValueKind.Array } staff)
                coach = staff.EnumerateArray()
                    .Where(s => s.Int("role") == 101 ||
                                string.Equals(s.Str("roleLabel"), "Head Coach", StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Str("shortName") ?? s.Str("displayName"))
                    .FirstOrDefault(n => n != null);

            var formation = t.Str("tacticalFormation");
            if (string.Equals(formation, "Unknown", StringComparison.OrdinalIgnoreCase)) formation = null;

            return new OfficialLineupSide(t.Str("shortName") ?? t.Str("officialName") ?? string.Empty,
                formation, starters, bench, coach);
        }

        /// <summary>
        /// Resmî taktik koordinatlardan "hat:sıra": aynı tacticalYPosition = aynı hat (kaleciden
        /// hücuma, Y azalarak), hat içinde tacticalXPosition artan sıra. Koordinatı eksik
        /// oyuncu varsa HİÇBİR oyuncuya grid verilmez (kısmi/uydurma konum yok).
        /// </summary>
        public static List<OfficialLineupPlayer> WithTacticalGrid(List<OfficialLineupPlayer> players, JsonElement? arr)
        {
            if (players.Count == 0 || arr is not { ValueKind: JsonValueKind.Array } a) return players;
            var coords = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            foreach (var p in a.EnumerateArray())
            {
                var id = p.Str("playerId");
                var x = p.Dbl("tacticalXPosition");
                var y = p.Dbl("tacticalYPosition");
                if (id == null || x == null || y == null) return players;
                coords[id] = (x.Value, Math.Round(y.Value, 2));
            }
            if (players.Any(p => p.OfficialPlayerId == null || !coords.ContainsKey(p.OfficialPlayerId))) return players;

            var lineOfY = coords.Values.Select(c => c.Y).Distinct().OrderByDescending(y => y)
                .Select((y, i) => (y, i + 1)).ToDictionary(t => t.y, t => t.Item2);
            var slotOf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var g in coords.GroupBy(c => c.Value.Y))
            {
                var i = 0;
                foreach (var kv in g.OrderBy(k => k.Value.X)) slotOf[kv.Key] = ++i;
            }
            return players.Select(p => p with
            {
                Grid = $"{lineOfY[coords[p.OfficialPlayerId!].Y]}:{slotOf[p.OfficialPlayerId!]}"
            }).ToList();
        }

        /// <summary>
        /// GERÇEK DEĞİŞİKLİK DAKİKASI — kaynağın oyuncu satırındaki <c>events</c> dizisinden.
        /// "substitution-out" ya da "substitution-in" olayı yoksa null döner: dakika UYDURULMAZ.
        /// </summary>
        private static int? SubstitutionMinute(JsonElement player)
        {
            if (player.Prop("events") is not { ValueKind: JsonValueKind.Array } events) return null;
            foreach (var e in events.EnumerateArray())
            {
                var type = e.Str("type");
                if (type is not ("substitution-out" or "substitution-in")) continue;
                if (e.Int("time") is { } minute) return minute;
            }
            return null;
        }

        private static List<OfficialLineupPlayer> Players(JsonElement? arr)
        {
            var list = new List<OfficialLineupPlayer>();
            if (arr is not { ValueKind: JsonValueKind.Array } a) return list;
            foreach (var p in a.EnumerateArray())
            {
                var name = p.Str("shortName") ?? p.Str("shirtName") ?? p.Str("displayName");
                if (name == null) continue;
                list.Add(new OfficialLineupPlayer(name, p.Int("bibNumber"),
                    OfficialJson.Position(p.Str("roleLabel")), p.Bool("isCaptain"), p.Str("playerId"),
                    SubstitutionMinute: SubstitutionMinute(p)));
            }
            return list;
        }
    }
}
