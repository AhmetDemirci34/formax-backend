using System;
using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Discovery;

/// <summary>
/// Discovery Engine'in KAYNAK-AGNOSTİK aday sözleşmesi. Personal Radar Score (FAZ 4.2) DEĞİŞTİRİLMEDEN taşınır;
/// çeşitlilik için lig/takım/tarih/güç meta verisi eklenir. Motor bu verinin nereden geldiğini bilmez (SOLID).
/// </summary>
public sealed record DiscoveryCandidate
{
    public required int MatchId { get; init; }
    /// <summary>Personal Radar Score (0-100) — Discovery bunu KORUR, değiştirmez.</summary>
    public required double PersonalScore { get; init; }
    public double BaseScore { get; init; }
    public bool HiddenGem { get; init; }

    public required string League { get; init; }
    public int HomeTeamId { get; init; }
    public int AwayTeamId { get; init; }
    /// <summary>Takım gücü [0,1] (normalize Elo) — büyük takım domination'ını dengelemek için.</summary>
    public double TeamStrength { get; init; }
    public DateTime MatchDate { get; init; }
    /// <summary>Tahmin güvenilirliği [0,1].</summary>
    public double Confidence { get; init; }
}

/// <summary>
/// Discovery Engine config'i — TAMAMEN config-driven. Relevance vs çeşitlilik dengesi + domination hard cap'leri
/// + hidden gem boost buradan yönetilir. Deterministik.
/// </summary>
public sealed class DiscoveryWeights
{
    public int FeedSize { get; set; } = 20;

    public double RelevanceWeight { get; set; } = 1.00;   // Personal Radar Score (korunur)
    public double LeagueRepeatPenalty { get; set; } = 0.15; // feed'de aynı ligden her maç için ceza
    public double ConsecutiveLeaguePenalty { get; set; } = 0.25; // bir önceki maçla AYNI lig ise ek ceza (art-arda spam önler)
    public double TeamRepeatPenalty { get; set; } = 0.50;   // aynı takımı paylaşan her maç için ceza
    public double BigTeamPenalty { get; set; } = 0.08;      // feed'deki güçlü takım sayısı başına (güçlü aday için)
    public double BigTeamThreshold { get; set; } = 0.70;    // bu gücün üstü "büyük takım"
    public double HiddenGemBoost { get; set; } = 0.20;      // hidden gem görünürlük artışı
    public double FreshnessWeight { get; set; } = 0.10;     // güncellik
    public double ConfidenceWeight { get; set; } = 0.10;
    public double TimeDiversityWeight { get; set; } = 0.08; // yeni zaman kovası

    // ── Hard cap'ler — domination'ı MUTLAK engeller ──
    public int MaxPerLeague { get; set; } = 3;
    public int MaxPerTeam { get; set; } = 1;

    public static DiscoveryWeights Default => new();
}

/// <summary>Discovery Feed'de tek maç: sıra + skor + gerekçe + breakdown. Personal Radar Score korunur.</summary>
public sealed record DiscoveryFeedItem
{
    public required int MatchId { get; init; }
    public int DiscoveryRank { get; init; }
    public double DiscoveryScore { get; init; }
    public required string DiscoveryReason { get; init; }
    public bool HiddenGem { get; init; }
    /// <summary>Değişmeden korunan Personal Radar Score.</summary>
    public double PersonalScore { get; init; }
    public double BaseScore { get; init; }
    public required IReadOnlyList<SignalContribution> Breakdown { get; init; }
}

/// <summary>Discovery Feed: en değerli + en çeşitli maç listesi (sıralı).</summary>
public sealed record DiscoveryFeed
{
    public required IReadOnlyList<DiscoveryFeedItem> Items { get; init; }
    public int CandidateCount { get; init; }
}
