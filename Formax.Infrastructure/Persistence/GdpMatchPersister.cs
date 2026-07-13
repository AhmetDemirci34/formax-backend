using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// Aynı FormaxMatchId için <see cref="GdpMatchLink"/> üzerinden mevcut Domain Match bulunur → duplicate oluşmaz.
/// Yalnızca DEĞİŞEN alanlar güncellenir; hiçbir şey değişmediyse Unchanged. Transaction + rollback; idempotent.
/// </summary>
public sealed class GdpMatchPersister : IGdpMatchPersister
{
    private readonly FormaxDbContext _db;

    public GdpMatchPersister(FormaxDbContext db)
    {
        _db = db;
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
            // Domain takımları çöz (yoksa oluştur) — GDP mevcut Team aggregate'ını kullanır.
            var homeTeamId = await ResolveTeamAsync(fixture.HomeTeam?.Name, cancellationToken).ConfigureAwait(false);
            var awayTeamId = await ResolveTeamAsync(fixture.AwayTeam?.Name, cancellationToken).ConfigureAwait(false);
            if (homeTeamId is null || awayTeamId is null)
                return Fail(request.FormaxMatchId, "Ev/deplasman takım adı eksik; Domain Match beslenemez.");

            // Kanonik Competition'ı ada göre çöz (yoksa oluştur) — Team ile AYNI pattern.
            var competitionId = await ResolveCompetitionAsync(fixture.Competition?.Name, fixture.Competition?.Country?.Name, cancellationToken).ConfigureAwait(false);

            // Kanonik Venue'yu ada göre çöz (yoksa oluştur) — Competition ile AYNI pattern.
            var venueId = await ResolveVenueAsync(fixture.Venue, cancellationToken).ConfigureAwait(false);

            var link = await _db.GdpMatchLinks
                .Include(l => l.Provenance)
                .Include(l => l.ConflictResolutions)
                .Include(l => l.ProviderReferences)
                .FirstOrDefaultAsync(l => l.FormaxMatchId == request.FormaxMatchId, cancellationToken)
                .ConfigureAwait(false);

            GdpPersistStatus status;
            var changed = false;

            if (link is null)
            {
                // Yeni: mevcut Domain Match oluştur.
                var match = new Match { CreatedAt = DateTime.UtcNow };
                ApplyMatchFields(match, fixture, homeTeamId.Value, awayTeamId.Value, competitionId, venueId);
                _db.Matches.Add(match);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // match.Id

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
                // Var olan: bağlı Domain Match'i güncelle (yalnızca değişen alanlar).
                var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == link.MatchId, cancellationToken).ConfigureAwait(false);
                if (match is null)
                {
                    match = new Match { CreatedAt = DateTime.UtcNow };
                    ApplyMatchFields(match, fixture, homeTeamId.Value, awayTeamId.Value, competitionId, venueId);
                    _db.Matches.Add(match);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    link.MatchId = match.Id;
                    changed = true;
                }
                else
                {
                    changed |= ApplyMatchFields(match, fixture, homeTeamId.Value, awayTeamId.Value, competitionId, venueId);
                }

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

                status = GdpPersistStatus.Updated;
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

    // ---- Domain Match/Team ----

    private async Task<int?> ResolveTeamAsync(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var team = await _db.Teams.FirstOrDefaultAsync(t => t.Name == name, cancellationToken).ConfigureAwait(false);
        if (team is null)
        {
            team = new Team { Name = name, CreatedAt = DateTime.UtcNow };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // team.Id
        }

        return team.Id;
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

        var homeScore = fixture.HomeScore ?? 0;
        if (match.HomeScore != homeScore) { match.HomeScore = homeScore; changed = true; }

        var awayScore = fixture.AwayScore ?? 0;
        if (match.AwayScore != awayScore) { match.AwayScore = awayScore; changed = true; }

        var status = MapStatus(fixture.Status);
        if (!string.Equals(match.Status, status, StringComparison.Ordinal)) { match.Status = status; changed = true; }

        var league = fixture.Competition?.Name ?? string.Empty;
        if (!string.Equals(match.League, league, StringComparison.Ordinal)) { match.League = league; changed = true; }

        if (match.CompetitionId != competitionId) { match.CompetitionId = competitionId; changed = true; }
        if (match.VenueId != venueId) { match.VenueId = venueId; changed = true; }

        return changed;
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
