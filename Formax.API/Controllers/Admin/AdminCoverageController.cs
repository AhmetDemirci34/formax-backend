using System.Linq;
using Formax.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers.Admin
{
    /// <summary>
    /// GDP Final Evolution — League Coverage Intelligence DİYAGNOSTİKLERİ (salt-okunur).
    /// GDP'nin kendi coverage'ını GERÇEK ingestion verisinden ölçtüğü/puanladığı sonuçları gösterir:
    /// Dashboard, League Coverage, Capability Matrix, Readiness, Trend. Yeni veri üretmez, api-football
    /// çağırmaz; yalnız mevcut DB'den hesaplanan coverage'ı okur.
    /// </summary>
    [ApiController]
    [Route("admin/coverage")]
    public class AdminCoverageController : ControllerBase
    {
        private readonly ILeagueCoverageService _coverage;

        public AdminCoverageController(ILeagueCoverageService coverage)
        {
            _coverage = coverage;
        }

        /// <summary>GDP genel durum: readiness skorları + tier dağılımı + capability readiness.</summary>
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            var r = _coverage.ComputeReadiness();
            return Ok(new
            {
                // GLOBAL: keşfedilen tüm lig evreni, tüm-zaman maçlar payda.
                global = new
                {
                    overallGdpReadiness = r.OverallGdpReadiness,
                    marketProbabilityEngineReadiness = r.MarketProbabilityEngineReadiness,
                    aiReadiness = r.AiReadiness,
                    capabilityReadiness = r.CapabilityReadiness
                },
                // OPERATIONAL: aktif pencere (yaklaşan maçı olan ligler), operasyonel capability payda = yaklaşan maç.
                operational = new
                {
                    coverage = r.OperationalCoverage,
                    marketProbabilityEngineReadiness = r.OperationalMotorReadiness,
                    aiReadiness = r.OperationalAiReadiness,
                    activeLeagues = r.OperationalLeagueCount,
                    capabilityReadiness = r.OperationalCapabilityReadiness
                },
                leagues = new
                {
                    total = r.LeagueCount,
                    premium = r.PremiumLeagues,
                    standard = r.StandardLeagues,
                    limited = r.LimitedLeagues,
                    passive = r.PassiveLeagues
                },
                // Geri-uyum: eski düz alanlar (GLOBAL) korunur.
                overallGdpReadiness = r.OverallGdpReadiness,
                marketProbabilityEngineReadiness = r.MarketProbabilityEngineReadiness,
                aiReadiness = r.AiReadiness,
                capabilityReadiness = r.CapabilityReadiness
            });
        }

        /// <summary>Per-league coverage — skor + tier + capability özeti.</summary>
        [HttpGet("leagues")]
        public IActionResult Leagues([FromQuery] string? tier = null, [FromQuery] int take = 100)
        {
            var all = _coverage.ComputeAll().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(tier))
                all = all.Where(l => string.Equals(l.Tier, tier, System.StringComparison.OrdinalIgnoreCase));

            var list = all.Take(take).Select(l => new
            {
                l.LeagueId,
                l.LeagueName,
                l.OverallScore,
                l.Tier,
                l.MatchCount,
                l.TeamCount,
                l.UpcomingCount,
                capabilities = l.Capabilities.Select(c => new
                {
                    c.Name, c.Status, c.CoverageRatio, c.Freshness, c.DataQuality, c.Scope, c.Detail
                })
            });

            return Ok(new { count = list.Count(), leagues = list });
        }

        /// <summary>Capability Matrix — her lig × capability durumu (Supported/Partial/Unavailable).</summary>
        [HttpGet("matrix")]
        public IActionResult Matrix([FromQuery] int take = 60)
        {
            var all = _coverage.ComputeAll().Take(take).ToList();
            var rows = all.Select(l => new
            {
                l.LeagueId,
                l.LeagueName,
                l.Tier,
                l.OverallScore,
                matrix = l.Capabilities.ToDictionary(c => c.Name, c => c.Status)
            });
            return Ok(new { count = all.Count, rows });
        }

        /// <summary>Readiness skorları — League/Capability/Overall/Motor/AI.</summary>
        [HttpGet("readiness")]
        public IActionResult Readiness()
        {
            var r = _coverage.ComputeReadiness();
            var topLeagues = _coverage.ComputeAll().Take(10)
                .Select(l => new { l.LeagueId, l.LeagueName, l.OverallScore, l.Tier });
            return Ok(new
            {
                overallGdpReadiness = r.OverallGdpReadiness,
                marketProbabilityEngineReadiness = r.MarketProbabilityEngineReadiness,
                aiReadiness = r.AiReadiness,
                capabilityReadiness = r.CapabilityReadiness,
                topLeagues
            });
        }

        /// <summary>Coverage Trend — in-memory anlık görüntüler (process başından beri).</summary>
        [HttpGet("trend")]
        public IActionResult Trend() => Ok(new { snapshots = _coverage.Trend() });
    }
}
