using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Formax.Application.Services.PostMatch;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.PostMatch
{
    public sealed record SourceDiscoveryReport(int Leagues, int TeamsSeen, int Mapped, int Verified, int Candidates, int Unmapped, int HttpCalls, IReadOnlyList<string> Notes);

    /// <summary>
    /// RESMÎ KAYNAK KATALOĞUNU OTOMATİK BÜYÜTEN KEŞİF.
    ///
    /// 1) Wikidata (kamuya açık, anahtarsız): lig öğesine bağlı kulüpler (P118), resmî YouTube kanal kimliği
    ///    (P2397) ve resmî site (P856). Lig öğesinin kendi kanalı da alınır.
    /// 2) FORMAX takımıyla eşleme: lig maçlarında görünen takım adı ↔ kulüp etiketi/diğer adları (katlanmış).
    ///    Belirsiz eşleşme (birden çok kulüp) kataloğa girmez.
    /// 3) İKİNCİ BAĞIMSIZ KANIT (zorunlu): kulübün resmî sitesindeki YouTube bağlantısı aynı kanala çıkıyor
    ///    mu (/@handle, /user/…, /c/… sayfasının canonical kimliği) ya da kanal akışının yazar adı kulüp adıyla
    ///    uyuşuyor mu. Wikidata kaydı TEK BAŞINA "Verified" yapmaz.
    /// Bütün istekler nezaket katmanından geçer (host aralığı, devre kesici, robots.txt).
    /// </summary>
    public sealed class OfficialVideoSourceDiscoveryService
    {
        public const string HttpClientName = "video-source-discovery";

        /// <summary>Kilitli müsabakaların Wikidata öğeleri (14.09.2026'da SPARQL ve wbsearchentities ile doğrulandı).</summary>
        public static readonly IReadOnlyDictionary<int, string> LeagueQids = new Dictionary<int, string>
        {
            [39] = "Q9448", [40] = "Q19510", [140] = "Q324867", [135] = "Q15804", [78] = "Q82595",
            [61] = "Q13394", [203] = "Q485568", [88] = "Q167541", [2] = "Q18756", [3] = "Q18760", [848] = "Q59365764"
        };

        private static readonly Regex YouTubeLink = new(
            @"youtube\.com/(?:channel/(?<id>UC[A-Za-z0-9_-]{22})|(?<path>@[A-Za-z0-9._-]{2,64}|c/[A-Za-z0-9._-]{2,64}|user/[A-Za-z0-9._-]{2,64}))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex CanonicalChannel = new(
            @"<link rel=""canonical"" href=""https://www\.youtube\.com/channel/(?<id>UC[A-Za-z0-9_-]{22})""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly FormaxDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly OfficialVideoSourceCatalog _catalog;
        private readonly ILogger<OfficialVideoSourceDiscoveryService> _log;
        private int _httpCalls;

        public OfficialVideoSourceDiscoveryService(FormaxDbContext db, IHttpClientFactory http,
            OfficialVideoSourceCatalog catalog, ILogger<OfficialVideoSourceDiscoveryService> log)
        {
            _db = db; _http = http; _catalog = catalog; _log = log;
        }

        public sealed record WikidataClub(string Qid, string Label, IReadOnlyList<string> AltLabels, IReadOnlyList<string> ChannelIds, string? Website);

        public async Task<SourceDiscoveryReport> RunAsync(DateTime nowUtc, int maxTeamsPerRun = 40, CancellationToken ct = default)
        {
            _httpCalls = 0;
            var notes = new List<string>();
            int teamsSeen = 0, mapped = 0, verified = 0, candidates = 0, unmapped = 0, leagues = 0;
            var recheckAfter = nowUtc.AddDays(-7);

            var existing = await _db.OfficialVideoSourceCatalog.ToListAsync(ct).ConfigureAwait(false);
            var byKey = existing.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var (leagueId, qid) in LeagueQids)
            {
                if (teamsSeen >= maxTeamsPerRun) break;
                ct.ThrowIfCancellationRequested();

                var from = nowUtc.AddDays(-150);
                var to = nowUtc.AddDays(60);
                var inLeague = _db.Matches.AsNoTracking()
                    .Where(m => m.LeagueId == leagueId && m.MatchDate >= from && m.MatchDate <= to);
                var teams = await inLeague.Select(m => m.HomeTeamId).Union(inLeague.Select(m => m.AwayTeamId))
                    .ToListAsync(ct).ConfigureAwait(false);
                if (teams.Count == 0) continue;
                var teamNames = await _db.Teams.AsNoTracking().Where(t => teams.Contains(t.Id))
                    .Select(t => new { t.Id, t.Name }).ToListAsync(ct).ConfigureAwait(false);

                var pending = teamNames.Where(t => !(byKey.TryGetValue("club:" + t.Id, out var r)
                                                     && r.LastCheckedAtUtc >= recheckAfter)).ToList();
                var leagueKey = "league:" + leagueId;
                var leagueDue = !(byKey.TryGetValue(leagueKey, out var lr) && lr.LastCheckedAtUtc >= recheckAfter);
                if (pending.Count == 0 && !leagueDue) continue;
                leagues++;

                IReadOnlyList<WikidataClub> clubs;
                WikidataClub? leagueItem;
                try
                {
                    clubs = await QueryClubsAsync(qid, ct).ConfigureAwait(false);
                    leagueItem = leagueDue ? await QueryItemAsync(qid, ct).ConfigureAwait(false) : null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    notes.Add($"league {leagueId}: wikidata okunamadi ({ex.GetType().Name})");
                    continue;
                }

                if (leagueItem != null)
                {
                    var rec = await VerifyAsync(leagueKey, leagueItem, leagueItem.Label, null, leagueId, OfficialVideoSourceTiers.League, nowUtc, ct).ConfigureAwait(false);
                    Upsert(byKey, rec);
                    if (rec.Status == OfficialVideoSourceCatalog.StatusVerified) verified++; else candidates++;
                }

                foreach (var team in pending)
                {
                    if (teamsSeen >= maxTeamsPerRun) break;
                    teamsSeen++;
                    var club = MatchClub(team.Name, clubs);
                    if (club == null)
                    {
                        unmapped++;
                        Upsert(byKey, new OfficialVideoSourceRecord
                        {
                            Key = "club:" + team.Id, Publisher = team.Name, Platform = "YouTube", Tier = OfficialVideoSourceTiers.Club,
                            TeamId = team.Id, ClubName = team.Name, Status = OfficialVideoSourceCatalog.StatusCandidate,
                            DiscoveredVia = "Wikidata", VerificationEvidence = $"wikidata {qid} kulüp listesinde tekil eşleşme yok",
                            CreatedAtUtc = nowUtc, LastCheckedAtUtc = nowUtc
                        });
                        continue;
                    }
                    mapped++;
                    var rec = await VerifyAsync("club:" + team.Id, club, team.Name, team.Id, null, OfficialVideoSourceTiers.Club, nowUtc, ct).ConfigureAwait(false);
                    Upsert(byKey, rec);
                    if (rec.Status == OfficialVideoSourceCatalog.StatusVerified) verified++; else candidates++;
                }
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _catalog.Invalidate();
            var report = new SourceDiscoveryReport(leagues, teamsSeen, mapped, verified, candidates, unmapped, _httpCalls, notes);
            _log.LogInformation("[VIDEO-SOURCES] kesif: lig={Leagues} takim={Teams} eslesen={Mapped} dogrulanan={Verified} aday={Cand} eslesmeyen={Unmapped} http={Http}",
                leagues, teamsSeen, mapped, verified, candidates, unmapped, _httpCalls);
            return report;
        }

        private void Upsert(Dictionary<string, OfficialVideoSourceRecord> byKey, OfficialVideoSourceRecord rec)
        {
            if (byKey.TryGetValue(rec.Key, out var row))
            {
                row.Publisher = rec.Publisher; row.YouTubeChannelId = rec.YouTubeChannelId; row.Tier = rec.Tier;
                row.TeamId = rec.TeamId; row.ClubName = rec.ClubName; row.LeagueIds = rec.LeagueIds;
                row.AllowsInAppEmbed = rec.AllowsInAppEmbed; row.DiscoveredVia = rec.DiscoveredVia;
                row.WikidataId = rec.WikidataId; row.OfficialWebsite = rec.OfficialWebsite;
                row.VerificationEvidence = rec.VerificationEvidence; row.LastCheckedAtUtc = rec.LastCheckedAtUtc;
                // Bir kez doğrulanmış kaynak geçici bir ağ hatasıyla düşürülmez; ancak yeni kanıt aksini söylerse düşer.
                if (!(row.Status == OfficialVideoSourceCatalog.StatusVerified && rec.DiscoveredVia == "Unreachable"))
                {
                    row.Status = rec.Status;
                    if (rec.Status == OfficialVideoSourceCatalog.StatusVerified) row.VerifiedAtUtc ??= rec.VerifiedAtUtc;
                }
                return;
            }
            _db.OfficialVideoSourceCatalog.Add(rec);
            byKey[rec.Key] = rec;
        }

        /// <summary>Tek kulüp/lig için kanal kanıtı toplar ve karar verir.</summary>
        private async Task<OfficialVideoSourceRecord> VerifyAsync(string key, WikidataClub item, string displayName, int? teamId,
            int? leagueId, int tier, DateTime nowUtc, CancellationToken ct)
        {
            var evidence = new List<string>();
            var siteChannels = new List<string>();
            var reachable = true;
            if (!string.IsNullOrWhiteSpace(item.Website))
            {
                var (ok, html) = await GetAsync(item.Website!, ct).ConfigureAwait(false);
                reachable = ok;
                if (ok)
                    foreach (var link in ExtractYouTubeLinks(html))
                    {
                        var id = link.StartsWith("UC", StringComparison.Ordinal) ? link : await ResolveChannelAsync(link, ct).ConfigureAwait(false);
                        if (id != null && !siteChannels.Contains(id)) siteChannels.Add(id);
                    }
                evidence.Add(ok ? $"resmî site {Host(item.Website!)} okundu, kanal bağlantıları: {string.Join(",", siteChannels)}"
                                : $"resmî site {Host(item.Website!)} okunamadı/robots izin vermedi");
            }

            // Aday sırası: Wikidata P2397 ∩ site → site → Wikidata.
            var ordered = item.ChannelIds.Where(siteChannels.Contains)
                .Concat(siteChannels).Concat(item.ChannelIds).Distinct(StringComparer.Ordinal).ToList();

            foreach (var channel in ordered.Take(3))
            {
                var wikidata = item.ChannelIds.Contains(channel);
                var site = siteChannels.Contains(channel);
                var author = await FeedAuthorAsync(channel, ct).ConfigureAwait(false);
                var authorMatches = author != null && NamesMatch(author, displayName, item);
                var proofs = (wikidata ? 1 : 0) + (site ? 1 : 0) + (authorMatches ? 1 : 0);
                if (proofs >= 2)
                {
                    evidence.Add($"kanal {channel}: " + string.Join(" + ", new[]
                    {
                        wikidata ? $"Wikidata {item.Qid} P2397" : null,
                        site ? "kulübün resmî sitesindeki bağlantı" : null,
                        authorMatches ? $"akış yazar adı \"{author}\"" : null
                    }.Where(x => x != null)));
                    return Record(key, author ?? displayName, channel, tier, teamId, displayName, leagueId,
                        OfficialVideoSourceCatalog.StatusVerified,
                        string.Join("+", new[] { wikidata ? "Wikidata" : null, site ? "OfficialSite" : null, authorMatches ? "ChannelName" : null }.Where(x => x != null)),
                        item, evidence, nowUtc);
                }
                evidence.Add($"kanal {channel}: yetersiz kanıt (wikidata={wikidata}, site={site}, yazar={author ?? "-"})");
            }

            return Record(key, displayName, ordered.FirstOrDefault(), tier, teamId, displayName, leagueId,
                OfficialVideoSourceCatalog.StatusCandidate, reachable ? "Wikidata" : "Unreachable", item, evidence, nowUtc);
        }

        private static OfficialVideoSourceRecord Record(string key, string publisher, string? channel, int tier, int? teamId, string clubName,
            int? leagueId, string status, string via, WikidataClub item, List<string> evidence, DateTime nowUtc)
            => new()
            {
                Key = key, Publisher = Trim(publisher, 160), Platform = "YouTube", YouTubeChannelId = channel, Tier = tier,
                TeamId = teamId, ClubName = teamId != null ? Trim(clubName, 160) : null,
                LeagueIds = leagueId?.ToString(), AllowsInAppEmbed = true, Status = status, DiscoveredVia = via,
                WikidataId = item.Qid, OfficialWebsite = item.Website == null ? null : Trim(item.Website, 300),
                VerificationEvidence = Trim(string.Join(" | ", evidence), 1000),
                CreatedAtUtc = nowUtc, LastCheckedAtUtc = nowUtc,
                VerifiedAtUtc = status == OfficialVideoSourceCatalog.StatusVerified ? nowUtc : null
            };

        // ── Eşleme ve kanıt yardımcıları (saf) ───────────────────────────────────────

        /// <summary>FORMAX takım adına TEK kulüp eşler; belirsizse null.</summary>
        public static WikidataClub? MatchClub(string teamName, IReadOnlyList<WikidataClub> clubs)
        {
            var team = Norm(teamName);
            if (team.Length < 3) return null;
            var aliases = TeamNameAliases.For(teamName).Select(Norm).Append(team).ToList();
            var hits = clubs.Where(c =>
            {
                var names = c.AltLabels.Append(c.Label).Select(Norm).Select(StripClubWords).Where(n => n.Length >= 3).ToList();
                return names.Any(n => aliases.Any(a => n == a || n == StripClubWords(a)))
                       || names.Any(n => aliases.Any(a => Contains(n, a) || Contains(a, n)));
            }).ToList();
            if (hits.Count <= 1) return hits.FirstOrDefault();
            // Tam eşleşen varsa o; yoksa belirsiz.
            var exact = hits.Where(c => c.AltLabels.Append(c.Label).Select(Norm).Select(StripClubWords).Any(n => aliases.Contains(n))).ToList();
            return exact.Count == 1 ? exact[0] : null;
        }

        public static IReadOnlyList<string> ExtractYouTubeLinks(string html)
            => YouTubeLink.Matches(html ?? string.Empty)
                .Select(m => m.Groups["id"].Success ? m.Groups["id"].Value : m.Groups["path"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();

        public static string? ParseCanonicalChannel(string html)
        {
            var m = CanonicalChannel.Match(html ?? string.Empty);
            return m.Success ? m.Groups["id"].Value : null;
        }

        public static bool NamesMatch(string author, string displayName, WikidataClub item)
        {
            var a = StripClubWords(Norm(author));
            if (a.Length < 3) return false;
            return item.AltLabels.Append(item.Label).Append(displayName).Select(Norm).Select(StripClubWords)
                .Where(n => n.Length >= 3).Any(n => a == n || Contains(a, n) || Contains(n, a));
        }

        private static readonly Regex ClubWords = new(@"\b(fc|cf|afc|sc|ac|as|ssc|us|rc|rcd|sk|fk|club|football|futbol|calcio|de|of|the|a f c|f c|c f|s p a|spa)\b",
            RegexOptions.CultureInvariant);

        private static string Norm(string s) => Regex.Replace(MatchVideoIdentityValidator.Fold(s), @"[^a-z0-9]+", " ").Trim();
        private static string StripClubWords(string s) => Regex.Replace(ClubWords.Replace(s, " "), @"\s+", " ").Trim();
        private static bool Contains(string hay, string needle) => needle.Length >= 5 && (" " + hay + " ").Contains(" " + needle + " ", StringComparison.Ordinal);
        private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
        private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];

        // ── Ağ ──────────────────────────────────────────────────────────────────────

        private async Task<(bool Ok, string Body)> GetAsync(string url, CancellationToken ct)
        {
            try
            {
                _httpCalls++;
                var client = _http.CreateClient(HttpClientName);
                using var res = await client.GetAsync(url, ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) return (false, string.Empty);
                var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return (true, body.Length > 3_000_000 ? body[..3_000_000] : body);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                return (false, string.Empty);
            }
        }

        private async Task<string?> ResolveChannelAsync(string path, CancellationToken ct)
        {
            var (ok, html) = await GetAsync("https://www.youtube.com/" + path, ct).ConfigureAwait(false);
            return ok ? ParseCanonicalChannel(html) : null;
        }

        private async Task<string?> FeedAuthorAsync(string channelId, CancellationToken ct)
        {
            var (ok, xml) = await GetAsync("https://www.youtube.com/feeds/videos.xml?channel_id=" + Uri.EscapeDataString(channelId), ct).ConfigureAwait(false);
            if (!ok) return null;
            try
            {
                XNamespace atom = "http://www.w3.org/2005/Atom";
                return XDocument.Parse(xml).Root?.Element(atom + "author")?.Element(atom + "name")?.Value;
            }
            catch { return null; }
        }

        private async Task<IReadOnlyList<WikidataClub>> QueryClubsAsync(string leagueQid, CancellationToken ct)
        {
            var sparql = "SELECT ?club ?clubLabel (GROUP_CONCAT(DISTINCT ?alt;separator=\"|\") AS ?alts) " +
                         "(GROUP_CONCAT(DISTINCT ?yt;separator=\",\") AS ?yts) (SAMPLE(?site) AS ?website) WHERE { " +
                         $"?club wdt:P118 wd:{leagueQid} . ?club wdt:P31 wd:Q476028 . " +
                         "OPTIONAL { ?club skos:altLabel ?alt . FILTER(LANG(?alt) = \"en\") } " +
                         "OPTIONAL { ?club wdt:P2397 ?yt } OPTIONAL { ?club wdt:P856 ?site } " +
                         "SERVICE wikibase:label { bd:serviceParam wikibase:language \"en\". } } GROUP BY ?club ?clubLabel";
            return await SparqlAsync(sparql, ct).ConfigureAwait(false);
        }

        private async Task<WikidataClub?> QueryItemAsync(string qid, CancellationToken ct)
        {
            var sparql = "SELECT ?club ?clubLabel (GROUP_CONCAT(DISTINCT ?alt;separator=\"|\") AS ?alts) " +
                         "(GROUP_CONCAT(DISTINCT ?yt;separator=\",\") AS ?yts) (SAMPLE(?site) AS ?website) WHERE { " +
                         $"VALUES ?club {{ wd:{qid} }} " +
                         "OPTIONAL { ?club skos:altLabel ?alt . FILTER(LANG(?alt) = \"en\") } " +
                         "OPTIONAL { ?club wdt:P2397 ?yt } OPTIONAL { ?club wdt:P856 ?site } " +
                         "SERVICE wikibase:label { bd:serviceParam wikibase:language \"en\". } } GROUP BY ?club ?clubLabel";
            return (await SparqlAsync(sparql, ct).ConfigureAwait(false)).FirstOrDefault();
        }

        private async Task<IReadOnlyList<WikidataClub>> SparqlAsync(string sparql, CancellationToken ct)
        {
            var url = "https://query.wikidata.org/sparql?format=json&query=" + Uri.EscapeDataString(sparql);
            var (ok, body) = await GetAsync(url, ct).ConfigureAwait(false);
            if (!ok) throw new HttpRequestException("wikidata sparql failed");
            return ParseSparql(body);
        }

        public static IReadOnlyList<WikidataClub> ParseSparql(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<WikidataClub>();
            foreach (var b in doc.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray())
            {
                string? V(string name) => b.TryGetProperty(name, out var p) ? p.GetProperty("value").GetString() : null;
                var qid = (V("club") ?? string.Empty).Split('/').Last();
                var label = V("clubLabel") ?? qid;
                if (string.IsNullOrWhiteSpace(qid)) continue;
                list.Add(new WikidataClub(qid, label,
                    (V("alts") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries),
                    (V("yts") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => Regex.IsMatch(x, "^UC[A-Za-z0-9_-]{22}$")).ToList(),
                    V("website")));
            }
            return list;
        }
    }
}
