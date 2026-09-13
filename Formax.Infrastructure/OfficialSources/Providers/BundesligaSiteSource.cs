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
            try
            {
                var records = ParseMatchdayPage(f.Body!);
                return records.Count == 0
                    ? new(null, OfficialReadOutcomes.ParseFailed, "ng-state içinde Bundesliga maçı bulunamadı", f)
                    : new(records, OfficialReadOutcomes.Ok, null, f);
            }
            catch (JsonException ex) { return new(null, OfficialReadOutcomes.ParseFailed, ex.Message, f); }
        }

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
            "CANCELLED" or "CANCELED" or "ABANDONED" => OfficialMatchStatuses.Cancelled,
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
