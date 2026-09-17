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
    /// DFL BUNDESLIGA — resmî sitenin herkese açık maç günü sayfası (www.bundesliga.com).
    ///
    /// Sayfa sunucuda üretilir ve maç verisini <c>&lt;script id="ng-state" type="application/json"&gt;</c>
    /// içinde taşır: tek istek o haftanın bütün maçlarını (durum, planlanan başlama, tam/ilk yarı
    /// skoru, takım adları) verir. Script ÇALIŞTIRILMAZ; yalnız JSON okunur. Anahtar isteyen veri
    /// ucu (wapp.bapi.bundesliga.com) KULLANILMAZ.
    ///
    /// ÖLÇÜLDÜ (13.09.2026): sayfa sunucu önbelleğinden gelir ve canlı durum gecikebilir
    /// (13:30 maçı 15:55'te hâlâ SECOND_HALF görünüyordu). Bu yüzden yalnız FINAL_WHISTLE +
    /// tam skor kesin sonuç sayılır; gecikme sonucu yalnız geciktirir, uydurmaz.
    /// </summary>
    public sealed class BundesligaSiteSource : IOfficialCompetitionSource
    {
        public const string ProviderName = "BundesligaSite";
        public const string MatchdayPageUrl = "https://www.bundesliga.com/de/bundesliga/spieltag";
        public const string CompetitionId = "DFL-COM-000001";

        private readonly IOfficialContentFetcher _fetcher;

        public BundesligaSiteSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => OfficialSourceRegistry.BundesligaSite;

        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(
            OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, MatchdayPageUrl, round.Purpose, round.RoundKey, Accept: "text/html"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            IReadOnlyList<OfficialMatchRecord> records;
            try { records = ParseMatchdayPage(f.Body!); }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
            if (records.Count == 0)
                return new(null, OfficialReadOutcomes.ParseFailed, "ng-state içinde Bundesliga maçı bulunamadı", f);

            // TELAFİ — /spieltag her zaman GÜNCEL haftayı verir. Güncel haftada hiç bitmiş maç yoksa (ölçüldü
            // 17.09.2026: 4. hafta 18 maç PRE_MATCH) az önce oynanan hafta bir önceki haftadır; onun sayfası da
            // okunur ki botun telafi kontrolü sonucu bulabilsin. Güncel haftada bitmiş maç varsa ek istek yapılmaz.
            if (!records.Any(r => r.Status == OfficialMatchStatuses.Finished)
                && PreviousMatchdayUrl(records) is { } previous)
            {
                var p = await _fetcher.FetchAsync(new OfficialFetchRequest(
                    SourceKey, ProviderName, previous, round.Purpose, round.RoundKey, Accept: "text/html"), ct);
                if (p.Ok)
                {
                    try
                    {
                        var seen = records.Select(r => r.OfficialMatchId).ToHashSet(StringComparer.Ordinal);
                        records = records.Concat(ParseMatchdayPage(p.Body!).Where(r => seen.Add(r.OfficialMatchId))).ToList();
                    }
                    catch (JsonException) { /* önceki hafta okunamadı: güncel hafta sonucu korunur */ }
                }
            }
            return new(records, OfficialReadOutcomes.Ok, null, f);
        }

        /// <summary>Güncel sayfadaki en küçük haftadan bir önceki haftanın herkese açık sayfası; 1. haftada null.</summary>
        public static string? PreviousMatchdayUrl(IReadOnlyList<OfficialMatchRecord> current)
        {
            var withDay = current
                .Select(r => (Day: Matchday(r), r.KickoffUtc))
                .Where(x => x.Day is > 1 && x.KickoffUtc.HasValue)
                .OrderBy(x => x.Day).FirstOrDefault();
            if (withDay.Day is not int day || withDay.KickoffUtc is not { } kickoff) return null;
            var y = kickoff.Month >= 7 ? kickoff.Year : kickoff.Year - 1;
            return $"https://www.bundesliga.com/de/bundesliga/spieltag/{y}-{y + 1}/{day - 1}";
        }

        /// <summary>Kaydın hafta numarası (kaynağın kendi <c>matchday</c> alanı); yoksa null.</summary>
        private static int? Matchday(OfficialMatchRecord r)
            => r.Extra?.GetValueOrDefault("matchday") is { } raw
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : null;

        /// <summary>Kadro bu kaynakta henüz bağlanmadı (kayıt defterinde Lineup yeteneği yok).</summary>
        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));

        private static readonly Regex NgState = new(
            @"<script id=""ng-state"" type=""application/json"">([\s\S]*?)</script>", RegexOptions.Compiled);

        public static IReadOnlyList<OfficialMatchRecord> ParseMatchdayPage(string html)
        {
            var m = NgState.Match(html);
            if (!m.Success) return Array.Empty<OfficialMatchRecord>();
            using var doc = JsonDocument.Parse(m.Groups[1].Value);

            var byId = new Dictionary<string, OfficialMatchRecord>(StringComparer.Ordinal);
            Walk(doc.RootElement, byId, 0);
            return byId.Values.ToList();
        }

        private static void Walk(JsonElement e, Dictionary<string, OfficialMatchRecord> byId, int depth)
        {
            if (depth > 40) return;
            if (e.ValueKind == JsonValueKind.Object)
            {
                if (e.Str("matchId") is { } id && e.Str("matchStatus") != null && e.Prop("teams") is { } teams
                    && string.Equals(e.Str("dflDatalibraryCompetitionId") ?? CompetitionId, CompetitionId, StringComparison.Ordinal)
                    && !byId.ContainsKey(id))
                {
                    var rec = ToRecord(e, id, teams);
                    if (rec != null) byId[id] = rec;
                }
                foreach (var p in e.EnumerateObject()) Walk(p.Value, byId, depth + 1);
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in e.EnumerateArray()) Walk(item, byId, depth + 1);
            }
        }

        private static OfficialMatchRecord? ToRecord(JsonElement e, string id, JsonElement teams)
        {
            var home = teams.Prop("home");
            var away = teams.Prop("away");
            var homeName = home?.Str("nameFull");
            var awayName = away?.Str("nameFull");
            if (homeName == null || awayName == null) return null;

            var raw = e.Str("matchStatus");
            var status = MapStatus(raw);
            var score = e.Prop("score");
            int? hs = score?.Prop("home")?.Int("fulltime"), aws = score?.Prop("away")?.Int("fulltime");
            int? hht = score?.Prop("home")?.Int("halftime"), aht = score?.Prop("away")?.Int("halftime");
            if (status != OfficialMatchStatuses.Finished) { hs = aws = hht = aht = null; }

            var kickoff = ParseOffset(e.Str("plannedKickOff"));
            var extra = new Dictionary<string, string>();
            if (home?.Str("nameShort") is { } hShort) extra["homeAltName"] = hShort;
            if (away?.Str("nameShort") is { } aShort) extra["awayAltName"] = aShort;
            if (e.Str("matchDateFixed") == "false") extra["kickoffUnknown"] = "true";
            if (e.Int("matchday") is int day) extra["matchday"] = day.ToString(CultureInfo.InvariantCulture);

            string? url = null;
            if (e.Prop("slugs")?.Str("slugLong") is { } slug && e.Int("matchday") is int md && kickoff.HasValue)
            {
                var y = kickoff.Value.Month >= 7 ? kickoff.Value.Year : kickoff.Value.Year - 1;
                url = $"https://www.bundesliga.com/de/bundesliga/spieltag/{y}-{y + 1}/{md}/{slug}";
            }

            return new OfficialMatchRecord(OfficialSourceRegistry.BundesligaSite, id, url, homeName, awayName,
                kickoff, status, hs, aws, raw, null, hht, aht, extra);
        }

        public static string MapStatus(string? raw) => raw?.Trim().ToUpperInvariant() switch
        {
            "PRE_MATCH" => OfficialMatchStatuses.Scheduled,
            "FINAL_WHISTLE" or "FINAL" => OfficialMatchStatuses.Finished,
            "FIRST_HALF" or "HALF" or "SECOND_HALF" or "PRE_EXTRA" or "FIRST_HALF_EXTRA" or "HALF_EXTRA"
                or "SECOND_HALF_EXTRA" or "PENALTY" => OfficialMatchStatuses.Live,
            "POSTPONED" => OfficialMatchStatuses.Postponed,
            "ABANDONED" => OfficialMatchStatuses.Abandoned,
            "CANCELLED" or "CANCELED" => OfficialMatchStatuses.Cancelled,
            "INTERRUPTED" or "SUSPENDED" => OfficialMatchStatuses.Suspended,
            _ => OfficialMatchStatuses.Unknown
        };

        /// <summary>"2026-09-12T13:30:00+0000" → UTC.</summary>
        public static DateTime? ParseOffset(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = Regex.Replace(raw.Trim(), @"([+-]\d{2})(\d{2})$", "$1:$2");
            return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)
                ? dto.UtcDateTime
                : null;
        }
    }
}
