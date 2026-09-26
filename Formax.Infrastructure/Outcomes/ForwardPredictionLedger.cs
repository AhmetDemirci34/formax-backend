using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Outcomes
{
    /// <summary>
    /// İLERİYE DÖNÜK GÖLGE DEFTERİ — Model 4.0 ve Model 5 gölge, başlamaya en çok <see cref="LockWindow"/> kala bir kez kaydedilir.
    /// Kayıt anı = kilit anı; olasılık alanları bir daha YAZILMAZ (yalnız ekleme). Başlama saati geçmiş maç için kayıt açılmaz.
    /// Puanlama sonuç botunun kanonik skoruyla yapılır; skor sonradan değişirse yeniden puanlanır ve denetim satırı eklenir.
    /// Yalnız DB; dış istek yok. Kullanıcı uçları bu tabloyu okumaz.
    /// </summary>
    public static class ForwardPredictionLedger
    {
        public static readonly TimeSpan LockWindow = TimeSpan.FromHours(24);
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

        public sealed record Input(int MatchId, int LeagueId, DateTime KickoffUtc, string? SnapshotId, ScoreDistribution Model40, ScoreDistribution Model5,
            int HomeSample, int AwaySample, double Coverage);

        /// <summary>Bir model dağılımından altı market satırı (tier: ileriye dönük gölge seçicisi).</summary>
        public static List<ForwardPredictionRecord> Rows(Input i, string modelVersion, string configHash, ScoreDistribution d, DateTime nowUtc)
        {
            var picks = SelectivePrediction.Markets.Select(m => SelectivePrediction.Choose(m, d, i.MatchId, i.LeagueId, i.KickoffUtc, null, null,
                i.HomeSample, i.AwaySample, i.Coverage)).ToList();
            var (_, strongest) = SelectivePrediction.Tier(picks, SelectivePrediction.ForwardThresholds, sufficient: true);
            return picks.Select(p => new ForwardPredictionRecord
            {
                MatchId = i.MatchId, ModelVersion = modelVersion, ConfigHash = configHash, SelectorVersion = SelectivePrediction.ForwardSelectorVersion,
                SnapshotId = i.SnapshotId, LeagueId = i.LeagueId, Market = p.Market, ProbabilitiesJson = JsonSerializer.Serialize(Probabilities(p.Market, d), Json),
                SelectedOutcome = p.Outcome, SelectedProbability = Math.Round(p.Probability, 6),
                SignalTier = strongest != null && strongest.Market == p.Market ? SelectivePrediction.Tiers.Strongest : SelectivePrediction.Tiers.Regular,
                MissRisk = Math.Round(p.MissRisk, 6), GeneratedAtUtc = nowUtc, KickoffUtc = i.KickoffUtc, PredictionLockedAtUtc = nowUtc
            }).ToList();
        }

        public static Dictionary<string, double> Probabilities(string market, ScoreDistribution d) => market switch
        {
            MarketFamilies.MatchResult => new() { ["1"] = R(d.HomeWin), ["X"] = R(d.Draw), ["2"] = R(d.AwayWin) },
            MarketFamilies.DoubleChance => new() { ["1X"] = R(d.HomeWin + d.Draw), ["X2"] = R(d.Draw + d.AwayWin), ["12"] = R(d.HomeWin + d.AwayWin) },
            MarketFamilies.TotalGoals15 => new() { ["Over"] = R(d.Over(1.5)), ["Under"] = R(1 - d.Over(1.5)) },
            MarketFamilies.TotalGoals25 => new() { ["Over"] = R(d.Over(2.5)), ["Under"] = R(1 - d.Over(2.5)) },
            MarketFamilies.TotalGoals35 => new() { ["Over"] = R(d.Over(3.5)), ["Under"] = R(1 - d.Over(3.5)) },
            MarketFamilies.BothTeamsToScore => new() { ["Yes"] = R(d.BttsYes), ["No"] = R(1 - d.BttsYes) },
            _ => throw new ArgumentOutOfRangeException(nameof(market))
        };

        /// <summary>
        /// Kilitli kayıt — yalnız başlamamış ve başlamasına ≤ 24 saat kalan maçlar; maçın bir modelde kaydı varsa o model için
        /// tekrar yazılmaz (duplicate yok, üzerine yazma yok). Dönen: eklenen satır sayısı.
        /// </summary>
        public static async Task<int> RecordAsync(FormaxDbContext db, IReadOnlyList<Input> inputs, DateTime nowUtc, CancellationToken ct = default)
        {
            var eligible = inputs.Where(i => i.KickoffUtc > nowUtc && i.KickoffUtc - nowUtc <= LockWindow).ToList();
            if (eligible.Count == 0) return 0;
            var ids = eligible.Select(i => i.MatchId).ToList();
            var existing = (await db.ForwardPredictionRecords.AsNoTracking().Where(r => ids.Contains(r.MatchId))
                    .Select(r => new { r.MatchId, r.ModelVersion }).Distinct().ToListAsync(ct).ConfigureAwait(false))
                .Select(r => (r.MatchId, r.ModelVersion)).ToHashSet();
            var added = 0;
            foreach (var i in eligible)
            {
                foreach (var (version, hash, dist) in new[]
                         {
                             (OutcomeModelVersion.Current, Model40ConfigHash, i.Model40),
                             (Model5Shadow.Version, Model5Shadow.ConfigHash, i.Model5)
                         })
                {
                    if (existing.Contains((i.MatchId, version))) continue;
                    var rows = Rows(i, version, hash, dist, nowUtc);
                    db.ForwardPredictionRecords.AddRange(rows);
                    existing.Add((i.MatchId, version));
                    added += rows.Count;
                }
            }
            if (added > 0) await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return added;
        }

        /// <summary>4.0 kaydının kimliği: model sürümü + backtest yöntemi + yayın config parmak izi.</summary>
        public static string Model40ConfigHash => BacktestEligibilityEvaluationSource.CurrentConfigHash;

        /// <summary>
        /// Otomatik puanlama — bitmiş maç: doğru/yanlış, log loss, Brier. Ertelenen/iptal: Void. Puanlanmış satırda kanonik skor
        /// değiştiyse yeniden puanlanır, eski/yeni değer ScoreAuditJson'a eklenir. Olasılık alanlarına dokunulmaz.
        /// </summary>
        public static async Task<(int Scored, int Rescored, int Voided)> ScoreAsync(FormaxDbContext db, DateTime nowUtc, CancellationToken ct = default)
        {
            var rows = await (from r in db.ForwardPredictionRecords
                              join m in db.Matches on r.MatchId equals m.Id
                              where r.KickoffUtc <= nowUtc
                                    && ((r.ScoredAtUtc == null && (m.Status == MatchStatuses.Finished || m.Status == MatchStatuses.Postponed || m.Status == MatchStatuses.Cancelled))
                                        || (r.ScoredAtUtc != null && m.Status == MatchStatuses.Finished && (r.ActualHomeGoals != m.HomeScore || r.ActualAwayGoals != m.AwayScore)))
                              select new { r, m.Status, m.HomeScore, m.AwayScore }).ToListAsync(ct).ConfigureAwait(false);
            int scored = 0, rescored = 0, voided = 0;
            foreach (var x in rows)
            {
                var r = x.r;
                if (x.Status != MatchStatuses.Finished)
                {
                    r.ActualOutcome = "Void"; r.ScoredAtUtc = nowUtc; voided++;
                    continue;
                }
                var isRescore = r.ScoredAtUtc != null;
                if (isRescore)
                {
                    var audit = string.IsNullOrEmpty(r.ScoreAuditJson) ? new List<object>() : JsonSerializer.Deserialize<List<object>>(r.ScoreAuditJson) ?? new List<object>();
                    audit.Add(new { at = nowUtc, oldHome = r.ActualHomeGoals, oldAway = r.ActualAwayGoals, newHome = x.HomeScore, newAway = x.AwayScore, oldCorrect = r.Correct });
                    r.ScoreAuditJson = JsonSerializer.Serialize(audit, Json);
                    r.RescoreCount++;
                    rescored++;
                }
                else scored++;
                Score(r, x.HomeScore, x.AwayScore, nowUtc);
            }
            if (rows.Count > 0) await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (scored, rescored, voided);
        }

        public static void Score(ForwardPredictionRecord r, int hg, int ag, DateTime nowUtc)
        {
            var probs = JsonSerializer.Deserialize<Dictionary<string, double>>(r.ProbabilitiesJson) ?? new Dictionary<string, double>();
            string actual; bool correct; double ll, brier;
            switch (r.Market)
            {
                case MarketFamilies.MatchResult:
                    actual = hg > ag ? "1" : hg == ag ? "X" : "2";
                    correct = r.SelectedOutcome == actual;
                    ll = -Math.Log(Math.Max(1e-6, probs.GetValueOrDefault(actual)));
                    brier = new[] { "1", "X", "2" }.Sum(k => Math.Pow(probs.GetValueOrDefault(k) - (k == actual ? 1 : 0), 2));
                    break;
                case MarketFamilies.DoubleChance:
                    actual = hg > ag ? "1" : hg == ag ? "X" : "2";
                    correct = r.SelectedOutcome.Contains(actual);
                    ll = -Math.Log(Math.Max(1e-6, correct ? r.SelectedProbability : 1 - r.SelectedProbability));
                    brier = Math.Pow(r.SelectedProbability - (correct ? 1 : 0), 2);
                    break;
                default:
                    var yes = r.Market switch
                    {
                        MarketFamilies.TotalGoals15 => hg + ag > 1,
                        MarketFamilies.TotalGoals25 => hg + ag > 2,
                        MarketFamilies.TotalGoals35 => hg + ag > 3,
                        _ => hg > 0 && ag > 0
                    };
                    actual = r.Market == MarketFamilies.BothTeamsToScore ? (yes ? "Yes" : "No") : (yes ? "Over" : "Under");
                    correct = r.SelectedOutcome == actual;
                    ll = -Math.Log(Math.Max(1e-6, probs.GetValueOrDefault(actual)));
                    brier = Math.Pow(probs.GetValueOrDefault(r.Market == MarketFamilies.BothTeamsToScore ? "Yes" : "Over") - (yes ? 1 : 0), 2);
                    break;
            }
            r.ActualOutcome = actual; r.ActualHomeGoals = hg; r.ActualAwayGoals = ag; r.Correct = correct;
            r.LogLoss = Math.Round(ll, 6); r.Brier = Math.Round(brier, 6); r.ScoredAtUtc = nowUtc;
        }

        private static double R(double v) => Math.Round(v, 6);
    }
}
