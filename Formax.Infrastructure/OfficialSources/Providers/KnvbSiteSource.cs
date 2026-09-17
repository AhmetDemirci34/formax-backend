using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;

namespace Formax.Infrastructure.OfficialSources.Providers
{
    /// <summary>
    /// EREDIVISIE — Hollanda Futbol Federasyonu'nun (KNVB) resmî müsabaka sayfaları (www.knvb.nl).
    ///
    /// ÖLÇÜLDÜ (17.09.2026): iki sayfa da SUNUCUDA üretilir, script çalıştırmak gerekmez:
    ///  • <c>/competities/eredivisie/uitslagen</c> — sezonun oynanmış maçları; tarih başlıklı bloklar
    ///    (<c>table-timetable</c>) içinde ev/deplasman adı, resmî KNVB kulüp kodu (logoapi clubcode) ve
    ///    orta hücrede skor "H-A" (oynanmamış/ertelenip yeni tarihe alınmış maçta "-"). 58 satır okundu;
    ///    Ajax 5-1 Willem II (15.09), PSV 4-1 Sparta Rotterdam (13.09), PEC Zwolle 0-7 Feyenoord (13.09).
    ///  • <c>/competities/eredivisie/programma</c> — oynanacak maçlar; orta hücrede YEREL BAŞLAMA SAATİ
    ///    "HH:mm" (Europe/Amsterdam). 149 satır okundu.
    /// robots.txt: www.knvb.nl/robots.txt → HTTP 403; RFC 9309 §2.3.1.3 uyarınca kısıt yok (EFL ile aynı durum).
    /// Kimlik doğrulama, çerez, anahtar İSTEMEZ. Tur başına iki istek; ikisi de koşullu GET ile önbelleklenir.
    ///
    /// DURUM KURALI — bu kaynak "bitti" bayrağı YAYIMLAMAZ, yalnız skor yayımlar. Bu yüzden:
    ///  • skorlu satır, aynı çiftin programma sayfasında HÂLÂ oynanacak görünmediği durumda final adayıdır;
    ///  • final adayı, FORMAX'ın bildiği başlama saatinden <see cref="SettleAfterKickoffMinutes"/> dakika
    ///    geçmeden kanonik yazılmaz (<see cref="OfficialResultSettleGate"/>) — canlı skor final sanılmaz;
    ///  • iki sayfa çelişirse (skor var AMA programda hâlâ oynanacak) satır <c>Unknown</c> kalır.
    /// Uzatma/penaltı, ilk yarı skoru, erteleme/iptal durumu bu sayfalarda YAYIMLANMIYOR → üretilmez (null).
    /// </summary>
    public sealed class KnvbSiteSource : IOfficialCompetitionSource
    {
        public const string Key = "knvb-site";
        public const string ProviderName = "KnvbSite";
        public const string ResultsUrl = "https://www.knvb.nl/competities/eredivisie/uitslagen";
        public const string ScheduleUrl = "https://www.knvb.nl/competities/eredivisie/programma";

        /// <summary>Skorun kesin sonuç sayılması için başlama saatinden sonra geçmesi gereken süre (dk).</summary>
        public const int SettleAfterKickoffMinutes = 120;

        private static readonly TimeZoneInfo Amsterdam = ResolveAmsterdam();

        private readonly IOfficialContentFetcher _fetcher;
        public KnvbSiteSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => Key;
        public IReadOnlyCollection<string> Purposes { get; } = new[] { OfficialPurposes.Schedule, OfficialPurposes.Result };

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(
            OfficialRoundContext round, CancellationToken ct = default)
        {
            var results = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, ResultsUrl, round.Purpose, round.RoundKey, Accept: "text/html"), ct);
            if (!results.Ok) return new(null, OfficialReadOutcomes.FetchFailed, results.Outcome, results);

            var played = ParseTimetable(results.Body!);
            if (played.Count == 0)
                return new(null, OfficialReadOutcomes.ParseFailed, "uitslagen sayfasında maç satırı yok", results);

