using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Persistence.H2H;

/// <summary>Kalıcılaştırma isteği: FORMAX Match ID + türetimin okunacağı Domain Match Id.</summary>
public sealed record GdpH2HPersistRequest
{
    public required string FormaxMatchId { get; init; }
    public int? MatchId { get; init; }
}

/// <summary>Kalıcılaştırma sonucu (durum + varsa hata).</summary>
public sealed record GdpH2HPersistOutcome
{
    public string? FormaxMatchId { get; init; }
    public required GdpPersistStatus Status { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// GDP H2H kalıcılaştırıcısı (EF Core) — Canonical Domain <see cref="HeadToHead"/>.
///
/// Weather/Match persister'larıyla AYNI pattern: idempotent, yalnız değişen alan güncellenir,
/// graceful skip. FARK: H2H bir provider'dan gelmez; canonical <see cref="Match"/> geçmişinden
/// (<see cref="H2HSummaryCalculator"/>) TÜRETİLİR → GDP'nin tek-sezon CSV sınırından bağımsız.
/// Kayıt anahtarı FormaxMatchId'dir (maç başına tek H2H).
/// </summary>
public sealed class GdpH2HPersister : IGdpH2HPersister
{
    private const int RecentMeetingLimit = 10;

    private readonly FormaxDbContext _db;
    private readonly ILogger<GdpH2HPersister> _logger;

    public GdpH2HPersister(FormaxDbContext db, ILogger<GdpH2HPersister> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GdpH2HPersistOutcome> PersistAsync(GdpH2HPersistRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.FormaxMatchId))
            return Fail(null, "FormaxMatchId boş.");

        // Türetim mevcut Domain Match'e dayanır; yoksa graceful skip (sahte H2H yazma).
        if (request.MatchId is not int matchId)
        {
            _logger.LogInformation("GdpH2HPersister: MatchId yok (formaxId={FormaxId}); H2H yazılmadı (graceful skip).", request.FormaxMatchId);
            return Fail(request.FormaxMatchId, "MatchId yok; H2H türetilemez.");
        }

        var match = await _db.Matches
            .AsNoTracking()
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken)
            .ConfigureAwait(false);

        if (match is null)
            return Fail(request.FormaxMatchId, $"Match {matchId} bulunamadı.");

        var homeId = match.HomeTeamId;
        var awayId = match.AwayTeamId;

        // Canonical geçmiş: iki takım arası, bitmiş, bu maçtan ÖNCEki karşılaşmalar (son N).
        var pastRows = await _db.Matches
            .AsNoTracking()
            .Where(m => m.Id != matchId
                        && m.Status == "Finished"
                        && m.MatchDate < match.MatchDate
                        && ((m.HomeTeamId == homeId && m.AwayTeamId == awayId)
                            || (m.HomeTeamId == awayId && m.AwayTeamId == homeId)))
            .OrderByDescending(m => m.MatchDate)
            .Take(RecentMeetingLimit)
            .Select(m => new H2HPastMeeting
            {
                HomeTeamId = m.HomeTeamId,
                AwayTeamId = m.AwayTeamId,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = H2HSummaryCalculator.Compute(pastRows, homeId, awayId, match.HomeTeam?.Name, match.AwayTeam?.Name);

        try
        {
            var existing = await _db.HeadToHeads
                .FirstOrDefaultAsync(h => h.FormaxMatchId == request.FormaxMatchId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                _db.HeadToHeads.Add(new HeadToHead
                {
                    FormaxMatchId = request.FormaxMatchId,
                    HomeTeam = match.HomeTeam?.Name,
                    AwayTeam = match.AwayTeam?.Name,
                    Summary = result.Summary,
                    LastUpdatedUtc = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Outcome(request.FormaxMatchId, GdpPersistStatus.Inserted);
            }

            var changed = false;
            if (!string.Equals(existing.HomeTeam, match.HomeTeam?.Name, StringComparison.Ordinal)) { existing.HomeTeam = match.HomeTeam?.Name; changed = true; }
            if (!string.Equals(existing.AwayTeam, match.AwayTeam?.Name, StringComparison.Ordinal)) { existing.AwayTeam = match.AwayTeam?.Name; changed = true; }
            if (!string.Equals(existing.Summary, result.Summary, StringComparison.Ordinal)) { existing.Summary = result.Summary; changed = true; }

            if (!changed)
                return Outcome(request.FormaxMatchId, GdpPersistStatus.Unchanged);

            existing.LastUpdatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Outcome(request.FormaxMatchId, GdpPersistStatus.Updated);
        }
        catch (Exception ex)
        {
            return Fail(request.FormaxMatchId, ex.Message);
        }
    }

    private static GdpH2HPersistOutcome Outcome(string? id, GdpPersistStatus status) =>
        new() { FormaxMatchId = id, Status = status };

    private static GdpH2HPersistOutcome Fail(string? id, string error) =>
        new() { FormaxMatchId = id, Status = GdpPersistStatus.Failed, Error = error };
}
