using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TM = Formax.Infrastructure.Historical.Features.FeatureCalculators.TeamMatch;

namespace Formax.Infrastructure.Historical.Features;

/// <summary>
/// Feature Store'u batch dolduran motor. TÜM HistoricalMatch'ler için feature üretir ve
/// <see cref="MatchFeatureRecord"/>'a yazar. TEK GEÇİŞ in-memory rolling durum (tarih sırasına göre):
/// her maç YALNIZ o ana kadarki (önceki) durumdan hesaplanır → leakage yapı gereği imkânsız + hızlı (O(N)).
/// Feature matematiği <see cref="FeatureCalculators"/> ile paylaşılır. İdempotent: HistoricalMatchId
/// benzersiz + içerik hash'i; ikinci çalıştırmada değişen yoksa hiçbir yazma olmaz (Unchanged).
/// Historical şeması SALT-OKUNUR (yalnız Feature Store yazılır).
/// </summary>
public sealed class FeatureStoreBuilder : IFeatureStoreBuilder
{
    private const int BatchSize = 5000;
    private const int ProgressEvery = 20000;
    private const int HistoryCap = 60;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly FormaxDbContext _db;
    private readonly ILogger<FeatureStoreBuilder> _logger;

    public FeatureStoreBuilder(FormaxDbContext db, ILogger<FeatureStoreBuilder> logger)
    {
        _db = db;
        _logger = logger;
    }

    private sealed class Rolling
    {
        public readonly Dictionary<int, List<TM>> TeamHistory = new();
        public readonly Dictionary<string, Dictionary<int, (int pts, int gd)>> Standings = new();
        public readonly Dictionary<string, (double sum, int cnt)> LeagueElo = new();
        public readonly Dictionary<string, (int aWins, int bWins, int draws, int goals, int n)> H2H = new();
    }

    public async Task<FeatureStoreSummary> BuildAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        // Mevcut store durumu (idempotency): HistoricalMatchId → (recordId, hash)
        var existing = await _db.MatchFeatureRecords.AsNoTracking()
            .Select(x => new { x.HistoricalMatchId, x.Id, x.FeatureHash })
            .ToDictionaryAsync(x => x.HistoricalMatchId, x => (x.Id, x.FeatureHash), cancellationToken).ConfigureAwait(false);

