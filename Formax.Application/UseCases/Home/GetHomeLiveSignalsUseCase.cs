using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.DTOs.Home;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Home
{
    /// <summary>
    /// FORMAX — FAZ 5 canlı derinlik sinyalleri.
    /// Tempo / sertlik / momentum / kritik kırılma okumalarını home ekranına taşır.
    /// </summary>
    public sealed class GetHomeLiveSignalsUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IMatchSapmaSnapshotRepository _snapshotRepository;

        public GetHomeLiveSignalsUseCase(
            IMatchReadRepository matchReadRepository,
            IMatchSapmaSnapshotRepository snapshotRepository)
        {
            _matchReadRepository = matchReadRepository;
            _snapshotRepository = snapshotRepository;
        }

        public async Task<HomeLiveSignalResponseDto> ExecuteAsync(DateTime utcNow)
        {
            var items = await _matchReadRepository.GetMatchListAsync();
            if (items == null || items.Count == 0)
            {
                return new HomeLiveSignalResponseDto { SummaryText = "Canlı sinyal için maç bulunamadı.", Matches = Array.Empty<HomeLiveSignalMatchDto>() };
            }

            var snapshots = await _snapshotRepository.GetFreshByMatchIdsAsync(items.Select(x => x.MatchId).ToList(), utcNow);
            var snapMap = snapshots.ToDictionary(x => x.MatchId, x => x);

            var matches = items
                .Where(x => snapMap.ContainsKey(x.MatchId))
                .Select(x =>
                {
                    var s = snapMap[x.MatchId];
                    var proximity = ComputeTimeProximityScore(x.StartTime, utcNow, x.Status);
                    var isLive = IsLive(x.Status);

                    var tempo = BuildSignal(
                        isLive ? 35 + (x.Minute ?? 0) / 2 + s.Sapma / 3 : 18 + proximity / 4 + s.Sapma / 4,
                        "Tempo",
                        isLive ? "Oyun akışı hızlanabilir." : "Maç yaklaşırken tempo ön işareti oluşuyor.");

                    var sertlik = BuildSignal(
                        isLive ? 22 + (x.Minute ?? 0) / 3 + (x.MatchId % 4) * 9 : 12 + (x.MatchId % 4) * 7,
                        "Sertlik",
                        isLive ? "Temas, kart ve gerilim izleniyor." : "Temas seviyesi için henüz erken ama çizgi kuruluyor.");

                    var momentum = BuildSignal(
                        isLive ? 28 + s.Sapma / 2 + (x.Minute ?? 0) / 4 : 16 + proximity / 5 + s.Sapma / 3,
                        "Momentum",
                        isLive ? "Baskı yönü canlı akışta değişebilir." : "Sapma ve yakınlık, baskı yönünü önceden fısıldıyor.");

                    var kritik = BuildSignal(
                        isLive ? 18 + s.Sapma / 2 + ((x.Minute ?? 0) > 70 ? 18 : 0) : (s.Sapma >= 60 ? 48 : 20),
                        "Kritik Kırılma",
                        isLive ? "Gol, kırmızı kart veya penaltı eşiği izleniyor." : "Kırılma eşiği düşük ama radar bu maçı bırakmıyor.");

                    return new HomeLiveSignalMatchDto
                    {
                        MatchId = x.MatchId,
                        Teams = new HomeTeamsDto { Home = x.HomeTeam, Away = x.AwayTeam },
                        League = x.League ?? string.Empty,
                        Status = string.IsNullOrWhiteSpace(x.Status) ? "PreMatch" : x.Status,
                        Tempo = tempo,
                        Sertlik = sertlik,
                        Momentum = momentum,
                        KritikKirilma = kritik,
                        Note = isLive
                            ? "Canlı akış açıldığında bu dört sinyal aynı anda okunur."
                            : "Maç henüz başlamasa da home ekranı canlı derinlik için ön işaretleri hazırlar."
                    };
                })
                .OrderByDescending(x => x.Momentum.Value + x.Tempo.Value + x.KritikKirilma.Value)
                .Take(4)
                .ToList();

            return new HomeLiveSignalResponseDto
            {
                SummaryText = matches.Any(m => IsLive(m.Status))
                    ? "Canlı derinlik açık: tempo, sertlik, momentum ve kırılma birlikte okunuyor."
                    : "FAZ 5 hazırlığı aktif: maç başlamadan önce bile sinyal zeminini görüyoruz.",
                Matches = matches
            };
        }

        private static bool IsLive(string status)
        {
            var s = status?.ToUpperInvariant() ?? string.Empty;
            return s.Contains("LIVE") || s.Contains("INPLAY");
        }

        private static int ComputeTimeProximityScore(DateTime startTimeUtc, DateTime utcNow, string status)
        {
            if (IsLive(status)) return 100;
            var hours = (startTimeUtc - utcNow).TotalHours;
            if (hours <= 0) return 90;
            if (hours <= 3) return 100;
            if (hours <= 6) return 85;
            if (hours <= 12) return 70;
            if (hours <= 24) return 55;
            if (hours <= 48) return 35;
            return 20;
        }

        private static LiveSignalDto BuildSignal(int value, string name, string baseText)
        {
            var v = Math.Max(0, Math.Min(100, value));
            var level = v >= 60 ? "Yüksek" : v >= 30 ? "Orta" : "Düşük";
            return new LiveSignalDto
            {
                Value = v,
                Level = level,
                Text = $"{name}: {baseText}"
            };
        }
    }
}
