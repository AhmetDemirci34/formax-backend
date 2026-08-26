using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Formax.Contract.Models;

namespace Formax.Contract.Services;

/// <summary>
/// Builds the identity of a prediction.
///
/// Deterministic on purpose: the id is a hash of the match, the four versions, the evidence cutoff
/// and the prediction timestamp. Two consequences, both wanted:
///
///   * re-running the pipeline over unchanged inputs produces the SAME id and the same numbers, so
///     republishing is idempotent instead of duplicating;
///   * a prediction made from newer evidence has a different cutoff, therefore a different id, so
///     it is a NEW prediction rather than an edit of the old one - which is exactly what section 10
///     of the contract requires.
/// </summary>
public static class PredictionIdFactory
{
    public static string Create(string matchId, ContractVersions v, DateOnly? evidenceCutoff, DateTime timestamp)
    {
        var canonical = string.Join('|',
            matchId, v.ModelVersion, v.TeamStrengthVersion, v.GateVersion, v.CalibrationVersion,
            evidenceCutoff?.ToString("yyyy-MM-dd") ?? "null",
            timestamp.ToString("O", CultureInfo.InvariantCulture));
        return "FMXP" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..20];
    }
}

public enum PublishOutcome
{
    /// <summary>A new prediction was stored.</summary>
    Published,
    /// <summary>This exact prediction was already stored. Nothing changed; republishing is safe.</summary>
    AlreadyPublished,
    /// <summary>The id exists with DIFFERENT content. Refused - a published prediction may not be rewritten.</summary>
    RejectedImmutable
}

/// <summary>
/// Append-only prediction store.
///
/// The one rule it exists to enforce: once a prediction is published, its probabilities are final.
/// A second publish of the same id with the same content is a no-op; with different content it is
/// refused outright rather than merged, overwritten or "corrected". Producing a different number
/// for a match is allowed - it just has to be a NEW prediction, with its own id and its own
/// timestamp, standing next to the old one instead of erasing it.
///
/// The store keeps every version. <see cref="Current"/> answers "what should be shown now"; the
/// history answers "what did we say, and when".
/// </summary>
public sealed class PredictionStore
{
    private readonly Dictionary<string, PredictionRecord> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PredictionRecord>> _byMatch = new(StringComparer.Ordinal);
    private long _nextSequence = 1;

    public int Count => _byId.Count;
    public IEnumerable<PredictionRecord> All => _byId.Values;

    public int ImmutabilityViolationsRefused { get; private set; }
    public int IdempotentRepublishes { get; private set; }

    public PublishOutcome Publish(PredictionContract prediction)
    {
        var hash = prediction.ContentHash;

        if (_byId.TryGetValue(prediction.PredictionId, out var existing))
        {
            if (existing.PublishedContentHash == hash) { IdempotentRepublishes++; return PublishOutcome.AlreadyPublished; }
            ImmutabilityViolationsRefused++;
            return PublishOutcome.RejectedImmutable;
        }

        var record = new PredictionRecord
        { Prediction = prediction, PublishedContentHash = hash, Sequence = _nextSequence++ };
        _byId[prediction.PredictionId] = record;
        if (!_byMatch.TryGetValue(prediction.MatchId, out var list))
        {
            list = new List<PredictionRecord>();
            _byMatch[prediction.MatchId] = list;
        }
        list.Add(record);
        return PublishOutcome.Published;
    }

    public PredictionRecord? Get(string predictionId)
        => _byId.TryGetValue(predictionId, out var r) ? r : null;

    /// <summary>Every prediction ever made for a match, in publication order.</summary>
    public IReadOnlyList<PredictionRecord> History(string matchId)
        => _byMatch.TryGetValue(matchId, out var l) ? l : Array.Empty<PredictionRecord>();

    /// <summary>
    /// The prediction a consumer should show now: the last one appended for this match.
    ///
    /// Publication order, not the timestamp. Two predictions of the same match can legitimately
    /// share a timestamp - in replay both carry the match day - and ordering by timestamp then
    /// falls back to comparing hashes, which is arbitrary. An append-only log already knows which
    /// row came second; that is the answer.
    /// </summary>
    public PredictionRecord? Current(string matchId)
        => _byMatch.TryGetValue(matchId, out var l) && l.Count > 0 ? l[^1] : null;

    public sealed class SettlementReport
    {
        public int Settled;
        public int AlreadySettled;
        public int NoResultYet;
        public int RejectedAsEarly;
        public int RefusedUnpublished;
    }

    /// <summary>
    /// Attaches results. Never rewrites a probability - it cannot, the prediction is a record with
    /// init-only members and the result lives in its own object.
    /// </summary>
    public SettlementReport Settle(
        IReadOnlyDictionary<string, (int home, int away, DateOnly played)> results,
        DateTime settlementTimestamp)
    {
        var report = new SettlementReport();
        foreach (var record in _byId.Values)
        {
            if (record.Settlement is not null) { report.AlreadySettled++; continue; }
            if (!results.TryGetValue(record.Prediction.MatchId, out var r)) { report.NoResultYet++; continue; }

            // a result may not predate the prediction it is attached to
            if (r.played < DateOnly.FromDateTime(record.Prediction.PredictionTimestamp))
            { report.RejectedAsEarly++; continue; }

            record.Attach(new Settlement
            {
                ActualHomeGoals = r.home,
                ActualAwayGoals = r.away,
                ActualResult = Settlement.ResultOf(r.home, r.away),
                SettlementTimestamp = settlementTimestamp
            });
            report.Settled++;
        }
        return report;
    }

    /// <summary>Rows whose prediction is no longer what was published. Must always be zero.</summary>
    public int TamperedRows() => _byId.Values.Count(r => !r.IsIntact);
}
