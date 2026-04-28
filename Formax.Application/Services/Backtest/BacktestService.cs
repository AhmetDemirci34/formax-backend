using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Backtest
{
    /// <summary>
    /// FORMAX Backtest (v1.3)
    /// Amaç:
    /// - Sapma eşikleri (0-59 / 60-74 / 75+) için sonuç dağılımını ölçmek.
    /// - "Sapma gerçekten maç sonucu ile korelasyon gösteriyor mu?" sorusunu test etmek.
    ///
    /// Kurallar:
    /// - Abartı yok. Çıktı sadece ölçüm.
    /// - Snapshot yoksa (RequireSnapshot=false) oynanma 50 kabul edilir ve "MissingSnapshot" sayılır.
    /// - Bu servis, ürün diline karışmaz; sadece veri verir.
    /// </summary>
    public sealed class BacktestService
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IMatchOynanmaSnapshotReadRepository _snapshotReadRepository;
        private readonly IGucSkoruCalculator _gucSkoruCalculator;

        public BacktestService(
            IMatchReadRepository matchReadRepository,
            IMatchOynanmaSnapshotReadRepository snapshotReadRepository,
            IGucSkoruCalculator gucSkoruCalculator)
        {
            _matchReadRepository = matchReadRepository;
            _snapshotReadRepository = snapshotReadRepository;
            _gucSkoruCalculator = gucSkoruCalculator;
        }

        public BacktestRunResult Run(BacktestRunRequest req)
        {
            if (req.SampleSize <= 0)
                req = new BacktestRunRequest
                {
                    SampleSize = 200,
                    RequireSnapshot = req.RequireSnapshot,
                    MinSapma = req.MinSapma
                };

            // En yeni bitmiş maçlardan sample al
            var finished = _matchReadRepository.Query()
                .Where(m => m.Status == "Finished")
                .OrderByDescending(m => m.MatchDate)
                .Take(req.SampleSize)
                .ToList();

            var rows = new List<Row>();
            int missingCount = 0;

            foreach (var m in finished)
            {
                // Kritik: MatchDate zaten UTC mantığında tutuluyor -> ToUniversalTime() yok
                var snap = req.RequireSnapshot
                    ? _snapshotReadRepository.GetPreMatchFinalBeforeMatchUtc(m.Id, m.MatchDate)
                    : _snapshotReadRepository.GetLatestBeforeMatchUtc(m.Id, m.MatchDate);
                var missing = snap == null;

                // RequireSnapshot=true ise snapshot yoksa sample'a alma ama say
                if (req.RequireSnapshot && missing)
                {
                    missingCount++;
                    continue;
                }

                var oynanma = missing ? 50 : Clamp01to100(snap!.OynanmaSkoru);

                // Güç skoru (veri temelli)
                var guc = _gucSkoruCalculator.CalculateForMatch(m.Id);
                var gucSkoru = Clamp01to100(guc.GucSkoru);

                var sapma = Math.Abs(oynanma - gucSkoru);
                if (sapma < req.MinSapma)
                    continue;

                var outcome = GetOutcome(m);

                rows.Add(new Row
                {
                    MatchId = m.Id,
                    OynanmaSkoru = oynanma,
                    GucSkoru = gucSkoru,
                    Sapma = sapma,
                    Outcome = outcome,
                    MissingSnapshot = missing
                });
            }

            var denge = Summarize("Denge", rows.Where(r => r.Sapma <= 59).ToList());
            var risk = Summarize("Yanılma Riski", rows.Where(r => r.Sapma >= 60 && r.Sapma <= 74).ToList());
            var high = Summarize("Yüksek Sapma", rows.Where(r => r.Sapma >= 75).ToList());

            return new BacktestRunResult
            {
                SampleSizeRequested = req.SampleSize,
                SampleSizeUsed = rows.Count,
                RequireSnapshot = req.RequireSnapshot,
                MinSapma = req.MinSapma,
                Denge = denge,
                YanilmaRiski = risk,
                YuksekSapma = high,
                Note = req.RequireSnapshot
                    ? $"Bu çıktı bir 'ölçüm'dür. Yönlendirme/tahmin değildir. RequireSnapshot=true iken sadece Source=PreMatchFinal snapshot ile örneklenir. MissingSnapshotTotal={missingCount}"
                    : $"Bu çıktı bir 'ölçüm'dür. Yönlendirme/tahmin değildir. Snapshot kalitesi arttıkça test güvenilirliği artar. MissingSnapshotTotal={missingCount}"
            };
        }

        private static BacktestBucketSummary Summarize(string bucket, List<Row> rows)
        {
            if (rows.Count == 0)
            {
                return new BacktestBucketSummary
                {
                    Bucket = bucket,
                    MatchCount = 0,
                    HomeWin = 0,
                    AwayWin = 0,
                    Draw = 0,
                    MajorityDidNotWinRate = 0,
                    MissingSnapshotCount = 0
                };
            }

            var homeWin = rows.Count(r => r.Outcome == Outcome.HomeWin);
            var awayWin = rows.Count(r => r.Outcome == Outcome.AwayWin);
            var draw = rows.Count(r => r.Outcome == Outcome.Draw);

            // Majority did not win = çoğunluk tarafı kazanmıyor (kaybediyor veya beraberlik)
            int majorityRelevant = 0;
            int majorityDidNotWin = 0;

            foreach (var r in rows)
            {
                // Denge (50) ise çoğunluk yok, hesap dışı
                if (r.OynanmaSkoru == 50) continue;

                majorityRelevant++;
                var majorityIsHome = r.OynanmaSkoru > 50;

                var majorityWon =
                    (majorityIsHome && r.Outcome == Outcome.HomeWin) ||
                    (!majorityIsHome && r.Outcome == Outcome.AwayWin);

                if (!majorityWon)
                    majorityDidNotWin++;
            }

            var rate = majorityRelevant == 0 ? 0.0 : (double)majorityDidNotWin / majorityRelevant;

            return new BacktestBucketSummary
            {
                Bucket = bucket,
                MatchCount = rows.Count,
                HomeWin = homeWin,
                AwayWin = awayWin,
                Draw = draw,
                MajorityDidNotWinRate = Math.Round(rate, 4),
                MissingSnapshotCount = rows.Count(r => r.MissingSnapshot)
            };
        }

        private static Outcome GetOutcome(Match m)
        {
            if (m.HomeScore > m.AwayScore) return Outcome.HomeWin;
            if (m.HomeScore < m.AwayScore) return Outcome.AwayWin;
            return Outcome.Draw;
        }

        private static int Clamp01to100(int v) => Math.Max(0, Math.Min(100, v));

        private enum Outcome { HomeWin, AwayWin, Draw }

        private sealed class Row
        {
            public int MatchId { get; set; }
            public int OynanmaSkoru { get; set; }
            public int GucSkoru { get; set; }
            public int Sapma { get; set; }
            public Outcome Outcome { get; set; }
            public bool MissingSnapshot { get; set; }
        }
    }
}