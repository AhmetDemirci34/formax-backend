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
    /// TÜRKİYE FUTBOL FEDERASYONU — Süper Lig'in resmî federasyon sayfaları (www.tff.org).
    ///
    /// Maç listesi: fikstür sayfasının "Haftanın Maçları" bölümü (tarih/saat/skor/macId) — tek
    /// istek bütün haftayı verir. Kadro: maç sayfası (İlk 11 / Yedekler / Teknik Sorumlu).
    /// Sayfalar sunucu tarafında üretilmiş HTML'dir; script çalıştırılmaz, form gönderilmez.
    ///
    /// DURUM KURALI (ölçüldü): TFF sayfası maçın bittiğini ayrıca yazmaz; skor, federasyonun
    /// sonuç listesinde yayımlanır. Skor, başlama saatinden en az <see cref="ResultSettleAfter"/>
    /// sonra VE maç sayfasındaki skorla aynıysa kesin sonuç sayılır; daha erken görülen skor
    /// "Unknown" kalır (canlı skor olabilir — yazılmaz).
    /// </summary>
    public sealed class TffSource
        : IOfficialCompetitionSource, IOfficialResultConfirmation, IOfficialMatchPageSource, IOfficialHistoricalLineupSource
    {
        public const string ProviderName = "TffSite";
        public const string FixturePageUrl = "https://www.tff.org/default.aspx?pageID=198";
        public static readonly TimeSpan ResultSettleAfter = TimeSpan.FromMinutes(120);

        private static readonly TimeZoneInfo Istanbul = ResolveIstanbul();

        private readonly IOfficialContentFetcher _fetcher;

        public TffSource(IOfficialContentFetcher fetcher) => _fetcher = fetcher;

        public string SourceKey => OfficialSourceRegistry.TffSite;

        public IReadOnlyCollection<string> Purposes { get; } = new[]
        {
            OfficialPurposes.Schedule, OfficialPurposes.Lineup, OfficialPurposes.Result
        };

        public static string MatchPageUrl(string macId) => $"https://www.tff.org/Default.aspx?pageId=29&macId={macId}";

        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadMatchesAsync(
            OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, FixturePageUrl, round.Purpose, round.RoundKey,
                Accept: "text/html", Encoding: "windows-1254"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);

            var rows = ParseWeekly(f.Body!, round.UtcNow);
            return rows.Count == 0
                ? new(null, OfficialReadOutcomes.ParseFailed, "Haftanın Maçları bölümü bulunamadı", f)
                : new(rows, OfficialReadOutcomes.Ok, null, f);
        }

        // ══ GEÇMİŞ KADRO — TEK SEZON ═════════════════════════════════════════════════════

        /// <summary>
        /// Kaynağın yayımladığı sezon(lar). TFF fikstür sayfası yalnız İÇİNDE BULUNULAN sezonu
        /// yayımlar; hafta/sezon parametreleri yok sayılır (19.09.2026 ölçüldü). Bu yüzden liste
        /// tek elemanlıdır — eski sezon macId'leri TAHMİN EDİLMEZ.
        /// </summary>
        public async Task<OfficialRead<IReadOnlyList<OfficialSeason>>> ReadSeasonsAsync(
            OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, FixturePageUrl, OfficialPurposes.Schedule, round.RoundKey,
                Accept: "text/html", Encoding: "windows-1254"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            var season = ParseSeason(f.Body!);
            return season == null
                ? new(null, OfficialReadOutcomes.ParseFailed, "sezon etiketi bulunamadı", f)
                : new(new[] { season }, OfficialReadOutcomes.Ok, null, f);
        }

        /// <summary>Sezonun tamamı — fikstür tablosundaki 306 satır (tek istek).</summary>
        public async Task<OfficialRead<IReadOnlyList<OfficialMatchRecord>>> ReadSeasonMatchesAsync(
            OfficialSeason season, OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, FixturePageUrl, OfficialPurposes.Schedule, round.RoundKey,
                Accept: "text/html", Encoding: "windows-1254"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            var rows = ParseSeasonFixtures(f.Body!);
            return rows.Count == 0
                ? new(null, OfficialReadOutcomes.ParseFailed, "sezon fikstür tablosu bulunamadı", f)
                : new(rows, OfficialReadOutcomes.Ok, null, f);
        }

        /// <summary>
        /// MAÇ SAYFASI — kadro VE başlama saati aynı sayfadadır. Geçmiş doldurma bunu kullanır:
        /// fikstür tablosu tarih vermediği için kimlik eşlemesi bu sayfanın saatiyle kurulur.
        /// </summary>
        public async Task<OfficialRead<(OfficialMatchRecord Record, OfficialLineupDocument? Lineup)>> ReadMatchWithLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, MatchPageUrl(match.OfficialMatchId), OfficialPurposes.Lineup,
                round.RoundKey, round.MatchId, Accept: "text/html", Encoding: "windows-1254"), ct);
            if (!f.Ok) return new(default, OfficialReadOutcomes.FetchFailed, f.Outcome, f);

            var page = ParseMatchPage(f.Body!);
            if (page == null) return new(default, OfficialReadOutcomes.ParseFailed, "maç sayfası biçimi tanınmadı", f);

            var record = match with
            {
                HomeName = page.HomeName,
                AwayName = page.AwayName,
                KickoffUtc = page.KickoffUtc,
                HomeScore = page.HomeScore,
                AwayScore = page.AwayScore
            };
            var doc = page.Home == null && page.Away == null
                ? null
                : new OfficialLineupDocument(SourceKey, match.OfficialMatchId, f.Url, f.ContentHash!, null,
                    page.Home, page.Away);
            return new((record, doc), OfficialReadOutcomes.Ok, null, f);
        }

        public async Task<OfficialRead<OfficialLineupDocument>> ReadLineupAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, MatchPageUrl(match.OfficialMatchId), OfficialPurposes.Lineup,
                round.RoundKey, round.MatchId, Accept: "text/html", Encoding: "windows-1254"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);

            var page = ParseMatchPage(f.Body!);
            if (page == null)
                return new(null, OfficialReadOutcomes.ParseFailed, "maç sayfası biçimi tanınmadı", f);
            if (page.Home == null && page.Away == null)
                return new(null, OfficialReadOutcomes.Ok, "kadro yayımlanmamış", f);

            return new(new OfficialLineupDocument(SourceKey, match.OfficialMatchId, f.Url, f.ContentHash!, null,
                page.Home, page.Away), OfficialReadOutcomes.Ok, null, f);
        }

        /// <summary>
        /// TELAFİ OKUMASI — "Haftanın Maçları" bölümü yalnız güncel haftayı yayımlar; hafta döndükten sonra kaçırılmış
        /// maç listede bulunmaz. Bu okuma, kayıtlı resmî maç kimliğiyle (macId) aynı federasyonun maç sayfasını
        /// tek GET ile okur ve listedeki satırla AYNI kuralları uygular (skor + başlama saatinden en az
        /// <see cref="ResultSettleAfter"/> geçmiş olması). Kimlik tahmin EDİLMEZ.
        /// </summary>
        public async Task<OfficialRead<OfficialMatchRecord>> ReadMatchAsync(
            string officialMatchId, OfficialRoundContext round, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(officialMatchId) || !officialMatchId.All(char.IsDigit))
                return new(null, OfficialReadOutcomes.NotSupported, "macId biçimi tanınmadı", null);
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, MatchPageUrl(officialMatchId), OfficialPurposes.Result,
                round.RoundKey, round.MatchId, Accept: "text/html", Encoding: "windows-1254"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);

            var page = ParseMatchPage(f.Body!);
            if (page == null)
                return new(null, OfficialReadOutcomes.ParseFailed, "maç sayfası biçimi tanınmadı", f);

            var status = ResolveStatus(page.KickoffUtc, page.HomeScore, page.AwayScore, round.UtcNow);
            int? hs = page.HomeScore, aws = page.AwayScore;
            if (status != OfficialMatchStatuses.Finished) { hs = null; aws = null; }
            return new(new OfficialMatchRecord(SourceKey, officialMatchId, MatchPageUrl(officialMatchId),
                page.HomeName, page.AwayName, page.KickoffUtc, status, hs, aws,
                RawStatus: hs.HasValue ? "MatchPageScore" : "MatchPageNoScore"), OfficialReadOutcomes.Ok, null, f);
        }

        /// <summary>Haftanın Maçları skorunu aynı federasyonun maç sayfasındaki skorla teyit eder.</summary>
        public async Task<OfficialRead<(int Home, int Away)?>> ConfirmScoreAsync(
            OfficialMatchRecord match, OfficialRoundContext round, CancellationToken ct = default)
        {
            var f = await _fetcher.FetchAsync(new OfficialFetchRequest(
                SourceKey, ProviderName, MatchPageUrl(match.OfficialMatchId), OfficialPurposes.Result,
                round.RoundKey, round.MatchId, Accept: "text/html", Encoding: "windows-1254"), ct);
            if (!f.Ok) return new(null, OfficialReadOutcomes.FetchFailed, f.Outcome, f);
            var page = ParseMatchPage(f.Body!);
            if (page?.HomeScore is not int h || page.AwayScore is not int a)
                return new(null, OfficialReadOutcomes.Ok, "maç sayfasında skor yok", f);
            return new((h, a), OfficialReadOutcomes.Ok, null, f);
        }

        // ── Saf ayrıştırıcılar ─────────────────────────────────────────────────────

        private static readonly Regex RowSplit = new("haftaninMaclariTr", RegexOptions.Compiled);
        private static readonly Regex DateRx = new(@"_lblTarih"">\s*([^<]*)<", RegexOptions.Compiled);
        private static readonly Regex TimeRx = new(@"_lblSaat"">\s*([^<]*)<", RegexOptions.Compiled);
        private static readonly Regex HomeRx = new(@"haftaninMaclariEv[\s\S]*?<span[^>]*>([^<]*)</span>", RegexOptions.Compiled);
        private static readonly Regex AwayRx = new(@"haftaninMaclariDeplasman[\s\S]*?<span[^>]*>([^<]*)</span>", RegexOptions.Compiled);
        private static readonly Regex MacIdRx = new(@"macId=(\d+)", RegexOptions.Compiled);
        private static readonly Regex HomeScoreRx = new(@"haftaninMaclariSkor[\s\S]*?_Label5"">\s*([^<]*)<", RegexOptions.Compiled);
        private static readonly Regex AwayScoreRx = new(@"haftaninMaclariSkor[\s\S]*?_Label6"">\s*([^<]*)<", RegexOptions.Compiled);

        public static IReadOnlyList<OfficialMatchRecord> ParseWeekly(string html, DateTime nowUtc)
        {
            var list = new List<OfficialMatchRecord>();
            var start = html.IndexOf("dtlHaftaninMaclari", StringComparison.Ordinal);
            if (start < 0) return list;

            foreach (var seg in RowSplit.Split(html[start..]).Skip(1))
            {
                var macId = MacIdRx.Match(seg).Groups[1].Value;
                var home = Clean(HomeRx.Match(seg).Groups[1].Value);
                var away = Clean(AwayRx.Match(seg).Groups[1].Value);
                if (macId.Length == 0 || home.Length == 0 || away.Length == 0) continue;

                var kickoff = ParseLocal(DateRx.Match(seg).Groups[1].Value, TimeRx.Match(seg).Groups[1].Value);
                var hs = ParseScore(HomeScoreRx.Match(seg).Groups[1].Value);
                var aws = ParseScore(AwayScoreRx.Match(seg).Groups[1].Value);

                var status = ResolveStatus(kickoff, hs, aws, nowUtc);
                if (status != OfficialMatchStatuses.Finished) { hs = null; aws = null; }

                list.Add(new OfficialMatchRecord(OfficialSourceRegistry.TffSite, macId, MatchPageUrl(macId),
                    home, away, kickoff, status, hs, aws,
                    RawStatus: hs.HasValue ? "ScorePublished" : "NoScore"));
            }
            return list;
        }

        // ══ GEÇMİŞ KADRO (19.09.2026 · ölçüldü) ══════════════════════════════════════════
        // pageID=198 sayfası "Haftanın Maçları" bloğunun yanında SEZONUN TAMAMINI da yayımlıyor
        // (fiksturListesiTable, 306 maç). Bu tabloda tarih/saat YOKTUR; yalnız hafta, takım adları,
        // skor ve macId vardır. Başlama saati maçın kendi sayfasından (pageId=29&macId=) gelir ve
        // kadro da aynı sayfadadır → maç başına TEK istek.
        //
        // GEÇMİŞ SEZON: sayfa yalnız İÇİNDE BULUNULAN sezonu yayımlıyor; hafta/sezon parametreleri
        // (hafta=, sezon=, ftId=) yok sayılıyor (aynı içerik döndü). Eski sezonlar için resmî bir
        // liste bulunamadı; macId TAHMİN EDİLMEZ. Bu yüzden Süper Lig geçmişi TEK SEZONDUR.

        private static readonly Regex SeasonLabelRx = new(@"Sezon\s*([0-9]{4})\s*-\s*([0-9]{4})", RegexOptions.Compiled);
        private static readonly Regex FixtureRowRx = new(
            @"pageID=28&kulupId=\d+"">([^<]*)</a>.*?pageID=29&macId=(\d+)"">([^<]*)</a>.*?pageID=28&kulupId=\d+"">([^<]*)</a>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>Sayfanın ilan ettiği sezon etiketi ("2026-2027"); bulunamazsa null.</summary>
        public static OfficialSeason? ParseSeason(string html)
        {
            var m = SeasonLabelRx.Match(html);
            if (!m.Success) return null;
            var start = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return new OfficialSeason(start.ToString(CultureInfo.InvariantCulture),
                $"{m.Groups[1].Value}/{m.Groups[2].Value}", start);
        }

        /// <summary>
        /// SEZONUN TAMAMI — fikstür tablosundaki her satır. Tarih bu tabloda yayımlanmadığı için
        /// <see cref="OfficialMatchRecord.KickoffUtc"/> NULL kalır (uydurulmaz); skor yayımlandıysa
        /// taşınır ama durum kararı maç sayfasına bırakılır.
        /// </summary>
        public static IReadOnlyList<OfficialMatchRecord> ParseSeasonFixtures(string html)
        {
            var list = new List<OfficialMatchRecord>();
            var start = html.IndexOf("fiksturListesiTable", StringComparison.Ordinal);
            if (start < 0) return list;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match m in FixtureRowRx.Matches(html[start..]))
            {
                var home = Clean(m.Groups[1].Value);
                var macId = m.Groups[2].Value;
                var scoreText = Clean(m.Groups[3].Value);
                var away = Clean(m.Groups[4].Value);
                if (macId.Length == 0 || home.Length == 0 || away.Length == 0) continue;
                if (!seen.Add(macId)) continue;

                int? hs = null, aws = null;
                var parts = scoreText.Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length == 2) { hs = ParseScore(parts[0]); aws = ParseScore(parts[1]); }

                list.Add(new OfficialMatchRecord(OfficialSourceRegistry.TffSite, macId, MatchPageUrl(macId),
                    home, away, null, OfficialMatchStatuses.Unknown, hs, aws,
                    RawStatus: hs.HasValue ? "ScorePublished" : "NoScore"));
            }
            return list;
        }

        /// <summary>Skor + zaman kapısı (bkz. sınıf açıklaması).</summary>
        public static string ResolveStatus(DateTime? kickoffUtc, int? home, int? away, DateTime nowUtc)
        {
            if (kickoffUtc == null) return OfficialMatchStatuses.Unknown;
            if (home.HasValue && away.HasValue)
                return nowUtc >= kickoffUtc.Value + ResultSettleAfter
                    ? OfficialMatchStatuses.Finished
                    : OfficialMatchStatuses.Unknown;
            return kickoffUtc.Value > nowUtc ? OfficialMatchStatuses.Scheduled : OfficialMatchStatuses.Unknown;
        }

        public sealed record TffMatchPage(
            string HomeName, string AwayName, DateTime? KickoffUtc, int? HomeScore, int? AwayScore,
            OfficialLineupSide? Home, OfficialLineupSide? Away);

        private static readonly Regex Team1Rx = new(@"_lnkTakim1""[^>]*>([^<]*)<", RegexOptions.Compiled);
        private static readonly Regex Team2Rx = new(@"_lnkTakim2""[^>]*>([^<]*)<", RegexOptions.Compiled);
        private static readonly Regex PageDateRx = new(@"_lblTarih"">\s*([0-9.]+)\s*-\s*([0-9:]+)\s*<", RegexOptions.Compiled);
        private static readonly Regex Score1Rx = new(@"_lblTakim1Skor"">\s*([^<]*)<", RegexOptions.Compiled);
        private static readonly Regex Score2Rx = new(@"dtMacBilgisi_Label12"">\s*([^<]*)<", RegexOptions.Compiled);

        public static TffMatchPage? ParseMatchPage(string html)
        {
            var home = Clean(Team1Rx.Match(html).Groups[1].Value);
            var away = Clean(Team2Rx.Match(html).Groups[1].Value);
            if (home.Length == 0 || away.Length == 0) return null;

            var dm = PageDateRx.Match(html);
            var kickoff = dm.Success ? ParseLocal(dm.Groups[1].Value, dm.Groups[2].Value) : null;

            return new TffMatchPage(home, away, kickoff,
                ParseScore(Score1Rx.Match(html).Groups[1].Value),
                ParseScore(Score2Rx.Match(html).Groups[1].Value),
                Side(html, "grdTakim1", home), Side(html, "grdTakim2", away));
        }

        private static OfficialLineupSide? Side(string html, string grid, string teamName)
        {
            var starters = Players(html, grid, "rptKadrolar");
            if (starters.Count == 0) return null;
            var bench = Players(html, grid, "rptYedekler");
            var coachRx = new Regex(grid + @"_rptTeknikKadro_ctl\d+_lnkTeknikSorumlu""[^>]*>([^<]*)<");
            var coach = Clean(coachRx.Match(html).Groups[1].Value);
            return new OfficialLineupSide(teamName, null, starters, bench, coach.Length == 0 ? null : coach);
        }

        private static List<OfficialLineupPlayer> Players(string html, string grid, string repeater)
        {
            var rx = new Regex(grid + "_" + repeater + @"_ctl(\d+)_formaNo"">\s*([^<]*)<\/span>\s*<a[^>]*_" + repeater +
                               @"_ctl\1_lnkOyuncu""[^>]*kisiId=(\d+)[^>]*>([^<]*)<");
            var list = new List<OfficialLineupPlayer>();
            foreach (Match m in rx.Matches(html))
            {
                var number = int.TryParse(m.Groups[2].Value.Trim().TrimEnd('.'), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var n) ? n : (int?)null;
                var name = Clean(m.Groups[4].Value);
                if (name.Length == 0) continue;
                list.Add(new OfficialLineupPlayer(name, number, null, false, "tff:" + m.Groups[3].Value));
            }
            return list;
        }

        private static string Clean(string raw) => WebUtility.HtmlDecode(raw ?? string.Empty).Trim();

        private static int? ParseScore(string raw)
            => int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

        private static DateTime? ParseLocal(string date, string time)
        {
            if (!DateTime.TryParseExact($"{date.Trim()} {time.Trim()}", "dd.MM.yyyy HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Istanbul);
        }

        private static TimeZoneInfo ResolveIstanbul()
        {
            foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("TRT", TimeSpan.FromHours(3), "TRT", "TRT");
        }
    }
}
