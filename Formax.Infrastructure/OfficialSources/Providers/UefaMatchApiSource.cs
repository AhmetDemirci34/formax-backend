using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;

namespace Formax.Infrastructure.OfficialSources.Providers
{
    /// <summary>
    /// UEFA — uefa.com maç merkezinin kendi kullandığı herkese açık maç ucu (match.uefa.com/v5/matches).
    ///
    /// ÖLÇÜLDÜ (17.09.2026): <c>?competitionId={1|14|2019}&amp;fromDate=&amp;toDate=</c> anahtar/kimlik istemeden 200 döner; maç başına
    /// status (FINISHED/UPCOMING/LIVE...), score.regular/total (varsa penalty), winner.match.reason (WIN_REGULAR/DRAW/...), kickOffTime,
    /// fullTimeAt ve takımların uluslararası/resmî adları. 16.09.2026 Avrupa Ligi: Omonia–Celta 1-0, Milan–Benfica 0-2 okundu.
    /// robots.txt: match.uefa.com/robots.txt aynı alan adı içinde (www.uefa.com/errors/404) yönlendiriliyor, geçerli kural yok →
    /// RFC 9309'a göre kısıt yok; www.uefa.com/robots.txt da maç verisini yasaklamıyor. CORS başlığı yalnız tarayıcıyı ilgilendirir
    /// (sunucu tarafı okumaya engel değildir). Anahtar isteyen başka UEFA ucu KULLANILMAZ; tur başına organizasyon başına tek istek.
    /// </summary>
    public sealed class UefaMatchApiSource : IOfficialCompetitionSource
    {
        public const string Key = "uefa-match-api";
        public const string ProviderName = "UefaMatchApi";
        public const string Base = "https://match.uefa.com/v5/matches";

        /// <summary>FORMAX kilitli organizasyon kimliği → UEFA competitionId.</summary>
        public static readonly IReadOnlyDictionary<int, int> Competitions = new Dictionary<int, int>
        {
            [2] = 1,     // Şampiyonlar Ligi
            [3] = 14,    // Avrupa Ligi
            [848] = 2019 // Konferans Ligi
        };

        private readonly IOfficialContentFetcher _fetcher;
        public UefaMatchApiSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => Key;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(OfficialRoundContext round, CancellationToken ct = default)
        {
            // Sonuç botunun geriye bakışıyla (10 gün) aynı pencere — kaçırılmış/bayat maçlar da kapanır.
            var from = round.UtcNow.AddDays(-10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var to = round.UtcNow.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var all = new List<OfficialMatchRecord>();
            OfficialFetchResult? last = null;
            const int limit = 100;
            foreach (var comp in Competitions.Values)
            {
                for (var page = 0; page < 5; page++)
                {
                    var url = $"{Base}?competitionId={comp}&fromDate={from}&toDate={to}&limit={limit}&offset={page * limit}&order=ASC";
                    var f = await _fetcher.FetchAsync(new OfficialFetchRequest(SourceKey, ProviderName, url, round.Purpose, round.RoundKey, Accept: "application/json"), ct);
                    last = f;
                    if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
                    IReadOnlyList<OfficialMatchRecord> batch;
                    try { batch = Parse(f.Body!, out var rawCount); if (rawCount < limit) { all.AddRange(batch); break; } }
                    catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
                    all.AddRange(batch);
                }
            }
            return new(all, OfficialReadOutcomes.Ok, null, last);
        }

        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));

        /// <summary>Saf ayrıştırıcı — yalnız final durumda skor taşınır; ertelenen/iptal edilen maç durumu korunur.</summary>
        public static IReadOnlyList<OfficialMatchRecord> Parse(string json) => Parse(json, out _);

        public static IReadOnlyList<OfficialMatchRecord> Parse(string json, out int rawCount)
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<OfficialMatchRecord>();
            rawCount = 0;
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
            rawCount = doc.RootElement.GetArrayLength();
            foreach (var x in doc.RootElement.EnumerateArray())
            {
                var id = x.Str("id");
                var home = x.Prop("homeTeam");
                var away = x.Prop("awayTeam");
                var homeName = home?.Str("internationalName");
                var awayName = away?.Str("internationalName");
                if (id == null || homeName == null || awayName == null) continue;
                var raw = x.Str("status");
                var score = x.Prop("score");
                var reason = x.Prop("winner")?.Prop("match")?.Str("reason");
                var penalty = score?.Prop("penalty");
                var status = MapStatus(raw, reason, penalty != null);
                var total = score?.Prop("total");
                int? hs = null, aws = null;
                var extra = new Dictionary<string, string>();
                if (status is OfficialMatchStatuses.Finished or OfficialMatchStatuses.FinishedAfterExtraTime or OfficialMatchStatuses.FinishedAfterPenalties)
                {
                    hs = total?.Int("home");
                    aws = total?.Int("away");
                    if (penalty is { } p && p.Int("home") is int ph && p.Int("away") is int pa)
                    {
                        extra["penaltyHome"] = ph.ToString(CultureInfo.InvariantCulture);
                        extra["penaltyAway"] = pa.ToString(CultureInfo.InvariantCulture);
                    }
                    if (x.Str("fullTimeAt") is { } fta) extra["sourcePublishedAtUtc"] = fta;
                }
                foreach (var (side, team) in new[] { ("home", home), ("away", away) })
                {
                    var official = team?.Prop("translations")?.Prop("displayOfficialName")?.Str("EN");
                    var shortName = team?.Prop("translations")?.Prop("shortName")?.Str("EN");
                    if (official != null) extra[side + "AltName"] = official;
                    // Kısa ad bazen kulübün şehirli adını taşır (ölçüm: "GNK Dinamo" → shortName "Dinamo Zagreb").
                    if (shortName != null && shortName != official) extra[side + "AltName2"] = shortName;
                }
                var kickoff = OfficialJson.Utc(x.Prop("kickOffTime")?.Str("dateTime"));
                list.Add(new OfficialMatchRecord(Key, id, "https://www.uefa.com/match/" + id, homeName, awayName, kickoff, status, hs, aws, raw,
                    x.Prop("stadium")?.Prop("translations")?.Prop("officialName")?.Str("EN"), null, null, extra));
            }
            return list;
        }

        public static string MapStatus(string? raw, string? winnerReason, bool hasPenalty) => (raw ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "FINISHED" when hasPenalty || (winnerReason?.Contains("PENALT", StringComparison.OrdinalIgnoreCase) ?? false) => OfficialMatchStatuses.FinishedAfterPenalties,
            "FINISHED" when winnerReason?.Contains("EXTRA", StringComparison.OrdinalIgnoreCase) ?? false => OfficialMatchStatuses.FinishedAfterExtraTime,
            "FINISHED" => OfficialMatchStatuses.Finished,
            "UPCOMING" or "SCHEDULED" => OfficialMatchStatuses.Scheduled,
            "LIVE" or "PLAYING" or "HALF_TIME" => OfficialMatchStatuses.Live,
            "POSTPONED" => OfficialMatchStatuses.Postponed,
            "CANCELLED" or "CANCELED" => OfficialMatchStatuses.Cancelled,
            "ABANDONED" => OfficialMatchStatuses.Abandoned,
            "SUSPENDED" or "INTERRUPTED" => OfficialMatchStatuses.Suspended,
            _ => OfficialMatchStatuses.Unknown
        };
    }
}
