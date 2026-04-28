using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Home;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Home
{
    /// <summary>
    /// FORMAX v1.4 — FAZ 4.2 (KİLİTLİ)
    /// Home vitrini: Günün en yüksek sapmalı 4 maçı (sade DTO).
    ///
    /// Kurallar:
    /// - Sapma >= 60 olanlar üstte
    /// - Liste 4'e tamamlanır
    /// - Hiç 60+ yoksa: en yüksek 4 + "Bugün ölçüm sakin."
    /// </summary>
    public sealed class GetHomeTopSapmaUseCase
    {
        private const int VITRIN_SIZE = 4;
        private const int SAPMA_STRONG_THRESHOLD = 60;

        private const int LIVE_TTL_SECONDS = 45;   // 🔒 LiveTTL
        private const int MAX_TTL_SECONDS = 120;   // 🔒 Analysis MaxTTL

        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IMatchSapmaSnapshotRepository _snapshotRepository;

        public GetHomeTopSapmaUseCase(
            IMatchReadRepository matchReadRepository,
            IMatchSapmaSnapshotRepository snapshotRepository)
        {
            _matchReadRepository = matchReadRepository;
            _snapshotRepository = snapshotRepository;
        }

        private sealed class Candidate
        {
            public required HomeTopSapmaMatchDto Dto { get; init; }
            public required DateTime StartTime { get; init; }
            public required int Sapma { get; init; }
            public required bool SessizMi { get; init; }
        }

        private static string ComputeFreshness(DateTime utcNow, DateTime computedAtUtc)
        {
            var ageSec = (int)Math.Max(0, (utcNow - computedAtUtc).TotalSeconds);

            if (ageSec <= LIVE_TTL_SECONDS) return "Live";
            if (ageSec <= MAX_TTL_SECONDS) return "Ok";
            return "Stale";
        }

        public async Task<HomeTopSapmaResponseDto> ExecuteAsync(DateTime utcNow)
        {
            // 1️⃣ Repo’dan listeyi al
            var items = await _matchReadRepository.GetMatchListAsync();

            if (items == null || items.Count == 0)
            {
                return new HomeTopSapmaResponseDto
                {
                    BannerText = "Bugün ölçüm sakin.",
                    Matches = Array.Empty<HomeTopSapmaMatchDto>()
                };
            }

            // 2️⃣ Snapshot'ları oku (kritik performans koruması)
            // SapmaMotor request-time çalışmaz. SapmaSnapshotJob önceden üretir.
            var matchIds = items.Select(x => x.MatchId).ToList();
            var snapshots = await _snapshotRepository.GetFreshByMatchIdsAsync(matchIds, utcNow);

            if (snapshots == null || snapshots.Count == 0)
            {
                // Sessiz prensip: snapshot yoksa sistem "sakin" döner.
                return new HomeTopSapmaResponseDto
                {
                    BannerText = "Bugün ölçüm sakin.",
                    Matches = Array.Empty<HomeTopSapmaMatchDto>()
                };
            }

            var snapMap = snapshots.ToDictionary(x => x.MatchId, x => x);

            // 3️⃣ Sadece snapshot bulunan maçlardan vitrin adaylarını üret (FAZ 4.2 sade DTO)
            var enriched = new List<Candidate>(snapshots.Count);

            foreach (var m in items)
            {
                if (!snapMap.TryGetValue(m.MatchId, out var s))
                    continue;

                var dto = new HomeTopSapmaMatchDto
                {
                    MatchId = m.MatchId,
                    Teams = new HomeTeamsDto { Home = m.HomeTeam, Away = m.AwayTeam },
                    Sapma = s.Sapma,
                    Bolge = s.Bolge,
                    Freshness = ComputeFreshness(utcNow, s.ComputedAtUtc), // ✅ s.Freshness YOK
                    SessizMi = s.SessizMi
                };

                enriched.Add(new Candidate
                {
                    Dto = dto,
                    StartTime = m.StartTime,
                    Sapma = s.Sapma,
                    SessizMi = s.SessizMi
                });
            }

            if (enriched.Count == 0)
            {
                return new HomeTopSapmaResponseDto
                {
                    BannerText = "Bugün ölçüm sakin.",
                    Matches = Array.Empty<HomeTopSapmaMatchDto>()
                };
            }

            // 4️⃣ Vitrin algoritması (FAZ 4.1 kuralı değişmedi)
            var strong = enriched
                .Where(x => x.Sapma >= SAPMA_STRONG_THRESHOLD)
                .OrderByDescending(x => x.Sapma)
                .ThenBy(x => x.SessizMi)  // sessiz olmayan önce
                .ThenBy(x => x.StartTime)
                .ToList();

            List<Candidate> vitrinde;

            if (strong.Count > 0)
            {
                vitrinde = strong.Take(VITRIN_SIZE).ToList();

                if (vitrinde.Count < VITRIN_SIZE)
                {
                    var fill = enriched
                        .Where(x => vitrinde.All(v => v.Dto.MatchId != x.Dto.MatchId))
                        .OrderByDescending(x => x.Sapma)
                        .ThenBy(x => x.StartTime)
                        .Take(VITRIN_SIZE - vitrinde.Count)
                        .ToList();

                    vitrinde.AddRange(fill);
                }

                return new HomeTopSapmaResponseDto
                {
                    BannerText = null,
                    Matches = vitrinde.Select(x => x.Dto).ToList()
                };
            }

            vitrinde = enriched
                .OrderByDescending(x => x.Sapma)
                .ThenBy(x => x.SessizMi) // sessiz olmayan önce
                .ThenBy(x => x.StartTime)
                .Take(VITRIN_SIZE)
                .ToList();

            return new HomeTopSapmaResponseDto
            {
                BannerText = "Bugün ölçüm sakin.",
                Matches = vitrinde.Select(x => x.Dto).ToList()
            };
        }
    }
}