        // Tüm maçları tarih sırasına göre yükle (tek geçiş).
        var matches = await _db.HistoricalMatches.AsNoTracking()
            .OrderBy(x => x.MatchDate).ThenBy(x => x.Id)
            .Select(x => new MatchRow(x.Id, x.HistoricalCompetitionId, x.HomeTeamId, x.AwayTeamId, x.MatchDate,
                x.FTHome, x.FTAway, x.HomeShots, x.AwayShots, x.HomeTarget, x.AwayTarget, x.HomeElo, x.AwayElo))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var state = new Rolling();
        int inserted = 0, updated = 0, unchanged = 0, processed = 0;
        var insertBuffer = new List<MatchFeatureRecord>(BatchSize);
        // Aynı tarihli maçlar birbirini GÖRMEMELİ (aynı gün sonucu maç öncesi bilinemez → leakage).
        // Bir günün TÜM feature'ları hesaplanmadan o günün maçları rolling duruma eklenmez.
        DateTime? currentDate = null;
        var pendingAdvance = new List<MatchRow>();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var m in matches)
            {
                if (currentDate is DateTime cd && m.Date != cd)
                {
                    foreach (var p in pendingAdvance) Advance(p, state);
                    pendingAdvance.Clear();
                }
                currentDate = m.Date;

                var vector = ComputeFromState(m, state);
                var json = JsonSerializer.Serialize(vector, Json);
                var (tRes, tHg, tAg) = TargetOf(m);
                var hash = Hash(json + "|" + (tRes ?? "?"));

                if (existing.TryGetValue(m.Id, out var ex))
                {
                    if (string.Equals(ex.FeatureHash, hash, StringComparison.Ordinal))
                        unchanged++;
                    else
                    {
                        var rec = new MatchFeatureRecord { Id = ex.Id, HistoricalMatchId = m.Id };
                        _db.Attach(rec);
                        rec.FeaturesJson = json; rec.FeatureHash = hash; rec.LastUpdatedUtc = DateTime.UtcNow;
                        rec.TargetResult = tRes; rec.TargetHomeGoals = tHg; rec.TargetAwayGoals = tAg;
                        _db.Entry(rec).Property(x => x.FeaturesJson).IsModified = true;
                        _db.Entry(rec).Property(x => x.FeatureHash).IsModified = true;
                        _db.Entry(rec).Property(x => x.LastUpdatedUtc).IsModified = true;
                        _db.Entry(rec).Property(x => x.TargetResult).IsModified = true;
                        _db.Entry(rec).Property(x => x.TargetHomeGoals).IsModified = true;
                        _db.Entry(rec).Property(x => x.TargetAwayGoals).IsModified = true;
                        updated++;
                    }
                }
                else
                {
                    insertBuffer.Add(new MatchFeatureRecord
                    {
                        HistoricalMatchId = m.Id, MatchDate = m.Date, HistoricalCompetitionId = m.CompId,
                        HomeTeamId = m.Home, AwayTeamId = m.Away,
                        FeaturesJson = json, FeatureHash = hash, LastUpdatedUtc = DateTime.UtcNow,
                        TargetResult = tRes, TargetHomeGoals = tHg, TargetAwayGoals = tAg
                    });
                    inserted++;
                }

                pendingAdvance.Add(m); // maçı GÜN SONUNDA state'e ekle (aynı-gün leakage'ı önlenir)

                if (insertBuffer.Count >= BatchSize)
                    await FlushAsync(insertBuffer, cancellationToken).ConfigureAwait(false);
                else if (_db.ChangeTracker.Entries().Count() >= BatchSize)
                {
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    _db.ChangeTracker.Clear();
                }

                if (++processed % ProgressEvery == 0)
                    _logger.LogInformation("Feature Store ilerleme: {P}/{T} işlendi (ins {I}, upd {U}, unc {Un})",
                        processed, matches.Count, inserted, updated, unchanged);
            }

            foreach (var p in pendingAdvance) Advance(p, state); // son günü de ekle (tamlık için)

            if (insertBuffer.Count > 0)
                await FlushAsync(insertBuffer, cancellationToken).ConfigureAwait(false);
            if (_db.ChangeTracker.Entries().Any())
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _db.ChangeTracker.Clear();
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var peak = Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024);
            var summary = new FeatureStoreSummary
            {
                MatchesTotal = matches.Count, Inserted = inserted, Updated = updated,
                Unchanged = unchanged, ElapsedMs = sw.ElapsedMilliseconds, PeakMemoryMB = peak
            };
            _logger.LogInformation("Feature Store BİTTİ: {T} maç, ins {I}, upd {U}, unc {Un}, {Ms} ms, {Mem} MB",
                summary.MatchesTotal, summary.Inserted, summary.Updated, summary.Unchanged, summary.ElapsedMs, summary.PeakMemoryMB);
            return summary;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task FlushAsync(List<MatchFeatureRecord> buffer, CancellationToken ct)
    {
        _db.MatchFeatureRecords.AddRange(buffer);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        buffer.Clear();
    }

    // ── Feature hesabı: mevcut rolling durumdan (yalnız önceki maçlar) ──

    private static MatchFeatureVector ComputeFromState(MatchRow m, Rolling s)
    {
        var priorHome = s.TeamHistory.TryGetValue(m.Home, out var ph) ? ph : Empty;
        var priorAway = s.TeamHistory.TryGetValue(m.Away, out var pa) ? pa : Empty;

        var csk = SeasonKey(m.CompId, m.Date);
        var (hPts, hRank) = StandingOf(s, csk, m.Home);
        var (aPts, aRank) = StandingOf(s, csk, m.Away);

        var home = FeatureCalculators.BuildTeamFeatures(priorHome, m.HomeElo, m.Date, hPts, hRank);
        var away = FeatureCalculators.BuildTeamFeatures(priorAway, m.AwayElo, m.Date, aPts, aRank);

        var (h2hWin, h2hGoals, h2hN) = H2HOf(s, m.Home, m.Away);
        var strength = s.LeagueElo.TryGetValue(csk, out var le) && le.cnt > 0 ? le.sum / le.cnt : 0d;

        return new MatchFeatureVector
        {
            MatchId = m.Id, Home = home, Away = away,
            EloDifference = (m.HomeElo is double he && m.AwayElo is double ae) ? he - ae : (double?)null,
            HomeAdvantage = home.HomeWinRate - away.AwayWinRate,
            HeadToHeadWinRate = h2hWin, HeadToHeadGoalsAverage = h2hGoals, HeadToHeadSampleSize = h2hN,
            LeagueStrength = strength
        };
    }

    private static readonly List<TM> Empty = new();

    private static (int pts, int? rank) StandingOf(Rolling s, string csk, int team)
    {
        if (!s.Standings.TryGetValue(csk, out var table) || !table.TryGetValue(team, out var cur))
            return (0, null);
        var (pts, gd) = cur;
        var rank = 1 + table.Count(kv =>
        {
            var (p, g) = kv.Value;
            return p > pts || (p == pts && g > gd) || (p == pts && g == gd && kv.Key < team);
        });
        return (pts, rank);
    }

    private static (double, double, int) H2HOf(Rolling s, int home, int away)
    {
        var key = PairKey(home, away);
        if (!s.H2H.TryGetValue(key, out var h) || h.n == 0) return (0d, 0d, 0);
        var homeWins = home < away ? h.aWins : h.bWins;
        return (homeWins / (double)h.n, h.goals / (double)h.n, h.n);
    }

    // ── Rolling durumu ilerlet (maçı EKLE) ──

    private static void Advance(MatchRow m, Rolling s)
    {
        if (m.FTHome is not int fh || m.FTAway is not int fa) return; // oynanmamış → geçmişe katma

        Push(s.TeamHistory, m.Home, new TM(m.Date, true, fh, fa, m.HomeShots, m.HomeTarget, m.AwayShots, m.Away));
        Push(s.TeamHistory, m.Away, new TM(m.Date, false, fa, fh, m.AwayShots, m.AwayTarget, m.HomeShots, m.Home));

        var csk = SeasonKey(m.CompId, m.Date);
        var table = s.Standings.TryGetValue(csk, out var t) ? t : (s.Standings[csk] = new());
        void Add(int team, int p, int diff)
        {
            var cur = table.TryGetValue(team, out var c) ? c : (0, 0);
            table[team] = (cur.Item1 + p, cur.Item2 + diff);
        }
        if (fh > fa) { Add(m.Home, 3, fh - fa); Add(m.Away, 0, fa - fh); }
        else if (fh == fa) { Add(m.Home, 1, 0); Add(m.Away, 1, 0); }
        else { Add(m.Home, 0, fh - fa); Add(m.Away, 3, fa - fh); }

        if (m.HomeElo is double h1) { var e = s.LeagueElo.GetValueOrDefault(csk); s.LeagueElo[csk] = (e.sum + h1, e.cnt + 1); }
        if (m.AwayElo is double a1) { var e = s.LeagueElo.GetValueOrDefault(csk); s.LeagueElo[csk] = (e.sum + a1, e.cnt + 1); }

        var pk = PairKey(m.Home, m.Away);
        var cur2 = s.H2H.GetValueOrDefault(pk);
        int aW = cur2.aWins, bW = cur2.bWins, dr = cur2.draws;
        var homeIsA = m.Home < m.Away;
        if (fh > fa) { if (homeIsA) aW++; else bW++; }
        else if (fh == fa) dr++;
        else { if (homeIsA) bW++; else aW++; }
        s.H2H[pk] = (aW, bW, dr, cur2.goals + fh + fa, cur2.n + 1);
    }

    private static void Push(Dictionary<int, List<TM>> hist, int team, TM tm)
    {
        var list = hist.TryGetValue(team, out var l) ? l : (hist[team] = new List<TM>(HistoryCap));
        list.Insert(0, tm);                 // en yeni önce
        if (list.Count > HistoryCap) list.RemoveAt(list.Count - 1);
    }

    private static string SeasonKey(int compId, DateTime d)
        => $"{compId}|{(d.Month >= 7 ? d.Year : d.Year - 1)}";

    private static string PairKey(int a, int b) => a < b ? $"{a}|{b}" : $"{b}|{a}";

    /// <summary>Model target'ı: maçın GERÇEK sonucu (label). 1X2 = H/D/A. Oynanmamışsa null.</summary>
    private static (string? result, int? hg, int? ag) TargetOf(MatchRow m)
    {
        if (m.FTHome is not int h || m.FTAway is not int a) return (null, null, null);
        return (h > a ? "H" : h == a ? "D" : "A", h, a);
    }

    private static string Hash(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    private readonly record struct MatchRow(int Id, int CompId, int Home, int Away, DateTime Date,
        int? FTHome, int? FTAway, int? HomeShots, int? AwayShots, int? HomeTarget, int? AwayTarget, double? HomeElo, double? AwayElo);
}
