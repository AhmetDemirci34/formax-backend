using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Coverage;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Timeline Operations & Coverage — GDP takım Timeline'ının salt-okunur, cache'li operasyonel görünümü.
    /// Tüm değerler GERÇEK veriden (Teams/Matches + telemetri/metrics) hesaplanır. Timeline sistemini
    /// ETKİLEMEZ (yalnız okur). Ağır sorgu yok: iki hafif tablo taraması + in-memory agregasyon + cache.
    /// </summary>
    public interface ITimelineCoverageService
    {
        TimelineDashboard GetDashboard();
        IReadOnlyList<LeagueTimelineCoverage> GetLeagues();
        Task<TimelineQuotaReport> GetQuotaReportAsync(CancellationToken ct = default);
        ColdStartPreview GetColdStartPreview(int count);
    }
}
