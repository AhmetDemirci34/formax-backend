using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Lineups;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Outcomes;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.Lineups
{
    /// <summary>
    /// KADRO KATMANI ÖLÇÜM SERVİSİ — zamansal (sızıntısız) geriye dönük test ve ablasyonları çalıştırır.
    ///
    /// DIŞ İSTEK YOKTUR: yalnız canonical DB okunur. Sonuç ÜRETİME HİÇBİR ŞEY AÇMAZ; karar kapısının
    /// çıktısı rapordur. Katmanın üretime geçmesi <c>LineupImpact:Production</c> ayarının AÇIKÇA
    /// açılmasını gerektirir.
    /// </summary>
    public sealed class LineupImpactBacktestService
    {
        /// <summary>
        /// Ölçüm penceresi — kadro verisi yalnız son aylarda toplandığı için model penceresi (600 gün)
        /// kullanılamaz. Test, kadro gözlemlerinin İLK gününden başlar; eğitim penceresi bütün geçmiştir.
        /// </summary>
        public static readonly TimeSpan DefaultTestWindow = TimeSpan.FromDays(120);

        private readonly OutcomeHistoryLoader _history;
        private readonly LineupHistoryLoader _lineups;
        private readonly OutcomeModelTrainingService _training;
        private readonly ILogger<LineupImpactBacktestService> _log;

        public LineupImpactBacktestService(
            OutcomeHistoryLoader history, LineupHistoryLoader lineups,
            OutcomeModelTrainingService training, ILogger<LineupImpactBacktestService> log)
        {
            _history = history; _lineups = lineups; _training = training; _log = log;
        }

        /// <param name="officialOnly">
        /// true = yalnız resmî kaynaktan doğrulanmış kadrolar (üretim tanımı). false = depodaki bütün
        /// kadro satırları; kapsamın üst sınırını görmek için ölçüm yolunda kullanılır ve raporda
        /// AYRI etiketlenir — üretim kararına bu ölçüm TEK BAŞINA temel olamaz.
        /// </param>
        public async Task<LineupBacktestReport> RunAsync(
            DateTime nowUtc, bool officialOnly = true, TimeSpan? testWindow = null, CancellationToken ct = default)
        {
            var history = await _history.LoadAsync(nowUtc, ct).ConfigureAwait(false);
            var names = await _history.LoadCompetitionNamesAsync(ct).ConfigureAwait(false);
            var lineups = await _lineups.LoadAsync(null, officialOnly, ct).ConfigureAwait(false);
            var (_, parameters) = await _training.LatestAcceptedAsync(ct).ConfigureAwait(false);

            // TEST PENCERESİ — kadro geçmişinin bir kısmı ÖĞRENMEYE ayrılmalı. Gözlemlerin ilk
            // yarısı yalnız eğitim (oyuncu örneklemi birikir), ikinci yarısı test olur. Pencere
            // elle verilirse o kullanılır. Bu bölme sonuca değil YALNIZ tarihe bakar: sızıntı yok.
            var verified = lineups.Where(l => LineupVerificationRule.Check(l).Accepted)
                .OrderBy(l => l.KickoffUtc).ToList();
            DateTime testStart;
            if (testWindow.HasValue) testStart = nowUtc - testWindow.Value;
            else if (verified.Count >= 4) testStart = verified[verified.Count / 2].KickoffUtc;
            else testStart = nowUtc - DefaultTestWindow;

            var report = await Task.Run(() =>
            {
                var catalog = CompetitionCatalog.Build(history, names);
                return LineupBacktest.Run(history, lineups, catalog, LockedCompetitions.All.ToHashSet(),
                    parameters, new PlayerImpactParameters(), testStart, nowUtc);
            }, ct).ConfigureAwait(false);

            _log.LogInformation(
                "[LINEUP IMPACT] ölçüm: kadro gözlemi={Obs} doğrulanmış={Ver} test maçı={Test} kadrolu={WithLineup} " +
                "yeterli örneklemli oyuncu={Players} karar={Decision} gerekçe={Reasons}",
                report.LineupObservations, report.VerifiedObservations, report.TestMatches, report.TestMatchesWithLineup,
                report.SufficientPlayers, report.Decision, string.Join(",", report.DecisionReasons));
            return report;
        }
    }
}
