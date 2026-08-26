using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Fixtures;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Normalize.Models;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Persistence;

/// <summary>
/// GDP maç kalıcılaştırıcısı (EF Core).
/// GDP kendi maç aggregate'ını OLUŞTURMAZ: maç verisi mevcut Domain <see cref="Match"/> (ve <see cref="Team"/>)
/// aggregate'ına yazılır. GDP yalnızca METADATA saklar (link + provenance + conflict resolution + provider reference).
///
/// KİMLİK KÖPRÜSÜ (maç iki kez oluşmasın):
///   1) <see cref="GdpMatchLink"/> → daha önce bağlanmış Domain Match doğrudan kullanılır.
///   2) Link yoksa, fikstürün DOĞAL KİMLİĞİ (FORMAX_MATCH_ID = <see cref="FormaxMatchIdFactory"/>:
///      slug(ev)|slug(deplasman)|UTC gün) ile MEVCUT Matches taranır. Tek ve kesin eşleşme varsa
///      YENİ MAÇ AÇILMAZ; mevcut Match benimsenir (adopt) ve link kurulur.
///      Aynı kimliğe birden fazla mevcut Match düşerse eşleştirme BELİRSİZDİR → tahmin edilmez,
///      yeni maç da açılmaz; sonuç Failed olarak raporlanır.
///   3) Hiçbir eşleşme yoksa ancak o zaman yeni Domain Match açılır.
///
/// EZME KORUMASI: Başka bir sağlayıcıya ait (ExternalMatchId dolu) veya bu çağrıda benimsenen mevcut
/// maçlarda GDP yalnızca BOŞ kanonik alanları doldurur; takım/tarih/skor/durum alanlarını EZMEZ.
///
/// Yalnızca DEĞİŞEN alanlar güncellenir; hiçbir şey değişmediyse Unchanged. Transaction + rollback; idempotent.
/// </summary>
public sealed class GdpMatchPersister : IGdpMatchPersister
{
    private readonly FormaxDbContext _db;
    private readonly FormaxMatchIdFactory _matchIdFactory;
    private readonly TeamIdentityResolver _teamIdentity;

    /// <summary>Kilitli müsabaka kapsamı (Coverage:LeagueAllowList). Boş = kısıtlama yok.</summary>
    private readonly HashSet<int> _allowedLeagues;

    /// <summary>Kanonik takım slug'ı → mevcut Team.Id (deterministik: en küçük Id). Scope başına bir kez kurulur.</summary>
    private Dictionary<string, int>? _teamSlugIndex;

    public GdpMatchPersister(
        FormaxDbContext db,
        FormaxMatchIdFactory matchIdFactory,
        TeamIdentityResolver teamIdentity,
        Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _db = db;
        _matchIdFactory = matchIdFactory;
        _teamIdentity = teamIdentity;
        _allowedLeagues = Formax.Infrastructure.BackgroundJobs.CoveragePolicy.LeagueAllowList(config);
    }

