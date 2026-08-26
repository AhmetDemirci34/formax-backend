using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.News.Intelligence;
using Formax.Application.Services.Social.Discovery;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// FORMAX GDP Phase 7 — Social Discovery scheduler. Her 5 dakikada bir yaklaşan maçların
    /// takımlarına ait DOĞRULANMIŞ resmi sosyal hesapların son paylaşımlarını (yalnız gerçek,
    /// registry'deki verified hesaplar) çeker → signal-type → Canonical SocialPost (FORMAX_MATCH_ID).
    /// Tek Canonical Social: aynı kayıt hem AI Context'i hem Match Detail'i besler.
    /// Job mimarisi: bu job YALNIZ resmi sosyal medyadan sorumlu (News/Live/Daily ayrı).
    /// </summary>
    public sealed class SocialDiscoveryJob : BackgroundService
    {
        private static readonly TimeSpan Cycle = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(50);
        private const int UpcomingHorizonDays = 7;

        /// <summary>
        /// BİTMİŞ MAÇLAR DA KAPSANIR — "Önemli Anları İzle" asıl olarak oynanmış maçın
        /// videolarını gösterir. Pencere yalnız -1 gün olduğu için maç bittikten sonra
        /// yayımlanan resmi özet/gol videoları hiç toplanmıyordu.
        /// </summary>
        // Video yayin kuyrugu ile HIZALI: resmi ozet video maçtan gunler sonra yayimlanabiliyor
        // (olculdu: Hajduk 4-0 Žalgiris maci 13.08, resmi ozet videosu 15.08). Bu yuzden
        // pencere VideoMatchValidator.PublishTail (7 gun) ile ayni tutulur.
        private const int RecentlyPlayedDays = 8;
        private const int RecentPostWindowDays = 10; // maça yakın resmi aktivite
        private const int MaxMatchesPerCycle = 40;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SocialDiscoveryJob> _logger;

        public SocialDiscoveryJob(IServiceScopeFactory scopeFactory, ILogger<SocialDiscoveryJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[SOCIAL] Discovery scheduler started.");
            try { await Task.Delay(StartupDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await RunCycleAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { _logger.LogError(ex, "[SOCIAL] Discovery döngüsü başarısız."); }

                try { await Task.Delay(Cycle, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>Public: admin/manuel doğrulama için (üretim tetikleyicisi 5 dk döngü).</summary>
        public async Task<int> RunCycleAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var matchRepo = sp.GetRequiredService<IMatchReadRepository>();
            var accountRepo = sp.GetRequiredService<IOfficialSocialAccountRepository>();
            var postRepo = sp.GetRequiredService<ISocialPostRepository>();
            var providers = sp.GetServices<ISocialProvider>().Where(p => p.IsEnabled).ToList();
            var signals = sp.GetRequiredService<SignalExtractor>();
            var idFactory = sp.GetRequiredService<FormaxMatchIdFactory>();
            var db = sp.GetRequiredService<FormaxDbContext>();

            if (providers.Count == 0) return 0;

            // REGISTRY-GÜDÜMLÜ: yalnız doğrulanmış resmi hesabı olan takımların maçları işlenir.
            var accounts = accountRepo.GetActiveVerified()
                .Where(a => !string.IsNullOrWhiteSpace(a.ExternalTeamId))
                .ToList();
            if (accounts.Count == 0) return 0;
            var coveredExt = new HashSet<string>(accounts.Select(a => a.ExternalTeamId!), StringComparer.Ordinal);
            var accountsByExt = accounts
                .GroupBy(a => a.ExternalTeamId!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var allMatches = matchRepo.GetUpcomingMatches(now.AddDays(-RecentlyPlayedDays), now.AddDays(UpcomingHorizonDays))
                .Where(m => m.HomeTeamId > 0 && m.AwayTeamId > 0)
                .ToList();
            if (allMatches.Count == 0) return 0;

            // internal Team.Id → external id (yalnız bu maçlardaki takımlar).
            var internalIds = allMatches.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).Distinct().ToList();
            var teams = await db.Teams
                .Where(t => internalIds.Contains(t.Id) && t.ExternalTeamId != null)
                .Select(t => new { t.Id, t.ExternalTeamId })
                .ToListAsync(ct);
            var intToExt = teams.ToDictionary(t => t.Id, t => t.ExternalTeamId!);

            // Kapsamlı takımı (home VEYA away) olan maçları seç, sonra üst sınır uygula.
            var matches = allMatches
                .Where(m => (intToExt.TryGetValue(m.HomeTeamId, out var he) && coveredExt.Contains(he))
                         || (intToExt.TryGetValue(m.AwayTeamId, out var ae) && coveredExt.Contains(ae)))
                .OrderBy(m => m.MatchDate)
                .Take(MaxMatchesPerCycle)
                .ToList();
            if (matches.Count == 0) return 0;

            // Hesap-başına feed'i döngü içinde bir kez çek (aynı hesap birçok maça bağlanır).
            var feedCache = new Dictionary<string, IReadOnlyList<SocialCandidate>>(StringComparer.Ordinal);
            var totalAdded = 0;
            var cutoff = now.AddDays(-RecentPostWindowDays);

            foreach (var m in matches)
            {
                ct.ThrowIfCancellationRequested();
                var league = m.League ?? "";
                var homeName = db.Teams.Where(t => t.Id == m.HomeTeamId).Select(t => t.Name).FirstOrDefault() ?? "";
                var awayName = db.Teams.Where(t => t.Id == m.AwayTeamId).Select(t => t.Name).FirstOrDefault() ?? "";

                string formaxMatchId;
                try { formaxMatchId = idFactory.Create(m.MatchDate, homeName, awayName); }
                catch { continue; }

                var posts = new List<SocialPost>();
                foreach (var internalTeamId in new[] { m.HomeTeamId, m.AwayTeamId })
                {
                    if (!intToExt.TryGetValue(internalTeamId, out var ext)) continue;
                    if (!accountsByExt.TryGetValue(ext, out var teamAccounts)) continue;

                    foreach (var account in teamAccounts)
                    {
                        var provider = providers.FirstOrDefault(p => p.CanHandle(account));
                        if (provider == null) continue;

                        var cacheKey = provider.Platform + "|" + account.Handle;
                        if (!feedCache.TryGetValue(cacheKey, out var candidates))
                        {
                            candidates = await provider.FetchAsync(account, ct);
                            feedCache[cacheKey] = candidates;
                        }

                        foreach (var c in candidates)
                        {
                            if (c.PublishedUtc < cutoff) continue; // yalnız maça yakın resmi aktivite
                            var type = signals.Primary(signals.Extract(c.Title, c.Summary));
                            posts.Add(new SocialPost
                            {
                                FormaxMatchId = formaxMatchId,
                                Platform = c.Platform,
                                AccountHandle = c.AccountHandle,
                                AccountName = c.AccountName,
                                RelatedTeamId = internalTeamId,
                                Headline = c.Title,
                                Summary = c.Summary,
                                Url = c.Url,
                                PublishedUtc = c.PublishedUtc,
                                SignalType = type,
                                SourceTrust = 95, // doğrulanmış resmi hesap
                                IsOfficial = true,
                                ContentHash = Hash(formaxMatchId + "|" + c.Platform + "|" + (string.IsNullOrEmpty(c.Url) ? c.Title : c.Url))
                            });
                        }
                    }
                }

                if (posts.Count > 0)
                    totalAdded += await postRepo.UpsertAsync(posts, ct);
            }

            _logger.LogInformation(
                "[SOCIAL] Cycle — {Matches} maç, {Accounts} resmi hesap, {Added} yeni paylaşım.",
                matches.Count, accounts.Count, totalAdded);
            return totalAdded;
        }

        private static string Hash(string s)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes, 0, 16);
        }
    }
}
