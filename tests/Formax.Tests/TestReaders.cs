using System;
using Formax.Application.Interfaces;
using Formax.Application.Services.Seasons;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace Formax.Tests;

/// <summary>
/// Testlerde okuma yollarını kurmanın ORTAK yolu.
///
/// NEDEN VAR: MatchResultsReader takım araması için sezon çözücü ve önbellek alır.
/// Her testin kendi sahtesini kurması, kurucu bir kez daha değiştiğinde onlarca
/// dosyayı elle düzeltmek demekti.
/// </summary>
internal static class TestReaders
{
    /// <summary>Temmuz kırılımlı sezon — projenin mevcut sözleşmesi.</summary>
    private sealed class JulySeasonResolver : ILeagueSeasonResolver
    {
        public LeagueSeasonResolution Resolve(int leagueId, DateTime referenceUtc)
            => throw new NotSupportedException("testler bu yolu kullanmaz");
        public int SeasonYearOf(DateTime utc) => utc.Month >= 7 ? utc.Year : utc.Year - 1;
    }

    public static MatchResultsReader Results(FormaxDbContext db)
        => new(db, new JulySeasonResolver(), new MemoryCache(new MemoryCacheOptions()));
}
