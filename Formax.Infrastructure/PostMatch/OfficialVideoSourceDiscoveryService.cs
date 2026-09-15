using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Formax.Application.Services.OfficialSources;
using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    public sealed record SourceDiscoveryReport(int Leagues, int TeamsSeen, int Mapped, int Verified, int Candidates, int Unmapped, int HttpCalls,
        IReadOnlyList<string> Notes, int LeagueSites = 0, int ClubSites = 0, int BroadcasterSites = 0, int FeedsAdded = 0);

    /// <summary>
    /// RESMÎ KAYNAK KATALOĞUNU OTOMATİK BÜYÜTEN KEŞİF — robots.txt'ye uyan iki-kanıt modeli (15.09.2026).
    ///
    /// Eski sürüm Wikidata SPARQL (query.wikidata.org/robots.txt: <c>Disallow: /sparql</c>) ve YouTube kanal akışı yazarını
    /// (<c>Disallow: /feeds/videos.xml</c>) kullanıyordu; ikisi de kaldırıldı. Şimdi kullanılan kanıtlar:
    ///  E1 · Wikidata <c>Special:EntityData/Q….json</c> (robots: <c>Allow: /wiki/Special:EntityData/*.</c>) resmî site P856 host'u;
    ///  E2 · sitenin kendi JSON-LD <c>sameAs</c> kaydında aynı Wikidata öğesi;
    ///  E3 · doğrulanmış üst resmî sitenin (lig → kulüp/yayıncı) o host'a bağlantı vermesi;
    ///  E4 · lig için: FORMAX resmî kaynak kayıt defterinde ölçülmüş veri host'uyla aynı alan adı;
    ///  E5 · sitenin doğrulanmış üst resmî siteye (lig) geri bağlantısı.
    /// Bir site ancak en az İKİ bağımsız kanıtla ve en az biri dışarıdan (E1/E3/E4) gelmek şartıyla doğrulanır; tek zayıf
    /// kanıt Verified yapmaz. Yayıncılar elle yazılmış liste değil, lig sitesinin bağlantılarından türetilir.
    /// </summary>
    public sealed class OfficialVideoSourceDiscoveryService
    {
        public const string HttpClientName = "video-source-discovery";

        /// <summary>Kilitli müsabakaların Wikidata öğeleri (14.09.2026'da doğrulandı).</summary>
        public static readonly IReadOnlyDictionary<int, string> LeagueQids = new Dictionary<int, string>
        {
            [39] = "Q9448", [40] = "Q19510", [140] = "Q324867", [135] = "Q15804", [78] = "Q82595",
            [61] = "Q13394", [203] = "Q485568", [88] = "Q167541", [2] = "Q18756", [3] = "Q18760", [848] = "Q59365764"
        };

        public static readonly IReadOnlyDictionary<int, string> LeagueCountry = new Dictionary<int, string>
        {
            [39] = "GB", [40] = "GB", [140] = "ES", [135] = "IT", [78] = "DE", [61] = "FR", [203] = "TR", [88] = "NL",
            [2] = "EU", [3] = "EU", [848] = "EU"
        };

        /// <summary>Yayıncı bağlantısını sponsor/mağaza bağlantısından ayıran host/başlık işaretleri (kaynak listesi DEĞİL).</summary>
        private static readonly Regex BroadcasterSignal = new(
            @"(\btv\b|tv\.|sport|spor|dazn|espn|bein|sky|canal|rai|movistar|\bnos\b|trt|ziggo|viaplay|prime ?video|paramount|peacock|mediaset|telefoot|\bl1\+|ligue1plus|broadcast|televis|emittente)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly FormaxDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly OfficialVideoSourceCatalog _catalog;
        private readonly ILogger<OfficialVideoSourceDiscoveryService> _log;
        private readonly OfficialWebFeedCrawler? _crawler;
        private int _httpCalls;

        public OfficialVideoSourceDiscoveryService(FormaxDbContext db, IHttpClientFactory http,
            OfficialVideoSourceCatalog catalog, ILogger<OfficialVideoSourceDiscoveryService> log, OfficialWebFeedCrawler? crawler = null)
        {
            _db = db; _http = http; _catalog = catalog; _log = log; _crawler = crawler;
        }

        public sealed record WikidataItem(string Qid, string? Label, IReadOnlyList<string> Websites, IReadOnlyList<string> ChannelIds,
            IReadOnlyList<string>? WikipediaUrls = null);

        public async Task<SourceDiscoveryReport> RunAsync(DateTime nowUtc, int maxTeamsPerRun = 60, CancellationToken ct = default, int candidateRecheckHours = 6)
        {
            _httpCalls = 0;
            var notes = new List<string>();
            int teamsSeen = 0, mapped = 0, verified = 0, candidates = 0, unmapped = 0, leagues = 0, leagueSites = 0, clubSites = 0, broadcasters = 0, feeds = 0;
            var recheckAfter = nowUtc.AddDays(-14);

            var existing = await _db.OfficialVideoSourceCatalog.ToListAsync(ct).ConfigureAwait(false);
            var byKey = existing.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var (leagueId, qid) in LeagueQids)
            {
                ct.ThrowIfCancellationRequested();
                leagues++;

                // ── LİG SİTESİ ────────────────────────────────────────────────────
                var leagueKey = "league:" + leagueId;
                if (!byKey.TryGetValue(leagueKey, out var leagueRec))
                {
                    leagueRec = new OfficialVideoSourceRecord
                    {
                        Key = leagueKey, Publisher = "league " + leagueId, Platform = "Web", Tier = OfficialVideoSourceTiers.League,
                        LeagueIds = leagueId.ToString(CultureInfo.InvariantCulture), Status = OfficialVideoSourceCatalog.StatusCandidate,
                        DiscoveredVia = "EntityData", VerificationEvidence = string.Empty, CreatedAtUtc = nowUtc, WikidataId = qid
                    };
                    _db.OfficialVideoSourceCatalog.Add(leagueRec);
                    byKey[leagueKey] = leagueRec;
                }
                OfficialPageFacts? leagueFacts = null;
                if (leagueRec.WebsiteVerifiedAtUtc == null || leagueRec.WebsiteVerifiedAtUtc < recheckAfter || leagueRec.WebsiteStatus != OfficialVideoSourceCatalog.StatusVerified)
                {
                    var item = await EntityDataAsync(qid, ct).ConfigureAwait(false);
                    var registryHosts = OfficialSourceRegistry.All
                        .Where(s => s.Status == OfficialSourceStatuses.Verified && s.LeagueIds.Contains(leagueId)).SelectMany(s => s.Hosts).ToList();
                    leagueFacts = await VerifySiteAsync(leagueRec, item, qid, null, registryHosts, null, nowUtc, ct).ConfigureAwait(false);
                    leagueRec.SourceKind = leagueId is 2 or 3 or 848 ? "Federation" : "League";
                    leagueRec.Country = LeagueCountry.GetValueOrDefault(leagueId);
                    if (leagueRec.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified) leagueSites++;
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                else if (leagueRec.Domain != null)
                {
                    // Doğrulanmış tam adres (ör. "/serie-a"): kökü konumsuz 307 dönen sitelerde de lig bağlantıları okunur.
                    var leagueUrl = leagueRec.OfficialWebsite ?? "https://" + leagueRec.Domain + "/";
                    var home = await GetAsync(leagueUrl, ct).ConfigureAwait(false);
                    if (home.Ok) leagueFacts = OfficialWebPageParser.Parse(home.Body, leagueUrl);
                }
                var leagueDomain = leagueRec.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified ? leagueRec.Domain : null;

                // ── KULÜP SİTELERİ ────────────────────────────────────────────────
                var from = nowUtc.AddDays(-150);
                var to = nowUtc.AddDays(60);
                var inLeague = _db.Matches.AsNoTracking().Where(m => m.LeagueId == leagueId && m.MatchDate >= from && m.MatchDate <= to);
                var teamIds = await inLeague.Select(m => m.HomeTeamId).Union(inLeague.Select(m => m.AwayTeamId)).ToListAsync(ct).ConfigureAwait(false);
                var teams = await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync(ct).ConfigureAwait(false);

                // Bütçe ligler arasında paylaşılır: bir ligin kulüpleri turun tamamını tüketip diğer ligleri aç bırakmaz.
                var perLeague = Math.Max(6, maxTeamsPerRun / Math.Max(1, LeagueQids.Count));
                var leagueSeen = 0;
                foreach (var team in teams)
                {
                    if (teamsSeen >= maxTeamsPerRun || leagueSeen >= perLeague) break;
                    var key = "club:" + team.Id;
                    byKey.TryGetValue(key, out var rec);
                    if (rec != null && rec.WebsiteVerifiedAtUtc >= recheckAfter && rec.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified) continue;
                    if (rec != null && rec.LastCheckedAtUtc >= nowUtc.AddHours(-Math.Max(0, candidateRecheckHours)) && rec.WebsiteStatus != null) continue;
                    teamsSeen++;
                    leagueSeen++;

                    if (rec == null)
                    {
                        rec = new OfficialVideoSourceRecord
                        {
                            Key = key, Publisher = team.Name, Platform = "Web", Tier = OfficialVideoSourceTiers.Club, TeamId = team.Id,
                            ClubName = team.Name, Status = OfficialVideoSourceCatalog.StatusCandidate, DiscoveredVia = "LeagueSite",
                            VerificationEvidence = string.Empty, CreatedAtUtc = nowUtc
                        };
                        _db.OfficialVideoSourceCatalog.Add(rec);
                        byKey[key] = rec;
                    }
                    rec.SourceKind = "Club";
                    rec.Country = LeagueCountry.GetValueOrDefault(leagueId) is "EU" ? rec.Country : LeagueCountry.GetValueOrDefault(leagueId);
                    rec.ClubName ??= team.Name;

                    // Aday host'lar: Wikidata P856 (öğe biliniyorsa) + lig sitesinin kulübe verdiği bağlantı.
                    WikidataItem? item = rec.WikidataId != null ? await EntityDataAsync(rec.WikidataId, ct).ConfigureAwait(false) : null;
                    var leagueLinks = leagueFacts == null ? new List<string>()
                        : LinksForTeam(team.Name, leagueFacts.ExternalLinks, teams.Select(t => t.Name).ToList()).ToList();
                    if (item == null && leagueLinks.Count == 0 && string.IsNullOrWhiteSpace(rec.OfficialWebsite))
                    {
                        unmapped++;
                        rec.WebsiteStatus = OfficialVideoSourceCatalog.StatusCandidate;
                        rec.WebsiteEvidence = "wikidata öğesi yok ve lig sitesi bu kulübe bağlantı vermiyor";
                        rec.LastCheckedAtUtc = nowUtc;
                        continue;
                    }
                    mapped++;
                    var facts = await VerifySiteAsync(rec, item, rec.WikidataId, leagueDomain, Array.Empty<string>(), leagueLinks, nowUtc, ct).ConfigureAwait(false);
                    if (rec.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified) { clubSites++; verified++; } else candidates++;
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                // ── YAYINCI SİTELERİ (lig sitesinin bağlantılarından; elle yazılmış liste yok) ──
                if (leagueFacts != null && leagueDomain != null)
                    broadcasters += await DiscoverBroadcastersAsync(leagueId, leagueDomain, leagueFacts, byKey, nowUtc, ct).ConfigureAwait(false);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            var demoted = DemoteInconsistent(byKey.Values, nowUtc);
            if (demoted > 0) notes.Add($"tutarsız {demoted} kayıt doğrulamadan çıkarıldı");
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            // ── DOĞRULANMIŞ SİTELERİN AKIŞLARI ────────────────────────────────────────
            if (_crawler != null)
            {
                foreach (var r in byKey.Values.Where(r => r.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified && r.IsActive && r.Domain != null
                                                          && (r.FeedsDiscoveredAtUtc == null || r.FeedsDiscoveredAtUtc < nowUtc.AddDays(-7))).Take(40))
                {
                    ct.ThrowIfCancellationRequested();
                    try { feeds += await _crawler.DiscoverFeedsAsync(r, nowUtc, ct).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        OfficialWebFeedCrawler.RecordFailure(r, nowUtc, "akış keşfi: " + ex.GetType().Name);
                    }
                }
                _httpCalls += _crawler.HttpCalls;
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _catalog.Invalidate();
            var report = new SourceDiscoveryReport(leagues, teamsSeen, mapped, verified, candidates, unmapped, _httpCalls, notes, leagueSites, clubSites, broadcasters, feeds);
            _log.LogInformation("[VIDEO-SOURCES] kesif: lig={Leagues} ligSitesi={LeagueSites} takim={Teams} kulupSitesi={Clubs} yayinci={Broadcasters} aday={Cand} eslesmeyen={Unmapped} akis={Feeds} http={Http}",
                leagues, leagueSites, teamsSeen, clubSites, broadcasters, candidates, unmapped, feeds, _httpCalls);
            return report;
        }

        /// <summary>
        /// Tek siteyi iki bağımsız kanıtla doğrular ve kayda yazar. Dönüş: okunan ana sayfanın gerçekleri (alt keşif için).
        /// </summary>
        private async Task<OfficialPageFacts?> VerifySiteAsync(OfficialVideoSourceRecord rec, WikidataItem? item, string? qid, string? parentDomain,
            IReadOnlyList<string> registryHosts, IReadOnlyList<string>? parentLinks, DateTime nowUtc, CancellationToken ct)
        {
            // Aday adresler TAM haliyle denenir (Wikidata P856 "https://www.legaseriea.it/serie-a"): bazı sitelerin kökü dil/konum
            // tespiti için Location'sız 307 döner (ölçüldü 15.09.2026). Aynı host ikinci kez denenmez.
            var urls = new List<string>();
            foreach (var w in item?.Websites ?? Array.Empty<string>())
                if (Uri.TryCreate(w, UriKind.Absolute, out var u)) urls.Add(u.Scheme == Uri.UriSchemeHttp ? "https://" + u.Host + u.PathAndQuery : u.AbsoluteUri);
            foreach (var l in parentLinks ?? Array.Empty<string>())
                if (Uri.TryCreate(l, UriKind.Absolute, out var u)) urls.Add("https://" + u.Host + "/");
            if (!string.IsNullOrWhiteSpace(rec.OfficialWebsite) && Uri.TryCreate(rec.OfficialWebsite, UriKind.Absolute, out var ow)) urls.Add("https://" + ow.Host + ow.PathAndQuery);

            rec.LastCheckedAtUtc = nowUtc;
            if (item?.Label != null && rec.Tier != OfficialVideoSourceTiers.Club) rec.Publisher = Trim(item.Label, 160);

            foreach (var target in urls.DistinctBy(x => new Uri(x).Host.ToLowerInvariant()).Take(2))
            {
                var host = new Uri(target).Host.ToLowerInvariant();
                var page = await GetAsync(target, ct).ConfigureAwait(false);
                if (!page.Ok && new Uri(target).AbsolutePath != "/")
                    page = await GetAsync("https://" + host + "/", ct).ConfigureAwait(false);
                if (!page.Ok)
                {
                    rec.RobotsStatus = page.Status == 451 ? "Disallowed" : rec.RobotsStatus;
                    OfficialWebFeedCrawler.RecordFailure(rec, nowUtc, $"{host} ana sayfa okunamadı ({page.Status?.ToString(CultureInfo.InvariantCulture) ?? page.Error})");
                    continue;
                }
                var finalHost = page.FinalHost ?? host;
                var facts = OfficialWebPageParser.Parse(page.Body, "https://" + finalHost + "/");
                if (qid == null && facts.WikidataIds.Count == 1 && rec.Tier == OfficialVideoSourceTiers.Club)
                {
                    // Kulüp öğesi bilinmiyordu: sitenin kendi sameAs kaydındaki öğe Wikidata'dan geri doğrulanır (E1'e dönüşür).
                    qid = facts.WikidataIds[0];
                    item = await EntityDataAsync(qid, ct).ConfigureAwait(false);
                }

                var evidence = new List<string>();
                var external = 0;
                if (item?.Websites.Any(w => Uri.TryCreate(w, UriKind.Absolute, out var wu) && OfficialWebPageParser.SameSite(wu.Host, finalHost)) == true)
                { evidence.Add($"E1 wikidata:{qid} P856={finalHost}"); external++; }
                if (qid != null && facts.WikidataIds.Contains(qid)) evidence.Add($"E2 site-sameAs:{qid}");
                else if (item?.WikipediaUrls is { Count: > 0 } wikis && facts.SameAs.Any(sa => wikis.Any(w => SameWikipediaArticle(sa, w))))
                    // Sitenin kendi sameAs kaydındaki Wikipedia maddesi, Wikidata öğesinin site bağlantılarından biri (iki yönlü kimlik).
                    evidence.Add($"E2 site-sameAs-wikipedia:{qid}");
                if (parentLinks?.Any(l => Uri.TryCreate(l, UriKind.Absolute, out var lu) && OfficialWebPageParser.SameSite(lu.Host, finalHost)) == true)
                { evidence.Add($"E3 parent-link:{parentDomain}"); external++; }
                if (registryHosts.Any(h => OfficialWebPageParser.SameSite(h, finalHost)))
                { evidence.Add($"E4 official-data-host:{string.Join("|", registryHosts)}"); external++; }
                else if (registryHosts.Count > 0 && RegistryNotesMention(finalHost, rec.LeagueIds))
                {
                    // FORMAX kayıt defterinde ölçülmüş: ligin resmî veri ucu bu sitenin kendi sayfasında/paketinde yayımlanıyor.
                    evidence.Add($"E4 registry-measured-publisher:{finalHost}"); external++;
                }
                if (registryHosts.Any(h => page.Body.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    evidence.Add("E6 site-references-official-data-host");
                if (parentDomain != null && facts.ExternalLinks.Any(l => Uri.TryCreate(l.Href, UriKind.Absolute, out var pu) && OfficialWebPageParser.SameSite(pu.Host, parentDomain)))
                    evidence.Add($"E5 backlink:{parentDomain}");

                rec.WikidataId ??= qid;
                rec.OfficialWebsite = Trim(page.FinalUrl ?? target, 300);
                rec.WebsiteEvidence = Trim(string.Join(" | ", evidence), 1000);
                if (facts.YouTubeHandles.Count > 0) rec.SiteYouTubeHandles = Trim(string.Join(",", facts.YouTubeHandles), 600);
                rec.LastSuccessUtc = nowUtc; rec.FailureCount = 0; rec.LastError = null; rec.CircuitState = "Closed"; rec.CircuitOpenUntilUtc = null;
                if (evidence.Count >= 2 && external >= 1)
                {
                    rec.Domain = finalHost;
                    rec.WebsiteStatus = OfficialVideoSourceCatalog.StatusVerified;
                    rec.WebsiteVerifiedAtUtc ??= nowUtc;
                    rec.IsActive = true;
                    rec.VerificationEvidence = Trim((rec.VerificationEvidence.Length > 0 ? rec.VerificationEvidence + " || " : "") + "site: " + rec.WebsiteEvidence, 1000);
                    return facts;
                }
                rec.WebsiteStatus = OfficialVideoSourceCatalog.StatusCandidate;
            }
            return null;
        }

        public static bool SameWikipediaArticle(string a, string b)
        {
            static string Norm(string url)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || !u.Host.EndsWith("wikipedia.org", StringComparison.OrdinalIgnoreCase)) return string.Empty;
                var host = u.Host.ToLowerInvariant().Replace(".m.", ".");
                return host + Uri.UnescapeDataString(u.AbsolutePath).Replace(' ', '_').TrimEnd('/').ToLowerInvariant();
            }
            var na = Norm(a);
            return na.Length > 0 && na == Norm(b);
        }

        /// <summary>Kayıt defterinde bu ligin doğrulanmış kaynağının ölçüm notu sitenin alan adını anıyor mu?</summary>
        private static bool RegistryNotesMention(string host, string? leagueIds)
        {
            var ids = (leagueIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var v) ? v : 0).ToHashSet();
            var root = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
            return OfficialSourceRegistry.All.Any(s => s.Status == OfficialSourceStatuses.Verified && s.LeagueIds.Any(ids.Contains)
                                                       && s.EvidenceNote.Contains(root, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Lig sitesinin dış bağlantılarından yayıncı adayları: yayıncı işareti taşıyan host, adayın kendi sitesi ligi anıyor ya da
        /// lig sitesine bağlantı veriyor. İki kanıt: E3 (lig bağlantısı) + E5/ad (yayıncının lig ilişkisi).
        /// </summary>
        private async Task<int> DiscoverBroadcastersAsync(int leagueId, string leagueDomain, OfficialPageFacts leagueFacts,
            Dictionary<string, OfficialVideoSourceRecord> byKey, DateTime nowUtc, CancellationToken ct)
        {
            var leagueRec = byKey["league:" + leagueId];
            var leagueWords = MatchVideoIdentityValidator.Fold(leagueRec.Publisher).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 4).ToList();
            var clubHosts = byKey.Values.Where(r => r.Domain != null && r.Tier == OfficialVideoSourceTiers.Club).Select(r => r.Domain!).ToList();
            var linkHosts = leagueFacts.ExternalLinks
                .Where(l => Uri.TryCreate(l.Href, UriKind.Absolute, out _))
                .Select(l => (Host: new Uri(l.Href).Host.ToLowerInvariant(), l.Text))
                .Where(x => !clubHosts.Any(c => OfficialWebPageParser.SameSite(c, x.Host))
                            && !Regex.IsMatch(x.Host, @"(facebook|instagram|twitter|x\.com|tiktok|youtube|linkedin|apple|google|wikipedia|whatsapp|snapchat|twitch|spotify)", RegexOptions.IgnoreCase)
                            // Yayıncı işareti yalnız HOST adında aranır (bağlantı metni kulüp/sponsor bağlantılarında da geçebiliyordu: ölçüldü asnl.net).
                            && BroadcasterSignal.IsMatch(x.Host))
                .GroupBy(x => x.Host).Select(g => g.First()).Take(6).ToList();

            var verified = 0;
            foreach (var (host, text) in linkHosts)
            {
                ct.ThrowIfCancellationRequested();
                var key = "broadcaster:" + host;
                if (byKey.TryGetValue(key, out var rec) && rec.LastCheckedAtUtc >= nowUtc.AddDays(-7)) continue;
                var page = await GetAsync("https://" + host + "/", ct).ConfigureAwait(false);
                if (rec == null)
                {
                    rec = new OfficialVideoSourceRecord
                    {
                        Key = Trim(key, 80), Publisher = Trim(string.IsNullOrWhiteSpace(text) ? host : text, 160), Platform = "Web",
                        Tier = OfficialVideoSourceTiers.Broadcaster, LeagueIds = leagueId.ToString(CultureInfo.InvariantCulture),
                        Status = OfficialVideoSourceCatalog.StatusCandidate, DiscoveredVia = "LeagueSiteLink", VerificationEvidence = string.Empty,
                        CreatedAtUtc = nowUtc, SourceKind = "Broadcaster", Country = LeagueCountry.GetValueOrDefault(leagueId)
                    };
                    _db.OfficialVideoSourceCatalog.Add(rec);
                    byKey[rec.Key] = rec;
                }
                else if (!(rec.LeagueIds ?? "").Split(',').Contains(leagueId.ToString(CultureInfo.InvariantCulture)))
                    rec.LeagueIds = Trim(string.Join(",", (rec.LeagueIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Append(leagueId.ToString(CultureInfo.InvariantCulture))), 100);
                rec.LastCheckedAtUtc = nowUtc;
                if (!page.Ok) { OfficialWebFeedCrawler.RecordFailure(rec, nowUtc, $"{host} okunamadı ({page.Status?.ToString(CultureInfo.InvariantCulture) ?? page.Error})"); continue; }

                var facts = OfficialWebPageParser.Parse(page.Body, "https://" + (page.FinalHost ?? host) + "/");
                var folded = MatchVideoIdentityValidator.Fold(facts.Title + " " + Regex.Replace(page.Body.Length > 400_000 ? page.Body[..400_000] : page.Body, "<[^>]+>", " "));
                var mentionsLeague = leagueWords.Count > 0 && leagueWords.All(w => folded.Contains(w, StringComparison.Ordinal));
                var backlink = facts.ExternalLinks.Any(l => Uri.TryCreate(l.Href, UriKind.Absolute, out var u) && OfficialWebPageParser.SameSite(u.Host, leagueDomain));
                var evidence = new List<string> { $"E3 league-link:{leagueDomain}" };
                if (backlink) evidence.Add($"E5 backlink:{leagueDomain}");
                if (mentionsLeague) evidence.Add($"league-named:{leagueRec.Publisher}");
                rec.WebsiteEvidence = Trim(string.Join(" | ", evidence), 1000);
                if (evidence.Count >= 2)
                {
                    rec.Domain = page.FinalHost ?? host;
                    rec.OfficialWebsite = Trim("https://" + rec.Domain + "/", 300);
                    rec.WebsiteStatus = OfficialVideoSourceCatalog.StatusVerified;
                    rec.WebsiteVerifiedAtUtc ??= nowUtc;
                    verified++;
                }
                else rec.WebsiteStatus = OfficialVideoSourceCatalog.StatusCandidate;
            }
            return verified;
        }

        /// <summary>Lig sitesinin dış bağlantılarından bu kulübe ait olanlar (bağlantı metni takım adı ya da host takım adını içeriyor).</summary>
        public static IEnumerable<string> LinksForTeam(string teamName, IReadOnlyList<(string Href, string Text)> links,
            IReadOnlyCollection<string>? otherTeamsInLeague = null)
        {
            // Bağlantı, ligdeki takımlar arasında EN İYİ ve TEK eşleşen takıma aittir. Metin eşleşmesi ortak parça sayısıyla, host
            // eşleşmesi takımın BÜTÜN ayırt edici parçalarını içermesiyle puanlanır ("paris" tek başına PSG değildir — ölçüldü: parisfc.fr;
            // "Paris Saint-Germain" metni "Paris FC"nin alt kümesi olsa da PSG'ye daha çok parçayla uyar).
            var league = (otherTeamsInLeague ?? Array.Empty<string>()).Append(teamName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var (href, text) in links)
            {
                if (!Uri.TryCreate(href, UriKind.Absolute, out var u)) continue;
                if (Regex.IsMatch(u.Host, @"(facebook|instagram|twitter|x\.com|tiktok|youtube|linkedin|wikipedia|apple|google)", RegexOptions.IgnoreCase)) continue;
                var host = Regex.Replace(u.Host.ToLowerInvariant(), @"[^a-z0-9]", "");
                var scores = league.Select(t => (Team: t, Score: LinkScore(text, host, t))).Where(x => x.Score > 0).ToList();
                if (scores.Count == 0) continue;
                var best = scores.Max(x => x.Score);
                var winners = scores.Where(x => x.Score == best).ToList();
                if (winners.Count == 1 && string.Equals(winners[0].Team, teamName, StringComparison.OrdinalIgnoreCase))
                    yield return u.GetLeftPart(UriPartial.Authority) + "/";
            }
        }

        private static int LinkScore(string text, string host, string team)
        {
            var teamTokens = OfficialTeamNameMatcher.Tokens(team).Where(t => t.Length >= 3).ToList();
            if (teamTokens.Count == 0) return 0;
            var score = 0;
            if (!string.IsNullOrWhiteSpace(text) && OfficialTeamNameMatcher.SameTeam(text, team))
            {
                var textTokens = OfficialTeamNameMatcher.Tokens(text).ToHashSet(StringComparer.Ordinal);
                score = 100 + teamTokens.Count(textTokens.Contains) * 10 - Math.Abs(textTokens.Count - teamTokens.Count);
            }
            var hostTokens = teamTokens.Where(t => t.Length >= 4).ToList();
            if (hostTokens.Count > 0 && hostTokens.All(t => host.Contains(t, StringComparison.Ordinal)))
                score = Math.Max(score, 50 + hostTokens.Count * 10);
            return score;
        }

        /// <summary>
        /// Tutarlılık bekçisi: aynı alan adına birden çok kulüp doğrulanmışsa (ya da yayıncı kaydı artık yayıncı işareti taşımıyorsa)
        /// kayıtlar doğrulamadan çıkarılır; yanlış eşleşme kendiliğinden düzelir.
        /// </summary>
        public static int DemoteInconsistent(IEnumerable<OfficialVideoSourceRecord> records, DateTime nowUtc)
        {
            var demoted = 0;
            var list = records.ToList();
            foreach (var g in list.Where(r => r.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified && r.Tier == OfficialVideoSourceTiers.Club && r.Domain != null)
                         .GroupBy(r => r.Domain!, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(r => r.TeamId).Distinct().Count() > 1))
                foreach (var r in g)
                {
                    r.WebsiteStatus = OfficialVideoSourceCatalog.StatusCandidate;
                    r.WebsiteEvidence = Trim($"aynı alan adı ({g.Key}) birden çok kulübe eşlendi — belirsiz, doğrulama kaldırıldı | {r.WebsiteEvidence}", 1000);
                    r.WebsiteVerifiedAtUtc = null;
                    r.OfficialWebsite = null;
                    r.Domain = null;
                    r.LastCheckedAtUtc = nowUtc.AddDays(-1);
                    demoted++;
                }
            foreach (var r in list.Where(r => r.SourceKind == "Broadcaster" && r.WebsiteStatus == OfficialVideoSourceCatalog.StatusVerified
                                              && (r.Domain == null || !BroadcasterSignal.IsMatch(r.Domain))))
            {
                r.WebsiteStatus = OfficialVideoSourceCatalog.StatusRejected;
                r.IsActive = false;
                r.WebsiteEvidence = Trim("yayıncı işareti host adında yok (kulüp/sponsor bağlantısı) — reddedildi | " + r.WebsiteEvidence, 1000);
                demoted++;
            }
            return demoted;
        }

        // ── Ağ ──────────────────────────────────────────────────────────────────────

        private sealed record Page(bool Ok, int? Status, string Body, string? Error, string? FinalHost, string? FinalUrl = null);

        private async Task<Page> GetAsync(string url, CancellationToken ct)
        {
            try
            {
                _httpCalls++;
                var client = _http.CreateClient(HttpClientName);
                using var res = await client.GetAsync(url, ct).ConfigureAwait(false);
                var code = (int)res.StatusCode;
                if (!res.IsSuccessStatusCode) return new Page(false, code, string.Empty, res.ReasonPhrase, null);
                var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return new Page(true, code, body.Length > 3_000_000 ? body[..3_000_000] : body, null, res.RequestMessage?.RequestUri?.Host, res.RequestMessage?.RequestUri?.AbsoluteUri);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                return new Page(false, null, string.Empty, ex.GetType().Name, null);
            }
        }

        /// <summary>Wikidata <c>Special:EntityData/Q….json</c> — robots.txt açıkça izin veriyor.</summary>
        private async Task<WikidataItem?> EntityDataAsync(string qid, CancellationToken ct)
        {
            if (!Regex.IsMatch(qid ?? string.Empty, "^Q[0-9]+$")) return null;
            var page = await GetAsync($"https://www.wikidata.org/wiki/Special:EntityData/{qid}.json", ct).ConfigureAwait(false);
            return page.Ok ? ParseEntityData(page.Body, qid!) : null;
        }

        public static WikidataItem? ParseEntityData(string json, string qid)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("entities", out var entities)) return null;
                JsonElement entity = default;
                var found = false;
                foreach (var p in entities.EnumerateObject()) { entity = p.Value; found = true; break; }
                if (!found) return null;
                string? label = null;
                if (entity.TryGetProperty("labels", out var labels))
                    foreach (var lang in new[] { "en", "it", "es", "fr", "de", "tr", "nl" })
                        if (labels.TryGetProperty(lang, out var l) && l.TryGetProperty("value", out var lv)) { label = lv.GetString(); break; }
                var sites = Claims(entity, "P856");
                var channels = Claims(entity, "P2397").Where(x => Regex.IsMatch(x, "^UC[A-Za-z0-9_-]{22}$")).ToList();
                var wikis = new List<string>();
                if (entity.TryGetProperty("sitelinks", out var links))
                    foreach (var l in links.EnumerateObject())
                    {
                        if (!l.Name.EndsWith("wiki", StringComparison.Ordinal) || l.Name.Contains("quote") || l.Name.Contains("news")) continue;
                        if (l.Value.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String) wikis.Add(url.GetString()!);
                        else if (l.Value.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                            wikis.Add($"https://{l.Name[..^4].Replace('_', '-')}.wikipedia.org/wiki/{title.GetString()!.Replace(' ', '_')}");
                    }
                return new WikidataItem(qid, label, sites, channels, wikis);
            }
            catch (JsonException) { return null; }
        }

        private static List<string> Claims(JsonElement entity, string property)
        {
            var list = new List<string>();
            if (!entity.TryGetProperty("claims", out var claims) || !claims.TryGetProperty(property, out var arr)) return list;
            foreach (var c in arr.EnumerateArray())
            {
                // Tercih edilmeyen (deprecated) sıra kanıt sayılmaz.
                if (c.TryGetProperty("rank", out var rank) && rank.GetString() == "deprecated") continue;
                if (c.TryGetProperty("mainsnak", out var snak) && snak.TryGetProperty("datavalue", out var dv)
                    && dv.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String)
                    list.Add(v.GetString()!);
            }
            return list;
        }

        private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
