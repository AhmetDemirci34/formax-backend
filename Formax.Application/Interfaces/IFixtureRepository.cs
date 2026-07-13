using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Fixtures;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// FORMAX Data Engine v1 — Fixtures kalıcılık katmanı. Keşfedilen maçları
    /// FORMAX_MATCH_ID'ye göre upsert eder (yeni → ekle, mevcut → saat/durum/güven güncelle).
    /// </summary>
    public interface IFixtureRepository
    {
        /// <summary>Upsert sonrası (eklenen, güncellenen) sayısını döndürür.</summary>
        Task<(int Added, int Updated)> UpsertAsync(
            IEnumerable<FixtureDiscoveryResult> fixtures, CancellationToken ct = default);

        /// <summary>Haber keşfi için aktif maçlar: pencere içinde, bitmemiş. (News Engine v2 kullanır.)</summary>
        Task<List<Fixture>> GetActiveAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    }
}
