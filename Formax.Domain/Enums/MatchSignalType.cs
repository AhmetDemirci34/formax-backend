namespace Formax.Domain.Enums;

/// <summary>
/// Radar Match Intelligence (R.9.1) — the kind of importance signal derived for a
/// match. Hardcoded rules produce these in this sprint; the type set is stable so
/// later data-driven rules reuse it.
/// </summary>
public enum MatchSignalType
{
    None = 0,
    Derby = 1,
    Rivalry = 2,
    Final = 3,
    Playoff = 4,
    RelegationBattle = 5,
    TitleRace = 6,
    ImportantMatch = 7,

    // ── R.9.3: context-derived signals ─────────────────────────────────────
    StrongForm = 8,
    WeakForm = 9,
    FormAdvantage = 10,
    HistoricalDominance = 11,
    BalancedRivalry = 12,
    HighImportance = 13,

    // ── R.9.5: enrichment-derived signals ──────────────────────────────────
    NewsAttention = 14,
    NewsMomentum = 15,
    UserAttention = 16,
    FollowAttention = 17,
    SourceConfidence = 18,

    // ── R.10.4: News Intelligence integration ──────────────────────────────
    NewsDrivenMatch = 19,

    // ── R.11.4: Synthetic Odds integration ─────────────────────────────────
    MarketAttention = 20
}