            // Program sayfası okunamazsa çelişki kapısı kapanmış olmaz: skorlu satır final adayı sayılmaz.
            var schedule = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, ScheduleUrl, round.Purpose, round.RoundKey, Accept: "text/html"), ct);
            if (!schedule.Ok) return new(null, OfficialReadOutcomes.FetchFailed, schedule.Outcome, schedule);

            var upcoming = ParseTimetable(schedule.Body!);
            if (upcoming.Count == 0)
                return new(null, OfficialReadOutcomes.ParseFailed, "programma sayfasında maç satırı yok", schedule);

            return new(Merge(played, upcoming, round.UtcNow), OfficialReadOutcomes.Ok, null, schedule);
        }

        /// <summary>Kadro bu kaynakta yayımlanmıyor (kayıt defterinde Lineup yeteneği yok).</summary>
        public Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
            => Task.FromResult(new OfficialRead<OfficialLineupDocument>(null, OfficialReadOutcomes.NotSupported, null, null));

        // ── Saf ayrıştırıcılar ─────────────────────────────────────────────────────

        /// <summary>
        /// Tek zaman çizelgesi satırı — sayfadaki ham hâli. Orta hücre üç biçimden birini taşır: skor "H-A",
        /// başlama saati "HH:mm" ya da federasyonun durum kodu (ölçüldü: <c>GWT</c> + title
        /// "Gestaakt wegens wanordelijkheden" = yarıda kaldı).
        /// </summary>
        public sealed record KnvbRow(
            DateOnly Date, string HomeName, string AwayName, string? HomeClubCode, string? AwayClubCode,
            string CenterValue, int? HomeScore, int? AwayScore, TimeOnly? KickoffLocal, string? StatusTitle = null);

        private static readonly Regex BlockSplit = new(@"<div class=""table-wrapper table-timetable""", RegexOptions.Compiled);
        private static readonly Regex TitleRx = new(@"<span class=""title""><span>([^<]*)</span>", RegexOptions.Compiled);
        private static readonly Regex RowSplit = new(@"<div class=""row"">", RegexOptions.Compiled);
        private static readonly Regex CenterRx = new(@"<div class=""value center"">((?:[^<]|<span[^>]*>|</span>)*)</div>", RegexOptions.Compiled);
        private static readonly Regex TitleAttrRx = new(@"title=""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex TagRx = new(@"<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex TeamRx = new(@"<div class=""team"">([^<]*)</div>", RegexOptions.Compiled);
        private static readonly Regex ClubCodeRx = new(@"clubcode=([A-Za-z0-9]+)", RegexOptions.Compiled);
        private static readonly Regex ScoreRx = new(@"^\s*(\d{1,2})\s*-\s*(\d{1,2})\s*$", RegexOptions.Compiled);
        private static readonly Regex TimeRx = new(@"^\s*(\d{1,2}):(\d{2})\s*$", RegexOptions.Compiled);

        /// <summary>Hollandaca ay adları — KNVB başlıkları ("15 september 2026", "vrijdag 18 september 2026").</summary>
        private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
        {
            ["januari"] = 1, ["februari"] = 2, ["maart"] = 3, ["april"] = 4, ["mei"] = 5, ["juni"] = 6,
            ["juli"] = 7, ["augustus"] = 8, ["september"] = 9, ["oktober"] = 10, ["november"] = 11, ["december"] = 12
        };

        /// <summary>Tarih başlığı → gün. Gün adı öneki ("zaterdag") yok sayılır; tanınmayan başlıkta null.</summary>
        public static DateOnly? ParseDutchDate(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var parts = WebUtility.HtmlDecode(title).Trim()
                .Split(new[] { ' ', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 2 < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].TrimEnd('.'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)) continue;
                if (!Months.TryGetValue(parts[i + 1], out var month)) continue;
                if (!int.TryParse(parts[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) continue;
                if (day is < 1 or > 31 || year is < 2000 or > 2100) continue;
                try { return new DateOnly(year, month, day); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
            return null;
        }

        /// <summary>Sayfadaki bütün zaman çizelgesi satırları (tarih başlığı + ev/deplasman + orta hücre).</summary>
        public static IReadOnlyList<KnvbRow> ParseTimetable(string html)
        {
            var rows = new List<KnvbRow>();
            var blocks = BlockSplit.Split(html ?? string.Empty);
            foreach (var block in blocks.Skip(1))
            {
                var date = ParseDutchDate(TitleRx.Match(block).Groups[1].Value);
                if (date == null) continue;
                foreach (var chunk in RowSplit.Split(block).Skip(1))
                {
                    // Satır: [ev hücresi] <div class="value center">orta</div> [deplasman hücresi]
                    var cm = CenterRx.Match(chunk);
                    if (!cm.Success) continue;
                    var homeSeg = chunk[..cm.Index];
                    var awaySeg = chunk[(cm.Index + cm.Length)..];
                    var home = Clean(TeamRx.Match(homeSeg).Groups[1].Value);
                    var away = Clean(TeamRx.Match(awaySeg).Groups[1].Value);
                    if (home.Length == 0 || away.Length == 0) continue;
                    var rawCenter = cm.Groups[1].Value ?? string.Empty;
                    var statusTitle = TitleAttrRx.Match(rawCenter) is { Success: true } tt ? Clean(tt.Groups[1].Value) : null;
                    var center = Clean(TagRx.Replace(rawCenter, string.Empty));

                    int? hs = null, aws = null;
                    var sm = ScoreRx.Match(center);
                    if (sm.Success)
                    {
                        hs = int.Parse(sm.Groups[1].Value, CultureInfo.InvariantCulture);
                        aws = int.Parse(sm.Groups[2].Value, CultureInfo.InvariantCulture);
                    }
                    TimeOnly? kickoff = null;
                    var tm = TimeRx.Match(center);
                    if (tm.Success)
                    {
                        var hh = int.Parse(tm.Groups[1].Value, CultureInfo.InvariantCulture);
                        var mi = int.Parse(tm.Groups[2].Value, CultureInfo.InvariantCulture);
                        if (hh < 24 && mi < 60) kickoff = new TimeOnly(hh, mi);
                    }

                    rows.Add(new KnvbRow(date.Value, home, away,
                        Code(ClubCodeRx.Match(homeSeg)), Code(ClubCodeRx.Match(awaySeg)),
                        center, hs, aws, kickoff, statusTitle));
                }
            }
            return rows;
        }

        /// <summary>
        /// İki sayfanın birleşimi. Sıra: programdaki maç her zaman "oynanacak"tır; skorlu satır ancak programda
        /// YOKSA final adayıdır. Aynı sıralı çift sezonda bir kez oynanır → çift anahtarı tekilliği sağlar.
        /// </summary>
        public static IReadOnlyList<OfficialMatchRecord> Merge(
            IReadOnlyList<KnvbRow> played, IReadOnlyList<KnvbRow> upcoming, DateTime nowUtc)
        {
            // Çelişki kapısı AYNI ÇİFT + AYNI GÜN üzerinden çalışır: yarıda kalıp başka güne alınan maç (ölçüldü
            // 05.09.2026 FC Utrecht–Go Ahead Eagles GWT, 08.09 tekrar 3-3) yanlışlıkla "hâlâ oynanacak" sayılmaz.
            var sameDay = new Dictionary<string, KnvbRow>(StringComparer.Ordinal);
            var anyDay = new Dictionary<string, KnvbRow>(StringComparer.Ordinal);
            foreach (var r in upcoming)
            {
                sameDay[DayKey(r)] = r;
                anyDay[PairKey(r)] = r;
            }

            var records = new List<OfficialMatchRecord>();
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            foreach (var r in played)
            {
                if (!emitted.Add(DayKey(r))) continue;
                sameDay.TryGetValue(DayKey(r), out var stillScheduled);

                string status;
                int? hs = null, aws = null;
                DateTime? kickoff = ToUtc(r.Date, stillScheduled?.KickoffLocal);
                if (r.HomeScore.HasValue && r.AwayScore.HasValue)
                {
                    if (stillScheduled != null)
                        // Kaynak kendisiyle çelişiyor (skor var ama maç aynı gün hâlâ programda) → sonuç yazılmaz.
                        status = OfficialMatchStatuses.Unknown;
                    else
                    {
                        status = OfficialMatchStatuses.Finished;
                        hs = r.HomeScore; aws = r.AwayScore;
                    }
                }
                else if (MapStatusCode(r.StatusTitle, r.CenterValue) is { } code)
                    status = code;
                else
                {
                    // Skor yok: ileri tarihli satır (yeni tarihe alınmış maç) oynanacak, geçmiş tarihli belirsiz.
                    if (anyDay.TryGetValue(PairKey(r), out var later)) kickoff = ToUtc(later.Date, later.KickoffLocal);
                    status = kickoff > nowUtc ? OfficialMatchStatuses.Scheduled : OfficialMatchStatuses.Unknown;
                }
                records.Add(ToRecord(r, stillScheduled, status, hs, aws, kickoff));
            }

            foreach (var r in upcoming)
            {
                if (!emitted.Add(DayKey(r))) continue;
                var kickoff = ToUtc(r.Date, r.KickoffLocal);
                records.Add(ToRecord(r, r, kickoff > nowUtc ? OfficialMatchStatuses.Scheduled : OfficialMatchStatuses.Unknown,
                    null, null, kickoff));
            }
            return records;
        }

        /// <summary>
        /// FEDERASYON DURUM KODU — yalnız ÖLÇÜLMÜŞ kod eşlenir. 17.09.2026 ölçümü: orta hücrede
        /// <c>&lt;span title="Gestaakt wegens wanordelijkheden"&gt;GWT&lt;/span&gt;</c> = maç yarıda kaldı.
        /// Erteleme/iptal kodu bu sezonun sayfasında GÖRÜLMEDİ → tanınmayan kod eşlenmez (null döner,
        /// çağıran Unknown yazar); durum UYDURULMAZ.
        /// </summary>
        public static string? MapStatusCode(string? title, string? code)
        {
            var text = OfficialTeamNameMatcher.Fold((title ?? string.Empty) + " " + (code ?? string.Empty));
            if (text.Length == 0) return null;
            return text.Contains("gestaakt", StringComparison.Ordinal) ? OfficialMatchStatuses.Abandoned : null;
        }

        private static OfficialMatchRecord ToRecord(KnvbRow row, KnvbRow? scheduled, string status, int? hs, int? aws, DateTime? kickoff)
        {
            var extra = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceDate"] = row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            if (row.HomeClubCode is { } hc) extra["homeClubCode"] = hc;
            if (row.AwayClubCode is { } ac) extra["awayClubCode"] = ac;
            if (scheduled?.KickoffLocal == null) extra["kickoffUnknown"] = "true";
            // Skor tek başına "bitti" demez: kanonik yazım FORMAX başlama saati + bekleme penceresine bağlıdır.
            if (status == OfficialMatchStatuses.Finished)
                extra[OfficialResultSettleGate.ExtraKey] = SettleAfterKickoffMinutes.ToString(CultureInfo.InvariantCulture);

            if (row.StatusTitle is { Length: > 0 } t) extra["sourceStatusTitle"] = t;

            return new OfficialMatchRecord(Key, DayKey(row), ResultsUrl, row.HomeName, row.AwayName, kickoff, status,
                hs, aws, RawStatus: row.CenterValue.Length == 0 ? null : row.CenterValue,
                Venue: null, HalfTimeHome: null, HalfTimeAway: null, Extra: extra);
        }

        /// <summary>Sıralı çift anahtarı — resmî kulüp kodu varsa ondan, yoksa katlanmış addan (yön korunur).</summary>
        public static string PairKey(KnvbRow row)
            => row.HomeClubCode is { Length: > 0 } h && row.AwayClubCode is { Length: > 0 } a
                ? h + "-" + a
                : OfficialTeamNameMatcher.Fold(row.HomeName).Replace(' ', '_') + "-vs-" +
                  OfficialTeamNameMatcher.Fold(row.AwayName).Replace(' ', '_');

        /// <summary>
        /// Resmî maç kimliği — çift + GÜN. Yarıda kalan maç yeni bir günde tekrar oynandığında iki satır da korunur;
        /// hangisinin FORMAX maçı olduğuna kimlik çözücünün tarih penceresi karar verir (yanlış güne yazım olmaz).
        /// </summary>
        public static string DayKey(KnvbRow row)
            => row.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ":" + PairKey(row);

        /// <summary>Amsterdam yerel tarih/saat → UTC. Saat yoksa günün ortası (kimlik penceresi ±12 sa).</summary>
        public static DateTime? ToUtc(DateOnly date, TimeOnly? local)
        {
            var time = local ?? new TimeOnly(12, 0);
            var unspecified = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, Amsterdam);
        }

        private static string? Code(Match m) => m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null;

        private static string Clean(string raw) => WebUtility.HtmlDecode(raw ?? string.Empty).Trim();

        private static TimeZoneInfo ResolveAmsterdam()
        {
            foreach (var id in new[] { "Europe/Amsterdam", "W. Europe Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("CET", TimeSpan.FromHours(1), "CET", "CET");
        }
    }
}
