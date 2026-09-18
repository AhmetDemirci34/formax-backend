using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.OfficialSources.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.OfficialSources
{
    /// <summary>Tek turun ölçülmüş sonucu (teşhis ve kabul kanıtı).</summary>
    public sealed record UefaFixtureSyncReport(
        string Outcome,
        string? Detail,
        int SourceRecords,
        int UpcomingInScope,
        int Created,
        int Updated,
        int Unchanged,
        int Ambiguous,
        int UnresolvedTeams,
        int TeamIdentitiesLearned,
        IReadOnlyList<string> UnmatchedTeamReport,
        IReadOnlyDictionary<int, int> CreatedByLeague)
    {
        public const string Ok = "Ok";
        public const string SourceFailed = "SourceFailed";

        public bool Succeeded => Outcome == Ok;
    }

    /// <summary>
    /// RESMÎ UEFA FİKSTÜR ALIMI — Şampiyonlar Ligi (2), Avrupa Ligi (3) ve Konferans Ligi (848) yaklaşan
    /// maçlarını API-FOOTBALL KULLANMADAN kanonik <c>Matches</c> satırlarına yazar.
    ///
    /// NEDEN VAR (ölçüldü 18.09.2026): api-football FREE planı ileri takvimi vermiyor (next/last/season kapalı,
    /// date yalnız ±1 gün). Resmî UEFA maç merkezi ise üç müsabakanın haftalar sonrasını yayımlıyor
    /// (18.09 ölçümü: her müsabakada 72 UPCOMING maç, 13.10–26.11).
    ///
    /// SINIRLAR (bilerek):
    ///  • YALNIZ yaklaşan maç yazılır; geçmiş maçın skoru/durumu bu servisin işi DEĞİLDİR (OfficialResultBot yazar).
    ///  • Takım ASLA yaratılmaz. Kanonik takım bulunamazsa maç YAZILMAZ ve eşleşmeyen takım raporlanır.
    ///  • Kanonik maç zaten varsa (api-football önce yazmış olabilir) İKİNCİ SATIR YAZILMAZ; o maça resmî
    ///    kaynak referansı (<c>OfficialMatchLinks</c>) bağlanır.
    ///  • Kaynak okuması başarısızsa hiçbir şey yazılmaz ve tur BAŞARISIZ sayılır (yarım takvim "tamam" değildir).
    /// </summary>
    public sealed class OfficialUefaFixtureService
    {
        public const string ScheduleSourceTag = "official:" + UefaMatchApiSource.Key;

        private readonly FormaxDbContext _db;
        private readonly UefaMatchApiSource _source;
        private readonly IConfiguration _config;
        private readonly ILogger<OfficialUefaFixtureService> _log;

        public OfficialUefaFixtureService(
            FormaxDbContext db, UefaMatchApiSource source, IConfiguration config,
            ILogger<OfficialUefaFixtureService> log)
        {
            _db = db; _source = source; _config = config; _log = log;
        }

        /// <summary>İleri pencere (gün) — en az 60.</summary>
        private int ForwardDays => Math.Clamp(
            _config.GetValue("OfficialSources:UefaFixtures:ForwardDays", 90), 60, 400);

        /// <summary>
        /// Geriye bakış (gün) — YAZIM İÇİN DEĞİL: doğrulanmış maç bağlantılarından UEFA takım kimliği öğrenmek için.
        /// Sonuç botu bu maçları zaten organizasyon + sıralı çift + pencere ile eşleştirdi; en güçlü kimlik kanıtı.
        /// </summary>
        private int IdentitySeedDays => Math.Clamp(
            _config.GetValue("OfficialSources:UefaFixtures:IdentitySeedDays", 30), 0, 120);

        public async Task<UefaFixtureSyncReport> ImportAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var from = nowUtc.Date.AddDays(-IdentitySeedDays);
            var to = nowUtc.Date.AddDays(ForwardDays);
            var round = new OfficialRoundContext($"uefafx:{nowUtc:yyyyMMddHH}", nowUtc, OfficialPurposes.Schedule);

            var read = await _source.ReadFixturesAsync(from, to, round, ct).ConfigureAwait(false);
            if (!read.Ok || read.Value == null)
                return Failed(read.Outcome + (read.Detail == null ? "" : ":" + read.Detail));

            var records = read.Value;
            var unmatched = new List<string>();
            var createdByLeague = new Dictionary<int, int>();
            int created = 0, updated = 0, unchanged = 0, ambiguous = 0, unresolved = 0, upcoming = 0;

            // ── 1. Kimlik tohumlama: doğrulanmış maç bağlantıları → UEFA takım kimliği ──────────
            var learned = await SeedTeamIdentitiesAsync(records, nowUtc, ct).ConfigureAwait(false);

            // ── 2. Çalışma kümesi: YALNIZ ileri tarihli, kapsam içi, oynanmamış maçlar ─────────
            var work = new List<(OfficialMatchRecord Rec, int LeagueId, DateTime Kickoff)>();
            foreach (var r in records)
            {
                if (r.KickoffUtc is not { } kickoff || kickoff <= nowUtc) continue;
                if (!TryLeague(r, out var leagueId)) continue;
                if (r.Status is OfficialMatchStatuses.Finished or OfficialMatchStatuses.FinishedAfterExtraTime
                    or OfficialMatchStatuses.FinishedAfterPenalties) continue;
                work.Add((r, leagueId, kickoff));
            }
            upcoming = work.Count;
            if (upcoming == 0)
                return new UefaFixtureSyncReport(UefaFixtureSyncReport.Ok, "NoUpcomingFixtures",
                    records.Count, 0, 0, 0, 0, 0, 0, learned, unmatched, createdByLeague);

            // ── 3. Tek seferde yükle: mevcut bağlantılar, kimlik haritası, aday maçlar ─────────
            var sourceIds = work.Select(w => w.Rec.OfficialMatchId).ToList();
            var links = await _db.OfficialMatchLinks
                .Where(l => l.SourceKey == UefaMatchApiSource.Key && sourceIds.Contains(l.OfficialMatchId))
                .ToDictionaryAsync(l => l.OfficialMatchId, StringComparer.Ordinal, ct).ConfigureAwait(false);

            var identities = await LoadIdentityMapAsync(ct).ConfigureAwait(false);
            var leagues = work.Select(w => w.LeagueId).Distinct().ToList();
            var windowFrom = work.Min(w => w.Kickoff) - OfficialFixtureIdentityPolicy.KickoffTolerance;
            var windowTo = work.Max(w => w.Kickoff) + OfficialFixtureIdentityPolicy.KickoffTolerance;
            var candidates = await _db.Matches.AsNoTracking()
                .Where(m => leagues.Contains(m.LeagueId) && m.MatchDate >= windowFrom && m.MatchDate <= windowTo)
                .Select(m => new CanonicalMatchCandidate(m.Id, m.LeagueId, m.HomeTeamId, m.AwayTeamId, m.MatchDate, m.ExternalMatchId))
                .ToListAsync(ct).ConfigureAwait(false);

            // Ad ile çözüm YALNIZ o organizasyonda oynadığı bilinen takımlar arasında ve TEK adayla yapılır.
            var byLeagueTeams = await LoadLeagueTeamsAsync(leagues, ct).ConfigureAwait(false);

            foreach (var (rec, leagueId, kickoff) in work)
            {
                ct.ThrowIfCancellationRequested();

                var home = ResolveTeam(rec, "home", leagueId, identities, byLeagueTeams, nowUtc, out var homeNote);
                var away = ResolveTeam(rec, "away", leagueId, identities, byLeagueTeams, nowUtc, out var awayNote);
                if (home == null || away == null)
                {
                    unresolved++;
                    if (home == null) unmatched.Add($"lig {leagueId} · uefaTeam {rec.Extra?.GetValueOrDefault("homeTeamId")} · {rec.HomeName} → {homeNote}");
                    if (away == null) unmatched.Add($"lig {leagueId} · uefaTeam {rec.Extra?.GetValueOrDefault("awayTeamId")} · {rec.AwayName} → {awayNote}");
                    continue;
                }
                if (home.Value == away.Value) { ambiguous++; continue; }

                // 3a. Bu UEFA maçı zaten kanonik bir maça bağlıysa yalnız GÜNCELLENİR.
                if (links.TryGetValue(rec.OfficialMatchId, out var link))
                {
                    var tracked = await _db.Matches.FirstOrDefaultAsync(m => m.Id == link.MatchId, ct).ConfigureAwait(false);
                    if (tracked == null) { ambiguous++; continue; }
                    if (Apply(tracked, rec, leagueId, kickoff, nowUtc)) updated++; else unchanged++;
                    Refresh(link, rec, kickoff, nowUtc);
                    continue;
                }

                // 3b. Kanonik kimlik: aynı maç api-football'dan zaten gelmiş olabilir → DUPLICATE YAZILMAZ.
                var decision = OfficialFixtureIdentityPolicy.Resolve(leagueId, home.Value, away.Value, kickoff, candidates);
                if (decision.Outcome == CanonicalFixtureDecision.Ambiguous)
                {
                    ambiguous++;
                    _log.LogWarning("[UEFA FIXTURE] {Uefa} {Home}–{Away} {Kickoff:u} belirsiz eşleşme ({Reason}) — yazılmadı.",
                        rec.OfficialMatchId, rec.HomeName, rec.AwayName, kickoff, decision.Reason);
                    continue;
                }

                if (decision.Outcome == CanonicalFixtureDecision.Adopt && decision.MatchId is { } existingId)
                {
                    var tracked = await _db.Matches.FirstOrDefaultAsync(m => m.Id == existingId, ct).ConfigureAwait(false);
                    if (tracked == null) { ambiguous++; continue; }
                    if (Apply(tracked, rec, leagueId, kickoff, nowUtc)) updated++; else unchanged++;
                    _db.OfficialMatchLinks.Add(NewLink(existingId, rec, kickoff, nowUtc));
                    continue;
                }

                var match = new Match
                {
                    LeagueId = leagueId,
                    League = LeagueName(leagueId, rec),
                    MatchDate = kickoff,
                    Status = CanonicalStatus(rec.Status),
                    HomeTeamId = home.Value,
                    AwayTeamId = away.Value,
                    HomeScore = 0,
                    AwayScore = 0,
                    Venue = Trim(rec.Venue, 200),
                    Round = Trim(rec.Extra?.GetValueOrDefault("matchday") ?? rec.Extra?.GetValueOrDefault("round"), 100),
                    ScheduleSource = ScheduleSourceTag,
                    KickoffPrecision = KickoffPrecisions.Confirmed,
                    ScheduleVerifiedAtUtc = nowUtc,
                    ScheduleRefreshAttemptedAtUtc = nowUtc,
                    CreatedAt = nowUtc
                };
                _db.Matches.Add(match);
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);   // Id gerekir (bağlantı satırı için)
                _db.OfficialMatchLinks.Add(NewLink(match.Id, rec, kickoff, nowUtc));
                candidates.Add(new CanonicalMatchCandidate(match.Id, leagueId, home.Value, away.Value, kickoff, null));
                created++;
                createdByLeague[leagueId] = createdByLeague.GetValueOrDefault(leagueId) + 1;
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            _log.LogInformation(
                "[UEFA FIXTURE] tur: kaynak={Source} yaklaşan={Upcoming} yeni={Created} güncel={Updated} " +
                "değişmedi={Unchanged} belirsiz={Ambiguous} takımÇözülemedi={Unresolved} kimlikÖğrenildi={Learned}",
                records.Count, upcoming, created, updated, unchanged, ambiguous, unresolved, learned);

            return new UefaFixtureSyncReport(UefaFixtureSyncReport.Ok, read.Detail, records.Count, upcoming,
                created, updated, unchanged, ambiguous, unresolved, learned,
                unmatched.Distinct().ToList(), createdByLeague);
        }

        private static UefaFixtureSyncReport Failed(string detail)
            => new(UefaFixtureSyncReport.SourceFailed, detail, 0, 0, 0, 0, 0, 0, 0, 0,
                Array.Empty<string>(), new Dictionary<int, int>());

        // ── Kimlik tohumlama ─────────────────────────────────────────────────────

        /// <summary>
        /// Doğrulanmış maç bağlantılarından UEFA takım kimliği öğrenir. Bu, isim tahmini DEĞİLDİR: sonuç botu
        /// o maçı organizasyon + sıralı takım çifti + başlama penceresiyle eşleştirip bağlantıyı yazmıştı.
        /// </summary>
        private async Task<int> SeedTeamIdentitiesAsync(
            IReadOnlyList<OfficialMatchRecord> records, DateTime nowUtc, CancellationToken ct)
        {
            var ids = records.Select(r => r.OfficialMatchId).Distinct().ToList();
            if (ids.Count == 0) return 0;

            var linked = await (from l in _db.OfficialMatchLinks.AsNoTracking()
                                join m in _db.Matches.AsNoTracking() on l.MatchId equals m.Id
                                where l.SourceKey == UefaMatchApiSource.Key && ids.Contains(l.OfficialMatchId)
                                select new { l.OfficialMatchId, m.HomeTeamId, m.AwayTeamId })
                .ToListAsync(ct).ConfigureAwait(false);
            if (linked.Count == 0) return 0;

            var byMatch = linked.ToDictionary(x => x.OfficialMatchId, StringComparer.Ordinal);
            var existing = await LoadIdentityMapAsync(ct).ConfigureAwait(false);
            var learned = 0;

            foreach (var r in records)
            {
                if (!byMatch.TryGetValue(r.OfficialMatchId, out var canonical)) continue;
                foreach (var (side, teamId) in new[] { ("home", canonical.HomeTeamId), ("away", canonical.AwayTeamId) })
                {
                    var providerTeamId = r.Extra?.GetValueOrDefault(side + "TeamId");
                    if (string.IsNullOrWhiteSpace(providerTeamId)) continue;
                    if (existing.ContainsKey(providerTeamId!)) continue;
                    _db.TeamProviderIdentities.Add(new TeamProviderIdentity
                    {
                        TeamId = teamId,
                        Provider = TeamIdentityProviders.Uefa,
                        ProviderTeamId = providerTeamId!,
                        ProviderTeamName = Trim(side == "home" ? r.HomeName : r.AwayName, 200),
                        MatchedBy = TeamIdentityEvidence.VerifiedMatchLink,
                        FirstSeenUtc = nowUtc,
                        VerifiedAtUtc = nowUtc
                    });
                    existing[providerTeamId!] = teamId;
                    learned++;
                }
            }
            if (learned > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return learned;
        }

        private async Task<Dictionary<string, int>> LoadIdentityMapAsync(CancellationToken ct)
            => await _db.TeamProviderIdentities.AsNoTracking()
                .Where(i => i.Provider == TeamIdentityProviders.Uefa)
                .ToDictionaryAsync(i => i.ProviderTeamId, i => i.TeamId, StringComparer.Ordinal, ct)
                .ConfigureAwait(false);

        /// <summary>Organizasyon başına, o organizasyonda oynadığı BİLİNEN kanonik takımlar (ad çözümünün kapsamı).</summary>
        private async Task<Dictionary<int, List<(int Id, string Name)>>> LoadLeagueTeamsAsync(
            IReadOnlyCollection<int> leagues, CancellationToken ct)
        {
            // İki taraf AYRI projeksiyonla okunur: "from side in new[]{...}" sorgusu EF'te çevrilemiyor.
            var pairs = await _db.Matches.AsNoTracking()
                .Where(m => leagues.Contains(m.LeagueId))
                .Select(m => new { m.LeagueId, m.HomeTeamId, m.AwayTeamId })
                .Distinct().ToListAsync(ct).ConfigureAwait(false);
            var rows = pairs
                .SelectMany(p => new[]
                {
                    new { p.LeagueId, TeamId = p.HomeTeamId },
                    new { p.LeagueId, TeamId = p.AwayTeamId }
                })
                .Distinct().ToList();

            var teamIds = rows.Select(r => r.TeamId).Distinct().ToList();
            var names = await _db.Teams.AsNoTracking().Where(t => teamIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name }).ToListAsync(ct).ConfigureAwait(false);
            var nameById = names.ToDictionary(t => t.Id, t => t.Name);

            return rows.GroupBy(r => r.LeagueId).ToDictionary(
                g => g.Key,
                g => g.Where(x => nameById.ContainsKey(x.TeamId))
                      .Select(x => (x.TeamId, nameById[x.TeamId])).Distinct().ToList());
        }

        /// <summary>
        /// Kanonik takımı çözer. Sıra: (1) kalıcı kimlik haritası, (2) AYNI ORGANİZASYONDA oynadığı bilinen
        /// takımlar arasında TEK adayla ad eşleşmesi. Hiçbiri değilse null — takım YARATILMAZ.
        /// </summary>
        private int? ResolveTeam(
            OfficialMatchRecord rec, string side, int leagueId,
            Dictionary<string, int> identities, Dictionary<int, List<(int Id, string Name)>> byLeagueTeams,
            DateTime nowUtc, out string note)
        {
            var providerTeamId = rec.Extra?.GetValueOrDefault(side + "TeamId");
            if (string.IsNullOrWhiteSpace(providerTeamId)) { note = "SourceTeamIdMissing"; return null; }
            if (identities.TryGetValue(providerTeamId!, out var known)) { note = "Identity"; return known; }

            var sourceNames = new[]
            {
                side == "home" ? rec.HomeName : rec.AwayName,
                rec.Extra?.GetValueOrDefault(side + "AltName"),
                rec.Extra?.GetValueOrDefault(side + "AltName2")
            }.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal).ToList();

            var pool = byLeagueTeams.TryGetValue(leagueId, out var teams)
                ? teams
                : new List<(int Id, string Name)>();
            var hits = pool.Where(t => sourceNames.Any(n => OfficialTeamNameMatcher.SameTeam(n, t.Name)))
                .Select(t => t.Id).Distinct().ToList();

            if (hits.Count == 1)
            {
                _db.TeamProviderIdentities.Add(new TeamProviderIdentity
                {
                    TeamId = hits[0],
                    Provider = TeamIdentityProviders.Uefa,
                    ProviderTeamId = providerTeamId!,
                    ProviderTeamName = Trim(sourceNames.FirstOrDefault(), 200),
                    MatchedBy = TeamIdentityEvidence.NameUniqueInCompetition,
                    FirstSeenUtc = nowUtc,
                    VerifiedAtUtc = nowUtc
                });
                identities[providerTeamId!] = hits[0];
                note = "NameUniqueInCompetition";
                return hits[0];
            }

            note = hits.Count > 1 ? "AmbiguousName:" + string.Join(",", hits) : "NoCandidate";
            return null;
        }

        // ── Kanonik yazım ────────────────────────────────────────────────────────

        /// <summary>
        /// Mevcut kanonik maça resmî takvim bilgisini uygular. KESİN SONUÇ ASLA BOZULMAZ: bitmiş maçın durumu,
        /// skoru ve sonuç damgası bu servisin işi değildir (OfficialResultBot sahibi). true = bir alan değişti.
        /// </summary>
        private static bool Apply(Match tracked, OfficialMatchRecord rec, int leagueId, DateTime kickoff, DateTime nowUtc)
        {
            var changed = false;
            if (string.Equals(tracked.Status, MatchStatuses.Finished, StringComparison.OrdinalIgnoreCase))
            {
                tracked.ScheduleRefreshAttemptedAtUtc = nowUtc;
                return false;
            }

            if (tracked.MatchDate != kickoff) { tracked.MatchDate = kickoff; changed = true; }

            var status = CanonicalStatus(rec.Status);
            if (status != null && !string.Equals(tracked.Status, status, StringComparison.Ordinal))
            {
                tracked.Status = status;
                changed = true;
            }
            if (rec.Venue is { Length: > 0 } venue && tracked.Venue != venue) { tracked.Venue = Trim(venue, 200); changed = true; }
            var round = Trim(rec.Extra?.GetValueOrDefault("matchday") ?? rec.Extra?.GetValueOrDefault("round"), 100);
            if (round != null && tracked.Round != round) { tracked.Round = round; changed = true; }
            if (tracked.LeagueId != leagueId) { tracked.LeagueId = leagueId; changed = true; }

            tracked.ScheduleSource = ScheduleSourceTag;
            tracked.KickoffPrecision = KickoffPrecisions.Confirmed;
            tracked.ScheduleVerifiedAtUtc = nowUtc;
            tracked.ScheduleRefreshAttemptedAtUtc = nowUtc;
            return changed;
        }

        /// <summary>Kaynak durumu → kanonik durum. Bitmiş durumlar BURADA yazılmaz (sonuç botunun işi).</summary>
        public static string? CanonicalStatus(string sourceStatus) => sourceStatus switch
        {
            OfficialMatchStatuses.Scheduled => MatchStatuses.NotStarted,
            OfficialMatchStatuses.Live => MatchStatuses.Live,
            OfficialMatchStatuses.Postponed => MatchStatuses.Postponed,
            OfficialMatchStatuses.Cancelled => MatchStatuses.Cancelled,
            OfficialMatchStatuses.Abandoned => MatchStatuses.Abandoned,
            OfficialMatchStatuses.Suspended => MatchStatuses.Live,
            _ => null
        };

        private static OfficialMatchLink NewLink(int matchId, OfficialMatchRecord rec, DateTime kickoff, DateTime nowUtc)
            => new()
            {
                MatchId = matchId,
                SourceKey = UefaMatchApiSource.Key,
                OfficialMatchId = rec.OfficialMatchId,
                OfficialUrl = Trim(rec.OfficialUrl, 500),
                OfficialHomeName = Trim(rec.HomeName, 200) ?? string.Empty,
                OfficialAwayName = Trim(rec.AwayName, 200) ?? string.Empty,
                OfficialKickoffUtc = kickoff,
                OfficialVenue = Trim(rec.Venue, 200),
                OfficialStatus = Trim(rec.Status, 32),
                LinkedAtUtc = nowUtc,
                VerifiedAtUtc = nowUtc
            };

        private static void Refresh(OfficialMatchLink link, OfficialMatchRecord rec, DateTime kickoff, DateTime nowUtc)
        {
            link.OfficialKickoffUtc = kickoff;
            link.OfficialStatus = Trim(rec.Status, 32);
            link.OfficialVenue = Trim(rec.Venue, 200) ?? link.OfficialVenue;
            link.VerifiedAtUtc = nowUtc;
        }

        private static bool TryLeague(OfficialMatchRecord rec, out int leagueId)
        {
            leagueId = 0;
            var raw = rec.Extra?.GetValueOrDefault("competitionId");
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var competitionId)) return false;
            if (!UefaMatchApiSource.LeagueByCompetition.TryGetValue(competitionId, out leagueId)) return false;
            return LockedCompetitions.IsLocked(leagueId);
        }

        private static string LeagueName(int leagueId, OfficialMatchRecord rec) => leagueId switch
        {
            LockedCompetitions.ChampionsLeague => "UEFA Champions League",
            LockedCompetitions.EuropaLeague => "UEFA Europa League",
            LockedCompetitions.ConferenceLeague => "UEFA Europa Conference League",
            _ => string.Empty
        };

        private static string? Trim(string? value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length > max ? value[..max] : value;
    }
}
