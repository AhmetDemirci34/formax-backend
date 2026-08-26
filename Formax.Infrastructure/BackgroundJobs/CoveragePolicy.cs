using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// GDP Final Coverage — fikstür evrenini api-football'un kapsadığı liglere DARALTMA politikası.
    ///
    /// Allow-list KONFİGÜRASYONDAN gelir (appsettings → "Coverage:LeagueAllowList": [39,140,...]),
    /// HARDCODE değildir; hangi liglere odaklanılacağı kullanıcının veri-strateji kararıdır. Liste
    /// BOŞSA hiçbir kısıtlama uygulanmaz (mevcut davranış = geri-uyum). Liste doluysa yalnız o
    /// api-football lig id'leri işlenir → kota kapsanan liglere odaklanır, minör lig gürültüsü kalkar.
    /// </summary>
    public static class CoveragePolicy
    {
        public const string ConfigKey = "Coverage:LeagueAllowList";

        /// <summary>Konfigüre lig allow-list'i (external api-football lig id'leri). Boş = kısıtlama yok.</summary>
        public static HashSet<int> LeagueAllowList(IConfiguration config)
        {
            var ids = config?.GetSection(ConfigKey).Get<int[]>() ?? Array.Empty<int>();
            return ids.Where(i => i > 0).ToHashSet();
        }

        /// <summary>Bu lig işlensin mi? Allow-list boşsa HER lig geçer (geri-uyum).</summary>
        public static bool Allows(HashSet<int> allow, int leagueId)
            => allow == null || allow.Count == 0 || allow.Contains(leagueId);

        // ── Coverage-driven Discovery (elle allow-list yerine) ──
        public const string DiscoveryFloorKey = "Coverage:DiscoveryFloorTier";

        /// <summary>Tier sıralaması: Premium 3 > Standard 2 > Limited 1 > Passive 0.</summary>
        public static int TierRank(string? tier) => tier switch
        {
            "Premium"  => 3,
            "Standard" => 2,
            "Limited"  => 1,
            _          => 0
        };

        /// <summary>Discovery taban tier'ı (config; varsayılan "Passive" = kısıtlama yok, geri-uyum).</summary>
        public static string DiscoveryFloorTier(IConfiguration config)
            => config?[DiscoveryFloorKey] ?? "Passive";
    }
}
