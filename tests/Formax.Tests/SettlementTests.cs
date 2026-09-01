using System;
using System.Linq;
using System.Threading.Tasks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Predictions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// SETTLEMENT — aynı sonuç İKİ KEZ yazılmamalı.
/// Sağlayıcıya istek yok; her şey bellekteki depoda.
/// </summary>
public class SettlementTests : IDisposable
{
    private readonly FormaxDbContext _db;
    private readonly PredictionSettlementService _svc;

    public SettlementTests()
    {
        var options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"settle-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FormaxDbContext(options);
        _svc = new PredictionSettlementService(_db, NullLogger<PredictionSettlementService>.Instance);
    }

    private void SeedFinishedMatchWithPrediction(int matchId, int home, int away)
    {
        var kickoff = DateTime.UtcNow.AddDays(-2);

        _db.Teams.Add(new Team { Id = 1, Name = "Ev" });
        _db.Teams.Add(new Team { Id = 2, Name = "Dep" });
        _db.Matches.Add(new Match
        {
            Id = matchId,
            Status = MatchStatuses.Finished,
            MatchDate = kickoff,
            HomeScore = home,
            AwayScore = away,
            LeagueId = 88,
            HomeTeamId = 1,
            AwayTeamId = 2
        });
        _db.Predictions.Add(new Formax.Domain.Entities.Prediction
        {
            PredictionId = $"pred-{matchId}",
            MatchId = matchId,
            MatchDate = kickoff,
            PredictionTimestamp = kickoff.AddDays(-1)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AyniSonuc_IkiKezSettlementUretmez()
    {
        SeedFinishedMatchWithPrediction(500, 2, 1);

        var first = await _svc.SettleAsync(100);
        var second = await _svc.SettleAsync(100);

        Assert.Equal(1, first.Settled);
        Assert.Equal(0, second.Settled);          // ikinci turda aday bile kalmaz
        Assert.Equal(0, second.Candidates);

        var rows = _db.PredictionSettlements.AsNoTracking().ToList();
        Assert.Single(rows);
        Assert.Equal(rows.Count, rows.Select(r => r.PredictionId).Distinct().Count());
    }

    [Fact]
    public async Task FinishedSkor_DogruSonucEtiketiUretir()
    {
        SeedFinishedMatchWithPrediction(501, 1, 3);

        await _svc.SettleAsync(100);
        var row = _db.PredictionSettlements.AsNoTracking().Single();

        Assert.Equal(1, row.ActualHomeGoals);
        Assert.Equal(3, row.ActualAwayGoals);
        Assert.Equal("AwayWin", row.ActualResult);
    }

    [Fact]
    public async Task SonucuOlmayanMac_SettleEdilmez()
    {
        var kickoff = DateTime.UtcNow.AddDays(-2);
        _db.Teams.Add(new Team { Id = 1, Name = "Ev" });
        _db.Teams.Add(new Team { Id = 2, Name = "Dep" });
        _db.Matches.Add(new Match
        {
            Id = 502, Status = MatchStatuses.NotStarted, MatchDate = kickoff,
            HomeScore = 0, AwayScore = 0, LeagueId = 88, HomeTeamId = 1, AwayTeamId = 2
        });
        _db.Predictions.Add(new Formax.Domain.Entities.Prediction
        {
            PredictionId = "pred-502", MatchId = 502,
            MatchDate = kickoff, PredictionTimestamp = kickoff.AddDays(-1)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var report = await _svc.SettleAsync(100);

        Assert.Equal(0, report.Settled);
        Assert.Equal(1, report.NoResultYet);
        Assert.Empty(_db.PredictionSettlements.AsNoTracking().ToList());
    }

    public void Dispose() => _db.Dispose();
}