    public async Task<GdpPersistOutcome> PersistAsync(GdpPersistRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.FormaxMatchId))
            return Fail(string.Empty, "FormaxMatchId boş.");

        var fixture = request.Merge.Value;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var link = await _db.GdpMatchLinks
                .Include(l => l.Provenance)
                .Include(l => l.ConflictResolutions)
                .Include(l => l.ProviderReferences)
                .FirstOrDefaultAsync(l => l.FormaxMatchId == request.FormaxMatchId, cancellationToken)
                .ConfigureAwait(false);

            // ---- 1) Link üzerinden bağlı Domain Match ----
            Match? match = link is null
                ? null
                : await _db.Matches.FirstOrDefaultAsync(m => m.Id == link.MatchId, cancellationToken).ConfigureAwait(false);

            // ---- 2) Link yoksa: doğal kimlikle MEVCUT Match'i ara (duplicate açmamak için) ----
            var adopted = false;
            if (match is null)
            {
                var resolution = await ResolveExistingMatchAsync(fixture, request, cancellationToken).ConfigureAwait(false);
                if (resolution.Ambiguous)
                {
                    await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                    return Fail(request.FormaxMatchId,
                        $"Doğal kimlik ({resolution.NaturalId}) birden fazla mevcut Match'e denk geliyor " +
                        $"({string.Join(",", resolution.CandidateIds)}); eşleştirme tahmin edilmedi, yeni maç açılmadı.");
                }

                match = resolution.Match;
                adopted = match is not null;
            }

            var changed = false;
            var created = false;

            if (match is null)
            {
                // ---- KİLİTLİ MÜSABAKA KAPSAMI ----
                // Kapsam dışı (ya da lig kimliği güvenle çözülemeyen) bir müsabaka için GDP
                // canonical Match AÇMAZ. Ölçüldü: bu kapı olmadan lig kimliği 0 kalan kayıtlar
                // (ör. "1. Fußball-Bundesliga 2026/2027", 387 satır) canonical Matches'e giriyordu.
                // Allow-list boşsa kısıtlama yoktur (geri-uyum).
                if (_allowedLeagues.Count > 0)
                {
                    var scopeLeagueId = await ResolveLeagueIdAsync(fixture.Competition?.Name, cancellationToken).ConfigureAwait(false);
                    if (scopeLeagueId is not int sid || !_allowedLeagues.Contains(sid))
                    {
                        await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                        return Fail(request.FormaxMatchId,
                            $"Müsabaka kilitli kapsam dışında ('{fixture.Competition?.Name}'); canonical Match açılmadı.");
                    }
                }

                // ---- 3) Hiçbir eşleşme yok → yeni Domain Match ----
                var homeTeamId = await ResolveTeamAsync(fixture.HomeTeam?.Name, cancellationToken).ConfigureAwait(false);
                var awayTeamId = await ResolveTeamAsync(fixture.AwayTeam?.Name, cancellationToken).ConfigureAwait(false);
                if (homeTeamId is null || awayTeamId is null)
                {
                    await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                    return Fail(request.FormaxMatchId, "Ev/deplasman takım adı eksik; Domain Match beslenemez.");
                }

                var competitionId = await ResolveCompetitionAsync(fixture.Competition?.Name, fixture.Competition?.Country?.Name, cancellationToken).ConfigureAwait(false);
                var venueId = await ResolveVenueAsync(fixture.Venue, cancellationToken).ConfigureAwait(false);

                match = new Match { CreatedAt = DateTime.UtcNow };
                ApplyMatchFields(match, fixture, homeTeamId.Value, awayTeamId.Value, competitionId, venueId);
                ApplyExternalMatchId(match, request);

                // Lig kimliği yalnız GÜVENLİ eşleşmede yazılır (tek-değerli ad eşleşmesi); yoksa 0 kalır.
                // GDP maçının FORMAX ingestion/AI zincirine (standings, team stats, Discover havuzu)
                // bağlanabilmesi bu alana bağlıdır.
                var newLeagueId = await ResolveLeagueIdAsync(fixture.Competition?.Name, cancellationToken).ConfigureAwait(false);
                if (newLeagueId is int lid) match.LeagueId = lid;

                _db.Matches.Add(match);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // match.Id

                created = true;
                changed = true;
            }
            else
            {
                var competitionId = await ResolveCompetitionAsync(fixture.Competition?.Name, fixture.Competition?.Country?.Name, cancellationToken).ConfigureAwait(false);
                var venueId = await ResolveVenueAsync(fixture.Venue, cancellationToken).ConfigureAwait(false);

                // Benimsenen (adopt) ya da başka sağlayıcıya ait maçta GDP yalnızca BOŞ alanları doldurur.
                if (adopted || !string.IsNullOrWhiteSpace(match.ExternalMatchId))
                {
                    changed |= FillMissingMatchFields(match, fixture, competitionId, venueId);

                    // Mevcut maçta lig kimliği hiç yoksa (0) güvenli eşleşmeyle doldur; DOLU ise EZME.
                    if (match.LeagueId == 0)
                    {
                        var fillLeagueId = await ResolveLeagueIdAsync(fixture.Competition?.Name, cancellationToken).ConfigureAwait(false);
                        if (fillLeagueId is int fid) { match.LeagueId = fid; changed = true; }
                    }
                }
                else
                {
                    var homeTeamId = await ResolveTeamAsync(fixture.HomeTeam?.Name, cancellationToken).ConfigureAwait(false);
                    var awayTeamId = await ResolveTeamAsync(fixture.AwayTeam?.Name, cancellationToken).ConfigureAwait(false);
                    if (homeTeamId is null || awayTeamId is null)
                    {
                        await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                        return Fail(request.FormaxMatchId, "Ev/deplasman takım adı eksik; Domain Match beslenemez.");
                    }

                    changed |= ApplyMatchFields(match, fixture, homeTeamId.Value, awayTeamId.Value, competitionId, venueId);
                }

                changed |= ApplyExternalMatchId(match, request);
            }

            GdpPersistStatus status;

            if (link is null)
            {
                link = new GdpMatchLink { FormaxMatchId = request.FormaxMatchId, MatchId = match.Id };
                link.Round = NormalizeRound(fixture.Round);
                link.Provenance = BuildProvenance(request);
                link.ConflictResolutions = BuildResolutions(request);
                link.ProviderReferences = BuildReferences(request);
                _db.GdpMatchLinks.Add(link);

                status = GdpPersistStatus.Inserted;
                changed = true;
            }
            else
            {
                if (link.MatchId != match.Id) { link.MatchId = match.Id; changed = true; }

                var round = NormalizeRound(fixture.Round);
                if (!string.Equals(link.Round, round, StringComparison.Ordinal)) { link.Round = round; changed = true; }

                changed |= SyncProvenance(link, request);
                changed |= SyncResolutions(link, request);
                changed |= SyncReferences(link, request);

                if (!changed)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new GdpPersistOutcome { FormaxMatchId = request.FormaxMatchId, Status = GdpPersistStatus.Unchanged };
                }

                status = created ? GdpPersistStatus.Inserted : GdpPersistStatus.Updated;
            }

            link.LastUpdatedUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new GdpPersistOutcome { FormaxMatchId = request.FormaxMatchId, Status = status };
        }
        catch (Exception ex)
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return Fail(request.FormaxMatchId, ex.Message);
        }
    }

    // ---- Doğal kimlik köprüsü (mevcut Match'i bul) ----

    private sealed record ExistingMatchResolution(Match? Match, bool Ambiguous, string? NaturalId, IReadOnlyList<int> CandidateIds)
    {
        public static readonly ExistingMatchResolution None = new(null, false, null, Array.Empty<int>());
    }

    /// <summary>
    /// Fikstürün doğal kimliğini (FORMAX_MATCH_ID) mevcut kimlik otoritesiyle hesaplar ve aynı UTC gündeki
    /// Matches kayıtları içinde AYNI kimliği üreten maçı arar. Takım adları kör string olarak değil,
    /// <see cref="TeamIdentityResolver"/> slug'ı üzerinden (kimlik hash'inin içinde) karşılaştırılır.
    /// Kickoff yoksa kimlik üretilemez → eşleştirme denenmez.
    /// </summary>
    private async Task<ExistingMatchResolution> ResolveExistingMatchAsync(
        NormalizedFixture fixture, GdpPersistRequest request, CancellationToken cancellationToken)
    {
        // A) EN GÜÇLÜ SİNYAL: provider referansları arasında api-football fixture id varsa,
        //    canonical Match zaten ExternalMatchId ile bu id'yi taşıyordur → doğrudan bağlan.
        //    Tahmin değil, birebir provider kimliği eşleşmesidir.
        var apiFootballId = ExtractApiFootballFixtureId(request);
        if (!string.IsNullOrWhiteSpace(apiFootballId))
        {
            var byExternal = await _db.Matches
                .FirstOrDefaultAsync(m => m.ExternalMatchId == apiFootballId, cancellationToken)
                .ConfigureAwait(false);

            if (byExternal is not null)
                return new ExistingMatchResolution(byExternal, false, apiFootballId, new[] { byExternal.Id });
        }

        // B) Doğal kimlik (FORMAX_MATCH_ID) ile mevcut Matches taraması.
        var home = fixture.HomeTeam?.Name;
        var away = fixture.AwayTeam?.Name;
        if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
            return ExistingMatchResolution.None;

        if (fixture.KickoffUtc is not DateTimeOffset kickoff)
            return ExistingMatchResolution.None;

        var kickoffUtc = kickoff.UtcDateTime;
        var naturalId = _matchIdFactory.Create(kickoffUtc, home!, away!);

        var dayStart = kickoffUtc.Date;
        var dayEnd = dayStart.AddDays(1);

        var candidates = await (
            from m in _db.Matches.AsNoTracking()
            join h in _db.Teams on m.HomeTeamId equals h.Id
            join a in _db.Teams on m.AwayTeamId equals a.Id
            where m.MatchDate >= dayStart && m.MatchDate < dayEnd
            select new { m.Id, m.MatchDate, Home = h.Name, Away = a.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hits = candidates
            .Where(c => string.Equals(_matchIdFactory.Create(c.MatchDate, c.Home, c.Away), naturalId, StringComparison.Ordinal))
            .Select(c => c.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (hits.Count == 0)
            return ExistingMatchResolution.None;

        if (hits.Count > 1)
            return new ExistingMatchResolution(null, true, naturalId, hits);

        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == hits[0], cancellationToken).ConfigureAwait(false);
        return match is null
            ? ExistingMatchResolution.None
            : new ExistingMatchResolution(match, false, naturalId, hits);
    }

    // ---- Domain Match/Team ----

    private async Task<int?> ResolveTeamAsync(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // 1) Birebir ad.
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Name == name, cancellationToken).ConfigureAwait(false);
        if (team is not null)
            return team.Id;

        // 2) Kanonik takım kimliği (mevcut Team identity motoru) — "Man Utd" ↔ "Manchester United".
        var slug = _teamIdentity.Slug(name!);
        var index = await GetTeamSlugIndexAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(slug) && index.TryGetValue(slug, out var existingId))
            return existingId;

        // 3) Hiç eşleşme yok → yeni Team.
        team = new Team { Name = name!, CreatedAt = DateTime.UtcNow };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // team.Id

        if (!string.IsNullOrEmpty(slug))
            index[slug] = team.Id;

        return team.Id;
    }

    private async Task<Dictionary<string, int>> GetTeamSlugIndexAsync(CancellationToken cancellationToken)
    {
        if (_teamSlugIndex is not null)
            return _teamSlugIndex;

        var teams = await _db.Teams
            .AsNoTracking()
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in teams.OrderBy(t => t.Id))
        {
            if (string.IsNullOrWhiteSpace(t.Name)) continue;
            var slug = _teamIdentity.Slug(t.Name);
            if (string.IsNullOrEmpty(slug)) continue;
            if (!index.ContainsKey(slug)) index[slug] = t.Id; // duplicate adlarda deterministik: en küçük Id
        }

        _teamSlugIndex = index;
        return index;
    }

    private async Task<int?> ResolveCompetitionAsync(string? name, string? country, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var competition = await _db.Competitions.FirstOrDefaultAsync(c => c.Name == name, cancellationToken).ConfigureAwait(false);
        if (competition is null)
        {
            competition = new Competition { Name = name, Country = country, LastUpdatedUtc = DateTime.UtcNow };
            _db.Competitions.Add(competition);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // competition.Id
        }

        return competition.Id;
    }

    private async Task<int?> ResolveVenueAsync(Formax.Infrastructure.Normalize.Models.NormalizedVenue? venue, CancellationToken cancellationToken)
    {
        var name = venue?.Name;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var existing = await _db.Venues.FirstOrDefaultAsync(v => v.Name == name, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            existing = new Venue
            {
                Name = name,
                City = venue!.City,
                Country = venue.Country?.Name,
                Capacity = venue.Capacity,
                LastUpdatedUtc = DateTime.UtcNow
            };
            _db.Venues.Add(existing);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // venue.Id
        }

        return existing.Id;
    }

    private static bool ApplyMatchFields(Match match, NormalizedFixture fixture, int homeTeamId, int awayTeamId, int? competitionId, int? venueId)
    {
        var changed = false;

        if (match.HomeTeamId != homeTeamId) { match.HomeTeamId = homeTeamId; changed = true; }
        if (match.AwayTeamId != awayTeamId) { match.AwayTeamId = awayTeamId; changed = true; }

        var matchDate = fixture.KickoffUtc?.UtcDateTime ?? default;
        if (match.MatchDate != matchDate) { match.MatchDate = matchDate; changed = true; }

        // Skor yalnızca sağlayıcı gerçekten verdiyse yazılır (veri yokken 0 yazmak sahte veridir).
        if (fixture.HomeScore is int homeScore && match.HomeScore != homeScore) { match.HomeScore = homeScore; changed = true; }
        if (fixture.AwayScore is int awayScore && match.AwayScore != awayScore) { match.AwayScore = awayScore; changed = true; }

        // Durum yalnızca sağlayıcı bildiyse yazılır (Unknown → mevcut durum korunur).
        if (fixture.Status != FixtureStatus.Unknown)
        {
            var status = MapStatus(fixture.Status);
            if (!string.Equals(match.Status, status, StringComparison.Ordinal)) { match.Status = status; changed = true; }
        }

        var league = fixture.Competition?.Name ?? string.Empty;
        if (!string.Equals(match.League, league, StringComparison.Ordinal)) { match.League = league; changed = true; }

        if (match.CompetitionId != competitionId) { match.CompetitionId = competitionId; changed = true; }
        if (match.VenueId != venueId) { match.VenueId = venueId; changed = true; }

        return changed;
    }

    /// <summary>
    /// Mevcut (benimsenen ya da başka sağlayıcıya ait) maçta yalnızca BOŞ kanonik alanları doldurur.
    /// Takım/tarih/skor/durum/lig alanları EZİLMEZ — bu maçın sahibi GDP değildir.
    /// </summary>
    private static bool FillMissingMatchFields(Match match, NormalizedFixture fixture, int? competitionId, int? venueId)
    {
        var changed = false;

        if (match.CompetitionId is null && competitionId is not null) { match.CompetitionId = competitionId; changed = true; }
        if (match.VenueId is null && venueId is not null) { match.VenueId = venueId; changed = true; }

        if (string.IsNullOrWhiteSpace(match.League) && !string.IsNullOrWhiteSpace(fixture.Competition?.Name))
        {
            match.League = fixture.Competition!.Name!;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// ExternalMatchId yalnızca GDP provider referansları arasında GERÇEK bir api-football fixture id
    /// varsa ve alan boşsa doldurulur. Mevcut değer asla ezilmez; sahte/türetilmiş id üretilmez.
    /// </summary>
    private static bool ApplyExternalMatchId(Match match, GdpPersistRequest request)
    {
        if (!string.IsNullOrWhiteSpace(match.ExternalMatchId))
            return false;

        var id = ExtractApiFootballFixtureId(request);
        if (string.IsNullOrWhiteSpace(id))
            return false;

        match.ExternalMatchId = id;
        return true;
    }

    /// <summary>GDP provider referansları arasındaki GERÇEK api-football fixture id (yoksa null).</summary>
    private static string? ExtractApiFootballFixtureId(GdpPersistRequest request) =>
        request.ProviderReferences
            .FirstOrDefault(r => r is not null
                                 && IsApiFootball(r.ProviderName)
                                 && !string.IsNullOrWhiteSpace(r.ProviderMatchId))
            ?.ProviderMatchId.Trim();

    /// <summary>
    /// GDP'nin lig adını MEVCUT canonical <c>Match.LeagueId</c> (api-football external lig id) ile eşler.
    /// Kural: aynı lig adını taşıyan mevcut maçlar TEK bir LeagueId gösteriyorsa o kullanılır.
    /// Birden fazla farklı LeagueId varsa (ör. iki ülkedeki "Serie A") eşleşme BELİRSİZDİR → null döner
    /// ve alan yazılmaz. Sahte/0 fallback ÜRETİLMEZ.
    /// </summary>
    private async Task<int?> ResolveLeagueIdAsync(string? leagueName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(leagueName))
            return null;

        var ids = await _db.Matches
            .AsNoTracking()
            .Where(m => m.League == leagueName && m.LeagueId > 0)
            .Select(m => m.LeagueId)
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.Count == 1 ? ids[0] : null;
    }

    private static bool IsApiFootball(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return false;

        var sb = new StringBuilder();
        foreach (var ch in providerName!)
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));

        return sb.ToString() is "apifootball" or "apifootballcom";
    }

    private static string? NormalizeRound(string? round)
    {
        if (string.IsNullOrWhiteSpace(round)) return null;
        var trimmed = round.Trim();
        return trimmed.Length > 64 ? trimmed.Substring(0, 64) : trimmed;
    }

    private static string MapStatus(FixtureStatus status) => status switch
    {
        FixtureStatus.Scheduled => "NotStarted",
        FixtureStatus.Live => "Live",
        FixtureStatus.Finished => "Finished",
        FixtureStatus.Postponed => "Postponed",
        FixtureStatus.Cancelled => "Cancelled",
        _ => "NotStarted"
    };

    // ---- GDP metadata sync ----

    private bool SyncProvenance(GdpMatchLink link, GdpPersistRequest request)
    {
        var desired = BuildProvenance(request);
        if (SameSet(
                link.Provenance.Select(p => $"{p.Field}|{p.State}|{p.Provider}"),
                desired.Select(p => $"{p.Field}|{p.State}|{p.Provider}")))
            return false;

        _db.Set<GdpProviderFieldProvenance>().RemoveRange(link.Provenance);
        link.Provenance = desired;
        return true;
    }

    private bool SyncResolutions(GdpMatchLink link, GdpPersistRequest request)
    {
        var desired = BuildResolutions(request);
        if (SameSet(
                link.ConflictResolutions.Select(r => $"{r.Field}|{r.SelectedProvider}|{r.ResolutionStrategy}|{r.Confidence}|{r.IsResolved}"),
                desired.Select(r => $"{r.Field}|{r.SelectedProvider}|{r.ResolutionStrategy}|{r.Confidence}|{r.IsResolved}")))
            return false;

        _db.Set<GdpConflictResolution>().RemoveRange(link.ConflictResolutions);
        link.ConflictResolutions = desired;
        return true;
    }

    private bool SyncReferences(GdpMatchLink link, GdpPersistRequest request)
    {
        var desired = BuildReferences(request);
        if (SameSet(
                link.ProviderReferences.Select(r => $"{r.ProviderName}|{r.ProviderMatchId}"),
                desired.Select(r => $"{r.ProviderName}|{r.ProviderMatchId}")))
            return false;

        _db.Set<GdpProviderMatchReference>().RemoveRange(link.ProviderReferences);
        link.ProviderReferences = desired;
        return true;
    }

    private static List<GdpProviderFieldProvenance> BuildProvenance(GdpPersistRequest request) =>
        request.Merge.Fields
            .Where(f => f is not null)
            .Select(f => new GdpProviderFieldProvenance
            {
                FormaxMatchId = request.FormaxMatchId,
                Field = f.Field,
                State = f.State.ToString(),
                Provider = f.Provider
            })
            .ToList();

    private static List<GdpConflictResolution> BuildResolutions(GdpPersistRequest request) =>
        request.Merge.ConflictResolutions
            .Where(r => r is not null)
            .Select(r => new GdpConflictResolution
            {
                FormaxMatchId = request.FormaxMatchId,
                Field = r.Field,
                SelectedProvider = r.SelectedProvider,
                ResolutionStrategy = r.ResolutionStrategy,
                Confidence = r.Confidence,
                IsResolved = r.IsResolved
            })
            .ToList();

    private static List<GdpProviderMatchReference> BuildReferences(GdpPersistRequest request) =>
        request.ProviderReferences
            .Where(r => r is not null && !string.IsNullOrWhiteSpace(r.ProviderName))
            .Select(r => new GdpProviderMatchReference
            {
                FormaxMatchId = request.FormaxMatchId,
                ProviderName = r.ProviderName,
                ProviderMatchId = r.ProviderMatchId
            })
            .ToList();

    // ---- Helpers ----

    private static bool SameSet(IEnumerable<string> a, IEnumerable<string> b)
    {
        var listA = a.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var listB = b.OrderBy(x => x, StringComparer.Ordinal).ToList();
        return listA.SequenceEqual(listB, StringComparer.Ordinal);
    }

    private static GdpPersistOutcome Fail(string id, string error) =>
        new() { FormaxMatchId = id, Status = GdpPersistStatus.Failed, Error = error };

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); }
        catch { /* rollback hatası yutulur; asıl hata döndürülür */ }
    }
}
