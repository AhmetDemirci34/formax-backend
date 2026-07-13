using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Persistence.Standings;

/// <summary>Kalıcılaştırma isteği: puan durumu hesaplanacak competition adı.</summary>
public sealed record GdpStandingsPersistRequest
{
    public required string CompetitionName { get; init; }
}

/// <summary>Kalıcılaştırma sonucu (durum + yazılan satır sayısı + varsa hata).</summary>
public sealed record GdpStandingsPersistOutcome
{
    public string? CompetitionName { get; init; }
    public required GdpPersistStatus Status { get; init; }
    public int Rows { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// GDP puan durumu kalıcılaştırıcısı (EF Core) — Canonical Domain <see cref="CompetitionStanding"/>.
///
/// H2H ile AYNI yaklaşım: provider'dan gelmez; canonical <see cref="Match"/> geçmişinden
/// (<see cref="StandingsCalculator"/>) TÜRETİLİR. Bir competition'ın TÜM bitmiş maçları taranır,
/// tablo hesaplanır ve (CompetitionName + TeamName) anahtarıyla upsert edilir (idempotent).
/// </summary>
public sealed class GdpStandingsPersister : IGdpStandingsPersister
{
    private readonly FormaxDbContext _db;
    private readonly ILogger<GdpStandingsPersister> _logger;

    public GdpStandingsPersister(FormaxDbContext db, ILogger<GdpStandingsPersister> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GdpStandingsPersistOutcome> PersistAsync(GdpStandingsPersistRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.CompetitionName))
            return Fail(null, "CompetitionName boş.");

        var comp = request.CompetitionName;

        // Canonical geçmiş: bu competition'ın bitmiş maçları (takım adlarıyla).
        var finished = await _db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Status == "Finished" && m.League == comp)
            .Select(m => new StandingMatch
            {
                HomeTeam = m.HomeTeam!.Name,
                AwayTeam = m.AwayTeam!.Name,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (finished.Count == 0)
        {
            _logger.LogInformation("GdpStandingsPersister: '{Comp}' için bitmiş maç yok; puan durumu yazılmadı (graceful skip).", comp);
            return new GdpStandingsPersistOutcome { CompetitionName = comp, Status = GdpPersistStatus.Unchanged, Rows = 0 };
        }

        var rows = StandingsCalculator.Compute(finished);

        try
        {
            var existing = await _db.CompetitionStandings
                .Where(s => s.CompetitionName == comp)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var byTeam = existing.ToDictionary(s => s.TeamName, StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (byTeam.TryGetValue(row.TeamName, out var s))
                {
                    s.Played = row.Played; s.Won = row.Won; s.Drawn = row.Drawn; s.Lost = row.Lost;
                    s.GoalsFor = row.GoalsFor; s.GoalsAgainst = row.GoalsAgainst; s.Points = row.Points; s.Rank = row.Rank;
                    s.LastUpdatedUtc = DateTime.UtcNow;
                }
                else
                {
                    _db.CompetitionStandings.Add(new CompetitionStanding
                    {
                        CompetitionName = comp,
                        TeamName = row.TeamName,
                        Played = row.Played, Won = row.Won, Drawn = row.Drawn, Lost = row.Lost,
                        GoalsFor = row.GoalsFor, GoalsAgainst = row.GoalsAgainst, Points = row.Points, Rank = row.Rank,
                        LastUpdatedUtc = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new GdpStandingsPersistOutcome { CompetitionName = comp, Status = GdpPersistStatus.Updated, Rows = rows.Count };
        }
        catch (Exception ex)
        {
            return Fail(comp, ex.Message);
        }
    }

    private static GdpStandingsPersistOutcome Fail(string? comp, string error) =>
        new() { CompetitionName = comp, Status = GdpPersistStatus.Failed, Error = error };
}
