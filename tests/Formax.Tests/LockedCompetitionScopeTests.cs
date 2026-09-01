using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KİLİTLİ KAPSAM — 11 organizasyon, 8 ulusal + 3 UEFA. Tek merkezî kaynak.
/// </summary>
public class LockedCompetitionScopeTests : IDisposable
{
    private readonly FormaxDbContext _db;
    private readonly LeagueSeasonResolver _resolver;

    public LockedCompetitionScopeTests()
    {
        var options = new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"locked-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new FormaxDbContext(options);
        _resolver = new LeagueSeasonResolver(
            _db, new ConfigurationBuilder().AddInMemoryCollection().Build());

        // LeagueSeasonResolver önbelleği STATİKTİR (süreç geneli). Her test kendi
        // InMemory veritabanını kurduğu için, önceki testin çözümü sızmasın diye
        // kilitli kapsamın tamamı bu sezon için geçersiz kılınır.
        foreach (var leagueId in LockedCompetitions.All)
            LeagueSeasonResolver.Invalidate(leagueId, 2026);
    }

    [Fact]
    public void KilitliMusabakaSayisi_11()
        => Assert.Equal(11, LockedCompetitions.All.Count);

    [Fact]
    public void UlusalLigSayisi_8()
        => Assert.Equal(8, LockedCompetitions.Domestic.Count);

    [Fact]
    public void UefaTurnuvaSayisi_3()
        => Assert.Equal(3, LockedCompetitions.Uefa.Count);

    [Fact]
    public void KapsamListesi_TamOlarakBeklenenIdler()
    {
        Assert.Equal(
            new[] { 2, 3, 39, 40, 61, 78, 88, 135, 140, 203, 848 },
            LockedCompetitions.All.OrderBy(x => x).ToArray());

        Assert.All(LockedCompetitions.Domestic, id => Assert.True(LockedCompetitions.IsDomestic(id)));
        Assert.All(LockedCompetitions.Uefa,     id => Assert.True(LockedCompetitions.IsUefa(id)));
        // Bir lig aynı anda hem ulusal hem UEFA olamaz.
        Assert.Empty(LockedCompetitions.Domestic.Intersect(LockedCompetitions.Uefa));
    }

    [Fact]
    public void OnbirMetadataKaydinin_Hepsi_Cozulur()
    {
        // Üretimdeki 11 kaydın aynısı: her kilitli organizasyonun bir metadata satırı var.
        foreach (var leagueId in LockedCompetitions.All)
        {
            _db.LeagueSeasons.Add(new LeagueSeason
            {
                LeagueId = leagueId,
                SeasonYear = 2026,
                StartUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                // Championship gibi bitişi doğrulanmamış kayıtlar NULL olabilir.
                EndUtc = leagueId == LockedCompetitions.Championship
                    ? null
                    : new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                Source = "Verified:Test",
                VerifiedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                VerificationStatus = leagueId == LockedCompetitions.Championship
                    ? "PendingOfficialConfirmation" : "Confirmed"
            });
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var reference = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        foreach (var leagueId in LockedCompetitions.All)
        {
            var r = _resolver.Resolve(leagueId, reference);
            Assert.True(r.Resolved, $"Lig {leagueId} icin sezon cozulemedi: {r.Error}");
            Assert.Equal(2026, r.Scope!.SeasonYear);
        }
    }

    [Fact]
    public void Championship_EndUtcNull_AktifSezonuYineDeCozer()
    {
        _db.LeagueSeasons.Add(new LeagueSeason
        {
            LeagueId = LockedCompetitions.Championship,
            SeasonYear = 2026,
            StartUtc = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc),
            EndUtc = null,                                  // resmi play-off finali yayimlanmadi
            Source = "Verified:OfficialLeague",
            VerifiedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            VerificationStatus = "PendingOfficialConfirmation"
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var r = _resolver.Resolve(LockedCompetitions.Championship,
            new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(r.Resolved);
        Assert.Equal(new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc), r.Scope!.StartUtc);
        // EndUtc yoksa kapsam bir sonraki sezon kovasinin basina kadar uzar — tarih UYDURULMAZ.
        Assert.Equal(new DateTime(2027, 7, 1, 0, 0, 0, DateTimeKind.Utc), r.Scope.EndUtc);
        Assert.True(r.Scope.Contains(new DateTime(2027, 5, 1, 14, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void SezonBaslangici_IlkFikstureden_TURETILMEZ()
    {
        // Metadata YOK, ama depoda fikstur VAR. Cozum basarisiz olmali; fikstur tarihi
        // yetkili sezon baslangici DEGILDIR.
        _db.Teams.Add(new Team { Id = 1, Name = "A" });
        _db.Teams.Add(new Team { Id = 2, Name = "B" });
        _db.Matches.Add(new Match
        {
            Id = 1, LeagueId = LockedCompetitions.LaLiga, HomeTeamId = 1, AwayTeamId = 2,
            MatchDate = new DateTime(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc),
            Status = MatchStatuses.Finished
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var r = _resolver.Resolve(LockedCompetitions.LaLiga,
            new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.False(r.Resolved);
        Assert.Contains("SEASON_START_UNRESOLVED", r.Error);
    }

    public void Dispose() => _db.Dispose();
}
