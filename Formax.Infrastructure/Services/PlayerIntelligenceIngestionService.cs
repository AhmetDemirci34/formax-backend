using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Players;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Services
{
    /// <summary>
    /// Football Intelligence v1.0 — GDP player/squad ingestion. api-football /players?team=&season=
    /// + /injuries?team=&season= verisinden takım oyuncu-düzeyi zekâsını türetir ve TeamPlayerIntelligence'e
    /// upsert eder. Coverage yoksa HasData=false (fake YOK). Olasılık/gol modeli OKUMAZ → hash etkilenmez.
    /// TeamProfileSignal ingestion deseniyle aynı: internal Team.Id → external id ile provider çağrısı.
    /// </summary>
    public sealed class PlayerIntelligenceIngestionService
    {
        private readonly FormaxDbContext _db;
        private readonly ISportsDataProvider _provider;
        private readonly ITeamPlayerIntelligenceRepository _repo;
        private readonly ILogger<PlayerIntelligenceIngestionService> _logger;

        public PlayerIntelligenceIngestionService(
            FormaxDbContext db, ISportsDataProvider provider,
            ITeamPlayerIntelligenceRepository repo, ILogger<PlayerIntelligenceIngestionService> logger)
        {
            _db = db; _provider = provider; _repo = repo; _logger = logger;
        }

        // ── Watermark (HistoricalSyncJob/TimelineSyncedAt deseniyle aynı) ──────────────
        // Kaynak: TeamPlayerIntelligence.UpdatedAt + HasData (mevcut veri modeli; yeni alan/tablo YOK).
        // Kapsamı olan takım FreshHours boyunca yeniden çekilmez; kapsamı OLMAYAN takım
        // (HasData=false) EmptyBackoffHours boyunca yeniden DENENMEZ — çünkü her deneme
        // N→N-1→N-2 season fallback'i yüzünden birkaç isteğe mal oluyor ve kesin boş dönüyor.
        // Süreler dolduğunda takım normal akışa geri döner → veri kalıcı olarak kilitlenmez.
        // Admin tekil tetik (IngestByExternalAsync) watermark'a TABİ DEĞİLDİR: acil tazeleme kapısı.
        private const int FreshHours = 20;          // job 12 saatte bir → her ikinci turda tazelenir
        private const int EmptyBackoffHours = 168;  // 7 gün: kapsamı olmayan takımı boşuna dövme

        public static int ResolveSeason(DateTime utc) => utc.Month >= 7 ? utc.Year : utc.Year - 1;

        /// <summary>
        /// Bu takım şu an çekilmeli mi? (watermark kararı — saf fonksiyon, test edilebilir)
        /// Kayıt yoksa → evet. HasData ise FreshHours, değilse EmptyBackoffHours beklenir.
        /// </summary>
        internal static bool IsDue(DateTime utcNow, DateTime? updatedAt, bool hasData)
        {
            if (updatedAt is not DateTime stamp) return true;
            var age = utcNow - stamp;
            if (age < TimeSpan.Zero) return true; // ileri tarihli/bozuk damga → kilitleme
            return age >= TimeSpan.FromHours(hasData ? FreshHours : EmptyBackoffHours);
        }

        /// <summary>Admin/test: external takım id ile ingest (internal team çözülür).</summary>
        public async Task<bool> IngestByExternalAsync(string externalTeamId, int? season, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(externalTeamId)) return false;
            var team = await _db.Teams.FirstOrDefaultAsync(t => t.ExternalTeamId == externalTeamId, ct);
            if (team == null) return false;
            return await IngestTeamAsync(team.Id, externalTeamId, season ?? ResolveSeason(DateTime.UtcNow), ct);
        }

        /// <summary>Yaklaşan (−1..+7g) maçlardaki takımlar için ingest (günlük job + admin).</summary>
        public async Task<int> IngestUpcomingAsync(int maxTeams, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var from = now.AddDays(-1); var to = now.AddDays(7);

            // Yaklaşan maçların takım id'lerini materialize et (EF SelectMany[array] çeviremiyor).
            var pairs = await _db.Matches
                .Where(m => m.MatchDate >= from && m.MatchDate <= to && m.HomeTeamId > 0 && m.AwayTeamId > 0)
                .Select(m => new { m.HomeTeamId, m.AwayTeamId })
                .ToListAsync(ct);
            var teamIds = pairs.SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId }).Distinct().ToList();

            var candidates = await _db.Teams
                .Where(t => teamIds.Contains(t.Id) && t.ExternalTeamId != null && t.ExternalTeamId != "")
                .Select(t => new { t.Id, t.ExternalTeamId })
                .ToListAsync(ct);

            // WATERMARK: yakın zamanda işlenmiş takımlar elenir → aynı takım her turda
            // yeniden çekilmez. Kota, gerçekten güncellenmesi gereken takımlara gider.
            var candidateIds = candidates.Select(c => c.Id).ToList();
            var marks = await _db.Set<TeamPlayerIntelligence>()
                .Where(x => candidateIds.Contains(x.TeamId))
                .Select(x => new { x.TeamId, x.UpdatedAt, x.HasData })
                .ToListAsync(ct);
            var markByTeam = marks
                .GroupBy(m => m.TeamId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

            var teams = candidates
                .Where(c => !markByTeam.TryGetValue(c.Id, out var m) || IsDue(now, m.UpdatedAt, m.HasData))
                .Take(maxTeams)
                .ToList();

            var skipped = candidates.Count - teams.Count;

            if (teams.Count == 0)
            {
                _logger.LogInformation(
                    "[PLAYER INTEL] Çekilecek takım yok — {Skipped} takım watermark ile atlandı.", skipped);
                return 0;
            }

            int withData = 0;
            var season = ResolveSeason(now);
            foreach (var t in teams)
            {
                if (await IngestTeamAsync(t.Id, t.ExternalTeamId!, season, ct)) withData++;
            }
            _logger.LogInformation(
                "[PLAYER INTEL] {Teams} takım işlendi, {WithData} kapsamlı, {Skipped} watermark ile atlandı (sezon {Season}).",
                teams.Count, withData, skipped, season);
            return withData;
        }

        /// <summary>Çekirdek: players + injuries çek (SEASON FALLBACK: N → N-1 → N-2), zekâyı hesapla,
        /// upsert et. İlk gerçek veri veren sezon kullanılır. HasData döner.</summary>
        public async Task<bool> IngestTeamAsync(int internalTeamId, string externalTeamId, int season, CancellationToken ct)
        {
            var players = new List<SportsPlayerSeasonStat>();
            int usedSeason = season;
            for (int s = season; s >= season - 2; s--)
            {
                players = await _provider.GetTeamPlayersAsync(externalTeamId, s, ct);
                if (players.Count > 0) { usedSeason = s; break; }
            }

            var injuries = await _provider.GetTeamInjuriesAsync(externalTeamId, usedSeason, ct);

            var intel = Compute(internalTeamId, externalTeamId, usedSeason, players, injuries);
            await _repo.UpsertAsync(intel, ct);
            return intel.HasData;
        }

        // Saf hesap (test edilebilir): oyuncu listesi + sakatlıklardan takım zekâsı.
        internal static TeamPlayerIntelligence Compute(
            int teamId, string externalTeamId, int season,
            List<SportsPlayerSeasonStat> players, List<SportsTeamInjury> injuries)
        {
            var intel = new TeamPlayerIntelligence
            {
                TeamId = teamId,
                ExternalTeamId = externalTeamId,
                Season = season,
                UpdatedAt = DateTime.UtcNow
            };

            if (players == null || players.Count == 0)
            {
                intel.HasData = false;
                return intel;
            }

            intel.HasData = true;
            intel.SquadPlayerCount = players.Count;

            static string Zone(string pos) => (pos ?? "").ToLowerInvariant() switch
            {
                var p when p.Contains("goalkeeper") => "GK",
                var p when p.Contains("defender")   => "DEF",
                var p when p.Contains("midfielder") => "MID",
                var p when p.Contains("attacker")   => "ATT",
                _ => ""
            };

            foreach (var p in players)
            {
                switch (Zone(p.Position))
                {
                    case "GK": intel.GkCount++; break;
                    case "DEF": intel.DefCount++; break;
                    case "MID": intel.MidCount++; break;
                    case "ATT": intel.AttCount++; break;
                }
            }

            var topScorer = players.Where(p => p.Goals > 0)
                .OrderByDescending(p => p.Goals).ThenByDescending(p => p.Assists).FirstOrDefault();
            if (topScorer != null)
            {
                intel.TopScorerName = topScorer.Name;
                intel.TopScorerGoals = topScorer.Goals;
                intel.TopScorerAssists = topScorer.Assists;
                intel.TopScorerRating = topScorer.Rating ?? 0;
            }

            var topAssist = players.Where(p => p.Assists > 0)
                .OrderByDescending(p => p.Assists).FirstOrDefault();
            if (topAssist != null)
            {
                intel.TopAssistName = topAssist.Name;
                intel.TopAssistCount = topAssist.Assists;
            }

            // Kilit oyuncu = en yüksek rating (yeterli süre); yoksa genel en yüksek rating.
            var rated = players.Where(p => p.Rating.HasValue && p.Rating > 0).ToList();
            var keyPlayer = rated.Where(p => p.Minutes >= 300).OrderByDescending(p => p.Rating).FirstOrDefault()
                            ?? rated.OrderByDescending(p => p.Rating).FirstOrDefault();
            if (keyPlayer != null)
            {
                intel.KeyPlayerName = keyPlayer.Name;
                intel.KeyPlayerRating = keyPlayer.Rating ?? 0;
            }

            var minutesLeader = players.OrderByDescending(p => p.Minutes).FirstOrDefault();
            if (minutesLeader != null && minutesLeader.Minutes > 0)
            {
                intel.MinutesLeaderName = minutesLeader.Name;
                intel.MinutesLeaderMinutes = minutesLeader.Minutes;
            }

            // Sakat/cezalı — isimli + bölge (oyuncu listesindeki pozisyondan).
            if (injuries is { Count: > 0 })
            {
                var posByName = players
                    .GroupBy(p => p.Name.Trim().ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().Position);

                var names = new List<string>();
                foreach (var inj in injuries)
                {
                    names.Add(inj.Name);
                    var pos = posByName.TryGetValue(inj.Name.Trim().ToLowerInvariant(), out var pp) ? Zone(pp) : "";
                    switch (pos)
                    {
                        case "DEF": intel.InjuredDefCount++; break;
                        case "MID": intel.InjuredMidCount++; break;
                        case "ATT": intel.InjuredAttCount++; break;
                    }
                }
                intel.InjuredCount = injuries.Count;
                intel.InjuredNames = string.Join("|", names.Take(8));
            }

            // ── v2 Deep Intelligence ────────────────────────────────────────────
            // Gol yükü konsantrasyonu + tek/iki-adam bağımlılığı (gerçek gol dağılımından).
            var teamGoals = players.Sum(p => p.Goals);
            intel.TeamTotalGoals = teamGoals;
            if (teamGoals > 0)
            {
                var scorers = players.Where(p => p.Goals > 0).OrderByDescending(p => p.Goals).ToList();
                var top1 = scorers.Count >= 1 ? scorers[0].Goals : 0;
                var top2 = top1 + (scorers.Count >= 2 ? scorers[1].Goals : 0);
                intel.TopScorerGoalSharePct = (int)System.Math.Round(100.0 * top1 / teamGoals);
                intel.Top2GoalSharePct = (int)System.Math.Round(100.0 * top2 / teamGoals);
                // Anlamlı olması için en az 5 takım golü.
                intel.OneManDependency = teamGoals >= 5 &&
                    (intel.TopScorerGoalSharePct >= 40 || intel.Top2GoalSharePct >= 60);
            }

            // Pozisyonel liderler (min. süre 300 dk, yoksa genel en iyi).
            static bool IsZone(SportsPlayerSeasonStat p, string zone)
                => (p.Position ?? "").ToLowerInvariant().Contains(zone);

            var defLeader = players.Where(p => IsZone(p, "defender") && p.Rating.HasValue && p.Rating > 0 && p.Minutes >= 300)
                                   .OrderByDescending(p => p.Rating)
                                   .FirstOrDefault()
                            ?? players.Where(p => IsZone(p, "defender") && p.Rating.HasValue && p.Rating > 0)
                                   .OrderByDescending(p => p.Rating).FirstOrDefault();
            if (defLeader != null)
            {
                intel.DefenseLeaderName = defLeader.Name;
                intel.DefenseLeaderRating = defLeader.Rating ?? 0;
            }

            // Orta sahanın beyni = orta sahada en çok asist (eşitlikte rating).
            var midBrain = players.Where(p => IsZone(p, "midfielder") && p.Minutes >= 300)
                                  .OrderByDescending(p => p.Assists).ThenByDescending(p => p.Rating ?? 0)
                                  .FirstOrDefault()
                           ?? players.Where(p => IsZone(p, "midfielder"))
                                  .OrderByDescending(p => p.Assists).ThenByDescending(p => p.Rating ?? 0)
                                  .FirstOrDefault();
            if (midBrain != null)
            {
                intel.MidfieldBrainName = midBrain.Name;
                intel.MidfieldBrainAssists = midBrain.Assists;
                intel.MidfieldBrainRating = midBrain.Rating ?? 0;
            }

            var shotsLeader = players.Where(p => p.Shots > 0).OrderByDescending(p => p.Shots).FirstOrDefault();
            if (shotsLeader != null)
            {
                intel.ShotsLeaderName = shotsLeader.Name;
                intel.ShotsLeaderCount = shotsLeader.Shots;
            }

            var keyPassLeader = players.Where(p => p.KeyPasses > 0).OrderByDescending(p => p.KeyPasses).FirstOrDefault();
            if (keyPassLeader != null)
            {
                intel.KeyPassLeaderName = keyPassLeader.Name;
                intel.KeyPassLeaderCount = keyPassLeader.KeyPasses;
            }

            var cardRisk = players.Where(p => p.Yellow > 0).OrderByDescending(p => p.Yellow).FirstOrDefault();
            if (cardRisk != null && cardRisk.Yellow >= 4)
            {
                intel.CardRiskName = cardRisk.Name;
                intel.CardRiskYellows = cardRisk.Yellow;
            }

            return intel;
        }
    }
}
