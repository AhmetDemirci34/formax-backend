using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.OfficialSources;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.OfficialSources
{
    /// <summary>Kaynak başına parser sürümleri — gözlem defterine ve katalogda yazılır.</summary>
    public static class OfficialParserVersions
    {
        private static readonly Dictionary<string, string> ByKey = new(StringComparer.Ordinal)
        {
            [OfficialSourceRegistry.PremierLeagueSdp] = "pl-sdp-v3",
            [OfficialSourceRegistry.SerieASdp] = "seriea-sdp-v2",
            [OfficialSourceRegistry.TffSite] = "tff-html-v2",
            [OfficialSourceRegistry.BundesligaSite] = "bundesliga-ngstate-v2",
            [OfficialSourceRegistry.LaLigaSite] = "laliga-nextdata-v2",
            [OfficialSourceRegistry.Ligue1Api] = "ligue1-api-v2",
            [OfficialSourceRegistry.EflApi] = "efl-multiclub-v2",
            [OfficialSourceRegistry.UefaMatchApi] = "uefa-match-v5-v1",
            [OfficialSourceRegistry.KnvbSite] = "knvb-timetable-v1"
        };

        public static string For(string sourceKey) => ByKey.TryGetValue(sourceKey, out var v) ? v : "none";
    }

    /// <summary>
    /// KALICI KAYNAK KATALOĞU + SAĞLIK — sonuç/istatistik botunun tek yazıcısı.
    ///
    /// Statik alanlar kayıt defterinden eşitlenir (restart'ta sağlık alanları korunur). Her okuma başarı ya da hata olarak
    /// yazılır; ardışık 5 hatadan sonra devre kesici açılır (10 dk × 2^(n−5), en fazla 6 sa). Bir kaynağın bozulması diğer
    /// kaynakları durdurmaz. robots.txt yasağı ya da ulaşılamazlığı katalogda görünür.
    /// </summary>
    public sealed class OfficialDataSourceCatalog
    {
        public const int CircuitThreshold = 5;
        public static readonly TimeSpan CircuitBase = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan CircuitMax = TimeSpan.FromHours(6);

        private readonly FormaxDbContext _db;
        public OfficialDataSourceCatalog(FormaxDbContext db) => _db = db;

        /// <summary>Kayıt defterindeki sonuç/istatistik kaynaklarını kataloğa eşitler (video kaynakları alınmaz).</summary>
        public async Task EnsureSeededAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            var rows = await _db.OfficialDataSources.ToDictionaryAsync(r => r.SourceId, StringComparer.Ordinal, ct).ConfigureAwait(false);
            var changed = false;
            foreach (var d in OfficialSourceRegistry.All.Where(IsDataSource))
            {
                if (!rows.TryGetValue(d.Key, out var row))
                {
                    row = new OfficialDataSource { SourceId = d.Key, UpdatedAtUtc = nowUtc };
                    _db.OfficialDataSources.Add(row);
                    changed = true;
                }
                var enabled = d.Status == OfficialSourceStatuses.Verified;
                var name = d.Organization;
                var domain = string.Join(",", d.Hosts);
                var orgs = string.Join(",", d.LeagueIds);
                var caps = string.Join(",", d.Capabilities);
                var evidence = d.EvidenceNote.Length > 1200 ? d.EvidenceNote[..1200] : d.EvidenceNote;
                var parser = OfficialParserVersions.For(d.Key);
                if (row.SourceName != name || row.OfficialDomain != domain || row.OrganizationIds != orgs || row.Capabilities != caps
                    || row.VerificationEvidence != evidence || row.RegistryStatus != d.Status || row.IsEnabled != enabled
                    || row.ParserVersion != parser || row.SourceType != d.Tier.ToString() || row.ContentKind != d.Kind)
                {
                    row.SourceName = name; row.OfficialDomain = domain; row.OrganizationIds = orgs; row.Capabilities = caps;
                    row.VerificationEvidence = evidence; row.RegistryStatus = d.Status; row.IsEnabled = enabled;
                    row.ParserVersion = parser; row.SourceType = d.Tier.ToString(); row.ContentKind = d.Kind;
                    if ((d.Status == OfficialSourceStatuses.Blocked || d.Status == OfficialSourceStatuses.Unsupported) && d.EvidenceNote.Contains("robots", StringComparison.OrdinalIgnoreCase))
                        row.RobotsStatus = "Disallowed";
                    row.UpdatedAtUtc = nowUtc;
                    changed = true;
                }
            }
            if (changed) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        private static bool IsDataSource(OfficialSourceDescriptor d)
            => d.Tier is not (OfficialSourceTier.OfficialYouTube or OfficialSourceTier.OfficialSocial)
               && !(d.Capabilities.Count == 1 && d.Capabilities.Contains(OfficialPurposes.Video))
               && d.Key != "trtspor";

        public async Task<OfficialDataSource?> GetAsync(string sourceKey, CancellationToken ct = default)
            => await _db.OfficialDataSources.FirstOrDefaultAsync(r => r.SourceId == sourceKey, ct).ConfigureAwait(false);

        /// <summary>Devre kesici açık mı? (açıksa istek üretilmez)</summary>
        public static bool IsCircuitOpen(OfficialDataSource? row, DateTime nowUtc)
            => row?.CircuitBreakerUntilUtc is DateTime until && until > nowUtc;

        /// <summary>Tek okumanın sonucunu sağlık kaydına yazar (başarı → sayaç sıfır; hata → devre kesici).</summary>
        public async Task RecordReadAsync<T>(string sourceKey, OfficialRead<T> read, DateTime nowUtc, CancellationToken ct = default)
        {
            var row = await GetAsync(sourceKey, ct).ConfigureAwait(false);
            if (row == null) return;
            Apply(row, read.Outcome, read.Detail, read.Fetch?.Outcome, nowUtc);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        /// <summary>Saf sağlık güncellemesi (test edilebilir).</summary>
        public static void Apply(OfficialDataSource row, string readOutcome, string? detail, string? fetchOutcome, DateTime nowUtc)
        {
            row.LastCheckedUtc = nowUtc;
            row.UpdatedAtUtc = nowUtc;
            var robotsBlocked = fetchOutcome == OfficialFetchOutcomes.RobotsDisallowed || detail == OfficialFetchOutcomes.RobotsDisallowed;
            if (robotsBlocked) row.RobotsStatus = "Disallowed";
            else if (fetchOutcome is OfficialFetchOutcomes.Fetched or OfficialFetchOutcomes.NotModified or OfficialFetchOutcomes.RoundMemo
                     || readOutcome == OfficialReadOutcomes.Ok)
                row.RobotsStatus = "Allowed";

            if (readOutcome == OfficialReadOutcomes.Ok || readOutcome == OfficialReadOutcomes.NotSupported)
            {
                row.LastSuccessUtc = nowUtc;
                row.ConsecutiveFailureCount = 0;
                row.CircuitBreakerUntilUtc = null;
                row.LastError = null;
                return;
            }

            row.ConsecutiveFailureCount++;
            var error = $"{readOutcome}:{detail ?? fetchOutcome}";
            row.LastError = error.Length > 400 ? error[..400] : error;
            if (row.ConsecutiveFailureCount >= CircuitThreshold)
            {
                var exp = Math.Min(10, row.ConsecutiveFailureCount - CircuitThreshold);
                var wait = TimeSpan.FromTicks(Math.Min(CircuitMax.Ticks, CircuitBase.Ticks * (1L << exp)));
                row.CircuitBreakerUntilUtc = nowUtc + wait;
            }
        }
    }
}
